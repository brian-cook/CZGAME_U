using UnityEngine;
using System;
using Unity.Profiling;
using UnityEngine.SceneManagement;
using NaughtyAttributes;
using System.Collections;
using CZ.Core.Pooling;
using CZ.Core.Configuration;

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
        [SerializeField] private MemoryConfiguration memoryConfig;

        // Performance monitoring
        private ProfilerRecorder drawCallsRecorder;
        private ProfilerRecorder memoryRecorder;
        private ProfilerRecorder gcMemoryRecorder;
        private ProfilerRecorder systemMemoryRecorder;
        private bool isCleaningUp;
        private int consecutiveCleanupAttempts;
        private const int MAX_CLEANUP_ATTEMPTS = 3;
        private const float CLEANUP_COOLDOWN = 5f;
        private float lastCleanupTime;
        private bool emergencyCleanupRequired;
        private float lastMemoryCheck;
        private const float MEMORY_CHECK_INTERVAL = 0.5f;
        private float startupMemoryBaseline;
        private float systemMemoryTotal;

        // Remove individual threshold fields as they're now in MemoryConfiguration
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

        // Remove public threshold properties as they're now in MemoryConfiguration

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
                
                // Initialize memory monitoring
                if (!InitializeMemoryMonitoring())
                {
                    Debug.LogError("[GameManager] Failed to initialize memory monitoring");
                    CleanupRecorders();
                    return false;
                }

                // Initialize GC monitoring
                if (!InitializeGCMonitoring())
                {
                    Debug.LogError("[GameManager] Failed to initialize GC monitoring");
                    CleanupRecorders();
                    return false;
                }

                // Set initial memory baseline
                if (memoryRecorder.Valid && memoryRecorder.CurrentValue > 0)
                {
                    float initialMemory = ConvertToMB(memoryRecorder.CurrentValue);
                    Debug.Log($"[GameManager] Initial memory reading: {initialMemory:F2}MB");
                    startupMemoryBaseline = initialMemory;
                    memoryBaseline = initialMemory;
                    Debug.Log($"[GameManager] Setting initial memory baseline to: {memoryBaseline:F2}MB");
                    
                    // Perform initial cleanup if needed
                    if (initialMemory > memoryConfig.BaseThreshold)
                    {
                        Debug.Log("[GameManager] Performing initial memory cleanup...");
                        StartCoroutine(PerformInitialCleanup(memoryConfig.BaseThreshold));
                    }
                }
                else
                {
                    Debug.LogError("[GameManager] Failed to get initial memory reading");
                    CleanupRecorders();
                    return false;
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

        private bool InitializeMemoryMonitoring()
        {
            // First, try to get system memory
            try
            {
                systemMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Memory");
                if (systemMemoryRecorder.Valid)
                {
                    systemMemoryTotal = ConvertToMB(systemMemoryRecorder.CurrentValue);
                    if (systemMemoryTotal > 0)
                    {
                        // Calculate dynamic thresholds
                        memoryConfig.BaseThreshold = systemMemoryTotal / 8f;
                        memoryConfig.WarningThreshold = memoryConfig.BaseThreshold * 1.5f;
                        memoryConfig.CriticalThreshold = memoryConfig.BaseThreshold * 1.75f;
                        memoryConfig.EmergencyThreshold = memoryConfig.BaseThreshold * 2.0f;
                        
                        Debug.Log($"[GameManager] Dynamic thresholds calculated - Base: {memoryConfig.BaseThreshold:F2}MB, Warning: {memoryConfig.WarningThreshold:F2}MB, Critical: {memoryConfig.CriticalThreshold:F2}MB, Emergency: {memoryConfig.EmergencyThreshold:F2}MB");
                    }
                    else
                    {
                        UseDefaultThresholds();
                    }
                }
                else
                {
                    UseDefaultThresholds();
                }
            }
            catch
            {
                UseDefaultThresholds();
            }

            // Initialize memory monitoring
            string[] memoryCounters = new string[] 
            {
                "Total Used Memory",
                "System Memory",
                "Total System Memory",
                "Total Reserved Memory"
            };
            
            foreach (string counterName in memoryCounters)
            {
                try
                {
                    memoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, counterName, 15);
                    if (memoryRecorder.Valid)
                    {
                        float testValue = ConvertToMB(memoryRecorder.CurrentValue);
                        if (testValue > 0)
                        {
                            Debug.Log($"[GameManager] Successfully initialized memory recorder with counter: {counterName} (Current: {testValue:F2}MB)");
                            currentMemoryUsageMB = testValue;
                            return true;
                        }
                        memoryRecorder.Dispose();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[GameManager] Failed to initialize counter '{counterName}': {e.Message}");
                    if (!memoryRecorder.Equals(default(ProfilerRecorder)))
                    {
                        memoryRecorder.Dispose();
                    }
                }
            }
            
            return false;
        }

        private void UseDefaultThresholds()
        {
            Debug.LogWarning("[GameManager] Using fallback memory thresholds");
            memoryConfig.WarningThreshold = memoryConfig.BaseThreshold * 1.5f;
            memoryConfig.CriticalThreshold = memoryConfig.BaseThreshold * 1.75f;
            memoryConfig.EmergencyThreshold = memoryConfig.BaseThreshold * 2.0f;
            memoryConfig.BaseThreshold = memoryConfig.WarningThreshold / 1.5f;
        }

        private bool InitializeGCMonitoring()
        {
            string[] gcCounters = new string[] 
            {
                "GC Used Memory",     // Primary counter
                "GC Memory",          // Fallback 1
                "GC Reserved Memory", // Fallback 2
                "GC Heap Size"       // Fallback 3
            };
            
            foreach (string counterName in gcCounters)
            {
                try
                {
                    gcMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, counterName, 15);
                    if (gcMemoryRecorder.Valid)
                    {
                        float testValue = ConvertToMB(gcMemoryRecorder.CurrentValue);
                        if (testValue > 0)
                        {
                            Debug.Log($"[GameManager] Successfully initialized GC recorder with counter: {counterName} (Current: {testValue:F2}MB)");
                            return true;
                        }
                        gcMemoryRecorder.Dispose();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[GameManager] Failed to initialize GC counter '{counterName}': {e.Message}");
                    if (!gcMemoryRecorder.Equals(default(ProfilerRecorder)))
                    {
                        gcMemoryRecorder.Dispose();
                    }
                }
            }
            
            return false;
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
                float relativeWarning = (memoryConfig.WarningThreshold / memoryConfig.BaseThreshold) * adjustedBaseline;
                float relativeCritical = (memoryConfig.CriticalThreshold / memoryConfig.BaseThreshold) * adjustedBaseline;
                float relativeEmergency = (memoryConfig.EmergencyThreshold / memoryConfig.BaseThreshold) * adjustedBaseline;
                
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
                
                // Calculate memory delta relative to baseline
                memoryDelta = currentMemoryUsageMB - memoryBaseline;
                float relativeDelta = memoryDelta / memoryBaseline;
                
                // Preemptive cleanup at warning threshold
                if (currentMemoryUsageMB > memoryConfig.WarningThreshold && currentMemoryUsageMB <= memoryConfig.CriticalThreshold)
                {
                    if (Time.time - lastCleanupTime >= CLEANUP_COOLDOWN)
                    {
                        Debug.Log($"Preemptive cleanup at {currentMemoryUsageMB:F2}MB (Delta: {memoryDelta:F2}MB, Relative: {relativeDelta:P2})");
                        StartCoroutine(PerformPreemptiveCleanup());
                    }
                }
                // Critical cleanup
                else if (currentMemoryUsageMB > memoryConfig.CriticalThreshold && currentMemoryUsageMB <= memoryConfig.EmergencyThreshold)
                {
                    if (Time.time - lastCleanupTime >= CLEANUP_COOLDOWN/2)
                    {
                        Debug.LogWarning($"Critical cleanup at {currentMemoryUsageMB:F2}MB (Delta: {memoryDelta:F2}MB, Relative: {relativeDelta:P2})");
                        StartCoroutine(PerformAggressiveCleanup());
                    }
                }
                // Emergency cleanup
                else if (currentMemoryUsageMB > memoryConfig.EmergencyThreshold && !emergencyCleanupRequired)
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
            
            if (consecutiveCleanupAttempts >= MAX_CLEANUP_ATTEMPTS && newMemoryUsage > memoryConfig.CriticalThreshold)
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
            
            if (newMemoryUsage > memoryConfig.CriticalThreshold)
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