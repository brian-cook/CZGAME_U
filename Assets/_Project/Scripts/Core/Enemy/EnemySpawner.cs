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
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private float spawnInterval = 1f;
        [SerializeField] private int enemiesPerWave = 5;
        [SerializeField] private float waveInterval = 5f;
        
        [Header("Spawn Area")]
        [SerializeField] private float spawnRadius = 10f;
        [MinValue(0)] [SerializeField] private float minSpawnDistance = 5f;
        
        // Performance monitoring
        private static readonly ProfilerMarker s_spawnMarker = 
            new(ProfilerCategory.Scripts, "EnemySpawner.SpawnEnemy");
        
        private ObjectPool<BaseEnemy> enemyPool;
        private bool isSpawning;
        private int totalSpawned;
        private Vector3 targetPosition; // Position enemies will move towards
        
        private void Start()
        {
            // Initialize pool through PoolManager
            enemyPool = PoolManager.Instance.CreatePool(
                createFunc: CreateEnemy,
                initialSize: 50,
                maxSize: 100,
                poolName: "EnemyPool"
            );
            
            // Start wave spawning
            StartCoroutine(SpawnWaves());
        }
        
        private BaseEnemy CreateEnemy()
        {
            var enemy = Instantiate(enemyPrefab).AddComponent<BaseEnemy>();
            return enemy;
        }
        
        private IEnumerator SpawnWaves()
        {
            while (enabled)
            {
                yield return StartCoroutine(SpawnWave());
                yield return new WaitForSeconds(waveInterval);
            }
        }
        
        private IEnumerator SpawnWave()
        {
            for (int i = 0; i < enemiesPerWave; i++)
            {
                SpawnEnemy();
                yield return new WaitForSeconds(spawnInterval);
            }
        }
        
        private void SpawnEnemy()
        {
            using var _ = s_spawnMarker.Auto();
            
            // Get enemy from pool
            var enemy = enemyPool.Get();
            if (enemy != null)
            {
                // Calculate spawn position
                float angle = Random.Range(0f, 360f);
                float distance = Random.Range(minSpawnDistance, spawnRadius);
                Vector2 spawnOffset = Quaternion.Euler(0, 0, angle) * Vector2.right * distance;
                
                // Position enemy
                enemy.transform.position = (Vector2)transform.position + spawnOffset;
                
                // Set target
                enemy.SetTarget(targetPosition);
                
                totalSpawned++;
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
        }
    }
} 