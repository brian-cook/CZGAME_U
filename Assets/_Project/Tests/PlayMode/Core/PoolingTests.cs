using UnityEngine;
using CZ.Core.Pooling;
using Unity.Profiling;
using System.Collections;

namespace CZ.Tests.PlayMode.Core
{
    /// <summary>
    /// Test script for verifying pooling system functionality
    /// Implements Unity 6.0 best practices for testing and performance monitoring
    /// </summary>
    public class PoolingTests : MonoBehaviour
    {
        [Header("Test Configuration")]
        [SerializeField] private GameObject testPrefab;
        [SerializeField] private int spawnBatchSize = 10;
        [SerializeField] private float spawnInterval = 0.1f;
        [SerializeField] private bool autoTest = false;
        
        private ObjectPool<TestPoolable> testPool;
        private float nextSpawnTime;
        private int totalSpawned;
        private PoolMonitor poolMonitor;
        
        // Performance markers
        private static readonly ProfilerMarker s_spawnMarker = new(ProfilerCategory.Memory, "PoolingTest.SpawnBatch");
        private static readonly ProfilerMarker s_returnMarker = new(ProfilerCategory.Memory, "PoolingTest.ReturnAll");
        private static readonly ProfilerMarker s_createMarker = new(ProfilerCategory.Memory, "PoolingTest.CreateObject");
        
        // Performance tracking
        private int gcAllocations;
        private float lastGcCheck;
        private const float GC_CHECK_INTERVAL = 1f;
        
        private void Start()
        {
            if (testPrefab == null)
            {
                Debug.LogError("TestPrefab not assigned to PoolingTests!");
                enabled = false;
                return;
            }
            
            using var _ = new ProfilerMarker("PoolingTest.Initialize").Auto();
            
            // Ensure PoolManager exists
            var poolManager = PoolManager.Instance;
            
            // Create test pool
            testPool = poolManager.CreatePool(
                createFunc: CreateTestObject,
                initialSize: 50,
                maxSize: 100,
                poolName: "TestPool"
            );
            
            // Add PoolMonitor if not present
            poolMonitor = Object.FindFirstObjectByType<PoolMonitor>();
            if (poolMonitor == null)
            {
                poolMonitor = gameObject.AddComponent<PoolMonitor>();
            }
            
            // Start performance monitoring
            StartCoroutine(MonitorPerformance());
            
            Debug.Log("Pool test initialized. Press keys 1-4 to test different scenarios.");
            Debug.Log("1: Spawn batch | 2: Return all | 3: Stress test | 4: Clear pool");
            Debug.Log("F3: Toggle pool monitor");
        }
        
        private void Update()
        {
            // Manual test controls
            if (Input.GetKeyDown(KeyCode.Alpha1)) SpawnBatch();
            if (Input.GetKeyDown(KeyCode.Alpha2)) ReturnAll();
            if (Input.GetKeyDown(KeyCode.Alpha3)) StartStressTest();
            if (Input.GetKeyDown(KeyCode.Alpha4)) ClearPool();
            
            // Auto test spawning
            if (autoTest && Time.time >= nextSpawnTime)
            {
                SpawnBatch();
                nextSpawnTime = Time.time + spawnInterval;
            }
        }
        
        private TestPoolable CreateTestObject()
        {
            using var _ = s_createMarker.Auto();
            
            var go = Instantiate(testPrefab);
            var poolable = go.AddComponent<TestPoolable>();
            return poolable;
        }
        
        private void SpawnBatch()
        {
            using var _ = s_spawnMarker.Auto();
            
            for (int i = 0; i < spawnBatchSize; i++)
            {
                var obj = testPool.Get();
                if (obj != null)
                {
                    totalSpawned++;
                    // Position randomly in view
                    obj.GameObject.transform.position = new Vector3(
                        Random.Range(-8f, 8f),
                        Random.Range(-4f, 4f),
                        0
                    );
                }
            }
            
            Debug.Log($"Spawned batch. Total: {totalSpawned}, Pool size: {testPool.CurrentCount}, Peak: {testPool.PeakCount}");
        }
        
        private void ReturnAll()
        {
            using var _ = s_returnMarker.Auto();
            
            var objects = Object.FindObjectsByType<TestPoolable>(FindObjectsSortMode.None);
            foreach (var obj in objects)
            {
                testPool.Return(obj);
            }
            totalSpawned = 0;
            
            Debug.Log($"Returned all objects. Pool size: {testPool.CurrentCount}");
        }
        
        private void StartStressTest()
        {
            autoTest = !autoTest;
            Debug.Log($"Stress test {(autoTest ? "started" : "stopped")}");
            
            if (autoTest)
            {
                Debug.Log("Starting performance monitoring...");
                gcAllocations = 0;
                lastGcCheck = Time.time;
            }
        }
        
        private void ClearPool()
        {
            ReturnAll();
            testPool.Clear();
            Debug.Log("Pool cleared");
        }
        
        private IEnumerator MonitorPerformance()
        {
            var memoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc");
            var mainThreadTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
            
            while (enabled)
            {
                yield return new WaitForSeconds(GC_CHECK_INTERVAL);
                
                if (!autoTest) continue;
                
                var currentTime = Time.time;
                var deltaTime = currentTime - lastGcCheck;
                lastGcCheck = currentTime;
                
                // Calculate metrics
                var allocRate = memoryRecorder.LastValue / (1024f * 1024f); // MB
                var frameTime = mainThreadTimeRecorder.LastValue / 1000000f; // ms
                
                // Log performance data
                Debug.Log($"Performance Metrics:\n" +
                         $"Memory Allocation Rate: {allocRate:F2} MB/s\n" +
                         $"Frame Time: {frameTime:F2}ms\n" +
                         $"Active Objects: {totalSpawned}\n" +
                         $"Pool Size: {testPool.CurrentCount}\n" +
                         $"Pool Peak: {testPool.PeakCount}");
            }
            
            memoryRecorder.Dispose();
            mainThreadTimeRecorder.Dispose();
        }
        
        private void OnDestroy()
        {
            if (testPool != null)
            {
                testPool.Clear();
            }
        }
    }
    
    /// <summary>
    /// Test implementation of IPoolable
    /// </summary>
    public class TestPoolable : MonoBehaviour, IPoolable
    {
        public GameObject GameObject => gameObject;
        
        private Vector3 moveDirection;
        private float moveSpeed;
        private static readonly ProfilerMarker s_updateMarker = new(ProfilerCategory.Scripts, "TestPoolable.Update");
        
        public void OnSpawn()
        {
            // Random movement on spawn
            moveDirection = Random.insideUnitCircle.normalized;
            moveSpeed = Random.Range(1f, 3f);
            
            // Random color
            var renderer = GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.color = Random.ColorHSV(0f, 1f, 0.8f, 1f, 0.8f, 1f);
            }
        }
        
        public void OnDespawn()
        {
            moveDirection = Vector3.zero;
            moveSpeed = 0;
        }
        
        private void Update()
        {
            using var _ = s_updateMarker.Auto();
            
            // Simple movement
            transform.position += moveDirection * (moveSpeed * Time.deltaTime);
            
            // Return to pool if out of bounds
            if (Mathf.Abs(transform.position.x) > 10f || Mathf.Abs(transform.position.y) > 6f)
            {
                var pool = PoolManager.Instance.GetPool<TestPoolable>();
                pool?.Return(this);
            }
        }
    }
} 