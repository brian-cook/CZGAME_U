using UnityEngine;
using CZ.Core.Pooling;
using CZ.Core.Player;
using Unity.Profiling;
using NaughtyAttributes;
using System.Collections;

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
        private int activeEnemies;
        private bool isInitialized;
        private bool isInitializing;
        private PlayerController playerController;
        private bool isGamePlaying;
        
        [Header("Update Settings")]
        [SerializeField, MinValue(0.01f)]
        private float targetUpdateInterval = 0.05f;
        private float nextTargetUpdateTime;
        private Vector3 debugTargetPosition; // For test visualization only
        
        public float SpawnInterval => spawnInterval;
        public int ActiveEnemyCount => activeEnemies;
        
        private void OnEnable()
        {
            Debug.Log("[EnemySpawner] OnEnable called");
            SubscribeToGameManager();
        }

        private void OnDisable()
        {
            Debug.Log("[EnemySpawner] OnDisable called");
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
            Debug.Log("[EnemySpawner] Awake called");
            isInitializing = false;
            isInitialized = false;
        }

        private void Start()
        {
            Debug.Log("[EnemySpawner] Start called");
            
            // Verify component state
            Debug.Log($"[EnemySpawner] Component State - IsInitialized: {isInitialized}, IsInitializing: {isInitializing}, HasPrefab: {enemyPrefab != null}");
            
            playerController = Object.FindFirstObjectByType<PlayerController>();
            if (playerController == null)
            {
                Debug.LogWarning("[EnemySpawner] PlayerController not found in scene!");
            }
            else
            {
                Debug.Log("[EnemySpawner] PlayerController found successfully");
            }
            
            if (enemyPrefab != null)
            {
                Debug.Log($"[EnemySpawner] Initializing pool with prefab: {enemyPrefab.name}");
                InitializePool();
            }
            else
            {
                Debug.LogError("[EnemySpawner] No enemy prefab assigned in inspector!");
            }
            
            // Subscribe to GameManager after initialization
            SubscribeToGameManager();
            
            // Log final state
            Debug.Log($"[EnemySpawner] Start completed - IsInitialized: {isInitialized}, IsGamePlaying: {isGamePlaying}, IsSpawning: {isSpawning}");
        }
        
        private void HandleGameStateChanged(GameManager.GameState newState)
        {
            Debug.Log($"[EnemySpawner] Game state changed to {newState} (Previous isGamePlaying: {isGamePlaying})");
            isGamePlaying = newState == GameManager.GameState.Playing;
            
            if (!isGamePlaying)
            {
                Debug.Log("[EnemySpawner] Game no longer playing, stopping spawn");
                StopSpawning();
                DespawnAllEnemies();
            }
            else if (isInitialized)
            {
                Debug.Log("[EnemySpawner] Starting spawning due to game state change to Playing");
                StartSpawning();
            }
            else
            {
                Debug.LogWarning($"[EnemySpawner] Cannot start spawning - not initialized when game state changed to Playing (Init: {isInitialized}, Initializing: {isInitializing})");
                // Attempt late initialization if we have a prefab
                if (enemyPrefab != null && !isInitializing)
                {
                    Debug.Log("[EnemySpawner] Attempting late initialization");
                    InitializePool();
                    if (isInitialized)
                    {
                        StartSpawning();
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
                Debug.LogError("[EnemySpawner] Enemy prefab is not set! Please assign a prefab in the inspector.", this);
                isInitializing = false;
                return;
            }
            
            var baseEnemy = enemyPrefab.GetComponent<BaseEnemy>();
            if (baseEnemy == null)
            {
                Debug.LogError("[EnemySpawner] Enemy prefab must have BaseEnemy component!", this);
                isInitializing = false;
                return;
            }
            
            try
            {
                // Ensure prefab is inactive before creating pool
                enemyPrefab.SetActive(false);
                
                enemyPool = new ObjectPool<BaseEnemy>(
                    createFunc: () => {
                        var obj = Instantiate(enemyPrefab).GetComponent<BaseEnemy>();
                        obj.gameObject.SetActive(false);
                        return obj;
                    },
                    initialSize: maxEnemiesPerWave,
                    maxSize: maxEnemiesPerWave * 2,
                    "EnemyPool"
                );
                
                isInitialized = true;
                Debug.Log($"[EnemySpawner] Pool initialized with {maxEnemiesPerWave} initial enemies and {maxEnemiesPerWave * 2} max size.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[EnemySpawner] Failed to initialize enemy pool: {e.Message}", this);
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
                Debug.LogError("[EnemySpawner] Cannot set null enemy prefab!", this);
                return;
            }
            
            if (prefab.GetComponent<BaseEnemy>() == null)
            {
                Debug.LogError("[EnemySpawner] Prefab must have BaseEnemy component!", this);
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
                Debug.LogError("[EnemySpawner] Cannot start spawning - pool not initialized!", this);
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
                Debug.LogWarning("[EnemySpawner] Cannot despawn enemies - pool not initialized!", this);
                return;
            }
            
            var enemies = Object.FindObjectsByType<BaseEnemy>(FindObjectsSortMode.None);
            foreach (var enemy in enemies)
            {
                if (enemy != null)
                {
                    enemyPool.Return(enemy);
                    System.Threading.Interlocked.Decrement(ref activeEnemies);
                }
            }
            
            if (activeEnemies < 0)
            {
                activeEnemies = 0;
            }
        }
        
        private void Update()
        {
            if (!isInitialized || !isSpawning || !isGamePlaying || playerController == null)
            {
                return;
            }
            
            Vector3 currentPlayerPos = playerController.GetPosition();
            
            // Ensure we always update targets when the player moves
            bool shouldUpdateTargets = Time.time >= nextTargetUpdateTime;
            if (shouldUpdateTargets)
            {
                UpdateEnemyTargets(currentPlayerPos);
                nextTargetUpdateTime = Time.time + targetUpdateInterval;
                
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[EnemySpawner] Scheduled next target update in {targetUpdateInterval}s");
                #endif
            }
            
            // Handle spawning
            if (activeEnemies < maxEnemiesPerWave && Time.time >= nextSpawnTime)
            {
                SpawnEnemy(currentPlayerPos);
                nextSpawnTime = Time.time + spawnInterval;
            }
        }
        
        private void UpdateEnemyTargets(Vector3 currentPlayerPos)
        {
            if (playerController == null)
            {
                Debug.LogError("[EnemySpawner] Cannot update targets - PlayerController is null!");
                return;
            }
            
            // Use debug target in editor test mode, otherwise use player position
            Vector3 targetPos = Application.isEditor && !Application.isPlaying ? debugTargetPosition : currentPlayerPos;
            
            var enemies = Object.FindObjectsByType<BaseEnemy>(FindObjectsSortMode.None);
            int updatedCount = 0;
            int failedCount = 0;
            
            foreach (var enemy in enemies)
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
                        Debug.LogError($"[EnemySpawner] Failed to update enemy target: {e.Message}");
                    }
                }
            }
            
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[EnemySpawner] Target update complete - Updated: {updatedCount}, Failed: {failedCount}, Target: {targetPos}");
            #endif
        }
        
        private void SpawnEnemy(Vector3 currentPlayerPos)
        {
            using var _ = s_spawnMarker.Auto();
            
            if (!isInitialized)
            {
                Debug.LogError("[EnemySpawner] Cannot spawn enemy - pool not initialized!", this);
                return;
            }
            
            var enemy = enemyPool.Get();
            if (enemy != null)
            {
                System.Threading.Interlocked.Increment(ref activeEnemies);
                
                // Calculate spawn position relative to spawner
                float angle = Random.Range(0f, 360f);
                float distance = Random.Range(minSpawnDistance, spawnRadius);
                Vector2 spawnOffset = Quaternion.Euler(0, 0, angle) * Vector2.right * distance;
                
                // Position enemy
                enemy.transform.position = (Vector2)transform.position + spawnOffset;
                
                // Set initial target
                enemy.SetTarget(currentPlayerPos);
                
                Debug.Log($"[EnemySpawner] Spawned enemy at {enemy.transform.position}. Active count: {activeEnemies}");
            }
            else
            {
                Debug.LogError("[EnemySpawner] Failed to get enemy from pool!");
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
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            }
            
            if (enemyPool != null)
            {
                enemyPool.Clear();
            }
        }
    }
} 