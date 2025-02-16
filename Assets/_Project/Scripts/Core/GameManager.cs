using UnityEngine;
using System;
using Unity.Profiling;
using UnityEngine.SceneManagement;
using NaughtyAttributes;
using System.Collections;

namespace CZ.Core
{
    /// <summary>
    /// Core GameManager class handling game state, scene management, and performance monitoring.
    /// Follows Unity 6.0 best practices and performance guidelines.
    /// </summary>
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
            InitializePerformanceMonitoring();
            StartCoroutine(CaptureMemoryBaseline());
            
            // Set initial game state
            CurrentGameState = GameState.MainMenu;
            
            // Additional initialization
            Application.targetFrameRate = 60; // Target 60 FPS as per requirements
            isCleaningUp = false;
            consecutiveCleanupAttempts = 0;
            lastCleanupTime = -CLEANUP_COOLDOWN; // Allow immediate first cleanup
        }

        private IEnumerator CaptureMemoryBaseline()
        {
            // Wait for initial load to settle
            yield return new WaitForSeconds(2f);
            
            if (memoryRecorder.Valid)
            {
                // More aggressive initial cleanup
                Resources.UnloadUnusedAssets();
                
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.UnloadUnusedAssetsImmediate();
                #endif
                
                // Multiple GC passes to ensure clean baseline
                for (int i = 0; i < 3; i++)
                {
                    GC.Collect(i, GCCollectionMode.Forced, true, true);
                    yield return new WaitForSeconds(0.1f);
                }
                
                yield return new WaitForSeconds(0.5f);
                
                float currentMemory = memoryRecorder.LastValue / (1024f * 1024f);
                startupMemoryBaseline = currentMemory;
                memoryBaseline = currentMemory;
                Debug.Log($"[GameManager] Initial memory baseline captured: {memoryBaseline:F2}MB");

                if (memoryBaseline > BASELINE_THRESHOLD_MB)
                {
                    Debug.Log($"[GameManager] High initial memory usage detected: {memoryBaseline:F2}MB > {BASELINE_THRESHOLD_MB}MB");
                    yield return StartCoroutine(PerformAggressiveCleanup());
                    
                    // Update baseline after cleanup
                    currentMemory = memoryRecorder.LastValue / (1024f * 1024f);
                    if (currentMemory < memoryBaseline)
                    {
                        memoryBaseline = currentMemory;
                        Debug.Log($"[GameManager] Memory baseline adjusted after cleanup: {memoryBaseline:F2}MB");
                    }
                }
            }
        }

        private void InitializePerformanceMonitoring()
        {
            try
            {
                if (!drawCallsRecorder.Valid)
                {
                    drawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
                }
                
                if (!memoryRecorder.Valid)
                {
                    memoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
                }
                
                if (!gcMemoryRecorder.Valid)
                {
                    gcMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Reserved Memory");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to initialize performance monitoring: {e.Message}");
            }
        }
        #endregion

        #region Game State Management
        [Button("Start Game")]
        public void StartGame()
        {
            if (CurrentGameState != GameState.Playing)
            {
                CurrentGameState = GameState.Playing;
                Time.timeScale = 1f;
                Debug.Log("Game Started");
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
            if (Time.time < 5f) // Skip monitoring for first 5 seconds during initialization
            {
                return;
            }

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
                currentMemoryUsageMB = memoryRecorder.LastValue / (1024f * 1024f);
                memoryDelta = currentMemoryUsageMB - memoryBaseline;
                
                // Preemptive cleanup at warning threshold
                if (currentMemoryUsageMB > WARNING_THRESHOLD_MB && currentMemoryUsageMB <= CRITICAL_THRESHOLD_MB)
                {
                    if (Time.time - lastCleanupTime >= CLEANUP_COOLDOWN)
                    {
                        Debug.Log($"Preemptive cleanup at {currentMemoryUsageMB:F2}MB (Delta: {memoryDelta:F2}MB)");
                        StartCoroutine(PerformPreemptiveCleanup());
                    }
                }
                // Critical cleanup
                else if (currentMemoryUsageMB > CRITICAL_THRESHOLD_MB && currentMemoryUsageMB <= EMERGENCY_THRESHOLD_MB)
                {
                    if (Time.time - lastCleanupTime >= CLEANUP_COOLDOWN/2)
                    {
                        Debug.LogWarning($"Critical cleanup at {currentMemoryUsageMB:F2}MB (Delta: {memoryDelta:F2}MB)");
                        StartCoroutine(PerformAggressiveCleanup());
                    }
                }
                // Emergency cleanup
                else if (currentMemoryUsageMB > EMERGENCY_THRESHOLD_MB && !emergencyCleanupRequired)
                {
                    emergencyCleanupRequired = true;
                    Debug.LogError($"EMERGENCY: Memory usage critical at {currentMemoryUsageMB:F2}MB (Delta: {memoryDelta:F2}MB)");
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
            
            Debug.LogWarning($"Aggressive cleanup initiated. Memory: {currentMemoryUsageMB:F2}MB - Attempt {consecutiveCleanupAttempts}/{MAX_CLEANUP_ATTEMPTS}");
            
            // More aggressive cleanup sequence
            yield return Resources.UnloadUnusedAssets();
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.UnloadUnusedAssetsImmediate();
            #endif
            
            // Force full GC collection
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            yield return new WaitForSeconds(0.1f);
            
            // Second pass with more aggressive settings
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            yield return new WaitForSeconds(0.1f);
            
            // Final pass targeting large objects
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
            
            float newMemoryUsage = memoryRecorder.Valid ? memoryRecorder.LastValue / (1024f * 1024f) : 0f;
            Debug.Log($"Aggressive cleanup completed. Memory reduced from {currentMemoryUsageMB:F2}MB to {newMemoryUsage:F2}MB");
            
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
            InitializePerformanceMonitoring();
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
                
                if (memoryRecorder.Valid)
                {
                    memoryRecorder.Dispose();
                }
                
                if (gcMemoryRecorder.Valid)
                {
                    gcMemoryRecorder.Dispose();
                }
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