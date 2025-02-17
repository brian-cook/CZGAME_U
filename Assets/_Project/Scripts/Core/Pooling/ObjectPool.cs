using UnityEngine;
using System.Collections.Generic;
using Unity.Profiling;
using System;

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
        
        private ProfilerRecorder memoryRecorder;
        private int peakCount;
        private bool isExpanding;
        private int activeCount;  // Track active objects
        
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
        public ObjectPool(Func<T> createFunc, int initialSize, int maxSize, string poolName)
        {
            this.createFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
            this.maxSize = maxSize;
            this.poolName = poolName;
            
            pool = new Queue<T>(initialSize);
            memoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, $"Pool_{poolName}_Memory");
            
            // Prewarm the pool
            for (int i = 0; i < initialSize; i++)
            {
                var obj = createFunc();
                obj.GameObject.SetActive(false);
                pool.Enqueue(obj);
            }
            
            peakCount = initialSize;
            activeCount = 0;
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
            }
            else if (TotalCount < maxSize)  // Check total objects against maxSize
            {
                isExpanding = true;
                obj = createFunc();
                peakCount = Math.Max(peakCount, TotalCount + 1);
                
                Debug.LogWarning($"Pool '{poolName}' expanded. Current size: {TotalCount + 1}, Peak: {peakCount}");
            }
            else
            {
                Debug.LogError($"Pool '{poolName}' reached max size of {maxSize}");
                return default;
            }
            
            activeCount++;
            obj.GameObject.SetActive(true);
            obj.OnSpawn();
            return obj;
        }
        
        /// <summary>
        /// Return an object to the pool
        /// </summary>
        public void Return(T obj)
        {
            if (obj == null) return;
            
            obj.OnDespawn();
            obj.GameObject.SetActive(false);
            pool.Enqueue(obj);
            activeCount--;
        }
        
        /// <summary>
        /// Clear the pool and release resources
        /// </summary>
        public void Clear()
        {
            pool.Clear();
            memoryRecorder.Dispose();
            activeCount = 0;
        }
        
        /// <summary>
        /// Get pool statistics for monitoring
        /// </summary>
        public (int current, int peak, long memoryUsage) GetStats()
        {
            return (CurrentCount, peakCount, memoryRecorder.LastValue);
        }
    }
} 