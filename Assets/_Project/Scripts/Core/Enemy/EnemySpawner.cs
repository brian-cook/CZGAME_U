using UnityEngine;
using CZ.Core.Pooling;
using CZ.Core.Interfaces;
using Unity.Profiling;
using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CZ.Core.Logging;

namespace CZ.Core.Enemy
{
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Spawn Configuration")]
        [SerializeField, Required] private List<GameObject> enemyPrefabs = new List<GameObject>();
        [SerializeField, Required] private GameObject defaultEnemyPrefab;
        [SerializeField] private float spawnInterval = 1f;
        [SerializeField] private int maxEnemiesPerWave = 5;
        
        [Header("Spawn Area")]
        [SerializeField] private float spawnRadius = 10f;
        [MinValue(0)] [SerializeField] private float minSpawnDistance = 5f;
        
        // Performance monitoring
        private static readonly ProfilerMarker s_spawnMarker = 
            new(ProfilerCategory.Scripts, "EnemySpawner.SpawnEnemy");
        private static readonly ProfilerMarker s_poolInitMarker = 
            new(ProfilerCategory.Scripts, "EnemySpawner.InitializePool");
        private static readonly ProfilerMarker s_despawnMarker =
            new(ProfilerCategory.Scripts, "EnemySpawner.DespawnEnemy");
        
        private Dictionary<GameObject, ObjectPool<BaseEnemy>> enemyPools = new Dictionary<GameObject, ObjectPool<BaseEnemy>>();
        private bool isSpawning;
        private float nextSpawnTime;
        private HashSet<BaseEnemy> activeEnemies = new HashSet<BaseEnemy>();
        private bool isInitialized;
        private bool isInitializing;
        private IPositionProvider targetPositionProvider;
        private bool isGamePlaying;
        
        [Header("Update Settings")]
        [SerializeField, MinValue(0.01f)]
        private float targetUpdateInterval = 0.05f;
        private float nextTargetUpdateTime;
        private Vector3 debugTargetPosition; // For test visualization only
        
        public float SpawnInterval => spawnInterval;
        public int ActiveEnemyCount => activeEnemies.Count;
        
        private void OnEnable()
        {
            CZLogger.LogInfo("OnEnable called", LogCategory.Enemy);
            SubscribeToGameManager();
        }

        private void OnDisable()
        {
            CZLogger.LogInfo("OnDisable called", LogCategory.Enemy);
            UnsubscribeFromGameManager();
            StopSpawning();
        }

        private void SubscribeToGameManager()
        {
            if (GameManager.Instance != null)
            {
                UnsubscribeFromGameManager(); // Ensure no duplicate subscriptions
                GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
                
                // Check current state
                if (GameManager.Instance.CurrentGameState == GameManager.GameState.Playing)
                {
                    HandleGameStateChanged(GameManager.GameState.Playing);
                }
            }
        }

        private void UnsubscribeFromGameManager()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            }
        }

        private void Awake()
        {
            CZLogger.LogInfo("Awake called", LogCategory.Enemy);
            isInitializing = false;
            isInitialized = false;
        }

        private void Start()
        {
            CZLogger.LogInfo("Start called", LogCategory.Enemy);
            
            // Verify component state
            CZLogger.LogDebug($"Component State - IsInitialized: {isInitialized}, IsInitializing: {isInitializing}, HasPrefabs: {enemyPrefabs.Count > 0}", LogCategory.Enemy);
            
            // Find target position provider
            targetPositionProvider = PositionProviderHelper.FindPositionProvider();
            
            if (targetPositionProvider == null)
            {
                CZLogger.LogWarning("IPositionProvider not found in scene!", LogCategory.Enemy);
            }
            else
            {
                CZLogger.LogDebug("IPositionProvider found successfully", LogCategory.Enemy);
            }
            
            // If we have prefabs (including the default one), initialize pool for each
            if (enemyPrefabs.Count > 0 || defaultEnemyPrefab != null)
            {
                CZLogger.LogInfo($"Initializing pools for {enemyPrefabs.Count} enemy types", LogCategory.Enemy);
                InitializePools();
            }
            else
            {
                CZLogger.LogError("No enemy prefabs assigned in inspector!", LogCategory.Enemy);
            }
            
            // Subscribe to GameManager after initialization
            SubscribeToGameManager();
            
            // Log final state
            CZLogger.LogDebug($"Start completed - IsInitialized: {isInitialized}, IsGamePlaying: {isGamePlaying}, IsSpawning: {isSpawning}", LogCategory.Enemy);
        }
        
        private void HandleGameStateChanged(GameManager.GameState newState)
        {
            bool wasGamePlaying = isGamePlaying;
            isGamePlaying = newState == GameManager.GameState.Playing;
            
            // Only process if state actually changed
            if (wasGamePlaying != isGamePlaying)
            {
                CZLogger.LogInfo($"Game state changed to {newState} (Previous isGamePlaying: {wasGamePlaying})", LogCategory.Enemy);
                
                if (!isGamePlaying)
                {
                    CZLogger.LogInfo("Game no longer playing, stopping spawn", LogCategory.Enemy);
                    StopSpawning();
                    DespawnAllEnemies();
                }
                else if (isInitialized && !GameManager.Instance.IsInEmergencyMode)
                {
                    CZLogger.LogInfo("Starting spawning due to game state change to Playing", LogCategory.Enemy);
                    StartSpawning();
                }
                else
                {
                    string reason = !isInitialized ? "not initialized" : "in emergency memory state";
                    CZLogger.LogWarning($"Cannot start spawning - {reason} when game state changed to Playing (Init: {isInitialized}, Initializing: {isInitializing})", LogCategory.Enemy);
                    
                    // Attempt late initialization if we have prefabs and not in emergency mode
                    if ((enemyPrefabs.Count > 0 || defaultEnemyPrefab != null) && !isInitializing && !GameManager.Instance.IsInEmergencyMode)
                    {
                        CZLogger.LogInfo("Attempting late initialization", LogCategory.Enemy);
                        InitializePools();
                        if (isInitialized)
                        {
                            StartSpawning();
                        }
                    }
                }
            }
        }
        
        private void InitializePools()
        {
            using var _ = s_poolInitMarker.Auto();
            
            if (isInitialized)
            {
                CZLogger.LogInfo("Enemy pools already initialized", LogCategory.Enemy);
                return;
            }
            
            isInitializing = true;
            
            // Ensure defaultEnemyPrefab is assigned - if no enemy prefabs are set, create one by default
            if (enemyPrefabs.Count == 0 && defaultEnemyPrefab != null)
            {
                CZLogger.LogInfo($"No prefabs in list. Adding default prefab {defaultEnemyPrefab.name}", LogCategory.Enemy);
                enemyPrefabs.Add(defaultEnemyPrefab);
            }
            
            if (enemyPrefabs.Count == 0)
            {
                CZLogger.LogError("No enemy prefabs assigned! Please assign at least one prefab in the inspector.", LogCategory.Enemy);
                isInitializing = false;
                return;
            }
            
            bool allPoolsInitialized = true;
            
            CZLogger.LogInfo($"Starting initialization of {enemyPrefabs.Count} enemy pools", LogCategory.Enemy);
            
            // Validate enemy prefabs before pool creation
            for (int i = enemyPrefabs.Count - 1; i >= 0; i--)
            {
                if (enemyPrefabs[i] == null)
                {
                    CZLogger.LogWarning($"Null enemy prefab found at index {i}! Removing from list.", LogCategory.Enemy);
                    enemyPrefabs.RemoveAt(i);
                    continue;
                }
                
                BaseEnemy baseEnemy = enemyPrefabs[i].GetComponent<BaseEnemy>();
                if (baseEnemy == null)
                {
                    CZLogger.LogError($"Enemy prefab {enemyPrefabs[i].name} at index {i} does not have a BaseEnemy component! Removing from list.", LogCategory.Enemy);
                    enemyPrefabs.RemoveAt(i);
                    continue;
                }
            }
            
            // Check again after removing invalid prefabs
            if (enemyPrefabs.Count == 0)
            {
                CZLogger.LogError("No valid enemy prefabs left after validation! Cannot initialize pools.", LogCategory.Enemy);
                isInitializing = false;
                return;
            }
            
            // Initialize a pool for each enemy prefab
            foreach (var prefab in enemyPrefabs)
            {
                CZLogger.LogInfo($"Initializing pool for prefab: {prefab.name}", LogCategory.Enemy);
                
                if (enemyPools.ContainsKey(prefab))
                {
                    CZLogger.LogDebug($"Pool for {prefab.name} already exists. Skipping.", LogCategory.Enemy);
                    continue;
                }
                
                var baseEnemy = prefab.GetComponent<BaseEnemy>();
                if (baseEnemy == null)
                {
                    CZLogger.LogError($"Enemy prefab {prefab.name} must have BaseEnemy component!", LogCategory.Enemy);
                    allPoolsInitialized = false;
                    continue;
                }
                
                try
                {
                    // Store original prefab activation state
                    bool wasActive = prefab.activeSelf;
                    
                    // Ensure prefab is inactive before creating pool
                    if (wasActive)
                    {
                        CZLogger.LogDebug($"Deactivating prefab {prefab.name} before pool creation", LogCategory.Enemy);
                        prefab.SetActive(false);
                    }
                    
                    // Get memory-aware pool size from GameManager
                    int initialPoolSize = Mathf.Min(maxEnemiesPerWave, 5); // Start with smaller initial size
                    int maxPoolSize = Mathf.Min(maxEnemiesPerWave * 2, 20); // Cap maximum size
                    
                    CZLogger.LogInfo($"Creating pool for {prefab.name} with initial size {initialPoolSize} and max size {maxPoolSize}", LogCategory.Enemy);
                    
                    // Use PoolManager instead of local pool for each prefab
                    ObjectPool<BaseEnemy> pool = PoolManager.Instance.CreatePool(
                        createFunc: () => {
                            var inst = Instantiate(prefab);
                            var enemyComponent = inst.GetComponent<BaseEnemy>();
                            
                            if (enemyComponent == null)
                            {
                                CZLogger.LogError($"Failed to get BaseEnemy component from instantiated prefab {prefab.name}", LogCategory.Enemy);
                                Destroy(inst); // Clean up the instance
                                return null;
                            }
                            
                            inst.SetActive(false);
                            return enemyComponent;
                        },
                        initialSize: initialPoolSize,
                        maxSize: maxPoolSize,
                        poolName: $"EnemyPool_{prefab.name}"
                    );
                    
                    if (pool == null)
                    {
                        CZLogger.LogError($"Failed to create pool for {prefab.name}", LogCategory.Enemy);
                        allPoolsInitialized = false;
                        continue;
                    }
                    
                    // Add pool to dictionary
                    enemyPools[prefab] = pool;
                    
                    // Restore original prefab state if needed
                    if (wasActive && !prefab.activeSelf)
                    {
                        CZLogger.LogDebug($"Restoring prefab {prefab.name} active state to {wasActive}", LogCategory.Enemy);
                        prefab.SetActive(true);
                    }
                    
                    CZLogger.LogInfo($"Pool for {prefab.name} created successfully with {pool.CurrentCount} instances", LogCategory.Enemy);
                }
                catch (System.Exception e)
                {
                    CZLogger.LogError($"Failed to create pool for {prefab.name}: {e.Message}\n{e.StackTrace}", LogCategory.Enemy);
                    allPoolsInitialized = false;
                }
            }
            
            if (allPoolsInitialized && enemyPools.Count > 0)
            {
                CZLogger.LogInfo($"All {enemyPools.Count} enemy pools initialized successfully", LogCategory.Enemy);
                isInitialized = true;
            }
            else
            {
                CZLogger.LogError($"Failed to initialize all enemy pools. Successful pools: {enemyPools.Count}, Failed pools: {enemyPrefabs.Count - enemyPools.Count}", LogCategory.Enemy);
                isInitialized = enemyPools.Count > 0; // At least we can spawn from the pools that did initialize
            }
            
            isInitializing = false;
        }
        
        public void AddEnemyPrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                CZLogger.LogError("Cannot add null enemy prefab!", LogCategory.Enemy);
                return;
            }
            
            if (prefab.GetComponent<BaseEnemy>() == null)
            {
                CZLogger.LogError("Prefab must have BaseEnemy component!", LogCategory.Enemy);
                return;
            }
            
            // Add to prefabs list if not already there
            if (!enemyPrefabs.Contains(prefab))
            {
                enemyPrefabs.Add(prefab);
                
                // If we're already initialized, create a pool for this prefab
                if (isInitialized && !enemyPools.ContainsKey(prefab))
                {
                    try
                    {
                        // Ensure prefab is inactive
                        prefab.SetActive(false);
                        
                        // Create pool for new prefab
                        int initialPoolSize = Mathf.Min(maxEnemiesPerWave, 5);
                        int maxPoolSize = Mathf.Min(maxEnemiesPerWave * 2, 20);
                        
                        ObjectPool<BaseEnemy> pool = PoolManager.Instance.CreatePool(
                            createFunc: () => {
                                var obj = Instantiate(prefab).GetComponent<BaseEnemy>();
                                obj.gameObject.SetActive(false);
                                return obj;
                            },
                            initialSize: initialPoolSize,
                            maxSize: maxPoolSize,
                            poolName: $"EnemyPool_{prefab.name}"
                        );
                        
                        enemyPools.Add(prefab, pool);
                        CZLogger.LogInfo($"Pool for {prefab.name} created successfully.", LogCategory.Enemy);
                    }
                    catch (System.Exception e)
                    {
                        CZLogger.LogError($"Failed to create pool for {prefab.name}: {e.Message}", LogCategory.Enemy);
                    }
                }
            }
        }
        
        public void RemoveEnemyPrefab(GameObject prefab)
        {
            if (prefab == null) return;
            
            // Remove from list
            enemyPrefabs.Remove(prefab);
            
            // Cleanup pool if it exists
            if (enemyPools.TryGetValue(prefab, out var pool))
            {
                // Despawn any active enemies of this type
                var enemiesToDespawn = activeEnemies.Where(e => e.gameObject.name.Contains(prefab.name)).ToList();
                foreach (var enemy in enemiesToDespawn)
                {
                    DespawnEnemy(enemy);
                }
                
                // Remove from pools dictionary
                enemyPools.Remove(prefab);
                
                // Note: PoolManager doesn't provide a method to destroy individual pools
                // The pool will be garbage collected when no longer referenced
                CZLogger.LogInfo($"Removed prefab {prefab.name} and cleared its pool references.", LogCategory.Enemy);
            }
        }
        
        public void SetDefaultEnemyPrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                CZLogger.LogError("Cannot set null default enemy prefab!", LogCategory.Enemy);
                return;
            }
            
            if (prefab.GetComponent<BaseEnemy>() == null)
            {
                CZLogger.LogError("Prefab must have BaseEnemy component!", LogCategory.Enemy);
                return;
            }
            
            defaultEnemyPrefab = prefab;
            
            // Add to prefabs list if not already there
            if (!enemyPrefabs.Contains(prefab))
            {
                AddEnemyPrefab(prefab);
            }
        }
        
        /// <summary>
        /// Backwards compatibility method for setting a single enemy prefab.
        /// Sets the prefab as both the default and adds it to the prefab list.
        /// </summary>
        /// <param name="prefab">The enemy prefab to set</param>
        public void SetEnemyPrefab(GameObject prefab)
        {
            // Clear existing prefabs for backward compatibility (tests expect single prefab behavior)
            enemyPrefabs.Clear();
            
            // Use new methods
            SetDefaultEnemyPrefab(prefab);
            AddEnemyPrefab(prefab);
            
            CZLogger.LogInfo($"Using backward compatibility SetEnemyPrefab with {prefab?.name ?? "null"}", LogCategory.Enemy);
        }
        
        public void SetSpawnCount(int count)
        {
            if (count < 0)
            {
                CZLogger.LogWarning("Invalid spawn count (negative value). Setting to 0.", LogCategory.Enemy);
                maxEnemiesPerWave = 0;
            }
            else
            {
                maxEnemiesPerWave = count;
                CZLogger.LogInfo($"Spawn count set to {count}", LogCategory.Enemy);
            }
        }
        
        public void StartSpawning()
        {
            if (isSpawning) return;
            
            CZLogger.LogInfo("Starting enemy spawning", LogCategory.Enemy);
            isSpawning = true;
            nextSpawnTime = Time.time;
        }
        
        public void StopSpawning()
        {
            isSpawning = false;
        }
        
        public void DespawnAllEnemies()
        {
            using var _ = s_despawnMarker.Auto();
            
            if (activeEnemies.Count > 0)
            {
                CZLogger.LogInfo($"Despawning {activeEnemies.Count} active enemies", LogCategory.Enemy);
                
                var enemiesToDespawn = activeEnemies.ToList(); // Create copy to avoid collection modification issues
                foreach (var enemy in enemiesToDespawn)
                {
                    if (enemy != null)
                    {
                        DespawnEnemy(enemy);
                    }
                }
                
                activeEnemies.Clear();
            }
        }
        
        private void DespawnEnemy(BaseEnemy enemy)
        {
            if (enemy == null) return;
            
            using var _ = s_despawnMarker.Auto();
            
            // Remove from active list
            activeEnemies.Remove(enemy);
            
            // Call OnDespawn and return to pool
            enemy.OnDespawn();
            enemy.gameObject.SetActive(false);
            
            // Find the original prefab for this enemy
            var prefabFound = false;
            foreach (var prefab in enemyPrefabs)
            {
                if (enemy.gameObject.name.Contains(prefab.name) && enemyPools.TryGetValue(prefab, out var pool))
                {
                    pool.Return(enemy);
                    prefabFound = true;
                    break;
                }
            }
            
            if (!prefabFound)
            {
                CZLogger.LogWarning($"Could not find matching pool for enemy {enemy.gameObject.name}. Destroying instead.", LogCategory.Enemy);
                Destroy(enemy.gameObject);
            }
        }
        
        private void Update()
        {
            if (!isInitialized || !isGamePlaying || !isSpawning) return;
            
            // Get target position from provider or debug value
            Vector3 currentPlayerPos;
            bool hasValidPosition = false;
            
            // First try to get position from position provider
            if (targetPositionProvider != null)
            {
                try
                {
                    currentPlayerPos = targetPositionProvider.GetPosition();
                    hasValidPosition = true;
                }
                catch (System.Exception e)
                {
                    CZLogger.LogWarning($"Error getting position from position provider: {e.Message}", LogCategory.Enemy);
                    currentPlayerPos = debugTargetPosition; // Fallback to debug position
                }
            }
            else
            {
                // Fallback to debug position
                currentPlayerPos = debugTargetPosition;
            }
            
            // Validate that we have a usable position - if debugTargetPosition is Vector3.zero, 
            // it might indicate it hasn't been set
            if (!hasValidPosition && currentPlayerPos == Vector3.zero)
            {
                CZLogger.LogWarning("No valid target position available for enemy spawning. Using default of (0,0,10).", LogCategory.Enemy);
                currentPlayerPos = new Vector3(0, 0, 10); // Use a safe default value off-center
            }
            
            // Check if it's time to spawn a new enemy
            if (Time.time >= nextSpawnTime && activeEnemies.Count < maxEnemiesPerWave)
            {
                // Pick a random enemy type from the available prefabs
                SpawnRandomEnemy(currentPlayerPos);
                
                // Set time for next spawn
                nextSpawnTime = Time.time + spawnInterval;
            }
            
            // Update target position for all enemies periodically
            if (Time.time >= nextTargetUpdateTime)
            {
                UpdateEnemyTargets(currentPlayerPos);
                nextTargetUpdateTime = Time.time + targetUpdateInterval;
            }
        }
        
        private void UpdateEnemyTargets(Vector3 currentPlayerPos)
        {
            if (activeEnemies.Count == 0) return;
            
            // Create a copy of the collection to avoid modification issues during iteration
            var enemiesCopy = activeEnemies.ToArray();
            
            foreach (var enemy in enemiesCopy)
            {
                if (enemy == null) continue;
                
                try
                {
                    // Check if the enemy is still active (could have been despawned in another process)
                    if (enemy.gameObject.activeInHierarchy)
                    {
                        enemy.SetTarget(currentPlayerPos);
                    }
                    else
                    {
                        // Remove inactive enemies from our tracking list
                        activeEnemies.Remove(enemy);
                        CZLogger.LogDebug($"Removed inactive enemy from tracking list, Active count: {activeEnemies.Count}", LogCategory.Enemy);
                    }
                }
                catch (System.Exception e)
                {
                    CZLogger.LogError($"Error updating target for enemy {enemy.name}: {e.Message}", LogCategory.Enemy);
                    // Continue with other enemies even if one fails
                }
            }
        }
        
        private void SpawnRandomEnemy(Vector3 currentPlayerPos)
        {
            if (enemyPrefabs.Count == 0)
            {
                CZLogger.LogError("No enemy prefabs available for spawning", LogCategory.Enemy);
                return;
            }
            
            int randomIndex = UnityEngine.Random.Range(0, enemyPrefabs.Count);
            GameObject selectedPrefab = enemyPrefabs[randomIndex];
            
            if (selectedPrefab == null)
            {
                CZLogger.LogError("Selected prefab is null", LogCategory.Enemy);
                return;
            }
            
            SpawnEnemy(selectedPrefab, currentPlayerPos);
        }
        
        private void SpawnEnemy(GameObject prefab, Vector3 currentPlayerPos)
        {
            using var _ = s_spawnMarker.Auto();
            
            if (!isInitialized || !isGamePlaying)
            {
                CZLogger.LogWarning("Cannot spawn enemy - system not initialized or game not playing", LogCategory.Enemy);
                return;
            }
            
            if (!enemyPools.TryGetValue(prefab, out var pool))
            {
                CZLogger.LogError($"Pool for prefab {prefab.name} not found!", LogCategory.Enemy);
                return;
            }
            
            try
            {
                // Generate spawn position
                Vector3 spawnPosition = GenerateSpawnPosition(currentPlayerPos);
                
                // Get enemy from pool
                BaseEnemy enemy = pool.Get();
                if (enemy == null)
                {
                    CZLogger.LogError($"Failed to get enemy from pool for {prefab.name}", LogCategory.Enemy);
                    return;
                }
                
                // Move enemy to spawn position
                enemy.transform.position = spawnPosition;
                
                // Ensure the enemy object is active
                enemy.gameObject.SetActive(true);
                
                // Ensure sprite renderer is visible
                SpriteRenderer spriteRenderer = enemy.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    spriteRenderer.enabled = true;
                    // Ensure full opacity
                    spriteRenderer.color = new Color(spriteRenderer.color.r, spriteRenderer.color.g, spriteRenderer.color.b, 1f);
                }
                
                // Ensure proper layer
                enemy.gameObject.layer = LayerMask.NameToLayer("Enemy");
                
                // Force initialization if needed by calling OnSpawn (this should happen in the pool)
                // But we explicitly call it here to ensure initialization
                enemy.OnSpawn();
                
                // Add to active enemies list
                activeEnemies.Add(enemy);
                
                // After we're sure the enemy is initialized, set the target
                try {
                    // Point towards target
                    if (targetPositionProvider != null)
                    {
                        enemy.SetTarget(targetPositionProvider.GetPosition());
                    }
                    else
                    {
                        // Fallback to player position
                        enemy.SetTarget(currentPlayerPos);
                    }
                }
                catch (System.Exception targetEx) {
                    CZLogger.LogError($"Error setting target for enemy: {targetEx.Message}", LogCategory.Enemy);
                    // Continue even if target setting fails - enemy will still be active
                }
                
                CZLogger.LogDebug($"Spawned enemy {enemy.name} at {spawnPosition}, Active count: {activeEnemies.Count}", LogCategory.Enemy);
            }
            catch (System.Exception e)
            {
                CZLogger.LogError($"Error spawning enemy: {e.Message}", LogCategory.Enemy);
            }
        }
        
        private Vector3 GenerateSpawnPosition(Vector3 currentPlayerPos)
        {
            // Calculate spawn position away from player
            Vector3 randomDir = Random.insideUnitCircle.normalized;
            float spawnDistance = Random.Range(minSpawnDistance, spawnRadius);
            Vector3 spawnPosition = currentPlayerPos + new Vector3(randomDir.x, randomDir.y, 0) * spawnDistance;
            
            CZLogger.LogDebug($"Generated spawn position: {spawnPosition} (Distance from player: {spawnDistance})", LogCategory.Enemy);
            return spawnPosition;
        }
        
        public void SetTargetPosition(Vector3 position)
        {
            // For manual setting of target position when no position provider exists
            // Useful for testing or special gameplay situations
            debugTargetPosition = position;
        }
        
        private void OnDrawGizmosSelected()
        {
            // Draw the spawn area in the editor
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, minSpawnDistance);
        }
        
        private void OnDestroy()
        {
            // Cleanup pools when destroyed
            foreach (var pool in enemyPools.Values)
            {
                // Pool cleanup is handled by PoolManager
            }
            
            UnsubscribeFromGameManager();
        }
    }
} 