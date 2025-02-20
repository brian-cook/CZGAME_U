using UnityEngine;
using System.Collections.Generic;
using CZ.Core.Pooling;
using NaughtyAttributes;
using Unity.Profiling;
using System;

namespace CZ.Core.Resource
{
    public class ResourceManager : MonoBehaviour
    {
        #region Singleton
        private static ResourceManager instance;
        public static ResourceManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<ResourceManager>();
                    if (instance == null)
                    {
                        var go = new GameObject("ResourceManager");
                        instance = go.AddComponent<ResourceManager>();
                    }
                }
                return instance;
            }
        }
        #endregion

        #region Configuration
        [Header("Resource Prefabs")]
        [SerializeField] private BaseResource experiencePrefab;
        [SerializeField] private BaseResource healthPrefab;
        [SerializeField] private BaseResource powerUpPrefab;
        [SerializeField] private BaseResource currencyPrefab;

        [Header("Pool Configuration")]
        [SerializeField, MinValue(10)] private int experiencePoolInitial = 50;
        [SerializeField, MinValue(10)] private int experiencePoolMax = 100;
        [SerializeField, MinValue(5)] private int healthPoolInitial = 25;
        [SerializeField, MinValue(5)] private int healthPoolMax = 50;
        [SerializeField, MinValue(5)] private int powerUpPoolInitial = 15;
        [SerializeField, MinValue(5)] private int powerUpPoolMax = 30;
        [SerializeField, MinValue(5)] private int currencyPoolInitial = 25;
        [SerializeField, MinValue(5)] private int currencyPoolMax = 50;
        #endregion

        #region State
        private Dictionary<ResourceType, ObjectPool<BaseResource>> resourcePools;
        private bool isInitialized;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeResourcePools();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
        #endregion

        #region Initialization
        private void InitializeResourcePools()
        {
            if (isInitialized) return;

            resourcePools = new Dictionary<ResourceType, ObjectPool<BaseResource>>();

            // Initialize Experience Pool
            if (experiencePrefab != null)
            {
                var experiencePool = PoolManager.Instance.CreatePool(
                    createFunc: () => Instantiate(experiencePrefab),
                    initialSize: experiencePoolInitial,
                    maxSize: experiencePoolMax,
                    poolName: "ExperiencePool"
                );
                resourcePools.Add(ResourceType.Experience, experiencePool);
            }

            // Initialize Health Pool
            if (healthPrefab != null)
            {
                var healthPool = PoolManager.Instance.CreatePool(
                    createFunc: () => Instantiate(healthPrefab),
                    initialSize: healthPoolInitial,
                    maxSize: healthPoolMax,
                    poolName: "HealthPool"
                );
                resourcePools.Add(ResourceType.Health, healthPool);
            }

            // Initialize PowerUp Pool
            if (powerUpPrefab != null)
            {
                var powerUpPool = PoolManager.Instance.CreatePool(
                    createFunc: () => Instantiate(powerUpPrefab),
                    initialSize: powerUpPoolInitial,
                    maxSize: powerUpPoolMax,
                    poolName: "PowerUpPool"
                );
                resourcePools.Add(ResourceType.PowerUp, powerUpPool);
            }

            // Initialize Currency Pool
            if (currencyPrefab != null)
            {
                var currencyPool = PoolManager.Instance.CreatePool(
                    createFunc: () => Instantiate(currencyPrefab),
                    initialSize: currencyPoolInitial,
                    maxSize: currencyPoolMax,
                    poolName: "CurrencyPool"
                );
                resourcePools.Add(ResourceType.Currency, currencyPool);
            }

            isInitialized = true;
            Debug.Log("[ResourceManager] Resource pools initialized");
        }
        #endregion

        #region Resource Spawning
        public BaseResource SpawnResource(ResourceType type, Vector3 position, int value = 1)
        {
            if (!isInitialized)
            {
                Debug.LogError("[ResourceManager] Attempting to spawn resource before initialization!");
                return null;
            }

            if (!resourcePools.TryGetValue(type, out var pool))
            {
                Debug.LogError($"[ResourceManager] No pool found for resource type: {type}");
                return null;
            }

            var resource = pool.Get();
            if (resource != null)
            {
                resource.transform.position = position;
                return resource;
            }

            Debug.LogWarning($"[ResourceManager] Failed to get resource from pool: {type}");
            return null;
        }

        public void SpawnResourceBurst(ResourceType type, Vector3 position, int count, float radius)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * radius;
                Vector3 spawnPosition = position + new Vector3(randomOffset.x, randomOffset.y, 0);
                SpawnResource(type, spawnPosition);
            }
        }
        #endregion

        #region Resource Collection
        // Event for resource collection
        public event Action<ResourceType, int> OnResourceCollected;

        public void CollectResource(ResourceType type, int value)
        {
            OnResourceCollected?.Invoke(type, value);
        }
        #endregion

        #region Debug
        [Button("Spawn Test Resources")]
        private void SpawnTestResources()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[ResourceManager] Cannot spawn test resources in edit mode");
                return;
            }

            Vector3 center = Vector3.zero;
            SpawnResourceBurst(ResourceType.Experience, center, 5, 2f);
            SpawnResourceBurst(ResourceType.Health, center + Vector3.right * 3f, 3, 1f);
            SpawnResourceBurst(ResourceType.PowerUp, center + Vector3.left * 3f, 2, 1f);
            SpawnResourceBurst(ResourceType.Currency, center + Vector3.up * 3f, 4, 1.5f);
        }
        #endregion
    }
} 