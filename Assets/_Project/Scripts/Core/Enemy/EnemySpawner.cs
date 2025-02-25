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
        [SerializeField, Required] private GameObject enemyPrefab;
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
        
        private ObjectPool<BaseEnemy> enemyPool;
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
            CZLogger.LogDebug($"Component State - IsInitialized: {isInitialized}, IsInitializing: {isInitializing}, HasPrefab: {enemyPrefab != null}", LogCategory.Enemy);
            
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
            
            if (enemyPrefab != null)
            {
                CZLogger.LogInfo($"Initializing pool with prefab: {enemyPrefab.name}", LogCategory.Enemy);
                InitializePool();
            }
            else
            {
                CZLogger.LogError("No enemy prefab assigned in inspector!", LogCategory.Enemy);
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
                    
                    // Attempt late initialization if we have a prefab and not in emergency mode
                    if (enemyPrefab != null && !isInitializing && !GameManager.Instance.IsInEmergencyMode)
                    {
                        CZLogger.LogInfo("Attempting late initialization", LogCategory.Enemy);
                        InitializePool();
                        if (isInitialized)
                        {
                            StartSpawning();
                        }
                    }
                }
            }
        }
        
        private void InitializePool()
        {
            using var _ = s_poolInitMarker.Auto();
            
            if (isInitialized || isInitializing) return;
            
            isInitializing = true;
            
            if (enemyPrefab == null)
            {
                CZLogger.LogError("Enemy prefab is not set! Please assign a prefab in the inspector.", LogCategory.Enemy);
                isInitializing = false;
                return;
            }
            
            var baseEnemy = enemyPrefab.GetComponent<BaseEnemy>();
            if (baseEnemy == null)
            {
                CZLogger.LogError("Enemy prefab must have BaseEnemy component!", LogCategory.Enemy);
                isInitializing = false;
                return;
            }
            
            try
            {
                // Ensure prefab is inactive before creating pool
                enemyPrefab.SetActive(false);
                
                // Get memory-aware pool size from GameManager
                int initialPoolSize = Mathf.Min(maxEnemiesPerWave, 5); // Start with smaller initial size
                int maxPoolSize = Mathf.Min(maxEnemiesPerWave * 2, 20); // Cap maximum size
                
                // Use PoolManager instead of local pool
                enemyPool = PoolManager.Instance.CreatePool(
                    createFunc: () => {
                        var obj = Instantiate(enemyPrefab).GetComponent<BaseEnemy>();
                        obj.gameObject.SetActive(false);
                        return obj;
                    },
                    initialSize: initialPoolSize,
                    maxSize: maxPoolSize,
                    poolName: "EnemyPool"
                );
                
                isInitialized = true;
                CZLogger.LogInfo($"Pool initialized with {initialPoolSize} initial enemies and {maxPoolSize} max size.", LogCategory.Enemy);
            }
            catch (System.Exception e)
            {
                CZLogger.LogError($"Failed to initialize enemy pool: {e.Message}", LogCategory.Enemy);
                isInitialized = false;
            }
            finally
            {
                isInitializing = false;
            }
        }
        
        public void SetEnemyPrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                CZLogger.LogError("Cannot set null enemy prefab!", LogCategory.Enemy);
                return;
            }
            
            if (prefab.GetComponent<BaseEnemy>() == null)
            {
                CZLogger.LogError("Prefab must have BaseEnemy component!", LogCategory.Enemy);
                return;
            }
            
            // Only reinitialize if the prefab actually changed
            if (enemyPrefab != prefab)
            {
                enemyPrefab = prefab;
                if (gameObject.activeInHierarchy)
                {
                    InitializePool();
                }
            }
        }
        
        public void SetSpawnCount(int count)
        {
            maxEnemiesPerWave = Mathf.Clamp(count, 1, 100);
            if (isInitialized && !isInitializing)
            {
                // Reinitialize pool with new size
                isInitialized = false;
                InitializePool();
            }
        }
        
        public void StartSpawning()
        {
            if (!isInitialized)
            {
                CZLogger.LogError("Cannot start spawning - pool not initialized!", LogCategory.Enemy);
                return;
            }
            
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
            
            if (!isInitialized)
            {
                CZLogger.LogWarning("Cannot despawn enemies - pool not initialized!", LogCategory.Enemy);
                return;
            }
            
            // Create a temporary list to avoid collection modification during enumeration
            var enemiesToDespawn = activeEnemies.ToList();
            foreach (var enemy in enemiesToDespawn)
            {
                if (enemy != null)
                {
                    enemyPool.Return(enemy);
                }
            }
            
            activeEnemies.Clear();
        }
        
        private void Update()
        {
            if (!isInitialized || !isSpawning || !isGamePlaying || targetPositionProvider == null)
            {
                return;
            }
            
            Vector3 currentPlayerPos = targetPositionProvider.GetPosition();
            
            // Ensure we always update targets when the player moves
            bool shouldUpdateTargets = Time.time >= nextTargetUpdateTime;
            if (shouldUpdateTargets)
            {
                UpdateEnemyTargets(currentPlayerPos);
                nextTargetUpdateTime = Time.time + targetUpdateInterval;
                
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                CZLogger.LogDebug($"Scheduled next target update in {targetUpdateInterval}s", LogCategory.Enemy);
                #endif
            }
            
            // Handle spawning
            if (activeEnemies.Count < maxEnemiesPerWave && Time.time >= nextSpawnTime)
            {
                SpawnEnemy(currentPlayerPos);
                nextSpawnTime = Time.time + spawnInterval;
            }
        }
        
        private void UpdateEnemyTargets(Vector3 currentPlayerPos)
        {
            if (targetPositionProvider == null)
            {
                CZLogger.LogError("Cannot update targets - IPositionProvider is null!", LogCategory.Enemy);
                return;
            }
            
            Vector3 targetPos = Application.isEditor && !Application.isPlaying ? debugTargetPosition : currentPlayerPos;
            
            int updatedCount = 0;
            int failedCount = 0;
            
            // Use cached active enemies instead of FindObjectsByType
            foreach (var enemy in activeEnemies)
            {
                if (enemy != null && enemy.gameObject.activeInHierarchy)
                {
                    try 
                    {
                        enemy.SetTarget(targetPos);
                        updatedCount++;
                    }
                    catch (System.Exception e)
                    {
                        failedCount++;
                        CZLogger.LogError($"Failed to update enemy target: {e.Message}", LogCategory.Enemy);
                    }
                }
            }
            
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            CZLogger.LogDebug($"Target update complete - Updated: {updatedCount}, Failed: {failedCount}, Target: {targetPos}", LogCategory.Enemy);
            #endif
        }
        
        private void SpawnEnemy(Vector3 currentPlayerPos)
        {
            using var _ = s_spawnMarker.Auto();
            
            if (!isInitialized)
            {
                CZLogger.LogError("Cannot spawn enemy - pool not initialized!", LogCategory.Enemy);
                return;
            }
            
            var enemy = enemyPool.Get();
            if (enemy != null)
            {
                activeEnemies.Add(enemy);
                
                // Calculate spawn position relative to spawner
                float angle = Random.Range(0f, 360f);
                float distance = Random.Range(minSpawnDistance, spawnRadius);
                Vector2 spawnOffset = Quaternion.Euler(0, 0, angle) * Vector2.right * distance;
                
                // Position enemy
                enemy.transform.position = (Vector2)transform.position + spawnOffset;
                
                // Set initial target
                enemy.SetTarget(currentPlayerPos);
                
                CZLogger.LogInfo($"Spawned enemy at {enemy.transform.position}. Active count: {activeEnemies.Count}", LogCategory.Enemy);
            }
            else
            {
                CZLogger.LogError("Failed to get enemy from pool!", LogCategory.Enemy);
            }
        }
        
        /// <summary>
        /// Sets a fixed target position for testing purposes.
        /// This method is primarily used by the test framework and should not be used in gameplay.
        /// </summary>
        /// <param name="position">The target position to set</param>
        public void SetTargetPosition(Vector3 position)
        {
            debugTargetPosition = position;
            if (isInitialized)
            {
                UpdateEnemyTargets(position);
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            // Draw spawn area
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, minSpawnDistance);
            
            // Draw debug target position if set (for tests)
            if (debugTargetPosition != Vector3.zero)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(debugTargetPosition, 0.5f);
            }
        }
        
        private void OnDestroy()
        {
            CZLogger.LogInfo("OnDestroy called", LogCategory.Enemy);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            }
            
            if (enemyPool != null)
            {
                DespawnAllEnemies();
                enemyPool.Clear();
            }
            
            activeEnemies.Clear();
        }
    }
} 