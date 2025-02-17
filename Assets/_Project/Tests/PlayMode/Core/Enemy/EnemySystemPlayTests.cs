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
using UnityEngine.SceneManagement;
using CZ.Core;
using CZ.Core.Player;

namespace CZ.Tests.PlayMode.Enemy
{
    public class EnemySystemPlayTests
    {
        private GameObject enemyPrefab;
        private GameObject spawnerObject;
        private EnemySpawner spawner;
        private PoolManager poolManager;
        private Scene testScene;
        private GameManager gameManager;
        private PlayerController playerController;
        
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
            bool testCompleted = false;
            
            // Setup test environment
            yield return SetupTestEnvironment();
            
            try
            {
                // Configure spawner
                spawner.SetSpawnCount(5);
                spawner.StartSpawning();
                
                // Wait for spawning to complete
                float timeout = Time.time + 5f; // 5 second timeout
                while (spawner.ActiveEnemyCount < 5 && Time.time < timeout)
                {
                    yield return null;
                }
                
                // Verify results
                Assert.AreEqual(5, spawner.ActiveEnemyCount, "Spawner created incorrect number of enemies");
                
                // Verify enemy behavior
                var enemies = Object.FindObjectsByType<BaseEnemy>(FindObjectsSortMode.None);
                Assert.AreEqual(5, enemies.Length, "Incorrect number of enemies found in scene");
                
                foreach (var enemy in enemies)
                {
                    Assert.IsTrue(enemy.gameObject.activeInHierarchy, "Enemy should be active");
                    Assert.IsNotNull(enemy.GetComponent<Rigidbody2D>(), "Enemy should have Rigidbody2D");
                }
                
                testCompleted = true;
            }
            finally
            {
                // Mark that we need cleanup
                if (!testCompleted)
                {
                    Debug.LogWarning("[EnemySystemPlayTests] Test did not complete successfully, performing cleanup");
                }
            }
            
            // Cleanup outside of finally
            yield return TearDownTestEnvironment();
        }
        
        [UnityTest]
        #if UNITY_INCLUDE_PERFORMANCE_TESTING
        [Performance]
        #endif
        public IEnumerator EnemyPool_HandlesStressTest()
        {
            bool testCompleted = false;
            
            // Setup test environment
            yield return SetupTestEnvironment();
            
            try
            {
                int maxEnemies = 10;
                spawner.SetSpawnCount(maxEnemies);
                
                // Rapid spawn/despawn cycles
                for (int cycle = 0; cycle < 3; cycle++)
                {
                    // Spawn phase
                    spawner.StartSpawning();
                    
                    float spawnTimeout = Time.time + 5f;
                    while (spawner.ActiveEnemyCount < maxEnemies && Time.time < spawnTimeout)
                    {
                        yield return null;
                    }
                    
                    Assert.AreEqual(maxEnemies, spawner.ActiveEnemyCount, 
                        $"Failed to spawn {maxEnemies} enemies in cycle {cycle}");
                    
                    // Despawn phase
                    spawner.DespawnAllEnemies();
                    yield return new WaitForSeconds(0.5f);
                    
                    Assert.AreEqual(0, spawner.ActiveEnemyCount, 
                        $"Failed to despawn all enemies in cycle {cycle}");
                }
                
                testCompleted = true;
            }
            finally
            {
                if (!testCompleted)
                {
                    Debug.LogWarning("[EnemySystemPlayTests] Stress test did not complete successfully, performing cleanup");
                }
            }
            
            // Cleanup outside of finally
            yield return TearDownTestEnvironment();
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
            bool testCompleted = false;
            
            // Setup test environment
            yield return SetupTestEnvironment();
            
            try
            {
                int testCount = 5;
                spawner.SetSpawnCount(testCount);
                
                // Test rapid spawn/despawn
                for (int i = 0; i < testCount; i++)
                {
                    spawner.StartSpawning();
                    yield return new WaitForSeconds(0.1f);
                    
                    Assert.Greater(spawner.ActiveEnemyCount, 0, 
                        $"Failed to spawn enemy in iteration {i}");
                    
                    spawner.DespawnAllEnemies();
                    yield return new WaitForSeconds(0.1f);
                    
                    Assert.AreEqual(0, spawner.ActiveEnemyCount, 
                        $"Failed to despawn enemies in iteration {i}");
                }
                
                testCompleted = true;
            }
            finally
            {
                if (!testCompleted)
                {
                    Debug.LogWarning("[EnemySystemPlayTests] Rapid spawn/despawn test did not complete successfully, performing cleanup");
                }
            }
            
            // Cleanup outside of finally
            yield return TearDownTestEnvironment();
        }
        
        private float GetTotalMemoryMB()
        {
            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory"))
            {
                var memoryMB = recorder.LastValue / (1024f * 1024f);
                return memoryMB;
            }
        }
        
        private IEnumerator SetupTestEnvironment()
        {
            // Create test scene
            testScene = SceneManager.CreateScene("EnemyTestScene");
            SceneManager.SetActiveScene(testScene);
            
            // Setup GameManager
            var gameManagerObj = new GameObject("GameManager");
            gameManager = gameManagerObj.AddComponent<GameManager>();
            Object.DontDestroyOnLoad(gameManagerObj);
            
            // Setup Player
            var playerObj = new GameObject("Player");
            playerController = playerObj.AddComponent<PlayerController>();
            
            // Setup EnemySpawner
            var spawnerObj = new GameObject("EnemySpawner");
            spawner = spawnerObj.AddComponent<EnemySpawner>();
            
            // Setup enemy prefab
            enemyPrefab = CreateEnemyPrefab();
            spawner.SetEnemyPrefab(enemyPrefab);
            
            // Wait for initialization
            yield return new WaitForSeconds(0.5f);
            
            // Start game
            gameManager.StartGame();
            yield return null;
        }
        
        private IEnumerator TearDownTestEnvironment()
        {
            if (spawner != null)
            {
                spawner.StopSpawning();
                spawner.DespawnAllEnemies();
            }
            
            // Cleanup GameManager
            if (gameManager != null)
            {
                Object.DestroyImmediate(gameManager.gameObject);
            }
            
            // Cleanup test scene
            yield return SceneManager.UnloadSceneAsync(testScene);
            
            // Cleanup prefab
            if (enemyPrefab != null)
            {
                Object.DestroyImmediate(enemyPrefab);
            }
        }
        
        private GameObject CreateEnemyPrefab()
        {
            var prefab = new GameObject("EnemyPrefab");
            
            // Add required components
            var enemy = prefab.AddComponent<BaseEnemy>();
            var rb = prefab.AddComponent<Rigidbody2D>();
            var collider = prefab.AddComponent<CircleCollider2D>();
            var renderer = prefab.AddComponent<SpriteRenderer>();
            
            // Configure components
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            collider.radius = 0.5f;
            
            prefab.SetActive(false);
            return prefab;
        }
    }
} 