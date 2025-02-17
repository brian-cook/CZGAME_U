using UnityEngine;
using System;
using Unity.Profiling;
using UnityEngine.SceneManagement;
using NaughtyAttributes;
using System.Collections;
using CZ.Core.Pooling;

namespace CZ.Core
{
    /// <summary>
    /// Core GameManager class handling game state, scene management, and performance monitoring.
    /// Follows Unity 6.0 best practices and performance guidelines.
    /// </summary>
    [DefaultExecutionOrder(-100)] // Ensure GameManager initializes before other scripts
    public class GameManager : MonoBehaviour
    {
        #region Singleton Setup
        private static GameManager instance;
        public static GameManager Instance => instance;
        
        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeManager();
        }
        #endregion

        #region Properties and Fields
        // Performance monitoring
        private ProfilerRecorder drawCallsRecorder;
        private ProfilerRecorder memoryRecorder;
        private ProfilerRecorder gcMemoryRecorder;
        private bool isCleaningUp;
        private int consecutiveCleanupAttempts;
        private const int MAX_CLEANUP_ATTEMPTS = 3;
        private const float CLEANUP_COOLDOWN = 5f;
        private float lastCleanupTime;
        private bool emergencyCleanupRequired;
        private float lastMemoryCheck;
        private const float MEMORY_CHECK_INTERVAL = 0.5f;
        private float startupMemoryBaseline;
        
        [BoxGroup("Performance Metrics")]
        [ShowNonSerializedField, ReadOnly]
        private int currentDrawCalls;
        
        [BoxGroup("Performance Metrics")]
        [ShowNonSerializedField, ReadOnly]
        private float drawCallsRatio;
        
        [BoxGroup("Performance Metrics")]
        [ShowNonSerializedField, ReadOnly]
        private float currentMemoryUsageMB;
        
        [BoxGroup("Performance Metrics")]
        [ShowNonSerializedField, ReadOnly]
        private float memoryUsageRatio;
        
        [BoxGroup("Performance Metrics")]
        [ShowNonSerializedField, ReadOnly]
        private float memoryBaseline;
        
        [BoxGroup("Performance Metrics")]
        [ShowNonSerializedField, ReadOnly]
        private float memoryDelta;
        
        [BoxGroup("Performance Metrics")]
        [ShowNonSerializedField, ReadOnly]
        private int cleanupAttempts;
        
        [BoxGroup("Performance Metrics")]
        [ShowNonSerializedField, ReadOnly]
        private bool isInEmergencyMode;
        public bool IsInEmergencyMode => isInEmergencyMode;

        // Memory thresholds with more granular levels
        private const float BASELINE_THRESHOLD_MB = 512f;  // Lowered baseline
        private const float WARNING_THRESHOLD_MB = 768f;   // Earlier warning
        private const float CRITICAL_THRESHOLD_MB = 896f;  // Earlier critical
        private const float EMERGENCY_THRESHOLD_MB = 1024f; // Matches max memory requirement
        
        // Game state
        public enum GameState
        {
            MainMenu,
            Playing,
            Paused,
            GameOver
        }
        
        [BoxGroup("Game State")]
        [ShowNonSerializedField, ReadOnly]
        private GameState currentGameState;
        public GameState CurrentGameState
        {
            get => currentGameState;
            private set
            {
                if (currentGameState != value)
                {
                    currentGameState = value;
                    OnGameStateChanged?.Invoke(currentGameState);
                }
            }
        }
        
        // Events
        public event Action<GameState> OnGameStateChanged;
        #endregion

        #region Initialization
        private void InitializeManager()
        {
            // Set initial game state
            CurrentGameState = GameState.MainMenu;
            
            // Additional initialization
            Application.targetFrameRate = 60; // Target 60 FPS as per requirements
            isCleaningUp = false;
            consecutiveCleanupAttempts = 0;
            lastCleanupTime = -CLEANUP_COOLDOWN; // Allow immediate first cleanup
            
            // Initialize monitoring
            if (InitializePerformanceMonitoring())
            {
                // Only start monitoring after initialization
                enabled = true;
                
                // Auto-start game for testing (remove in production)
                #if UNITY_EDITOR
                StartCoroutine(AutoStartGame());
                #endif
            }
            else
            {
                Debug.LogError("[GameManager] Failed to initialize performance monitoring. Memory management disabled.");
                enabled = false;
            }
        }

        private float ConvertToMB(long bytes)
        {
            return bytes / (1024f * 1024f);
        }

        private bool InitializePerformanceMonitoring()
        {
            try
            {
                // Dispose any existing recorders
                CleanupRecorders();
                
                // Initialize with Unity 6.0 specific profiler categories and sampling
                drawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
                
                if (!drawCallsRecorder.Valid)
                {
                    Debug.LogError("[GameManager] Failed to initialize draw calls recorder");
                    return false;
                }
                
                // Use Unity 6.0 specific memory counter with fallbacks
                try
                {
                    // Try each known memory counter name in order with proper category
                    string[] memoryCounters = new string[] 
                    {
                        "Total Used Memory",       // Primary counter
                        "System Memory",           // Fallback 1
                        "Total System Memory",     // Fallback 2
                        "Total Reserved Memory"    // Fallback 3
                    };
                    
                    bool memoryInitialized = false;
                    foreach (string counterName in memoryCounters)
                    {
                        try
                        {
                            memoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, counterName, 15);
                            if (memoryRecorder.Valid)
                            {
                                // Wait for valid reading
                                float testValue = 0f;
                                for (int i = 0; i < 10; i++)
                                {
                                    testValue = ConvertToMB(memoryRecorder.CurrentValue);
                                    if (testValue > 0)
                                    {
                                        Debug.Log($"[GameManager] Successfully initialized memory recorder with counter: {counterName} (Current: {testValue:F2}MB)");
                                        memoryInitialized = true;
                                        // Cache the initial valid reading
                                        currentMemoryUsageMB = testValue;
                                        break;
                                    }
                                    System.Threading.Thread.Sleep(50); // Short delay between checks
                                }
                                
                                if (!memoryInitialized)
                                {
                                    Debug.LogWarning($"[GameManager] Counter '{counterName}' failed to provide valid reading after initialization");
                                    memoryRecorder.Dispose();
                                }
                                else
                                {
                                    break;
                                }
                            }
                            else
                            {
                                memoryRecorder.Dispose();
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[GameManager] Failed to initialize counter '{counterName}': {e.Message}");
                            continue;
                        }
                    }
                    
                    if (!memoryInitialized)
                    {
                        Debug.LogError("[GameManager] Failed to initialize memory recorder - No valid Unity 6.0 memory counters found");
                        CleanupRecorders();
                        return false;
                    }
                }
                catch (Exception memEx)
                {
                    Debug.LogError($"[GameManager] Error initializing memory recorder: {memEx.Message}");
                    CleanupRecorders();
                    return false;
                }
                
                // Use Unity 6.0 GC specific counter with fallbacks
                try
                {
                    string[] gcCounters = new string[] 
                    {
                        "GC Used Memory",     // Primary counter
                        "GC Memory",          // Fallback 1
                        "GC Reserved Memory", // Fallback 2
                        "GC Heap Size"       // Fallback 3
                    };
                    
                    bool gcInitialized = false;
                    foreach (string counterName in gcCounters)
                    {
                        try
                        {
                            gcMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, counterName, 15);
                            if (gcMemoryRecorder.Valid)
                            {
                                // Wait for valid reading
                                float testValue = 0f;
                                for (int i = 0; i < 10; i++)
                                {
                                    testValue = ConvertToMB(gcMemoryRecorder.CurrentValue);
                                    if (testValue > 0)
                                    {
                                        Debug.Log($"[GameManager] Successfully initialized GC recorder with counter: {counterName} (Current: {testValue:F2}MB)");
                                        gcInitialized = true;
                                        break;
                                    }
                                    System.Threading.Thread.Sleep(50); // Short delay between checks
                                }
                                
                                if (!gcInitialized)
                                {
                                    Debug.LogWarning($"[GameManager] GC Counter '{counterName}' failed to provide valid reading after initialization");
                                    gcMemoryRecorder.Dispose();
                                }
                                else
                                {
                                    break;
                                }
                            }
                            else
                            {
                                gcMemoryRecorder.Dispose();
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[GameManager] Failed to initialize GC counter '{counterName}': {e.Message}");
                            continue;
                        }
                    }
                    
                    if (!gcInitialized)
                    {
                        Debug.LogError("[GameManager] Failed to initialize GC memory recorder - No valid Unity 6.0 GC counters found");
                        CleanupRecorders();
                        return false;
                    }
                }
                catch (Exception gcEx)
                {
                    Debug.LogError($"[GameManager] Error initializing GC memory recorder: {gcEx.Message}");
                    CleanupRecorders();
                    return false;
                }
                
                // Use cached initial memory reading
                Debug.Log($"[GameManager] Initial memory reading: {currentMemoryUsageMB:F2}MB");
                
                // Set initial baseline - this will be our starting point regardless of current usage
                memoryBaseline = currentMemoryUsageMB;
                startupMemoryBaseline = currentMemoryUsageMB; // Cache startup baseline
                
                // Adjust thresholds based on initial memory state if above baseline
                if (currentMemoryUsageMB > BASELINE_THRESHOLD_MB)
                {
                    float thresholdScale = currentMemoryUsageMB / BASELINE_THRESHOLD_MB;
                    float adjustedBaseline = BASELINE_THRESHOLD_MB * thresholdScale;
                    
                    Debug.Log($"[GameManager] Adjusting memory thresholds for high initial state (Scale: {thresholdScale:F2})");
                    Debug.Log($"[GameManager] Setting initial memory baseline to: {memoryBaseline:F2}MB (Adjusted threshold: {adjustedBaseline:F2}MB)");
                    
                    // Start with a preemptive cleanup to try to reduce memory
                    StartCoroutine(PerformInitialCleanup(adjustedBaseline));
                }
                else
                {
                    Debug.Log($"[GameManager] Setting initial memory baseline to: {memoryBaseline:F2}MB");
                    // Start with a preemptive cleanup to try to reduce memory
                    StartCoroutine(PerformInitialCleanup(BASELINE_THRESHOLD_MB));
                }
                
                Debug.Log("[GameManager] Performance monitoring initialized successfully");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameManager] Failed to initialize performance monitoring: {e.Message}");
                CleanupRecorders();
                return false;
            }
        }

        private IEnumerator PerformInitialCleanup(float adjustedBaseline)
        {
            if (isCleaningUp) yield break;
            
            isCleaningUp = true;
            Debug.Log("[GameManager] Performing initial memory cleanup...");
            
            // Wait for initial load to settle
            yield return new WaitForSeconds(1f);
            
            // Verify recorder is still valid
            if (!memoryRecorder.Valid || !gcMemoryRecorder.Valid)
            {
                Debug.LogError("[GameManager] Memory recorders not valid during initial cleanup");
                enabled = false;
                yield break;
            }
            
            // Unload all unused assets
            yield return Resources.UnloadUnusedAssets();
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.UnloadUnusedAssetsImmediate();
            #endif
            
            // Multiple targeted GC passes
            for (int i = 0; i < 3; i++)
            {
                GC.Collect(i, GCCollectionMode.Forced, true, true);
                GC.WaitForPendingFinalizers();
                yield return new WaitForSeconds(0.1f);
            }
            
            // Clear pools
            var poolManager = PoolManager.Instance;
            if (poolManager != null)
            {
                var pools = poolManager.GetAllPoolStats();
                foreach (var pool in pools)
                {
                    var poolType = Type.GetType(pool.Key);
                    if (poolType != null && typeof(IPoolable).IsAssignableFrom(poolType))
                    {
                        var getPoolMethod = typeof(PoolManager).GetMethod("GetPool").MakeGenericMethod(poolType);
                        var poolObj = getPoolMethod.Invoke(poolManager, null);
                        if (poolObj != null)
                        {
                            var clearMethod = poolObj.GetType().GetMethod("Clear");
                            clearMethod?.Invoke(poolObj, null);
                        }
                    }
                }
            }
            
            yield return new WaitForSeconds(0.5f);
            
            // Check memory after cleanup
            if (memoryRecorder.Valid)
            {
                float newMemoryUsage = ConvertToMB(memoryRecorder.CurrentValue);
                float delta = newMemoryUsage - startupMemoryBaseline;
                
                Debug.Log($"[GameManager] Initial cleanup completed. Memory: {newMemoryUsage:F2}MB (Delta: {delta:F2}MB)");
                
                // Calculate relative thresholds based on adjusted baseline
                float relativeWarning = (WARNING_THRESHOLD_MB / BASELINE_THRESHOLD_MB) * adjustedBaseline;
                float relativeCritical = (CRITICAL_THRESHOLD_MB / BASELINE_THRESHOLD_MB) * adjustedBaseline;
                float relativeEmergency = (EMERGENCY_THRESHOLD_MB / BASELINE_THRESHOLD_MB) * adjustedBaseline;
                
                Debug.Log($"[GameManager] Adjusted thresholds - Warning: {relativeWarning:F2}MB, Critical: {relativeCritical:F2}MB, Emergency: {relativeEmergency:F2}MB");
                
                // Update baseline if we achieved reduction
                if (newMemoryUsage < memoryBaseline)
                {
                    memoryBaseline = newMemoryUsage;
                    Debug.Log($"[GameManager] Memory baseline adjusted after cleanup: {memoryBaseline:F2}MB");
                }
            }
            
            isCleaningUp = false;
        }

        private IEnumerator AutoStartGame()
        {
            // Wait for all systems to initialize
            yield return new WaitForSeconds(0.5f);
            StartGame();
        }
        #endregion

        #region Game State Management
        [Button("Start Game")]
        public void StartGame()
        {
            if (CurrentGameState != GameState.Playing)
            {
                // Reset performance counters for new game session
                ResetPerformanceCounters();
                
                // Enable input system
                if (UnityEngine.InputSystem.InputSystem.devices.Count == 0)
                {
                    Debug.LogWarning("[GameManager] No input devices found. Attempting to initialize input system.");
                    UnityEngine.InputSystem.InputSystem.Update();
                }
                
                // Set game state to playing which enables systems
                CurrentGameState = GameState.Playing;
                
                // Log game start for debugging
                Debug.Log("[GameManager] Game started - Input and gameplay systems enabled");
                
                // Notify systems that game has started
                OnGameStateChanged?.Invoke(GameState.Playing);
            }
        }

        [Button("Pause Game")]
        public void PauseGame()
        {
            if (CurrentGameState == GameState.Playing)
            {
                CurrentGameState = GameState.Paused;
                Time.timeScale = 0f;
                Debug.Log("Game Paused");
            }
        }

        [Button("Resume Game")]
        public void ResumeGame()
        {
            if (CurrentGameState == GameState.Paused)
            {
                CurrentGameState = GameState.Playing;
                Time.timeScale = 1f;
                Debug.Log("Game Resumed");
            }
        }

        [Button("End Game")]
        public void EndGame()
        {
            if (CurrentGameState == GameState.Playing)
            {
                CurrentGameState = GameState.GameOver;
                Debug.Log("Game Ended");
            }
        }
        #endregion

        #region Performance Monitoring
        private void Update()
        {
            if (Time.time - lastMemoryCheck >= MEMORY_CHECK_INTERVAL)
            {
                MonitorPerformance();
                lastMemoryCheck = Time.time;
            }
        }

        private void MonitorPerformance()
        {
            if (Time.time < 5f) return;

            if (drawCallsRecorder.Valid)
            {
                currentDrawCalls = (int)drawCallsRecorder.LastValue;
                drawCallsRatio = currentDrawCalls / 100f;
                if (currentDrawCalls > 100)
                {
                    Debug.LogWarning($"Draw calls exceeded limit: {currentDrawCalls}/100");
                }
            }

            if (memoryRecorder.Valid && !isCleaningUp)
            {
                currentMemoryUsageMB = ConvertToMB(memoryRecorder.LastValue);
                
                // Calculate adjusted thresholds based on startup baseline
                float thresholdScale = Math.Max(1f, startupMemoryBaseline / BASELINE_THRESHOLD_MB);
                float adjustedWarning = WARNING_THRESHOLD_MB * thresholdScale;
                float adjustedCritical = CRITICAL_THRESHOLD_MB * thresholdScale;
                float adjustedEmergency = EMERGENCY_THRESHOLD_MB * thresholdScale;
                
                // Calculate memory delta relative to baseline
                memoryDelta = currentMemoryUsageMB - memoryBaseline;
                float relativeDelta = memoryDelta / memoryBaseline;
                
                // Preemptive cleanup at warning threshold
                if (currentMemoryUsageMB > adjustedWarning && currentMemoryUsageMB <= adjustedCritical)
                {
                    if (Time.time - lastCleanupTime >= CLEANUP_COOLDOWN)
                    {
                        Debug.Log($"Preemptive cleanup at {currentMemoryUsageMB:F2}MB (Delta: {memoryDelta:F2}MB, Relative: {relativeDelta:P2})");
                        StartCoroutine(PerformPreemptiveCleanup());
                    }
                }
                // Critical cleanup
                else if (currentMemoryUsageMB > adjustedCritical && currentMemoryUsageMB <= adjustedEmergency)
                {
                    if (Time.time - lastCleanupTime >= CLEANUP_COOLDOWN/2)
                    {
                        Debug.LogWarning($"Critical cleanup at {currentMemoryUsageMB:F2}MB (Delta: {memoryDelta:F2}MB, Relative: {relativeDelta:P2})");
                        StartCoroutine(PerformAggressiveCleanup());
                    }
                }
                // Emergency cleanup
                else if (currentMemoryUsageMB > adjustedEmergency && !emergencyCleanupRequired)
                {
                    emergencyCleanupRequired = true;
                    Debug.LogError($"EMERGENCY: Memory usage critical at {currentMemoryUsageMB:F2}MB (Delta: {memoryDelta:F2}MB, Relative: {relativeDelta:P2})");
                    StartCoroutine(PerformEmergencyCleanup());
                }
            }
        }

        private IEnumerator PerformPreemptiveCleanup()
        {
            if (isCleaningUp) yield break;
            
            isCleaningUp = true;
            lastCleanupTime = Time.time;
            
            // Light cleanup only
            Resources.UnloadUnusedAssets();
            GC.Collect(0);
            
            yield return new WaitForSeconds(0.2f);
            
            float newMemoryUsage = memoryRecorder.Valid ? memoryRecorder.LastValue / (1024f * 1024f) : 0f;
            float delta = newMemoryUsage - currentMemoryUsageMB;
            Debug.Log($"Preemptive cleanup completed. Change: {delta:F2}MB");
            
            // Update baseline if significantly lower
            if (newMemoryUsage < memoryBaseline * 0.9f)
            {
                memoryBaseline = newMemoryUsage;
                Debug.Log($"Memory baseline adjusted to: {memoryBaseline:F2}MB");
            }
            
            isCleaningUp = false;
        }

        private IEnumerator PerformAggressiveCleanup()
        {
            if (isCleaningUp) yield break;
            
            isCleaningUp = true;
            lastCleanupTime = Time.time;
            consecutiveCleanupAttempts++;
            
            float initialMemory = currentMemoryUsageMB;
            Debug.LogWarning($"Aggressive cleanup initiated. Memory: {initialMemory:F2}MB - Attempt {consecutiveCleanupAttempts}/{MAX_CLEANUP_ATTEMPTS}");
            
            // Stop all coroutines except essential ones
            StopAllCoroutines();
            this.StartCoroutine(PerformAggressiveCleanup());
            
            // Clear pools first
            var poolManager = PoolManager.Instance;
            if (poolManager != null)
            {
                var pools = poolManager.GetAllPoolStats();
                foreach (var pool in pools)
                {
                    var poolType = Type.GetType(pool.Key);
                    if (poolType != null && typeof(IPoolable).IsAssignableFrom(poolType))
                    {
                        var getPoolMethod = typeof(PoolManager).GetMethod("GetPool").MakeGenericMethod(poolType);
                        var poolObj = getPoolMethod.Invoke(poolManager, null);
                        if (poolObj != null)
                        {
                            var clearMethod = poolObj.GetType().GetMethod("Clear");
                            clearMethod?.Invoke(poolObj, null);
                        }
                    }
                }
            }
            
            // Unload unused assets
            yield return Resources.UnloadUnusedAssets();
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.UnloadUnusedAssetsImmediate();
            #endif
            
            // Multiple targeted GC passes
            for (int i = 0; i < 3; i++)
            {
                GC.Collect(i, GCCollectionMode.Forced, true, true);
                GC.WaitForPendingFinalizers();
                yield return new WaitForSeconds(0.1f);
            }
            
            float newMemoryUsage = memoryRecorder.Valid ? memoryRecorder.LastValue / (1024f * 1024f) : 0f;
            Debug.Log($"Aggressive cleanup completed. Memory reduced from {initialMemory:F2}MB to {newMemoryUsage:F2}MB");
            
            if (consecutiveCleanupAttempts >= MAX_CLEANUP_ATTEMPTS && newMemoryUsage > CRITICAL_THRESHOLD_MB)
            {
                emergencyCleanupRequired = true;
                StartCoroutine(PerformEmergencyCleanup());
            }
            
            isCleaningUp = false;
        }

        private IEnumerator PerformEmergencyCleanup()
        {
            if (isCleaningUp) yield break;
            
            isCleaningUp = true;
            isInEmergencyMode = true;
            Debug.LogError($"EMERGENCY CLEANUP - Memory usage critical: {currentMemoryUsageMB:F2}MB");
            
            // Immediate aggressive cleanup
            Resources.UnloadUnusedAssets();
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.UnloadUnusedAssetsImmediate();
            #endif
            
            // Multiple aggressive GC passes with different generations
            for (int i = 0; i < 3; i++)
            {
                // Target different generations in each pass
                GC.Collect(i, GCCollectionMode.Forced, true, true);
                yield return new WaitForSeconds(0.1f);
                GC.WaitForPendingFinalizers();
                yield return new WaitForSeconds(0.1f);
            }
            
            float newMemoryUsage = memoryRecorder.Valid ? memoryRecorder.LastValue / (1024f * 1024f) : 0f;
            
            if (newMemoryUsage > CRITICAL_THRESHOLD_MB)
            {
                Debug.LogError($"CRITICAL: Emergency cleanup failed to reduce memory usage. Restarting scene...");
                RestartCurrentScene();
            }
            else
            {
                Debug.Log($"Emergency cleanup successful. Memory reduced from {currentMemoryUsageMB:F2}MB to {newMemoryUsage:F2}MB");
                emergencyCleanupRequired = false;
                isInEmergencyMode = false;
                
                // Reset memory baseline after successful emergency cleanup
                memoryBaseline = newMemoryUsage;
            }
            
            isCleaningUp = false;
            consecutiveCleanupAttempts = 0;
            lastCleanupTime = Time.time;
        }

        private void OnEnable()
        {
            // Only initialize if we don't have valid recorders
            bool needsInitialization = true;
            
            try
            {
                needsInitialization = memoryRecorder.Equals(default(ProfilerRecorder)) || 
                                    !memoryRecorder.Valid || 
                                    gcMemoryRecorder.Equals(default(ProfilerRecorder)) || 
                                    !gcMemoryRecorder.Valid;
            }
            catch
            {
                // If we get an exception during checks, we definitely need initialization
                needsInitialization = true;
            }
            
            if (needsInitialization)
            {
                if (!InitializePerformanceMonitoring())
                {
                    Debug.LogError("[GameManager] Failed to initialize performance monitoring in OnEnable. Memory management will be disabled.");
                    enabled = false; // Disable the component if initialization fails
                    return;
                }
            }
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            CleanupRecorders();
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Reset cleanup state on new scene
            consecutiveCleanupAttempts = 0;
            lastCleanupTime = -CLEANUP_COOLDOWN;
            StartCoroutine(PerformPreemptiveCleanup());
        }

        private void CleanupRecorders()
        {
            try
            {
                if (drawCallsRecorder.Valid)
                {
                    drawCallsRecorder.Dispose();
                }
                drawCallsRecorder = default;
                
                if (memoryRecorder.Valid)
                {
                    memoryRecorder.Dispose();
                }
                memoryRecorder = default;
                
                if (gcMemoryRecorder.Valid)
                {
                    gcMemoryRecorder.Dispose();
                }
                gcMemoryRecorder = default;
                
                // Force a GC collection after disposing recorders
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error during recorder cleanup: {e.Message}");
            }
        }
        #endregion

        #region Scene Management
        public void LoadScene(string sceneName)
        {
            SceneManager.LoadSceneAsync(sceneName).completed += (asyncOperation) =>
            {
                // Scene load completed callback
                Debug.Log($"Scene {sceneName} loaded successfully");
            };
        }

        public void RestartCurrentScene()
        {
            LoadScene(SceneManager.GetActiveScene().name);
        }
        #endregion

        #region Debug Methods
        [Button("Force GC Collect")]
        private void ForceGCCollect()
        {
            StartCoroutine(PerformAggressiveCleanup());
        }

        [Button("Reset Performance Counters")]
        private void ResetPerformanceCounters()
        {
            CleanupRecorders();
            InitializePerformanceMonitoring();
            consecutiveCleanupAttempts = 0;
            lastCleanupTime = -CLEANUP_COOLDOWN;
            Debug.Log("Performance Counters Reset");
        }
        #endregion
    }
} 