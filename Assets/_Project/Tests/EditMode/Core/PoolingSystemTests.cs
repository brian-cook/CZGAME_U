using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CZ.Core.Pooling;

namespace CZ.Tests.EditMode.Core
{
    public class PoolingSystemTests
    {
        private GameObject testPrefab;
        private ObjectPool<TestPoolable> pool;
        private const int INITIAL_SIZE = 5;
        private const int MAX_SIZE = 10;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Create test prefab
            testPrefab = new GameObject("TestPrefab");
            testPrefab.AddComponent<TestPoolable>();
        }

        [SetUp]
        public void SetUp()
        {
            // Create new pool for each test
            pool = new ObjectPool<TestPoolable>(
                createFunc: () =>
                {
                    var go = Object.Instantiate(testPrefab);
                    return go.GetComponent<TestPoolable>();
                },
                initialSize: INITIAL_SIZE,
                maxSize: MAX_SIZE,
                poolName: "TestPool"
            );
        }

        [TearDown]
        public void TearDown()
        {
            pool.Clear();
            pool = null;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            Object.DestroyImmediate(testPrefab);
        }

        [Test]
        public void Pool_InitializesWithCorrectSize()
        {
            Assert.That(pool.CurrentCount, Is.EqualTo(INITIAL_SIZE));
            Assert.That(pool.PeakCount, Is.EqualTo(INITIAL_SIZE));
        }

        [Test]
        public void Pool_Get_ReturnsValidObject()
        {
            var obj = pool.Get();
            Assert.That(obj, Is.Not.Null);
            Assert.That(obj.GameObject.activeInHierarchy, Is.True);
            Assert.That(pool.CurrentCount, Is.EqualTo(INITIAL_SIZE - 1));
        }

        [Test]
        public void Pool_Return_AddsObjectBackToPool()
        {
            var obj = pool.Get();
            pool.Return(obj);
            
            Assert.That(pool.CurrentCount, Is.EqualTo(INITIAL_SIZE));
            Assert.That(obj.GameObject.activeInHierarchy, Is.False);
        }

        [Test]
        public void Pool_Expansion_StaysWithinMaxSize()
        {
            var objects = new TestPoolable[MAX_SIZE + 1];
            
            // Try to get more objects than max size
            for (int i = 0; i < MAX_SIZE + 1; i++)
            {
                objects[i] = pool.Get();
            }
            
            // Last object should be null (exceeded max size)
            Assert.That(objects[MAX_SIZE], Is.Null);
            Assert.That(pool.CurrentCount, Is.EqualTo(0));
            Assert.That(pool.PeakCount, Is.EqualTo(MAX_SIZE));
        }

        [Test]
        public void Pool_Clear_ReleasesAllObjects()
        {
            // Get some objects
            var obj1 = pool.Get();
            var obj2 = pool.Get();
            
            pool.Clear();
            
            Assert.That(pool.CurrentCount, Is.EqualTo(0));
            Assert.That(obj1.GameObject, Is.Not.Null); // Objects still exist
            Assert.That(obj2.GameObject, Is.Not.Null); // but are not in pool
        }

        [UnityTest]
        public IEnumerator Pool_StressTest_MaintainsPerformance()
        {
            var startTime = Time.realtimeSinceStartup;
            var objects = new TestPoolable[MAX_SIZE];
            
            // Rapid get/return operations
            for (int i = 0; i < 1000; i++)
            {
                // Get all objects
                for (int j = 0; j < MAX_SIZE; j++)
                {
                    objects[j] = pool.Get();
                }
                
                // Return all objects
                for (int j = 0; j < MAX_SIZE; j++)
                {
                    if (objects[j] != null)
                    {
                        pool.Return(objects[j]);
                    }
                }
                
                if (i % 100 == 0) // Yield occasionally to prevent test timeout
                {
                    yield return null;
                }
            }
            
            var duration = Time.realtimeSinceStartup - startTime;
            Debug.Log($"Stress test completed in {duration:F2} seconds");
            
            Assert.That(duration, Is.LessThan(5f), "Stress test took too long");
            Assert.That(pool.CurrentCount, Is.EqualTo(INITIAL_SIZE), "Pool size changed after stress test");
        }

        [Test]
        public void Pool_OnSpawnAndOnDespawn_AreCalled()
        {
            var obj = pool.Get();
            Assert.That(obj.WasSpawnCalled, Is.True, "OnSpawn was not called");
            
            pool.Return(obj);
            Assert.That(obj.WasDespawnCalled, Is.True, "OnDespawn was not called");
        }
    }

    /// <summary>
    /// Test implementation of IPoolable for unit tests
    /// </summary>
    public class TestPoolable : MonoBehaviour, IPoolable
    {
        public GameObject GameObject => gameObject;
        public bool WasSpawnCalled { get; private set; }
        public bool WasDespawnCalled { get; private set; }

        public void OnSpawn()
        {
            WasSpawnCalled = true;
            WasDespawnCalled = false;
        }

        public void OnDespawn()
        {
            WasDespawnCalled = true;
            WasSpawnCalled = false;
        }
    }
} 