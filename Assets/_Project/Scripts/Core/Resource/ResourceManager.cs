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
        private static bool isQuitting;
        
        public static ResourceManager Instance
        {
            get
            {
                if (isQuitting)
                {
                    Debug.LogWarning("[ResourceManager] Instance requested during application quit, returning null");
                    return null;
                }

                if (instance == null)
                {
                    instance = FindAnyObjectByType<ResourceManager>();
                    
                    if (instance == null)
                    {
                        Debug.LogError("[ResourceManager] No ResourceManager found in scene. Please ensure a ResourceManager prefab is placed in the initial scene.");
                        return null;
                    }
                }
                return instance;
            }
        }
        #endregion

        #region Configuration
        [Header("Resource Prefabs")]
        [SerializeField, Required, InfoBox("Experience resource prefab")] 
        private BaseResource experiencePrefab;
        
        [SerializeField, Required, InfoBox("Health resource prefab")]
        private BaseResource healthPrefab;
        
        [SerializeField, Required, InfoBox("PowerUp resource prefab")]
        private BaseResource powerUpPrefab;
        
        [SerializeField, Required, InfoBox("Currency resource prefab")]
        private BaseResource currencyPrefab;

        [Header("Pool Configuration")]
        [SerializeField, MinValue(10), InfoBox("Initial size of experience pool")] 
        private int experiencePoolInitial = 50;
        
        [SerializeField, MinValue(10)] 
        private int experiencePoolMax = 100;
        
        [SerializeField, MinValue(5)] 
        private int healthPoolInitial = 25;
        
        [SerializeField, MinValue(5)] 
        private int healthPoolMax = 50;
        
        [SerializeField, MinValue(5)] 
        private int powerUpPoolInitial = 15;
        
        [SerializeField, MinValue(5)] 
        private int powerUpPoolMax = 30;
        
        [SerializeField, MinValue(5)] 
        private int currencyPoolInitial = 25;
        
        [SerializeField, MinValue(5)] 
        private int currencyPoolMax = 50;
        #endregion

        #region State
        private Dictionary<ResourceType, ObjectPool<BaseResource>> resourcePools;
        private bool isInitialized;
        private static readonly ProfilerMarker s_cleanupMarker = new(ProfilerCategory.Scripts, "ResourceManager.Cleanup");
        private static readonly ProfilerMarker s_initMarker = new(ProfilerCategory.Scripts, "ResourceManager.Initialize");
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            using var _ = s_initMarker.Auto();

            if (instance != null && instance != this)
            {
                Debug.LogWarning("[ResourceManager] Multiple instances detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            // Reset quitting state on new instance
            isQuitting = false;
            
            // Validate prefabs
            if (!ValidatePrefabs())
            {
                Debug.LogError("[ResourceManager] Required prefabs are missing! Check inspector assignments.");
                Debug.LogError($"[ResourceManager] Experience: {experiencePrefab != null}, Health: {healthPrefab != null}, PowerUp: {powerUpPrefab != null}, Currency: {currencyPrefab != null}");
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeResourcePools();
        }

        private bool ValidatePrefabs()
        {
            bool isValid = true;
            
            if (experiencePrefab == null)
            {
                Debug.LogError("[ResourceManager] Experience prefab is missing!");
                isValid = false;
            }
            
            if (healthPrefab == null)
            {
                Debug.LogError("[ResourceManager] Health prefab is missing!");
                isValid = false;
            }
            
            if (powerUpPrefab == null)
            {
                Debug.LogError("[ResourceManager] PowerUp prefab is missing!");
                isValid = false;
            }
            
            if (currencyPrefab == null)
            {
                Debug.LogError("[ResourceManager] Currency prefab is missing!");
                isValid = false;
            }

            return isValid;
        }

        private void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        }

        private void OnDestroy()
        {
            using var _ = s_cleanupMarker.Auto();

            if (instance == this)
            {
                CleanupPools();
                isInitialized = false;
                instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            isQuitting = true;
            if (instance == this)
            {
                CleanupPools();
                instance = null;
            }
        }

        private void CleanupPools()
        {
            if (resourcePools != null)
            {
                foreach (var pool in resourcePools.Values)
                {
                    if (pool != null)
                    {
                        try
                        {
                            pool.Clear();
                            Debug.Log($"[ResourceManager] Successfully cleared pool");
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[ResourceManager] Error clearing pool: {e.Message}");
                        }
                    }
                }
                resourcePools.Clear();
            }
        }
        #endregion

        #region Initialization
        private void InitializeResourcePools()
        {
            using var _ = s_initMarker.Auto();
            
            if (isInitialized)
            {
                Debug.LogWarning("[ResourceManager] Pools already initialized!");
                return;
            }

            try
            {
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
                    if (experiencePool != null)
                    {
                        resourcePools.Add(ResourceType.Experience, experiencePool);
                        Debug.Log("[ResourceManager] Experience pool initialized successfully");
                    }
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
                    if (healthPool != null)
                    {
                        resourcePools.Add(ResourceType.Health, healthPool);
                        Debug.Log("[ResourceManager] Health pool initialized successfully");
                    }
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
                    if (powerUpPool != null)
                    {
                        resourcePools.Add(ResourceType.PowerUp, powerUpPool);
                        Debug.Log("[ResourceManager] PowerUp pool initialized successfully");
                    }
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
                    if (currencyPool != null)
                    {
                        resourcePools.Add(ResourceType.Currency, currencyPool);
                        Debug.Log("[ResourceManager] Currency pool initialized successfully");
                    }
                }

                isInitialized = resourcePools.Count > 0;
                if (isInitialized)
                {
                    Debug.Log($"[ResourceManager] Successfully initialized {resourcePools.Count} resource pools");
                }
                else
                {
                    Debug.LogError("[ResourceManager] Failed to initialize any resource pools!");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ResourceManager] Error during pool initialization: {e.Message}");
                CleanupPools();
                isInitialized = false;
            }
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

        #region Scene Management
        private void HandleSceneUnloaded(UnityEngine.SceneManagement.Scene scene)
        {
            using var _ = s_cleanupMarker.Auto();
            
            Debug.Log($"[ResourceManager] Scene unloaded: {scene.name}, performing cleanup");
            
            // Perform cleanup but maintain the manager
            if (resourcePools != null)
            {
                foreach (var pool in resourcePools.Values)
                {
                    if (pool != null)
                    {
                        try
                        {
                            pool.Clear();
                            Debug.Log($"[ResourceManager] Successfully cleared pool during scene cleanup");
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[ResourceManager] Error clearing pool during scene cleanup: {e.Message}");
                        }
                    }
                }
            }
        }
        #endregion
    }
} 