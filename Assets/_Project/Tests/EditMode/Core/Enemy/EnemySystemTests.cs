using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CZ.Core.Enemy;
using CZ.Core.Pooling;

namespace CZ.Tests.EditMode.Enemy
{
    public class EnemySystemTests
    {
        private GameObject enemyObject;
        private BaseEnemy enemy;
        private GameObject spawnerObject;
        private EnemySpawner spawner;
        private ObjectPool<BaseEnemy> testPool;
        
        [SetUp]
        public void Setup()
        {
            // Create test objects
            enemyObject = new GameObject("TestEnemy");
            enemy = enemyObject.AddComponent<BaseEnemy>();
            
            // Add required components
            var spriteRenderer = enemyObject.AddComponent<SpriteRenderer>();
            var circleCollider = enemyObject.AddComponent<CircleCollider2D>();
            var rb = enemyObject.AddComponent<Rigidbody2D>();
            
            spawnerObject = new GameObject("TestSpawner");
            spawner = spawnerObject.AddComponent<EnemySpawner>();
            
            // Create test pool without using PoolManager
            testPool = new ObjectPool<BaseEnemy>(
                createFunc: () => {
                    var obj = new GameObject("PooledEnemy");
                    var pooledEnemy = obj.AddComponent<BaseEnemy>();
                    obj.AddComponent<SpriteRenderer>();
                    obj.AddComponent<CircleCollider2D>();
                    obj.AddComponent<Rigidbody2D>();
                    return pooledEnemy;
                },
                initialSize: 5,
                maxSize: 10,
                "TestPool"
            );
        }
        
        [TearDown]
        public void Teardown()
        {
            // Clean up test objects
            if (testPool != null)
            {
                testPool.Clear();
            }
            Object.DestroyImmediate(enemyObject);
            Object.DestroyImmediate(spawnerObject);
        }
        
        [Test]
        public void BaseEnemy_WhenCreated_HasCorrectDefaultValues()
        {
            // Test initial state
            Assert.That(enemy, Is.Not.Null);
            Assert.That(enemy.GameObject, Is.EqualTo(enemyObject));
            
            // Verify required components
            Assert.That(enemy.GetComponent<SpriteRenderer>(), Is.Not.Null);
            Assert.That(enemy.GetComponent<CircleCollider2D>(), Is.Not.Null);
            Assert.That(enemy.GetComponent<Rigidbody2D>(), Is.Not.Null);
        }
        
        [Test]
        public void BaseEnemy_WhenTakingDamage_HealthDecreasesCorrectly()
        {
            // Setup - Get private health field using reflection
            var healthField = typeof(BaseEnemy).GetField("health", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            
            // Initial health should be 100
            int initialHealth = (int)healthField.GetValue(enemy);
            Assert.That(initialHealth, Is.EqualTo(100));
            
            // Apply damage
            enemy.TakeDamage(25);
            
            // Check new health value
            int newHealth = (int)healthField.GetValue(enemy);
            Assert.That(newHealth, Is.EqualTo(75));
        }
        
        [Test]
        public void BaseEnemy_WhenHealthReachesZero_IsDeactivated()
        {
            // Setup
            enemy.OnSpawn();
            
            // Apply fatal damage
            enemy.TakeDamage(100);
            
            Assert.That(enemy.GameObject.activeSelf, Is.False);
        }
        
        [Test]
        public void EnemySpawner_WhenCreated_HasCorrectDefaultValues()
        {
            // Test initial state
            Assert.That(spawner, Is.Not.Null);
            
            // Verify default serialized fields using reflection
            var spawnIntervalField = typeof(EnemySpawner).GetField("spawnInterval", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            var maxEnemiesPerWaveField = typeof(EnemySpawner).GetField("maxEnemiesPerWave", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            
            float spawnInterval = (float)spawnIntervalField.GetValue(spawner);
            int maxEnemiesPerWave = (int)maxEnemiesPerWaveField.GetValue(spawner);
            
            Assert.That(spawnInterval, Is.EqualTo(1f));
            Assert.That(maxEnemiesPerWave, Is.EqualTo(5));
        }
        
        [Test]
        public void BaseEnemy_WhenTargetSet_UpdatesTargetPosition()
        {
            // Setup
            Vector3 testPosition = new Vector3(1f, 2f, 3f);
            
            // Set target
            enemy.SetTarget(testPosition);
            
            // Get private targetPosition field using reflection
            var targetPositionField = typeof(BaseEnemy).GetField("targetPosition", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            Vector3 storedPosition = (Vector3)targetPositionField.GetValue(enemy);
            
            // Verify position was set
            Assert.That(storedPosition, Is.EqualTo(testPosition));
        }
        
        [Test]
        public void BaseEnemy_OnSpawn_InitializesCorrectly()
        {
            // Setup
            enemy.OnSpawn();
            
            // Get private fields using reflection
            var healthField = typeof(BaseEnemy).GetField("health", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            var isInitializedField = typeof(BaseEnemy).GetField("isInitialized", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            
            int health = (int)healthField.GetValue(enemy);
            bool isInitialized = (bool)isInitializedField.GetValue(enemy);
            
            // Verify initialization
            Assert.That(health, Is.EqualTo(100));
            Assert.That(isInitialized, Is.True);
            Assert.That(enemy.GameObject.activeSelf, Is.True);
        }
        
        [Test]
        public void BaseEnemy_OnDespawn_CleansUpCorrectly()
        {
            // Setup
            enemy.OnSpawn();
            enemy.OnDespawn();
            
            // Get private isInitialized field using reflection
            var isInitializedField = typeof(BaseEnemy).GetField("isInitialized", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            bool isInitialized = (bool)isInitializedField.GetValue(enemy);
            
            // Verify cleanup
            Assert.That(enemy.GameObject.activeSelf, Is.False);
        }
    }
} 