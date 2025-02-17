using UnityEngine;
using CZ.Core.Pooling;
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
        [SerializeField] private float waveInterval = 5f;
        
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
        private Vector3 targetPosition;
        private bool isInitialized;
        private bool isInitializing;
        
        public float SpawnInterval => spawnInterval;
        public int ActiveEnemyCount => activeEnemies;
        
        private void Awake()
        {
            // Defer initialization to Start to avoid Awake/OnEnable race conditions
            isInitializing = false;
            isInitialized = false;
        }
        
        private void Start()
        {
            if (enemyPrefab != null)
            {
                InitializePool();
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
                enemyPool = new ObjectPool<BaseEnemy>(
                    createFunc: () => Instantiate(enemyPrefab).GetComponent<BaseEnemy>(),
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
            
            var enemies = FindObjectsOfType<BaseEnemy>();
            foreach (var enemy in enemies)
            {
                if (enemy != null)
                {
                    enemyPool.Return(enemy);
                    System.Threading.Interlocked.Decrement(ref activeEnemies);
                }
            }
            
            // Ensure count doesn't go negative
            if (activeEnemies < 0)
            {
                activeEnemies = 0;
            }
        }
        
        private void Update()
        {
            if (!isInitialized || !isSpawning || activeEnemies >= maxEnemiesPerWave) return;
            
            if (Time.time >= nextSpawnTime)
            {
                SpawnEnemy();
                nextSpawnTime = Time.time + spawnInterval;
            }
        }
        
        private void SpawnEnemy()
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
                
                // Calculate spawn position
                float angle = Random.Range(0f, 360f);
                float distance = Random.Range(minSpawnDistance, spawnRadius);
                Vector2 spawnOffset = Quaternion.Euler(0, 0, angle) * Vector2.right * distance;
                
                // Position enemy
                enemy.transform.position = (Vector2)transform.position + spawnOffset;
                
                // Set target if available
                if (targetPosition != Vector3.zero)
                {
                    enemy.SetTarget(targetPosition);
                }
                else
                {
                    Debug.LogWarning("[EnemySpawner] Target position not set - enemy will not move!", this);
                }
            }
        }
        
        public void SetTargetPosition(Vector3 position)
        {
            targetPosition = position;
        }
        
        private void OnDrawGizmosSelected()
        {
            // Draw spawn area
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, minSpawnDistance);
            
            // Draw target position if set
            if (targetPosition != Vector3.zero)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(targetPosition, 0.5f);
            }
        }
        
        private void OnDestroy()
        {
            if (enemyPool != null)
            {
                enemyPool.Clear();
            }
        }
    }
} 