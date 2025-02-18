using System;
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
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

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
        
        // Test configuration constants
        private const int TARGET_COUNT = 10;
        private const float SPAWN_INTERVAL = 0.2f;
        private const float CYCLE_TIMEOUT = 10.0f;
        private const int MAX_CYCLES = 3;
        private const float MAX_MEMORY_DELTA = 50f;

        private string GetUniqueSceneName()
        {
            return $"EnemyTestScene_{System.Guid.NewGuid()}";
        }

        [UnitySetUp]
        public IEnumerator Setup()
        {
            // Setup log handling for initialization messages
            LogAssert.ignoreFailingMessages = true;
            
            try
            {
                // Create a new scene for the test
                testScene = SceneManager.CreateScene(GetUniqueSceneName());
                SceneManager.SetActiveScene(testScene);

                // Create pool manager
                var poolManagerObject = new GameObject("PoolManager");
                poolManager = poolManagerObject.AddComponent<PoolManager>();
                SceneManager.MoveGameObjectToScene(poolManagerObject, testScene);
                
                // Create and configure test prefab
                enemyPrefab = new GameObject("EnemyPrefab");
                SceneManager.MoveGameObjectToScene(enemyPrefab, testScene);
                
                var enemy = enemyPrefab.AddComponent<BaseEnemy>();
                if (enemyPrefab.GetComponent<SpriteRenderer>() == null)
                {
                    var renderer = enemyPrefab.AddComponent<SpriteRenderer>();
                    renderer.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
                }
                
                if (enemyPrefab.GetComponent<Rigidbody2D>() == null)
                {
                    var rb = enemyPrefab.AddComponent<Rigidbody2D>();
                    rb.gravityScale = 0f;
                    rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                }
                
                // Create spawner object first
                spawnerObject = new GameObject("EnemySpawner");
                SceneManager.MoveGameObjectToScene(spawnerObject, testScene);
                
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
            bool teardownComplete = false;
            Exception teardownException = null;

            try
            {
                // Stop spawning first
                if (spawner != null)
                {
                    spawner.StopSpawning();
                    spawner.DespawnAllEnemies();
                }
                
                teardownComplete = true;
            }
            catch (Exception e)
            {
                teardownException = e;
                Debug.LogError($"Error during test teardown: {e}");
            }

            if (teardownException != null)
                throw teardownException;

            if (teardownComplete)
            {
                yield return new WaitForSeconds(0.1f);
                
                // Cleanup input system
                var inputComponents = Object.FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
                foreach (var input in inputComponents)
                {
                    if (input != null && input.actions != null)
                    {
                        input.actions.Disable();
                        Object.Destroy(input.gameObject);
                    }
                }
                
                // Clear any remaining objects
                var remainingEnemies = Object.FindObjectsByType<BaseEnemy>(FindObjectsSortMode.None);
                foreach (var enemy in remainingEnemies)
                {
                    if (enemy != null)
                    {
                        Object.Destroy(enemy.gameObject);
                    }
                }
                
                yield return null;

                // Cleanup scene
                if (testScene.isLoaded)
                {
                    var asyncOperation = SceneManager.UnloadSceneAsync(testScene);
                    while (!asyncOperation.isDone)
                    {
                        yield return null;
                    }
                }
                
                // Force cleanup
                yield return new WaitForSeconds(0.1f);
                System.GC.Collect();
                yield return null;
            }
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
                var enemies = UnityEngine.Object.FindObjectsByType<BaseEnemy>(FindObjectsSortMode.None);
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
            // Setup state tracking
            bool setupSuccess = false;
            bool testInProgress = false;
            float initialMemory = 0f;
            
            // Pre-test setup
            try
            {
                initialMemory = GetTotalMemoryMB();
                setupSuccess = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to initialize memory tracking: {e.Message}");
                Assert.Fail($"Test setup failed: {e.Message}");
                yield break;
            }

            if (setupSuccess)
            {
                yield return SetupTestEnvironment();
                
                try
                {
                    // Initialize pool with sufficient capacity
                    spawner.SetSpawnCount(TARGET_COUNT);
                    testInProgress = true;
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to initialize spawner: {e.Message}");
                    Assert.Fail($"Spawner initialization failed: {e.Message}");
                    yield break;
                }
            }

            // Main test execution
            if (testInProgress)
            {
                LogPoolStats("Initial");
                
                // Track performance metrics
                float totalElapsedTime = 0f;
                int totalSpawnAttempts = 0;
                int successfulSpawns = 0;
                
                // Perform spawn/despawn cycles
                for (int cycle = 0; cycle < MAX_CYCLES && testInProgress; cycle++)
                {
                    Debug.Log($"Starting cycle {cycle}");
                    float cycleStartMemory = GetTotalMemoryMB();
                    float cycleStartTime = Time.time;
                    
                    // Start spawning phase
                    bool spawningComplete = false;
                    spawner.StartSpawning();
                    
                    // Monitor spawn progress
                    while (!spawningComplete && Time.time - cycleStartTime < CYCLE_TIMEOUT)
                    {
                        totalSpawnAttempts++;
                        
                        if (Mathf.FloorToInt(Time.time - cycleStartTime) > 
                            Mathf.FloorToInt(Time.time - cycleStartTime - Time.deltaTime))
                        {
                            LogPoolStats($"Cycle {cycle} Progress");
                        }
                        
                        if (spawner.ActiveEnemyCount >= TARGET_COUNT)
                        {
                            spawningComplete = true;
                            successfulSpawns += spawner.ActiveEnemyCount;
                        }
                        
                        yield return new WaitForSeconds(SPAWN_INTERVAL);
                    }
                    
                    float elapsedTime = Time.time - cycleStartTime;
                    totalElapsedTime += elapsedTime;
                    
                    // Verify cycle results
                    if (!spawningComplete)
                    {
                        Debug.LogError($"Cycle {cycle} failed to complete within timeout. " +
                                     $"Spawned {spawner.ActiveEnemyCount}/{TARGET_COUNT} enemies");
                        testInProgress = false;
                        break;
                    }
                    
                    // Memory verification
                    float cycleDeltaMemory = GetTotalMemoryMB() - cycleStartMemory;
                    if (cycleDeltaMemory >= MAX_MEMORY_DELTA)
                    {
                        Debug.LogError($"Cycle {cycle} exceeded memory threshold. " +
                                     $"Delta: {cycleDeltaMemory:F2}MB");
                        testInProgress = false;
                        break;
                    }
                    
                    // Cleanup phase
                    LogPoolStats($"Before Despawn Cycle {cycle}");
                    spawner.DespawnAllEnemies();
                    yield return new WaitForSeconds(0.5f);
                    
                    if (spawner.ActiveEnemyCount > 0)
                    {
                        Debug.LogError($"Failed to despawn all enemies in cycle {cycle}. " +
                                     $"Remaining: {spawner.ActiveEnemyCount}");
                        testInProgress = false;
                        break;
                    }
                    
                    LogPoolStats($"After Despawn Cycle {cycle}");
                    
                    // Force cleanup between cycles
                    System.GC.Collect();
                    yield return null;
                }
                
                // Final verification
                if (testInProgress)
                {
                    float finalMemoryDelta = GetTotalMemoryMB() - initialMemory;
                    Debug.Log($"Stress Test Statistics:\n" +
                             $"Total Elapsed Time: {totalElapsedTime:F2}s\n" +
                             $"Average Time Per Cycle: {totalElapsedTime/MAX_CYCLES:F2}s\n" +
                             $"Total Spawn Attempts: {totalSpawnAttempts}\n" +
                             $"Successful Spawns: {successfulSpawns}\n" +
                             $"Memory Delta: {finalMemoryDelta:F2}MB");
                    
                    Assert.That(successfulSpawns, Is.GreaterThan(0), 
                        "No successful spawns completed during test");
                }
            }
            
            // Always perform cleanup
            Debug.Log($"Final Memory Delta: {GetTotalMemoryMB() - initialMemory:F2}MB");
            yield return TearDownTestEnvironment();
        }
        
        private void LogPoolStats(string phase)
        {
            if (spawner != null)
            {
                Debug.Log($"[Pool Stats] {phase}" +
                         $"\nActive Count: {spawner.ActiveEnemyCount}" +
                         $"\nMemory Usage: {GetTotalMemoryMB():F2}MB");
            }
        }
        
        [UnityTest]
        public IEnumerator Enemy_FollowsTargetCorrectly()
        {
            // Spawn single enemy
            var enemy = UnityEngine.Object.Instantiate(enemyPrefab).GetComponent<BaseEnemy>();
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
            
            UnityEngine.Object.Destroy(enemy.gameObject);
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
            bool setupComplete = false;
            Exception setupException = null;

            // Check for existing test scene and unload if found
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.name.StartsWith("EnemyTestScene"))
                {
                    yield return SceneManager.UnloadSceneAsync(scene);
                }
            }

            // Create test scene with unique name
            string uniqueSceneName = $"EnemyTestScene_{System.Guid.NewGuid().ToString("N")}";
            
            try
            {
                testScene = SceneManager.CreateScene(uniqueSceneName);
                SceneManager.SetActiveScene(testScene);

                // Setup GameManager
                var gameManagerObj = new GameObject("GameManager");
                gameManager = gameManagerObj.AddComponent<GameManager>();
                SceneManager.MoveGameObjectToScene(gameManagerObj, testScene);

                // Setup Player
                var playerObj = new GameObject("Player");
                playerController = playerObj.AddComponent<PlayerController>();
                SceneManager.MoveGameObjectToScene(playerObj, testScene);

                // Setup EnemySpawner
                var spawnerObj = new GameObject("EnemySpawner");
                spawner = spawnerObj.AddComponent<EnemySpawner>();
                SceneManager.MoveGameObjectToScene(spawnerObj, testScene);

                // Setup enemy prefab
                enemyPrefab = CreateEnemyPrefab();
                spawner.SetEnemyPrefab(enemyPrefab);

                setupComplete = true;
            }
            catch (System.Exception e)
            {
                setupException = e;
                Debug.LogError($"[EnemySystemPlayTests] Failed to setup test environment: {e.Message}");
            }

            // Wait for initialization outside try-catch
            if (setupComplete)
            {
                yield return new WaitForSeconds(0.5f);
                gameManager.StartGame();
                yield return null;
            }
            else if (setupException != null)
            {
                throw setupException;
            }
        }
        
        private IEnumerator TearDownTestEnvironment()
        {
            bool teardownComplete = false;
            Exception teardownException = null;

            try
            {
                if (spawner != null)
                {
                    spawner.StopSpawning();
                    spawner.DespawnAllEnemies();
                }

                if (gameManager != null)
                {
                    gameManager.EndGame();
                }

                teardownComplete = true;
            }
            catch (System.Exception e)
            {
                teardownException = e;
                Debug.LogError($"[EnemySystemPlayTests] Failed initial teardown: {e.Message}");
            }

            if (teardownException != null)
                throw teardownException;

            if (teardownComplete)
            {
                yield return null;

                // Cleanup test scene
                if (testScene.isLoaded)
                {
                    var sceneName = testScene.name;
                    var asyncOperation = SceneManager.UnloadSceneAsync(testScene);
                    while (!asyncOperation.isDone)
                    {
                        yield return null;
                    }
                    Debug.Log($"[EnemySystemPlayTests] Successfully unloaded test scene: {sceneName}");
                }

                // Cleanup objects
                if (gameManager != null) Object.DestroyImmediate(gameManager.gameObject);
                if (enemyPrefab != null) Object.DestroyImmediate(enemyPrefab);

                yield return null;

                // Final verification using new API
                var remainingEnemies = Object.FindObjectsByType<BaseEnemy>(FindObjectsSortMode.None);
                if (remainingEnemies.Length > 0)
                {
                    Debug.LogWarning($"[EnemySystemPlayTests] Found {remainingEnemies.Length} remaining enemies after cleanup");
                    foreach (var enemy in remainingEnemies)
                    {
                        Object.DestroyImmediate(enemy.gameObject);
                    }
                }
            }
        }
        
        private GameObject CreateEnemyPrefab()
        {
            var prefab = new GameObject("EnemyPrefab");
            
            try
            {
                // Add required components in correct order
                var enemy = prefab.AddComponent<BaseEnemy>();
                
                // Only add components if they don't exist
                if (prefab.GetComponent<Rigidbody2D>() == null)
                {
                    var rb = prefab.AddComponent<Rigidbody2D>();
                    rb.gravityScale = 0f;
                    rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                }
                
                if (prefab.GetComponent<CircleCollider2D>() == null)
                {
                    var collider = prefab.AddComponent<CircleCollider2D>();
                    collider.radius = 0.5f;
                }
                
                if (prefab.GetComponent<SpriteRenderer>() == null)
                {
                    var renderer = prefab.AddComponent<SpriteRenderer>();
                    renderer.sprite = UnityEngine.Sprite.Create(
                        Texture2D.whiteTexture,
                        new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                        new Vector2(0.5f, 0.5f)
                    );
                }
                
                // Set initial state instead of calling Initialize
                enemy.enabled = true;
                
                prefab.SetActive(false);
                return prefab;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[EnemySystemPlayTests] Failed to create enemy prefab: {e.Message}");
                UnityEngine.Object.DestroyImmediate(prefab);
                throw;
            }
        }
    }
} 