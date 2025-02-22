using UnityEngine;
using System.Collections.Generic;
using CZ.Core.Pooling;
using NaughtyAttributes;
using Unity.Profiling;
using System;
using System.Collections;
using UnityEditor;

namespace CZ.Core.Resource
{
    public class ResourceManager : MonoBehaviour
    {
        #region Singleton
        private static ResourceManager instance;
        private static bool isQuitting;
        private static bool isInitialized;
        
        public static ResourceManager Instance
        {
            get
            {
                if (isQuitting)
                {
                    Debug.LogWarning("[ResourceManager] Instance requested during application quit, returning null");
                    return null;
                }

                if (instance == null && !isQuitting)
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

        [Header("Resource Configuration")]
        [SerializeField, Required]
        private ResourceConfiguration resourceConfig;

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

            instance = this;
            DontDestroyOnLoad(gameObject);
            
            isQuitting = false;
            
            if (!ValidatePrefabs())
            {
                Debug.LogError("[ResourceManager] Required prefabs are missing! Check inspector assignments.");
                Debug.LogError($"[ResourceManager] Experience: {experiencePrefab != null}, Health: {healthPrefab != null}, PowerUp: {powerUpPrefab != null}, Currency: {currencyPrefab != null}");
                instance = null;
                return;
            }

            StartCoroutine(InitializeWhenPoolManagerReady());
        }

        private IEnumerator InitializeWhenPoolManagerReady()
        {
            Debug.Log("[ResourceManager] Starting initialization sequence...");
            
            float timeout = 5f;
            float elapsed = 0f;
            
            while (PoolManager.Instance == null && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (PoolManager.Instance == null)
            {
                Debug.LogError("[ResourceManager] Failed to initialize - PoolManager not available after timeout!");
                isInitialized = false;
                yield break;
            }
            
            Debug.Log("[ResourceManager] PoolManager ready, initializing resource pools...");
            
            try
            {
                resourcePools = new Dictionary<ResourceType, ObjectPool<BaseResource>>();
                
                // Create pools with unique identifiers for each resource type
                if (experiencePrefab != null)
                {
                    var expPool = CreateTypeSpecificPool(
                        ResourceType.Experience,
                        experiencePrefab,
                        experiencePoolInitial,
                        experiencePoolMax
                    );
                    if (expPool != null)
                    {
                        resourcePools.Add(ResourceType.Experience, expPool);
                        Debug.Log($"[ResourceManager] Created Experience pool with size {experiencePoolInitial}");
                    }
                }
                
                if (healthPrefab != null)
                {
                    var healthPool = CreateTypeSpecificPool(
                        ResourceType.Health,
                        healthPrefab,
                        healthPoolInitial,
                        healthPoolMax
                    );
                    if (healthPool != null)
                    {
                        resourcePools.Add(ResourceType.Health, healthPool);
                        Debug.Log($"[ResourceManager] Created Health pool with size {healthPoolInitial}");
                    }
                }
                
                if (powerUpPrefab != null)
                {
                    var powerUpPool = CreateTypeSpecificPool(
                        ResourceType.PowerUp,
                        powerUpPrefab,
                        powerUpPoolInitial,
                        powerUpPoolMax
                    );
                    if (powerUpPool != null)
                    {
                        resourcePools.Add(ResourceType.PowerUp, powerUpPool);
                        Debug.Log($"[ResourceManager] Created PowerUp pool with size {powerUpPoolInitial}");
                    }
                }
                
                if (currencyPrefab != null)
                {
                    var currencyPool = CreateTypeSpecificPool(
                        ResourceType.Currency,
                        currencyPrefab,
                        currencyPoolInitial,
                        currencyPoolMax
                    );
                    if (currencyPool != null)
                    {
                        resourcePools.Add(ResourceType.Currency, currencyPool);
                        Debug.Log($"[ResourceManager] Created Currency pool with size {currencyPoolInitial}");
                    }
                }

                isInitialized = resourcePools.Count > 0;
                
                if (isInitialized)
                {
                    Debug.Log($"[ResourceManager] Successfully initialized {resourcePools.Count} resource pools");
                    // Initialize all pool objects
                    foreach (var pool in resourcePools.Values)
                    {
                        for (int i = 0; i < pool.CurrentCount; i++)
                        {
                            var obj = pool.Get();
                            if (obj != null)
                            {
                                obj.gameObject.SetActive(false);
                                pool.Return(obj);
                            }
                        }
                    }
                }
                else
                {
                    Debug.LogError("[ResourceManager] Failed to initialize any resource pools!");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ResourceManager] Error during pool initialization: {e.Message}\nStack trace: {e.StackTrace}");
                CleanupPools();
                isInitialized = false;
            }
        }

        private ObjectPool<BaseResource> CreateTypeSpecificPool(ResourceType type, BaseResource prefab, int initialSize, int maxSize)
        {
            string poolName = $"ResourcePool_{type}";
            return PoolManager.Instance.CreatePool(
                createFunc: () => {
                    var resource = Instantiate(prefab);
                    resource.name = $"{type}Resource(Clone)";
                    
                    // Ensure the resource is properly configured
                    var spriteRenderer = resource.GetComponent<SpriteRenderer>();
                    if (spriteRenderer != null)
                    {
                        // Set type-specific color from configuration
                        Color color = type switch
                        {
                            ResourceType.Health => resourceConfig.healthColor,
                            ResourceType.Experience => resourceConfig.experienceColor,
                            ResourceType.PowerUp => resourceConfig.powerUpColor,
                            ResourceType.Currency => resourceConfig.currencyColor,
                            _ => Color.white
                        };
                        spriteRenderer.color = color;
                        Debug.Log($"[ResourceManager] Created {type} resource with color: {color}");
                    }
                    else
                    {
                        Debug.LogError($"[ResourceManager] Failed to get SpriteRenderer for {type} resource");
                    }

                    // Set type-specific value
                    var baseResource = resource.GetComponent<BaseResource>();
                    if (baseResource != null)
                    {
                        baseResource.SetResourceType(type);
                        baseResource.SetResourceValue(type switch
                        {
                            ResourceType.Health => resourceConfig.baseHealthValue,
                            ResourceType.Experience => resourceConfig.baseExperienceValue,
                            ResourceType.PowerUp => 1,
                            ResourceType.Currency => resourceConfig.baseCurrencyValue,
                            _ => 1
                        });
                    }
                    
                    return resource;
                },
                initialSize: initialSize,
                maxSize: maxSize,
                poolName: poolName
            );
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

        private void OnApplicationQuit()
        {
            isQuitting = true;
            if (instance == this)
            {
                using var _ = s_cleanupMarker.Auto();
                Debug.Log("[ResourceManager] Starting application quit cleanup...");
                
                try
                {
                    // Cleanup pools first
                    CleanupPools();
                    
                    // Clear instance and state
                    instance = null;
                    isInitialized = false;
                    
                    Debug.Log("[ResourceManager] Cleanup completed successfully");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[ResourceManager] Error during quit cleanup: {e.Message}");
                }
            }
        }

        private void OnDestroy()
        {
            using var _ = s_cleanupMarker.Auto();

            if (instance == this && !isQuitting)
            {
                Debug.Log("[ResourceManager] Starting destroy cleanup...");
                CleanupPools();
                isInitialized = false;
                instance = null;
                Debug.Log("[ResourceManager] Destroy cleanup completed");
            }
        }

        private void CleanupPools()
        {
            if (resourcePools == null) return;

            foreach (var pool in resourcePools.Values)
            {
                if (pool != null)
                {
                    try
                    {
                        pool.Clear();
                        Debug.Log($"[ResourceManager] Successfully cleared pool");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[ResourceManager] Error clearing pool: {e.Message}");
                    }
                }
            }
            resourcePools.Clear();
        }
        #endregion

        #region Resource Spawning
        public BaseResource SpawnResource(ResourceType type, Vector3 position, int value = 1)
        {
            if (!isInitialized)
            {
                Debug.LogError($"[ResourceManager] Cannot spawn resource of type {type} - Manager not initialized!");
                return null;
            }

            if (isQuitting)
            {
                Debug.LogWarning($"[ResourceManager] Cannot spawn resource of type {type} - Application is quitting");
                return null;
            }

            if (!resourcePools.TryGetValue(type, out var pool))
            {
                Debug.LogError($"[ResourceManager] No pool found for resource type: {type}. Available pools: {string.Join(", ", resourcePools.Keys)}");
                return null;
            }

            try
            {
                var resource = pool.Get();
                if (resource != null)
                {
                    resource.transform.position = position;
                    resource.gameObject.SetActive(true);
                    Debug.Log($"[ResourceManager] Successfully spawned resource of type {type} at {position}. Pool count: {pool.CurrentCount}");
                    return resource;
                }

                Debug.Log($"[ResourceManager] Failed to get resource from pool: {type}. Pool count: {pool.CurrentCount}");
                return null;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ResourceManager] Error spawning resource: {e.Message}\nStack trace: {e.StackTrace}");
                return null;
            }
        }

        public void SpawnResourceBurst(ResourceType type, Vector3 position, int count, float radius)
        {
            if (!isInitialized || isQuitting)
            {
                Debug.LogWarning("[ResourceManager] Cannot spawn resource burst - manager not ready or quitting");
                return;
            }

            try
            {
                int successfulSpawns = 0;
                for (int i = 0; i < count; i++)
                {
                    Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * radius;
                    Vector3 spawnPosition = position + new Vector3(randomOffset.x, randomOffset.y, 0);
                    var resource = SpawnResource(type, spawnPosition);
                    if (resource != null)
                    {
                        successfulSpawns++;
                    }
                }

                if (successfulSpawns < count)
                {
                    Debug.LogWarning($"[ResourceManager] Resource burst partially failed - spawned {successfulSpawns}/{count} resources");
                }
                else
                {
                    Debug.Log($"[ResourceManager] Successfully spawned resource burst - {count} resources of type {type}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ResourceManager] Error during resource burst: {e.Message}");
            }
        }
        #endregion

        #region Resource Collection
        // Event for resource collection
        public event Action<ResourceType, int> OnResourceCollected;

        public void CollectResource(ResourceType type, int value)
        {
            try
            {
                if (isQuitting)
                {
                    Debug.LogWarning($"[ResourceManager] Cannot collect resource - Application is quitting");
                    return;
                }

                Debug.Log($"[ResourceManager] Collecting resource: {type} with value {value}");
                OnResourceCollected?.Invoke(type, value);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ResourceManager] Error during resource collection: {e.Message}");
            }
        }
        #endregion

        #region Pool Management
        public bool ReturnResourceToPool(BaseResource resource)
        {
            if (!isInitialized || resource == null)
            {
                return false;
            }

            if (resourcePools.TryGetValue(resource.ResourceType, out var pool))
            {
                pool.Return(resource);
                return true;
            }

            Debug.LogError($"[ResourceManager] No pool found for resource type: {resource.ResourceType}");
            return false;
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