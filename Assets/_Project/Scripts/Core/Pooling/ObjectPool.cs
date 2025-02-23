using UnityEngine;
using System.Collections.Generic;
using Unity.Profiling;
using System;
using CZ.Core.Interfaces;
using CZ.Core.Logging;

namespace CZ.Core.Pooling
{
    /// <summary>
    /// Generic object pool implementation optimized for Unity 6.0
    /// Implements memory-efficient pooling with monitoring capabilities
    /// </summary>
    public class ObjectPool<T> where T : IPoolable
    {
        private readonly Queue<T> pool;
        private readonly Func<T> createFunc;
        private readonly int maxSize;
        private readonly string poolName;
        private readonly float memoryThresholdMB;
        
        private ProfilerRecorder memoryRecorder;
        private ProfilerRecorder totalMemoryRecorder;
        private int peakCount;
        private bool isExpanding;
        private int activeCount;
        private float startupBaseline;
        
        public int CurrentCount => pool.Count;
        public int ActiveCount => activeCount;
        public int TotalCount => CurrentCount + activeCount;
        public int PeakCount => peakCount;
        public bool IsExpanding => isExpanding;
        public int MaxSize => maxSize;
        
        /// <summary>
        /// Creates a new object pool
        /// </summary>
        /// <param name="createFunc">Factory function to create new instances</param>
        /// <param name="initialSize">Initial pool size</param>
        /// <param name="maxSize">Maximum pool size</param>
        /// <param name="poolName">Name for profiling and debugging</param>
        /// <param name="memoryThresholdMB">Memory threshold in MB</param>
        public ObjectPool(Func<T> createFunc, int initialSize, int maxSize, string poolName, float memoryThresholdMB = 1024f)
        {
            this.createFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
            this.maxSize = maxSize;
            this.poolName = poolName;
            this.memoryThresholdMB = memoryThresholdMB;
            
            pool = new Queue<T>(initialSize);
            memoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, $"Pool_{poolName}_Memory");
            totalMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
            
            // Get initial memory baseline
            startupBaseline = totalMemoryRecorder.LastValue / (1024f * 1024f);
            
            // Prewarm the pool with memory monitoring
            for (int i = 0; i < initialSize; i++)
            {
                if (ShouldExpandPool())
                {
                    var obj = createFunc();
                    obj.GameObject.SetActive(false);
                    pool.Enqueue(obj);
                }
                else
                {
                    Debug.LogWarning($"Pool '{poolName}' prewarming stopped at {i} due to memory constraints");
                    break;
                }
            }
            
            peakCount = pool.Count;
            activeCount = 0;
        }
        
        private bool ShouldExpandPool()
        {
            var currentMemory = totalMemoryRecorder.LastValue / (1024f * 1024f);
            var memoryDelta = currentMemory - startupBaseline;
            
            // Apply stricter memory threshold scaling based on Unity 6.0 guidelines
            var thresholdScale = Mathf.Clamp(startupBaseline / 512f, 0.5f, 2f);
            var adjustedThreshold = memoryThresholdMB * thresholdScale;
            
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (currentMemory >= adjustedThreshold)
            {
                CZLogger.LogWarning($"Pool '{poolName}' memory threshold reached: {currentMemory:F2}MB/{adjustedThreshold:F2}MB", LogCategory.Pool);
            }
            #endif
            
            return currentMemory < adjustedThreshold;
        }
        
        /// <summary>
        /// Get an object from the pool
        /// </summary>
        public T Get()
        {
            T obj;
            isExpanding = false;
            
            if (pool.Count > 0)
            {
                obj = pool.Dequeue();
                System.Threading.Interlocked.Increment(ref activeCount);
                obj.GameObject.SetActive(true);
                obj.OnSpawn();
                return obj;
            }
            
            if (TotalCount < maxSize && ShouldExpandPool())
            {
                isExpanding = true;
                obj = createFunc();
                
                int newPeakCount = TotalCount + 1;
                if (newPeakCount > peakCount)
                {
                    System.Threading.Interlocked.Exchange(ref peakCount, newPeakCount);
                }
                
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                CZLogger.LogWarning($"Pool '{poolName}' expanded. Size: {TotalCount + 1}, Peak: {peakCount}, Memory: {totalMemoryRecorder.LastValue / (1024f * 1024f):F2}MB", LogCategory.Pool);
                #endif
                
                System.Threading.Interlocked.Increment(ref activeCount);
                obj.GameObject.SetActive(true);
                obj.OnSpawn();
                return obj;
            }
            
            var currentMemory = totalMemoryRecorder.LastValue / (1024f * 1024f);
            CZLogger.LogWarning($"Pool '{poolName}' at capacity. Size: {TotalCount}, Max: {maxSize}, Memory: {currentMemory:F2}MB/{memoryThresholdMB}MB. Waiting for object return.", LogCategory.Pool);
            return default;
        }
        
        /// <summary>
        /// Return an object to the pool
        /// </summary>
        public void Return(T obj)
        {
            if (obj == null)
            {
                CZLogger.LogError($"Pool '{poolName}' attempted to return null object", LogCategory.Pool);
                return;
            }

            if (!ValidateObjectType(obj))
            {
                CZLogger.LogError($"Pool '{poolName}' received object of incorrect type", LogCategory.Pool);
                return;
            }

            obj.OnDespawn();
            obj.GameObject.SetActive(false);
            
            if (pool.Count < maxSize)
            {
                pool.Enqueue(obj);
            }
            else
            {
                CZLogger.LogWarning($"Pool '{poolName}' destroying excess object to maintain size limit", LogCategory.Pool);
                UnityEngine.Object.Destroy(obj.GameObject);
            }
            
            System.Threading.Interlocked.Decrement(ref activeCount);
        }
        
        private bool ValidateObjectType(T obj)
        {
            try
            {
                // Type-specific validation using reflection to avoid direct dependencies
                if (obj != null)
                {
                    var objType = obj.GetType();
                    var resourceTypeProp = objType.GetProperty("ResourceType");
                    
                    if (resourceTypeProp != null)
                    {
                        var resourceType = resourceTypeProp.GetValue(obj)?.ToString();
                        var poolTypeName = poolName.Replace("Resource_", "");
                        
                        var isValid = string.Equals(resourceType, poolTypeName, StringComparison.OrdinalIgnoreCase);
                        if (!isValid)
                        {
                            CZLogger.LogError($"Type mismatch in pool '{poolName}'. Expected: {poolTypeName}, Got: {resourceType}", LogCategory.Pool);
                        }
                        return isValid;
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ObjectPool] Error during type validation: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Clear the pool and release resources
        /// </summary>
        public void Clear()
        {
            pool.Clear();
            memoryRecorder.Dispose();
            totalMemoryRecorder.Dispose();
            activeCount = 0;
            peakCount = 0;
        }
        
        /// <summary>
        /// Get pool statistics for monitoring
        /// </summary>
        public (int current, int peak, long memory) GetStats()
        {
            var memoryMB = totalMemoryRecorder.LastValue / (1024f * 1024f);
            return (CurrentCount, peakCount, (long)(memoryMB * 1024 * 1024));
        }
    }
} 