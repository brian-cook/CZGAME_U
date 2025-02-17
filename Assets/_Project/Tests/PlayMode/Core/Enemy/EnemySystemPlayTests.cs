using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Profiling;
using CZ.Core.Enemy;
using CZ.Core.Pooling;

namespace CZ.Tests.PlayMode.Enemy
{
    public class EnemySystemPlayTests
    {
        private GameObject enemyPrefab;
        private GameObject spawnerObject;
        private EnemySpawner spawner;
        private PoolManager poolManager;
        
        [UnitySetUp]
        public IEnumerator Setup()
        {
            // Create pool manager
            var poolManagerObject = new GameObject("PoolManager");
            poolManager = poolManagerObject.AddComponent<PoolManager>();
            
            // Create test prefab
            enemyPrefab = new GameObject("EnemyPrefab");
            enemyPrefab.AddComponent<BaseEnemy>();
            enemyPrefab.AddComponent<SpriteRenderer>();
            
            // Create spawner
            spawnerObject = new GameObject("EnemySpawner");
            spawner = spawnerObject.AddComponent<EnemySpawner>();
            
            // Set spawner properties using reflection
            var prefabField = typeof(EnemySpawner).GetField("enemyPrefab", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            prefabField.SetValue(spawner, enemyPrefab);
            
            yield return null;
        }
        
        [UnityTearDown]
        public IEnumerator Teardown()
        {
            Object.Destroy(spawnerObject);
            Object.Destroy(enemyPrefab);
            Object.Destroy(poolManager.gameObject);
            
            yield return null;
        }
        
        [UnityTest]
        public IEnumerator EnemySpawner_SpawnsCorrectNumberOfEnemies()
        {
            // Setup test values using reflection
            var enemiesPerWaveField = typeof(EnemySpawner).GetField("enemiesPerWave", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            var spawnIntervalField = typeof(EnemySpawner).GetField("spawnInterval", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            
            enemiesPerWaveField.SetValue(spawner, 5);
            spawnIntervalField.SetValue(spawner, 0.1f); // Fast spawning for tests
            
            // Start spawner
            spawner.gameObject.SetActive(true);
            
            // Wait for wave to complete
            yield return new WaitForSeconds(1f);
            
            // Count active enemies
            var enemies = Object.FindObjectsOfType<BaseEnemy>();
            Assert.That(enemies.Length, Is.EqualTo(5));
        }
        
        [UnityTest]
        public IEnumerator EnemyPool_HandlesStressTest()
        {
            // Setup performance monitoring
            using var drawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            using var memoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
            
            // Configure spawner for stress test
            var enemiesPerWaveField = typeof(EnemySpawner).GetField("enemiesPerWave", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            var spawnIntervalField = typeof(EnemySpawner).GetField("spawnInterval", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            
            enemiesPerWaveField.SetValue(spawner, 50); // Max pool size test
            spawnIntervalField.SetValue(spawner, 0.05f);
            
            // Start spawner
            spawner.gameObject.SetActive(true);
            
            // Wait for spawning to complete
            yield return new WaitForSeconds(3f);
            
            // Verify performance metrics
            Assert.That(drawCallsRecorder.LastValue, Is.LessThan(100), "Draw calls exceeded limit");
            Assert.That(memoryRecorder.LastValue / (1024 * 1024), Is.LessThan(1024), "Memory usage exceeded limit");
            
            // Verify pool behavior
            var enemies = Object.FindObjectsOfType<BaseEnemy>();
            Assert.That(enemies.Length, Is.LessThanOrEqualTo(100), "Pool exceeded max size");
        }
        
        [UnityTest]
        public IEnumerator Enemy_FollowsTargetCorrectly()
        {
            // Spawn single enemy
            var enemy = Object.Instantiate(enemyPrefab).GetComponent<BaseEnemy>();
            enemy.OnSpawn();
            
            // Set initial position
            enemy.transform.position = Vector3.zero;
            
            // Set target
            Vector3 target = new Vector3(5f, 0f, 0f);
            enemy.SetTarget(target);
            
            // Wait for movement
            yield return new WaitForSeconds(2f);
            
            // Verify position (should be at or near target)
            Assert.That(Vector3.Distance(enemy.transform.position, target), Is.LessThan(0.1f));
            
            Object.Destroy(enemy.gameObject);
        }
        
        [UnityTest]
        public IEnumerator EnemyPool_HandlesRapidSpawnDespawn()
        {
            // Setup
            int spawnCount = 20;
            var enemies = new BaseEnemy[spawnCount];
            
            // Rapid spawn
            for (int i = 0; i < spawnCount; i++)
            {
                var enemy = Object.Instantiate(enemyPrefab).GetComponent<BaseEnemy>();
                enemy.OnSpawn();
                enemies[i] = enemy;
                yield return new WaitForSeconds(0.1f);
            }
            
            // Verify all spawned
            Assert.That(Object.FindObjectsOfType<BaseEnemy>().Length, Is.EqualTo(spawnCount));
            
            // Rapid despawn
            for (int i = 0; i < spawnCount; i++)
            {
                enemies[i].OnDespawn();
                yield return new WaitForSeconds(0.1f);
            }
            
            // Verify all despawned
            var activeEnemies = Object.FindObjectsOfType<BaseEnemy>();
            int activeCount = 0;
            foreach (var enemy in activeEnemies)
            {
                if (enemy.GameObject.activeSelf)
                    activeCount++;
            }
            Assert.That(activeCount, Is.Zero);
        }
    }
} 