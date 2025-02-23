using UnityEngine;
using System;
using Unity.Profiling;
using UnityEngine.SceneManagement;
using NaughtyAttributes;
using System.Collections;
using CZ.Core.Pooling;
using CZ.Core.Configuration;
using CZ.Core.Interfaces;
using CZ.Core.Logging;

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
            
            // Ensure configuration exists before initialization
            EnsureMemoryConfiguration();
            InitializeManager();
        }
        #endregion

        #region Properties and Fields
        [SerializeField] private MemoryConfiguration memoryConfig;
        private const string MEMORY_CONFIG_PATH = "Configuration/MemoryConfiguration";

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

        private bool isMonitoring = false;
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
                CZLogger.LogError("[GameManager] Failed to initialize performance monitoring. Memory management disabled.", LogCategory.System);
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
                    CZLogger.LogError("[GameManager] Failed to initialize draw calls recorder", LogCategory.Performance);
                    return false;
                }
                
                // Initialize memory monitoring
                if (!InitializeMemoryMonitoring())
                {
                    CZLogger.LogError("[GameManager] Failed to initialize memory monitoring", LogCategory.Performance);
                    CleanupRecorders();
                    return false;
                }

                // Initialize GC monitoring
                if (!InitializeGCMonitoring())
                {
                    CZLogger.LogError("[GameManager] Failed to initialize GC monitoring", LogCategory.Performance);
                    CleanupRecorders();
                    return false;
                }

                // Set initial memory baseline
                if (memoryRecorder.Valid && memoryRecorder.CurrentValue > 0)
                {
                    float initialMemory = ConvertToMB(memoryRecorder.CurrentValue);
                    CZLogger.LogInfo($"[GameManager] Initial memory reading: {initialMemory:F2}MB", LogCategory.Performance);
                    startupMemoryBaseline = initialMemory;
                    memoryBaseline = initialMemory;
                    CZLogger.LogInfo($"[GameManager] Setting initial memory baseline to: {memoryBaseline:F2}MB", LogCategory.Performance);
                    
                    // Perform initial cleanup if needed
                    if (initialMemory > memoryConfig.BaseThreshold)
                    {
                        CZLogger.LogInfo("[GameManager] Performing initial memory cleanup...", LogCategory.Performance);
                        StartCoroutine(PerformInitialCleanup(memoryConfig.BaseThreshold));
                    }
                }
                else
                {
                    CZLogger.LogError("[GameManager] Failed to get initial memory reading", LogCategory.Performance);
                    CleanupRecorders();
                    return false;
                }

                CZLogger.LogInfo("[GameManager] Performance monitoring initialized successfully", LogCategory.Performance);
                return true;
            }
            catch (Exception e)
            {
                CZLogger.LogError($"[GameManager] Failed to initialize performance monitoring: {e.Message}", LogCategory.Performance);
                CleanupRecorders();
                return false;
            }
        }

        private bool InitializeMemoryMonitoring()
        {
            // Ensure configuration exists
            EnsureMemoryConfiguration();
            
            int retryAttempts = 0;
            const int MAX_RETRY_ATTEMPTS = 3;
            
            while (retryAttempts < MAX_RETRY_ATTEMPTS)
            {
                try
                {
                    // First, try to get system memory
                    if (systemMemoryRecorder.Equals(default(ProfilerRecorder)) || !systemMemoryRecorder.Valid)
                    {
                        systemMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Memory");
                    }

                    if (systemMemoryRecorder.Valid)
                    {
                        systemMemoryTotal = ConvertToMB(systemMemoryRecorder.CurrentValue);
                        if (systemMemoryTotal > 0)
                        {
                            // Calculate dynamic thresholds
                            float baseValue = systemMemoryTotal / 8f;
                            memoryConfig.BaseThreshold = baseValue;
                            memoryConfig.WarningThreshold = baseValue * 1.5f;
                            memoryConfig.CriticalThreshold = baseValue * 1.75f;
                            memoryConfig.EmergencyThreshold = baseValue * 2.0f;
                            
                            CZLogger.LogInfo($"[GameManager] Dynamic thresholds calculated - Base: {memoryConfig.BaseThreshold:F2}MB, Warning: {memoryConfig.WarningThreshold:F2}MB, Critical: {memoryConfig.CriticalThreshold:F2}MB, Emergency: {memoryConfig.EmergencyThreshold:F2}MB", LogCategory.Performance);
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

                    // Initialize memory monitoring with multiple fallback counters
                    string[] memoryCounters = new string[] 
                    {
                        "Total Used Memory",
                        "System Memory",
                        "Total System Memory",
                        "Total Reserved Memory"
                    };
                    
                    bool memoryRecorderInitialized = false;
                    
                    foreach (string counterName in memoryCounters)
                    {
                        try
                        {
                            if (!memoryRecorder.Equals(default(ProfilerRecorder)) && memoryRecorder.Valid)
                            {
                                memoryRecorder.Dispose();
                            }
                            
                            memoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, counterName, 15);
                            if (memoryRecorder.Valid)
                            {
                                float testValue = ConvertToMB(memoryRecorder.CurrentValue);
                                if (testValue > 0)
                                {
                                    CZLogger.LogInfo($"[GameManager] Successfully initialized memory recorder with counter: {counterName} (Current: {testValue:F2}MB)", LogCategory.Performance);
                                    currentMemoryUsageMB = testValue;
                                    memoryRecorderInitialized = true;
                                    break;
                                }
                                memoryRecorder.Dispose();
                            }
                        }
                        catch (Exception e)
                        {
                            CZLogger.LogWarning($"[GameManager] Failed to initialize counter '{counterName}': {e.Message}", LogCategory.Performance);
                            if (!memoryRecorder.Equals(default(ProfilerRecorder)))
                            {
                                memoryRecorder.Dispose();
                            }
                        }
                    }
                    
                    if (memoryRecorderInitialized)
                    {
                        return true;
                    }
                    
                    retryAttempts++;
                    if (retryAttempts < MAX_RETRY_ATTEMPTS)
                    {
                        CZLogger.LogWarning($"[GameManager] Memory monitoring initialization attempt {retryAttempts} failed. Retrying...", LogCategory.Performance);
                        System.Threading.Thread.Sleep(100); // Brief pause before retry
                    }
                }
                catch (Exception e)
                {
                    CZLogger.LogError($"[GameManager] Error during memory monitoring initialization: {e.Message}", LogCategory.Performance);
                    retryAttempts++;
                    if (retryAttempts < MAX_RETRY_ATTEMPTS)
                    {
                        System.Threading.Thread.Sleep(100);
                    }
                }
            }
            
            CZLogger.LogError("[GameManager] Failed to initialize memory monitoring after multiple attempts", LogCategory.Performance);
            return false;
        }

        private void UseDefaultThresholds()
        {
            CZLogger.LogWarning("[GameManager] Using fallback memory thresholds", LogCategory.Performance);
            
            // Ensure we have a valid configuration
            EnsureMemoryConfiguration();
            
            // Calculate thresholds based on system memory or use project defaults
            float systemMemory = (!systemMemoryRecorder.Equals(default(ProfilerRecorder)) && systemMemoryRecorder.Valid) ? 
                ConvertToMB(systemMemoryRecorder.LastValue) : 2048f; // Default to 2GB if can't detect
            
            // Use at least 1GB base, or 1/4 of system memory if higher
            float baseValue = Mathf.Max(1024f, systemMemory / 4f);
            
            memoryConfig.BaseThreshold = baseValue;
            memoryConfig.WarningThreshold = Mathf.Min(baseValue * 1.5f, 1536f);  // Cap at 1.5GB
            memoryConfig.CriticalThreshold = Mathf.Min(baseValue * 1.75f, 1792f); // Cap at 1.75GB
            memoryConfig.EmergencyThreshold = Mathf.Min(baseValue * 2f, 2048f);   // Cap at 2GB
            
            // Set pool thresholds as percentage of base memory per infrastructure.txt
            memoryConfig.PoolWarningThreshold = baseValue * 0.25f;  // 25% of base
            memoryConfig.PoolCriticalThreshold = baseValue * 0.35f;  // 35% of base
            memoryConfig.PoolEmergencyThreshold = baseValue * 0.45f; // 45% of base
            
            CZLogger.LogInfo($"[GameManager] System-aware thresholds set:\n" +
                     $"System Memory: {systemMemory:F2}MB\n" +
                     $"Base: {baseValue:F2}MB\n" +
                     $"Warning: {memoryConfig.WarningThreshold:F2}MB\n" +
                     $"Critical: {memoryConfig.CriticalThreshold:F2}MB\n" +
                     $"Emergency: {memoryConfig.EmergencyThreshold:F2}MB\n" +
                     $"Pool Warning: {memoryConfig.PoolWarningThreshold:F2}MB\n" +
                     $"Pool Critical: {memoryConfig.PoolCriticalThreshold:F2}MB\n" +
                     $"Pool Emergency: {memoryConfig.PoolEmergencyThreshold:F2}MB", LogCategory.Performance);
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
                            CZLogger.LogInfo($"[GameManager] Successfully initialized GC recorder with counter: {counterName} (Current: {testValue:F2}MB)", LogCategory.Performance);
                            return true;
                        }
                        gcMemoryRecorder.Dispose();
                    }
                }
                catch (Exception e)
                {
                    CZLogger.LogWarning($"[GameManager] Failed to initialize GC counter '{counterName}': {e.Message}", LogCategory.Performance);
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
            CZLogger.LogInfo("[GameManager] Performing initial memory cleanup...", LogCategory.Performance);
            
            // Wait for initial load to settle
            yield return new WaitForSeconds(1f);
            
            // Verify recorder is still valid
            if (!memoryRecorder.Valid || !gcMemoryRecorder.Valid)
            {
                CZLogger.LogError("[GameManager] Memory recorders not valid during initial cleanup", LogCategory.Performance);
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
                
                CZLogger.LogInfo($"[GameManager] Initial cleanup completed. Memory: {newMemoryUsage:F2}MB (Delta: {delta:F2}MB)", LogCategory.Performance);
                
                // Calculate relative thresholds based on adjusted baseline
                float relativeWarning = (memoryConfig.WarningThreshold / memoryConfig.BaseThreshold) * adjustedBaseline;
                float relativeCritical = (memoryConfig.CriticalThreshold / memoryConfig.BaseThreshold) * adjustedBaseline;
                float relativeEmergency = (memoryConfig.EmergencyThreshold / memoryConfig.BaseThreshold) * adjustedBaseline;
                
                CZLogger.LogInfo($"[GameManager] Adjusted thresholds - Warning: {relativeWarning:F2}MB, Critical: {relativeCritical:F2}MB, Emergency: {relativeEmergency:F2}MB", LogCategory.Performance);
                
                // Update baseline if we achieved reduction
                if (newMemoryUsage < memoryBaseline)
                {
                    memoryBaseline = newMemoryUsage;
                    CZLogger.LogInfo($"[GameManager] Memory baseline adjusted after cleanup: {memoryBaseline:F2}MB", LogCategory.Performance);
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
                #if UNITY_EDITOR
                if (!UnityEditor.EditorApplication.isPlaying)
                {
                    Debug.Log("[GameManager] Starting game in editor mode...");
                    UnityEditor.EditorApplication.isPlaying = true;
                    return;
                }
                #endif

                CZLogger.LogInfo("[GameManager] Initializing game start sequence...", LogCategory.System);

                // Verify memory state before starting
                if (isInEmergencyMode)
                {
                    CZLogger.LogError("[GameManager] Cannot start game while in emergency memory state", LogCategory.System);
                    return;
                }

                // Reset performance counters for new game session
                ResetPerformanceCounters();

                // Validate all game systems
                if (!ValidateGameSystems())
                {
                    CZLogger.LogError("[GameManager] Game system validation failed", LogCategory.System);
                    return;
                }

                // Direct state transition for editor button
                SetGameState(GameState.Playing);
            }
        }

        private void SetGameState(GameState newState)
        {
            try
            {
                CZLogger.LogInfo($"[GameManager] Transitioning game state from {CurrentGameState} to {newState}", LogCategory.System);
                
                // Pre-transition setup
                switch (newState)
                {
                    case GameState.Playing:
                        // Ensure input system is ready
                        UnityEngine.InputSystem.InputSystem.Update();
                        
                        // Start monitoring if transitioning to Playing
                        if (!isMonitoring)
                        {
                            isMonitoring = true;
                            StartCoroutine(GameStateMonitor());
                        }
                        break;
                        
                    case GameState.Paused:
                        Time.timeScale = 0f;
                        break;
                        
                    case GameState.MainMenu:
                    case GameState.GameOver:
                        Time.timeScale = 1f;
                        isMonitoring = false;
                        break;
                }

                // Update state
                CurrentGameState = newState;
                
                // Post-transition setup
                if (newState == GameState.Playing)
                {
                    CZLogger.LogInfo("[GameManager] Game started successfully - All systems initialized and enabled", LogCategory.System);
                }
            }
            catch (System.Exception e)
            {
                CZLogger.LogError($"[GameManager] Error during state transition: {e.Message}", LogCategory.System);
                CurrentGameState = GameState.MainMenu;
                Time.timeScale = 1f;
            }
        }

        private IEnumerator GameStateMonitor()
        {
            CZLogger.LogInfo("[GameManager] Starting game state monitoring", LogCategory.System);
            
            while (isMonitoring && CurrentGameState == GameState.Playing)
            {
                // Monitor game state
                if (!ValidateGameSystems())
                {
                    CZLogger.LogError("[GameManager] Critical system failure detected during gameplay", LogCategory.System);
                    SetGameState(GameState.MainMenu);
                    yield break;
                }
                
                yield return new WaitForSeconds(1f);
            }
            
            CZLogger.LogInfo("Game state monitoring ended", LogCategory.System);
        }

        private bool ValidateGameSystems()
        {
            try
            {
                CZLogger.LogInfo("[GameManager] Validating game systems...", LogCategory.System);

                // Validate input system first
                UnityEngine.InputSystem.InputSystem.Update();
                if (UnityEngine.InputSystem.InputSystem.devices.Count == 0)
                {
                    CZLogger.LogWarning("[GameManager] No input devices found. Attempting to initialize input system...", LogCategory.System);
                    
                    // Force multiple updates to ensure initialization
                    for (int i = 0; i < 3; i++)
                    {
                        UnityEngine.InputSystem.InputSystem.Update();
                        System.Threading.Thread.Sleep(10);
                    }
                    
                    if (UnityEngine.InputSystem.InputSystem.devices.Count == 0)
                    {
                        CZLogger.LogError("[GameManager] Input system failed to initialize after multiple attempts", LogCategory.System);
                        return false;
                    }
                }

                // Validate essential systems
                if (PoolManager.Instance == null)
                {
                    CZLogger.LogError("[GameManager] PoolManager not initialized", LogCategory.System);
                    return false;
                }

                CZLogger.LogInfo("[GameManager] All game systems validated successfully", LogCategory.System);
                return true;
            }
            catch (System.Exception e)
            {
                CZLogger.LogError($"[GameManager] Error during system validation: {e.Message}", LogCategory.System);
                return false;
            }
        }

        [Button("Pause Game")]
        public void PauseGame()
        {
            if (CurrentGameState == GameState.Playing)
            {
                CurrentGameState = GameState.Paused;
                Time.timeScale = 0f;
                CZLogger.LogInfo("Game Paused", LogCategory.System);
            }
        }

        [Button("Resume Game")]
        public void ResumeGame()
        {
            if (CurrentGameState == GameState.Paused)
            {
                CurrentGameState = GameState.Playing;
                Time.timeScale = 1f;
                CZLogger.LogInfo("Game Resumed", LogCategory.System);
            }
        }

        [Button("End Game")]
        public void EndGame()
        {
            if (CurrentGameState == GameState.Playing)
            {
                CurrentGameState = GameState.GameOver;
                CZLogger.LogInfo("Game Ended", LogCategory.System);
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
                    CZLogger.LogWarning($"Draw calls exceeded limit: {currentDrawCalls}/100", LogCategory.Performance);
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
                        CZLogger.LogInfo($"Preemptive cleanup at {currentMemoryUsageMB:F2}MB (Delta: {memoryDelta:F2}MB, Relative: {relativeDelta:P2})", LogCategory.Performance);
                        StartCoroutine(PerformPreemptiveCleanup());
                    }
                }
                // Critical cleanup
                else if (currentMemoryUsageMB > memoryConfig.CriticalThreshold && currentMemoryUsageMB <= memoryConfig.EmergencyThreshold)
                {
                    if (Time.time - lastCleanupTime >= CLEANUP_COOLDOWN/2)
                    {
                        CZLogger.LogWarning($"Critical cleanup at {currentMemoryUsageMB:F2}MB (Delta: {memoryDelta:F2}MB, Relative: {relativeDelta:P2})", LogCategory.Performance);
                        StartCoroutine(PerformAggressiveCleanup());
                    }
                }
                // Emergency cleanup
                else if (currentMemoryUsageMB > memoryConfig.EmergencyThreshold && !emergencyCleanupRequired)
                {
                    CZLogger.LogError($"Emergency cleanup required at {currentMemoryUsageMB:F2}MB (Delta: {memoryDelta:F2}MB, Relative: {relativeDelta:P2})", LogCategory.Performance);
                    emergencyCleanupRequired = true;
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
            CZLogger.LogInfo($"Preemptive cleanup completed. Change: {delta:F2}MB", LogCategory.Performance);
            
            // Update baseline if significantly lower
            if (newMemoryUsage < memoryBaseline * 0.9f)
            {
                memoryBaseline = newMemoryUsage;
                CZLogger.LogInfo($"Memory baseline adjusted to: {memoryBaseline:F2}MB", LogCategory.Performance);
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
            CZLogger.LogWarning($"Aggressive cleanup initiated. Memory: {initialMemory:F2}MB - Attempt {consecutiveCleanupAttempts}/{MAX_CLEANUP_ATTEMPTS}", LogCategory.Performance);
            
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
            CZLogger.LogInfo($"Aggressive cleanup completed. Memory reduced from {initialMemory:F2}MB to {newMemoryUsage:F2}MB", LogCategory.Performance);
            
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
            CZLogger.LogError($"EMERGENCY CLEANUP - Memory usage critical: {currentMemoryUsageMB:F2}MB", LogCategory.Performance);
            
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
                CZLogger.LogError($"CRITICAL: Emergency cleanup failed to reduce memory usage. Restarting scene...", LogCategory.Performance);
                RestartCurrentScene();
            }
            else
            {
                CZLogger.LogInfo($"Emergency cleanup successful. Memory reduced from {currentMemoryUsageMB:F2}MB to {newMemoryUsage:F2}MB", LogCategory.Performance);
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
                    CZLogger.LogError("[GameManager] Failed to initialize performance monitoring in OnEnable. Memory management will be disabled.", LogCategory.System);
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
                if (!drawCallsRecorder.Equals(default(ProfilerRecorder)) && drawCallsRecorder.Valid)
                {
                    drawCallsRecorder.Dispose();
                }
                drawCallsRecorder = default;
                
                if (!memoryRecorder.Equals(default(ProfilerRecorder)) && memoryRecorder.Valid)
                {
                    memoryRecorder.Dispose();
                }
                memoryRecorder = default;
                
                if (!gcMemoryRecorder.Equals(default(ProfilerRecorder)) && gcMemoryRecorder.Valid)
                {
                    gcMemoryRecorder.Dispose();
                }
                gcMemoryRecorder = default;
                
                if (!systemMemoryRecorder.Equals(default(ProfilerRecorder)) && systemMemoryRecorder.Valid)
                {
                    systemMemoryRecorder.Dispose();
                }
                systemMemoryRecorder = default;
                
                // Force a GC collection after disposing recorders
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception e)
            {
                CZLogger.LogError($"Error during recorder cleanup: {e.Message}", LogCategory.System);
            }
        }
        #endregion

        #region Scene Management
        public void LoadScene(string sceneName)
        {
            SceneManager.LoadSceneAsync(sceneName).completed += (asyncOperation) =>
            {
                CZLogger.LogInfo($"Scene {sceneName} loaded successfully", LogCategory.System);
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
            CZLogger.LogInfo("Performance Counters Reset", LogCategory.Performance);
        }
        #endregion

        private void EnsureMemoryConfiguration()
        {
            if (memoryConfig == null)
            {
                // Try to load from Resources first
                memoryConfig = Resources.Load<MemoryConfiguration>(MEMORY_CONFIG_PATH);
                
                if (memoryConfig == null)
                {
                    CZLogger.LogWarning("[GameManager] MemoryConfiguration not found in Resources. Creating runtime instance.", LogCategory.System);
                    memoryConfig = ScriptableObject.CreateInstance<MemoryConfiguration>();
                    
                    // Set values according to project requirements
                    float baseMemory = 1024f; // 1GB base
                    memoryConfig.BaseThreshold = baseMemory;
                    memoryConfig.WarningThreshold = 1536f;  // 1.5GB
                    memoryConfig.CriticalThreshold = 1792f; // 1.75GB
                    memoryConfig.EmergencyThreshold = 2048f; // 2GB
                    
                    // Pool thresholds as percentage of base memory
                    memoryConfig.PoolWarningThreshold = baseMemory * 0.25f;  // 25% of base
                    memoryConfig.PoolCriticalThreshold = baseMemory * 0.35f;  // 35% of base
                    memoryConfig.PoolEmergencyThreshold = baseMemory * 0.45f; // 45% of base
                    
                    CZLogger.LogInfo($"[GameManager] Created runtime MemoryConfiguration with project-specified thresholds:\nBase: {baseMemory:F2}MB\nWarning: {memoryConfig.WarningThreshold:F2}MB\nCritical: {memoryConfig.CriticalThreshold:F2}MB\nEmergency: {memoryConfig.EmergencyThreshold:F2}MB", LogCategory.System);
                }
            }
        }
    }
} 