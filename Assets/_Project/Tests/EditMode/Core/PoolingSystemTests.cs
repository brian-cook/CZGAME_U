using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CZ.Core.Pooling;

namespace CZ.Tests.EditMode.Core
{
    public class PoolingSystemTests
    {
        private ObjectPool<TestPoolable> testPool;
        private GameObject testPrefab;
        private const int EXPECTED_PEAK = 100; // From performance guidelines
        private const int INITIAL_SIZE = 50;   // Expected peak / 2
        private const int MAX_SIZE = 120;      // Expected peak * 1.2
        
        [SetUp]
        public void Setup()
        {
            testPrefab = new GameObject("TestPrefab");
            testPrefab.AddComponent<TestPoolable>();
            
            testPool = new ObjectPool<TestPoolable>(
                createFunc: () => {
                    var obj = Object.Instantiate(testPrefab).GetComponent<TestPoolable>();
                    return obj;
                },
                initialSize: INITIAL_SIZE,
                maxSize: MAX_SIZE,
                "TestPool"
            );
        }
        
        [TearDown]
        public void Teardown()
        {
            if (testPool != null)
            {
                testPool.Clear();
            }
            Object.DestroyImmediate(testPrefab);
        }
        
        [Test]
        public void Pool_InitializesWithCorrectSize()
        {
            Assert.That(testPool.CurrentCount, Is.EqualTo(INITIAL_SIZE));
            Assert.That(testPool.MaxSize, Is.EqualTo(MAX_SIZE));
        }
        
        [Test]
        public void Pool_Get_ReturnsValidObject()
        {
            var obj = testPool.Get();
            Assert.That(obj, Is.Not.Null);
            Assert.That(obj.GameObject.activeSelf, Is.True);
        }
        
        [Test]
        public void Pool_Return_AddsObjectBackToPool()
        {
            var obj = testPool.Get();
            int countBeforeReturn = testPool.CurrentCount;
            
            testPool.Return(obj);
            
            Assert.That(testPool.CurrentCount, Is.EqualTo(countBeforeReturn + 1));
            Assert.That(obj.GameObject.activeSelf, Is.False);
        }
        
        [Test]
        public void Pool_Expansion_StaysWithinMaxSize()
        {
            // Ignore all warning messages since we'll have multiple expansions
            LogAssert.ignoreFailingMessages = true;
            
            var objects = new TestPoolable[MAX_SIZE + 10];
            int successfulGets = 0;
            
            // Try to get more objects than max size
            for (int i = 0; i < MAX_SIZE + 10; i++)
            {
                var obj = testPool.Get();
                if (obj != null)
                {
                    objects[successfulGets] = obj;
                    successfulGets++;
                }
            }
            
            // Verify we didn't exceed max size
            Assert.That(successfulGets, Is.LessThanOrEqualTo(MAX_SIZE), 
                "Pool returned more objects than max size");
            Assert.That(testPool.TotalCount, Is.LessThanOrEqualTo(MAX_SIZE), 
                "Pool total count exceeded max size");
            
            // Clean up
            for (int i = 0; i < successfulGets; i++)
            {
                if (objects[i] != null)
                    testPool.Return(objects[i]);
            }
            
            // Reset log assert settings
            LogAssert.ignoreFailingMessages = false;
        }
        
        [Test]
        public void Pool_Clear_ReleasesAllObjects()
        {
            // Get some objects
            var objects = new TestPoolable[10];
            for (int i = 0; i < objects.Length; i++)
            {
                objects[i] = testPool.Get();
            }
            
            testPool.Clear();
            
            Assert.That(testPool.CurrentCount, Is.Zero);
        }
        
        [Test]
        public void Pool_OnSpawnAndOnDespawn_AreCalled()
        {
            var obj = testPool.Get();
            Assert.That(obj.WasSpawnCalled, Is.True);
            
            testPool.Return(obj);
            Assert.That(obj.WasDespawnCalled, Is.True);
        }
        
        [UnityTest]
        public IEnumerator Pool_StressTest_MaintainsPerformance()
        {
            int initialCount = testPool.CurrentCount;
            var objects = new TestPoolable[INITIAL_SIZE];
            
            // Rapid get/return cycle
            for (int cycle = 0; cycle < 3; cycle++)
            {
                // Get phase
                for (int i = 0; i < INITIAL_SIZE; i++)
                {
                    objects[i] = testPool.Get();
                    yield return null;
                }
                
                // Return phase
                for (int i = 0; i < INITIAL_SIZE; i++)
                {
                    if (objects[i] != null)
                        testPool.Return(objects[i]);
                    yield return null;
                }
            }
            
            // Verify pool maintained its size
            Assert.That(testPool.CurrentCount, Is.EqualTo(initialCount), 
                "Pool size changed after stress test");
            Assert.That(testPool.PeakCount, Is.LessThanOrEqualTo(MAX_SIZE), 
                "Pool exceeded max size during stress test");
        }
    }
    
    public class TestPoolable : MonoBehaviour, IPoolable
    {
        public bool WasSpawnCalled { get; private set; }
        public bool WasDespawnCalled { get; private set; }
        public GameObject GameObject => gameObject;
        
        public void OnSpawn()
        {
            WasSpawnCalled = true;
            WasDespawnCalled = false;
            gameObject.SetActive(true);
        }
        
        public void OnDespawn()
        {
            WasDespawnCalled = true;
            WasSpawnCalled = false;
            gameObject.SetActive(false);
        }
    }
} 