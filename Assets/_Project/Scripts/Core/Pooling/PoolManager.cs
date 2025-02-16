using UnityEngine;
using System.Collections.Generic;
using Unity.Profiling;
using System;

namespace CZ.Core.Pooling
{
    /// <summary>
    /// Central manager for all object pools in the game
    /// Implements Unity 6.0 memory management best practices
    /// </summary>
    public class PoolManager : MonoBehaviour
    {
        private static PoolManager instance;
        public static PoolManager Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("PoolManager");
                    instance = go.AddComponent<PoolManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        // Dictionary to store all pools
        private readonly Dictionary<Type, object> pools = new();
        private readonly Dictionary<string, (int current, int peak, long memory)> poolStats = new();
        
        // Memory monitoring
        private ProfilerRecorder totalMemoryRecorder;
        private float nextStatsUpdate;
        private const float STATS_UPDATE_INTERVAL = 0.5f; // 500ms

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            instance = this;
            totalMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total_Pools_Memory");
        }

        private void Update()
        {
            if (Time.time >= nextStatsUpdate)
            {
                UpdatePoolStats();
                nextStatsUpdate = Time.time + STATS_UPDATE_INTERVAL;
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

            // Log warnings if total memory exceeds thresholds
            var totalMemoryMB = totalMemory / (1024 * 1024);
            if (totalMemoryMB > 256) // 256MB warning threshold for pools
            {
                Debug.LogWarning($"Total pool memory usage high: {totalMemoryMB}MB");
            }
        }

        /// <summary>
        /// Gets statistics for a specific pool
        /// </summary>
        private (int current, int peak, long memory) GetPoolStats(object pool)
        {
            var statsMethod = pool.GetType().GetMethod("GetStats");
            if (statsMethod == null) return (0, 0, 0);

            return ((int, int, long))statsMethod.Invoke(pool, null);
        }

        /// <summary>
        /// Gets current statistics for all pools
        /// </summary>
        public IReadOnlyDictionary<string, (int current, int peak, long memory)> GetAllPoolStats()
        {
            return poolStats;
        }

        private void OnDestroy()
        {
            foreach (var pool in pools.Values)
            {
                var clearMethod = pool.GetType().GetMethod("Clear");
                clearMethod?.Invoke(pool, null);
            }
            
            pools.Clear();
            totalMemoryRecorder.Dispose();
        }
    }
} 