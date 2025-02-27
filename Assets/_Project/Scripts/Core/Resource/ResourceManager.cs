using UnityEngine;
using System.Collections.Generic;
using CZ.Core.Pooling;
using NaughtyAttributes;
using Unity.Profiling;
using System;
using System.Collections;
using UnityEditor;
using CZ.Core.Logging;
using System.Reflection;

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
        [SerializeField, MinValue(5)] 
        private int poolSize = 10;
        
        [SerializeField, MinValue(1)]
        private float memoryThresholdPerPoolMB = 64f;
        #endregion

        #region State
        private Dictionary<ResourceType, ObjectPool<BaseResource>> resourcePools;
        private static readonly ProfilerMarker s_cleanupMarker = new(ProfilerCategory.Scripts, "ResourceManager.Cleanup");
        private static readonly ProfilerMarker s_initMarker = new(ProfilerCategory.Scripts, "ResourceManager.Initialize");
        #endregion

        #region PlayerHealth Reflection Cache
        private MonoBehaviour cachedPlayerHealth;
        private MethodInfo cachedHealMethod;
        private bool playerHealthSearched = false;

        private void FindPlayerHealthComponent()
        {
            if (playerHealthSearched) return; // Only search once
            
            playerHealthSearched = true;
            using var _ = new ProfilerMarker("ResourceManager.FindPlayerHealthComponent").Auto();
            
            try
            {
                // Find any GameObject with a component that has the name "PlayerHealth"
                var playerObjects = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                foreach (var component in playerObjects)
                {
                    if (component.GetType().Name == "PlayerHealth")
                    {
                        // When we find it, cache the component and method
                        cachedPlayerHealth = component;
                        cachedHealMethod = component.GetType().GetMethod("Heal");
                        
                        if (cachedHealMethod != null)
                        {
                            CZLogger.LogInfo("[ResourceManager] Found and cached PlayerHealth component", LogCategory.Resource);
                            return;
                        }
                    }
                }
                CZLogger.LogWarning("[ResourceManager] PlayerHealth component not found during initialization", LogCategory.Resource);
            }
            catch (Exception ex)
            {
                CZLogger.LogError($"[ResourceManager] Error finding PlayerHealth: {ex.Message}", LogCategory.Resource);
            }
        }
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

            resourcePools = new Dictionary<ResourceType, ObjectPool<BaseResource>>();
            StartCoroutine(InitializeWhenReady());
            
            // Find PlayerHealth component early
            FindPlayerHealthComponent();
        }

        private IEnumerator InitializeWhenReady()
        {
            float timeout = 5f;
            float elapsed = 0f;

            while (PoolManager.Instance == null && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (PoolManager.Instance == null)
            {
                Debug.LogError("[ResourceManager] PoolManager not available after timeout!");
                yield break;
            }

            InitializeResourcePools();
        }

        private void OnDestroy()
        {
            using var _ = s_cleanupMarker.Auto();

            // Handle cleanup if this is the instance and not quitting
            if (instance == this && !isQuitting)
            {
                Debug.Log("[ResourceManager] Starting destroy cleanup...");
                ClearAllPools();
                
                // Clear cached references
                cachedPlayerHealth = null;
                cachedHealMethod = null;
                playerHealthSearched = false;
                
                isInitialized = false;
                instance = null;
                Debug.Log("[ResourceManager] Destroy cleanup completed");
            }
        }

        private void InitializeResourcePools()
        {
            if (isInitialized) return;

            // Create type-specific pools
            CreatePool(ResourceType.Health, healthPrefab);
            CreatePool(ResourceType.PowerUp, powerUpPrefab);
            CreatePool(ResourceType.Currency, currencyPrefab);
            CreatePool(ResourceType.Experience, experiencePrefab);

            isInitialized = true;
            Debug.Log("Resource pools initialized successfully");
        }

        private void CreatePool(ResourceType type, BaseResource prefab)
        {
            if (prefab == null)
            {
                Debug.LogError($"Cannot create pool for {type}: Prefab is null");
                return;
            }

            if (prefab.ResourceType != type)
            {
                Debug.LogError($"Type mismatch for {type} pool: Prefab has type {prefab.ResourceType}");
                return;
            }

            string poolName = $"Resource_{type}";
            ObjectPool<BaseResource> pool = new ObjectPool<BaseResource>(
                () =>
                {
                    var instance = Instantiate(prefab);
                    instance.transform.SetParent(transform);
                    instance.gameObject.SetActive(false);
                    return instance;
                },
                poolSize,
                poolSize,
                poolName,
                memoryThresholdPerPoolMB
            );

            resourcePools[type] = pool;
            Debug.Log($"Created pool for {type} with size {poolSize}");
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
                    ClearAllPools();
                    
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

        private void ClearAllPools()
        {
            foreach (var pool in resourcePools.Values)
            {
                pool.Clear();
            }
            resourcePools.Clear();
            isInitialized = false;
            Debug.Log("All resource pools cleared");
        }
        #endregion

        #region Resource Spawning
        public BaseResource SpawnResource(ResourceType type, Vector3 position)
        {
            if (!isInitialized)
            {
                CZLogger.LogError($"Cannot spawn resource of type {type} - Manager not initialized!", LogCategory.Resource);
                return null;
            }

            if (isQuitting)
            {
                CZLogger.LogWarning($"Cannot spawn resource of type {type} - Application is quitting", LogCategory.Resource);
                return null;
            }

            if (!resourcePools.TryGetValue(type, out var pool))
            {
                CZLogger.LogError($"No pool found for resource type: {type}. Available pools: {string.Join(", ", resourcePools.Keys)}", LogCategory.Resource);
                return null;
            }

            try
            {
                var resource = pool.Get();
                if (resource != null)
                {
                    resource.transform.position = position;
                    resource.gameObject.SetActive(true);
                    CZLogger.LogDebug($"Successfully spawned resource of type {type} at {position}. Pool count: {pool.CurrentCount}", LogCategory.Resource);
                    return resource;
                }

                CZLogger.LogWarning($"Failed to get resource from pool: {type}. Pool count: {pool.CurrentCount}", LogCategory.Resource);
                return null;
            }
            catch (System.Exception e)
            {
                CZLogger.LogError($"Error spawning resource: {e.Message}\nStack trace: {e.StackTrace}", LogCategory.Resource);
                return null;
            }
        }

        public void SpawnResourceBurst(ResourceType type, Vector3 position, int count, float radius)
        {
            if (!isInitialized || isQuitting)
            {
                CZLogger.LogWarning("Cannot spawn resource burst - manager not ready or quitting", LogCategory.Resource);
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
                    CZLogger.LogWarning($"Resource burst partially failed - spawned {successfulSpawns}/{count} resources", LogCategory.Resource);
                }
                else
                {
                    CZLogger.LogDebug($"Successfully spawned resource burst - {count} resources of type {type}", LogCategory.Resource);
                }
            }
            catch (System.Exception e)
            {
                CZLogger.LogError($"Error during resource burst: {e.Message}", LogCategory.Resource);
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
                
                // Special handling for health resources - they directly heal the player
                if (type == ResourceType.Health)
                {
                    ApplyHealth(null, value);
                }
                
                // Notify all listeners about the resource collection
                OnResourceCollected?.Invoke(type, value);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ResourceManager] Error during resource collection: {e.Message}");
            }
        }
        
        public void ApplyHealth(GameObject target, int healthValue)
        {
            if (target == null)
            {
                CZLogger.LogWarning("[ResourceManager] Cannot apply health to null target", LogCategory.Resource);
                return;
            }
            
            // First make sure we've tried to find the PlayerHealth component
            if (!playerHealthSearched)
            {
                FindPlayerHealthComponent();
            }
            
            // If we have a cached component and method, use them
            if (cachedPlayerHealth != null && cachedHealMethod != null && cachedPlayerHealth)
            {
                try
                {
                    cachedHealMethod.Invoke(cachedPlayerHealth, new object[] { healthValue });
                    CZLogger.LogInfo($"[ResourceManager] Applied {healthValue} health to player", LogCategory.Resource);
                }
                catch (Exception ex)
                {
                    CZLogger.LogError($"[ResourceManager] Error applying health: {ex.Message}", LogCategory.Resource);
                    // Reset cache so we can try to find it again next time
                    cachedPlayerHealth = null;
                    cachedHealMethod = null;
                    playerHealthSearched = false;
                }
            }
            else
            {
                CZLogger.LogWarning("[ResourceManager] PlayerHealth component not available, cannot apply healing", LogCategory.Resource);
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
            
            CZLogger.LogInfo($"Scene unloaded: {scene.name}, performing cleanup", LogCategory.Resource);
            
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
                            CZLogger.LogDebug("Successfully cleared pool during scene cleanup", LogCategory.Resource);
                        }
                        catch (System.Exception e)
                        {
                            CZLogger.LogError($"Error clearing pool during scene cleanup: {e.Message}", LogCategory.Resource);
                        }
                    }
                }
            }
        }
        #endregion
    }
} 