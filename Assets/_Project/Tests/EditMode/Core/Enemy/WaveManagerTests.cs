using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CZ.Core.Enemy;

namespace Tests.EditMode.Core.Enemy
{
    public class WaveManagerTests
    {
        private GameObject testManagerObject;
        private WaveManager waveManager;
        private GameObject testEnemyPrefab;
        private GameObject[] testSpawnPoints;

        /// <summary>
        /// Setup for each test - creates a test wave manager object with required components
        /// </summary>
        [SetUp]
        public void Setup()
        {
            // Create test manager game object
            testManagerObject = new GameObject("TestWaveManager");
            waveManager = testManagerObject.AddComponent<WaveManager>();
            
            // Create test enemy prefab
            testEnemyPrefab = new GameObject("TestEnemyPrefab");
            testEnemyPrefab.AddComponent<SpriteRenderer>();
            testEnemyPrefab.AddComponent<CircleCollider2D>();
            testEnemyPrefab.AddComponent<Rigidbody2D>().gravityScale = 0;
            
            // Add a mock BaseEnemy component
            MockBaseEnemy mockEnemy = testEnemyPrefab.AddComponent<MockBaseEnemy>();
            
            // Create test spawn points
            testSpawnPoints = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                testSpawnPoints[i] = new GameObject($"SpawnPoint_{i}");
                testSpawnPoints[i].transform.position = new Vector3(i * 5, 0, 0);
            }
            
            // Create test player
            GameObject testPlayer = new GameObject("TestPlayer");
            testPlayer.transform.position = Vector3.zero;
            
            // Set up private fields through reflection
            System.Reflection.FieldInfo enemyTypesField = typeof(WaveManager).GetField("enemyTypes", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            System.Reflection.FieldInfo spawnPointsField = typeof(WaveManager).GetField("spawnPoints", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            System.Reflection.FieldInfo playerTransformField = typeof(WaveManager).GetField("playerTransform", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (enemyTypesField != null)
            {
                var enemyTypes = new List<WaveManager.EnemyTypeInfo>
                {
                    new WaveManager.EnemyTypeInfo
                    {
                        enemyType = "BasicEnemy",
                        enemyPrefab = testEnemyPrefab,
                        initialPoolSize = 5,
                        maxPoolSize = 10,
                        spawnWeight = 1.0f,
                        difficultyScaling = 1.0f
                    }
                };
                enemyTypesField.SetValue(waveManager, enemyTypes);
            }
            
            if (spawnPointsField != null)
            {
                Transform[] spawnPoints = new Transform[testSpawnPoints.Length];
                for (int i = 0; i < testSpawnPoints.Length; i++)
                {
                    spawnPoints[i] = testSpawnPoints[i].transform;
                }
                spawnPointsField.SetValue(waveManager, spawnPoints);
            }
            
            if (playerTransformField != null)
            {
                playerTransformField.SetValue(waveManager, testPlayer.transform);
            }
        }

        /// <summary>
        /// Teardown after each test - destroys test objects
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(testManagerObject);
            Object.DestroyImmediate(testEnemyPrefab);
            
            for (int i = 0; i < testSpawnPoints.Length; i++)
            {
                if (testSpawnPoints[i] != null)
                {
                    Object.DestroyImmediate(testSpawnPoints[i]);
                }
            }
        }

        /// <summary>
        /// Test to verify wave auto-generation creates the expected number of waves
        /// </summary>
        [Test]
        public void WaveManager_GeneratesCorrectNumberOfWaves()
        {
            // Set test values via reflection
            System.Reflection.FieldInfo maxWaveCountField = typeof(WaveManager).GetField("maxWaveCount", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            System.Reflection.FieldInfo waveConfigsField = typeof(WaveManager).GetField("waveConfigurations", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (maxWaveCountField != null && waveConfigsField != null)
            {
                // Set max wave count
                int testMaxWaveCount = 10;
                maxWaveCountField.SetValue(waveManager, testMaxWaveCount);
                
                // Call the private method to generate waves
                System.Reflection.MethodInfo generateMethod = typeof(WaveManager).GetMethod("GenerateWaveConfigurations", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                generateMethod?.Invoke(waveManager, null);
                
                // Verify the number of waves
                var waveConfigs = waveConfigsField.GetValue(waveManager) as List<WaveManager.WaveConfig>;
                Assert.IsNotNull(waveConfigs, "Wave configurations should not be null");
                Assert.AreEqual(testMaxWaveCount, waveConfigs.Count, "Should generate the correct number of waves");
                
                // Verify wave properties
                for (int i = 0; i < waveConfigs.Count; i++)
                {
                    Assert.AreEqual(i + 1, waveConfigs[i].waveNumber, $"Wave {i} should have correct wave number");
                    
                    // Verify that wave 3 has a tank enemy
                    if ((i + 1) % 3 == 0)
                    {
                        bool hasTank = false;
                        foreach (var specialEnemy in waveConfigs[i].specialEnemies)
                        {
                            if (specialEnemy.enemyType == "TankEnemy")
                            {
                                hasTank = true;
                                break;
                            }
                        }
                        
                        Assert.IsTrue(hasTank, $"Wave {i + 1} should have a tank enemy");
                    }
                }
            }
        }

        /// <summary>
        /// Test to verify that spawn positions are generated properly
        /// </summary>
        [Test]
        public void WaveManager_GeneratesValidSpawnPositions()
        {
            // Call the private method to get a spawn position
            System.Reflection.MethodInfo getSpawnPositionMethod = typeof(WaveManager).GetMethod("GetSpawnPosition", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (getSpawnPositionMethod != null)
            {
                // Get a spawn position
                Vector3 spawnPosition = (Vector3)getSpawnPositionMethod.Invoke(waveManager, new object[] { Vector2.zero });
                
                // Verify it's a valid position
                Assert.IsFalse(spawnPosition == Vector3.zero, "Spawn position should not be zero (the error case)");
                
                // Verify it's one of our test spawn points
                bool isValidPosition = false;
                foreach (var spawnPoint in testSpawnPoints)
                {
                    if (Vector3.Distance(spawnPosition, spawnPoint.transform.position) < 0.01f)
                    {
                        isValidPosition = true;
                        break;
                    }
                }
                
                Assert.IsTrue(isValidPosition, "Spawn position should match one of the test spawn points");
            }
        }

        /// <summary>
        /// Mock implementation of BaseEnemy for testing
        /// </summary>
        private class MockBaseEnemy : BaseEnemy
        {
            public override void TakeDamage(int damageAmount)
            {
                // Mock implementation
            }
            
            protected override void FixedUpdate()
            {
                // Mock implementation
            }
            
            public override void SetTarget(Vector3 position)
            {
                // Mock implementation
            }
        }
    }
} 