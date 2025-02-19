using UnityEngine;
using System.Collections.Generic;
using Unity.Profiling;
using System;
using System.Collections;
using CZ.Core.Configuration;

namespace CZ.Core.Pooling
{
    /// <summary>
    /// Central manager for all object pools in the game
    /// Implements Unity 6.0 memory management best practices
    /// </summary>
    public class PoolManager : MonoBehaviour
    {
        [SerializeField] private MemoryConfiguration memoryConfig;

        private static PoolManager instance;
        public static PoolManager Instance
        {
            get
            {
                if (instance == null)
                {
                    // Handle edit mode differently
                    if (!Application.isPlaying)
                    {
                        // In edit mode, just create a temporary instance without DontDestroyOnLoad
                        var go = new GameObject("PoolManager_EditorOnly");
                        instance = go.AddComponent<PoolManager>();
                        instance.InitializeManager(true);
                    }
                    else
                    {
                        var go = new GameObject("PoolManager");
                        instance = go.AddComponent<PoolManager>();
                        DontDestroyOnLoad(go);
                        instance.InitializeManager(false);
                    }
                }
                return instance;
            }
        }

        private Dictionary<Type, object> pools = new Dictionary<Type, object>();
        private readonly Dictionary<string, (int current, int peak, long memory)> poolStats = new();
        
        // Memory monitoring
        private ProfilerRecorder totalMemoryRecorder;
        private float nextStatsUpdate;
        private const float STATS_UPDATE_INTERVAL = 0.5f; // 500ms
        
        // Pool memory thresholds (as percentage of total memory thresholds)
        private const float POOL_MEMORY_WARNING_RATIO = 0.25f;    // 25% of total memory
        private const float POOL_MEMORY_CRITICAL_RATIO = 0.35f;   // 35% of total memory
        private const float POOL_MEMORY_EMERGENCY_RATIO = 0.45f;  // 45% of total memory

        private float poolWarningThreshold;
        private float poolCriticalThreshold;
        private float poolEmergencyThreshold;

        private int totalActiveObjects;
        private bool isEditorInstance;
        
        public int ActiveCount => totalActiveObjects;

        private void InitializeManager(bool isEditor)
        {
            isEditorInstance = isEditor;
            
            // Try to load MemoryConfiguration from Resources if not assigned
            if (memoryConfig == null)
            {
                memoryConfig = Resources.Load<MemoryConfiguration>("Configuration/MemoryConfiguration");
            }
            
            // Calculate system-aware thresholds
            float systemMemoryMB = SystemInfo.systemMemorySize;
            float baseMemory = Mathf.Max(1024f, systemMemoryMB / 4f); // At least 1GB or 1/4 system memory
            
            if (memoryConfig != null)
            {
                poolWarningThreshold = memoryConfig.PoolWarningThreshold;
                poolCriticalThreshold = memoryConfig.PoolCriticalThreshold;
                poolEmergencyThreshold = memoryConfig.PoolEmergencyThreshold;
            }
            else
            {
                // Calculate pool thresholds as percentage of base memory
                poolWarningThreshold = baseMemory * POOL_MEMORY_WARNING_RATIO;
                poolCriticalThreshold = baseMemory * POOL_MEMORY_CRITICAL_RATIO;
                poolEmergencyThreshold = baseMemory * POOL_MEMORY_EMERGENCY_RATIO;
                
                Debug.LogWarning($"[PoolManager] Using calculated thresholds - Warning: {poolWarningThreshold:F2}MB, Critical: {poolCriticalThreshold:F2}MB, Emergency: {poolEmergencyThreshold:F2}MB");
            }

            // Initialize monitoring
            if (!isEditor)
            {
                totalMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
                StartCoroutine(UpdateStats());
            }
        }

        private IEnumerator UpdateStats()
        {
            while (enabled && !isEditorInstance)
            {
                if (Time.time >= nextStatsUpdate)
                {
                    UpdatePoolStats();
                    nextStatsUpdate = Time.time + STATS_UPDATE_INTERVAL;
                }
                yield return new WaitForSeconds(STATS_UPDATE_INTERVAL);
            }
        }

        /// <summary>
        /// Creates or gets an existing pool
        /// </summary>
        public ObjectPool<T> CreatePool<T>(Func<T> createFunc, int initialSize, int maxSize, string poolName) where T : IPoolable
        {
            var type = typeof(T);
            if (pools.ContainsKey(type))
            {
                Debug.LogWarning($"Pool for type {type.Name} already exists");
                return (ObjectPool<T>)pools[type];
            }

            var pool = new ObjectPool<T>(createFunc, initialSize, maxSize, poolName);
            pools.Add(type, pool);
            return pool;
        }

        /// <summary>
        /// Gets an existing pool
        /// </summary>
        public ObjectPool<T> GetPool<T>() where T : IPoolable
        {
            var type = typeof(T);
            if (!pools.ContainsKey(type))
            {
                Debug.LogError($"No pool found for type {type.Name}");
                return null;
            }

            return (ObjectPool<T>)pools[type];
        }

        /// <summary>
        /// Updates statistics for all pools
        /// </summary>
        private void UpdatePoolStats()
        {
            poolStats.Clear();
            long totalMemory = 0;

            foreach (var pool in pools)
            {
                var type = pool.Key;
                var stats = GetPoolStats(pool.Value);
                poolStats.Add(type.Name, stats);
                totalMemory += stats.memory;
            }

            // Convert to MB for threshold checks
            var totalMemoryMB = totalMemory / (1024f * 1024f);
            
            // Log warnings based on dynamic thresholds
            if (totalMemoryMB > poolEmergencyThreshold)
            {
                Debug.LogError($"[PoolManager] EMERGENCY: Pool memory usage critical: {totalMemoryMB:F2}MB/{poolEmergencyThreshold:F2}MB");
                TriggerEmergencyCleanup();
            }
            else if (totalMemoryMB > poolCriticalThreshold)
            {
                Debug.LogWarning($"[PoolManager] CRITICAL: Pool memory usage high: {totalMemoryMB:F2}MB/{poolCriticalThreshold:F2}MB");
                TriggerPoolCleanup(true);
            }
            else if (totalMemoryMB > poolWarningThreshold)
            {
                Debug.LogWarning($"[PoolManager] WARNING: Pool memory usage elevated: {totalMemoryMB:F2}MB/{poolWarningThreshold:F2}MB");
                TriggerPoolCleanup(false);
            }
        }

        private void TriggerPoolCleanup(bool aggressive)
        {
            foreach (var pool in pools.Values)
            {
                var cleanupMethod = pool.GetType().GetMethod(aggressive ? "AggressiveCleanup" : "Cleanup");
                cleanupMethod?.Invoke(pool, null);
            }
        }

        private void TriggerEmergencyCleanup()
        {
            // Clear all inactive objects from pools
            foreach (var pool in pools.Values)
            {
                var emergencyCleanupMethod = pool.GetType().GetMethod("EmergencyCleanup");
                emergencyCleanupMethod?.Invoke(pool, null);
            }
            
            // Force GC collection
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
        }

        /// <summary>
        /// Gets statistics for a specific pool
        /// </summary>
        private (int current, int peak, long memory) GetPoolStats(object pool)
        {
            var statsMethod = pool.GetType().GetMethod("GetStats");
            if (statsMethod == null) return (0, 0, 0);

            var result = statsMethod.Invoke(pool, null);
            if (result == null) return (0, 0, 0);

            // Handle the tuple conversion safely
            if (result is ValueTuple<int, int, float> floatTuple)
            {
                return (floatTuple.Item1, floatTuple.Item2, (long)(floatTuple.Item3 * 1024 * 1024));
            }
            else if (result is ValueTuple<int, int, long> longTuple)
            {
                return longTuple;
            }
            
            return (0, 0, 0);
        }

        /// <summary>
        /// Gets current statistics for all pools
        /// </summary>
        public IReadOnlyDictionary<string, (int current, int peak, long memory)> GetAllPoolStats()
        {
            return poolStats;
        }

        public T Get<T>() where T : IPoolable
        {
            if (pools.TryGetValue(typeof(T), out var poolObj))
            {
                var pool = (ObjectPool<T>)poolObj;
                var obj = pool.Get();
                if (obj != null)
                {
                    totalActiveObjects++;
                }
                return obj;
            }
            return default;
        }
        
        public void Return<T>(T obj) where T : IPoolable
        {
            if (obj == null) return;
            
            if (pools.TryGetValue(typeof(T), out var poolObj))
            {
                var pool = (ObjectPool<T>)poolObj;
                pool.Return(obj);
                totalActiveObjects--;
            }
        }
        
        public void ClearAllPools()
        {
            foreach (var pool in pools.Values)
            {
                if (pool is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            pools.Clear();
            totalActiveObjects = 0;
        }

        private void OnDestroy()
        {
            if (this == instance)
            {
                instance = null;
            }
            
            ClearAllPools();
            if (!isEditorInstance && totalMemoryRecorder.Valid)
            {
                totalMemoryRecorder.Dispose();
            }
        }
    }
} 