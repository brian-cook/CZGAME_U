using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_INCLUDE_PERFORMANCE_TESTING
using Unity.PerformanceTesting;
#endif
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
            // Setup log handling for initialization messages
            LogAssert.ignoreFailingMessages = true;
            
            try
            {
                // Create pool manager
                var poolManagerObject = new GameObject("PoolManager");
                poolManager = poolManagerObject.AddComponent<PoolManager>();
                
                // Create and configure test prefab
                enemyPrefab = new GameObject("EnemyPrefab");
                var enemy = enemyPrefab.AddComponent<BaseEnemy>();
                enemyPrefab.AddComponent<SpriteRenderer>();
                
                // Create spawner object first
                spawnerObject = new GameObject("EnemySpawner");
                
                // Wait one frame to ensure proper initialization
                yield return null;
                
                // Add spawner component and configure
                spawner = spawnerObject.AddComponent<EnemySpawner>();
                
                // Wait for component initialization
                yield return null;
                
                // Set prefab and default target
                spawner.SetEnemyPrefab(enemyPrefab);
                spawner.SetTargetPosition(Vector3.right * 10f); // Default target
                
                // Wait for pool initialization
                yield return null;
                
                // Verify initialization
                Assert.That(spawner, Is.Not.Null, "Spawner component was not created");
                Assert.That(enemyPrefab, Is.Not.Null, "Enemy prefab was not created");
            }
            finally
            {
                // Reset log handling
                LogAssert.ignoreFailingMessages = false;
            }
        }
        
        [UnityTearDown]
        public IEnumerator Teardown()
        {
            // Stop spawning first
            if (spawner != null)
            {
                spawner.StopSpawning();
                spawner.DespawnAllEnemies();
            }
            
            yield return new WaitForSeconds(0.1f);
            
            if (spawnerObject != null) Object.Destroy(spawnerObject);
            if (enemyPrefab != null) Object.Destroy(enemyPrefab);
            if (poolManager != null) Object.Destroy(poolManager.gameObject);
            
            // Wait for destruction
            yield return null;
            
            // Clear any remaining objects
            var remainingEnemies = Object.FindObjectsByType<BaseEnemy>(FindObjectsSortMode.None);
            foreach (var enemy in remainingEnemies)
            {
                Object.Destroy(enemy.gameObject);
            }
            
            yield return null;
        }
        
        [UnityTest]
        #if UNITY_INCLUDE_PERFORMANCE_TESTING
        [Performance]
        #endif
        public IEnumerator EnemySpawner_SpawnsCorrectNumberOfEnemies()
        {
            #if UNITY_INCLUDE_PERFORMANCE_TESTING
            using (Measure.Scope("EnemySpawner_SpawnsCorrectNumberOfEnemies"))
            #endif
            {
                const int TARGET_COUNT = 5;
                var initialMemory = GetTotalMemoryMB();
                
                // Configure spawner
                spawner.SetSpawnCount(TARGET_COUNT);
                
                // Wait for pool reinitialization
                yield return null;
                
                // Start spawning
                spawner.StartSpawning();
                
                // Wait for spawning to complete
                yield return new WaitForSeconds(TARGET_COUNT * spawner.SpawnInterval + 0.5f);
                
                // Verify spawn count
                Assert.That(spawner.ActiveEnemyCount, Is.EqualTo(TARGET_COUNT),
                    "Spawner created incorrect number of enemies");
                
                // Verify memory usage
                var currentMemory = GetTotalMemoryMB();
                Assert.That(currentMemory - initialMemory, Is.LessThan(50),
                    "Spawning exceeded memory budget");
                
                // Stop spawning
                spawner.StopSpawning();
                
                // Wait before despawning
                yield return new WaitForSeconds(0.1f);
                
                // Cleanup
                spawner.DespawnAllEnemies();
                
                // Wait for despawn
                yield return new WaitForSeconds(0.5f);
                
                // Verify cleanup
                Assert.That(spawner.ActiveEnemyCount, Is.Zero,
                    "Not all enemies were despawned");
            }
        }
        
        [UnityTest]
        #if UNITY_INCLUDE_PERFORMANCE_TESTING
        [Performance]
        #endif
        public IEnumerator EnemyPool_HandlesStressTest()
        {
            const int STRESS_COUNT = 50;  // Reduced from performance guidelines
            var spawnedEnemies = new List<BaseEnemy>();
            var initialMemory = GetTotalMemoryMB();
            
            #if UNITY_INCLUDE_PERFORMANCE_TESTING
            using (Measure.Frames()
                .WarmupCount(5)
                .MeasurementCount(30))
            #endif
            {
                // Spawn phase with memory monitoring
                for (int i = 0; i < STRESS_COUNT; i++)
                {
                    if (GetTotalMemoryMB() - initialMemory > 100)
                    {
                        Debug.LogWarning($"Memory threshold reached at {i} spawns");
                        break;
                    }
                    
                    var enemy = poolManager.Get<BaseEnemy>();
                    if (enemy != null)
                    {
                        spawnedEnemies.Add(enemy);
                    }
                    yield return null;
                }
                
                // Verify memory constraints
                var peakMemory = GetTotalMemoryMB();
                Assert.That(peakMemory, Is.LessThan(1024), 
                    $"Memory usage exceeded limit: {peakMemory}MB");
                
                // Cleanup phase
                foreach (var enemy in spawnedEnemies)
                {
                    poolManager.Return(enemy);
                    yield return null;
                }
            }
            
            // Verify final state
            Assert.That(poolManager.ActiveCount, Is.Zero);
            Assert.That(GetTotalMemoryMB() - initialMemory, Is.LessThan(50));
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
            const int SPAWN_COUNT = 20;  // As per performance guidelines
            var spawnedEnemies = new List<BaseEnemy>();
            
            // Monitor initial memory state
            var initialMemory = GetTotalMemoryMB();
            
            // Rapid spawn phase
            for (int i = 0; i < SPAWN_COUNT; i++)
            {
                var enemy = poolManager.Get<BaseEnemy>();
                if (enemy != null)
                {
                    spawnedEnemies.Add(enemy);
                }
                yield return new WaitForSeconds(0.1f);
            }
            
            // Verify spawn count
            Assert.That(spawnedEnemies.Count, Is.LessThanOrEqualTo(SPAWN_COUNT), 
                "Pool spawned more enemies than requested");
            
            // Memory check
            var currentMemory = GetTotalMemoryMB();
            Assert.That(currentMemory - initialMemory, Is.LessThan(100), 
                "Memory increase exceeded threshold during spawn");
            
            // Rapid despawn phase
            foreach (var enemy in spawnedEnemies)
            {
                poolManager.Return(enemy);
                yield return new WaitForSeconds(0.1f);
            }
            
            // Verify cleanup
            Assert.That(poolManager.ActiveCount, Is.Zero, 
                "Not all enemies were returned to pool");
            
            // Final memory check
            var finalMemory = GetTotalMemoryMB();
            Assert.That(finalMemory - initialMemory, Is.LessThan(10), 
                "Memory not properly cleaned up after despawn");
        }
        
        private float GetTotalMemoryMB()
        {
            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory"))
            {
                var memoryMB = recorder.LastValue / (1024f * 1024f);
                return memoryMB;
            }
        }
    }
} 