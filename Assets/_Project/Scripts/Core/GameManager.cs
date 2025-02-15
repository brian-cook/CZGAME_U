using UnityEngine;
using System;
using Unity.Profiling;
using UnityEngine.SceneManagement;

namespace CZ.Core
{
    /// <summary>
    /// Core GameManager class handling game state, scene management, and performance monitoring.
    /// Follows Unity 6.0 best practices and performance guidelines.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        #region Singleton Setup
        public static GameManager Instance { get; private set; }
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeManager();
        }
        #endregion

        #region Properties and Fields
        // Performance monitoring
        private ProfilerRecorder drawCallsRecorder;
        private ProfilerRecorder memoryRecorder;
        
        // Game state
        public enum GameState
        {
            MainMenu,
            Playing,
            Paused,
            GameOver
        }
        
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
            // Initialize performance monitoring
            drawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            memoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
            
            // Set initial game state
            CurrentGameState = GameState.MainMenu;
            
            // Additional initialization
            Application.targetFrameRate = 60; // Target 60 FPS as per requirements
        }
        #endregion

        #region Game State Management
        public void StartGame()
        {
            if (CurrentGameState != GameState.Playing)
            {
                CurrentGameState = GameState.Playing;
                // Additional game start logic
            }
        }

        public void PauseGame()
        {
            if (CurrentGameState == GameState.Playing)
            {
                CurrentGameState = GameState.Paused;
                Time.timeScale = 0f;
            }
        }

        public void ResumeGame()
        {
            if (CurrentGameState == GameState.Paused)
            {
                CurrentGameState = GameState.Playing;
                Time.timeScale = 1f;
            }
        }

        public void EndGame()
        {
            if (CurrentGameState == GameState.Playing)
            {
                CurrentGameState = GameState.GameOver;
                // Additional game end logic
            }
        }
        #endregion

        #region Performance Monitoring
        private float lastMemoryCheck;
        private const float MEMORY_CHECK_INTERVAL = 1f; // Check every second instead of every frame

        private void Update()
        {
            // Only check memory periodically
            if (Time.time - lastMemoryCheck >= MEMORY_CHECK_INTERVAL)
            {
                MonitorPerformance();
                lastMemoryCheck = Time.time;
            }
        }

        private void MonitorPerformance()
        {
            // Monitor draw calls (max 100 as per requirements)
            if (drawCallsRecorder.Valid && drawCallsRecorder.LastValue > 100)
            {
                Debug.LogWarning($"Draw calls exceeded limit: {drawCallsRecorder.LastValue}");
            }

            // Monitor memory usage (max 1024MB as per requirements)
            if (memoryRecorder.Valid)
            {
                float memoryUsageMB = memoryRecorder.LastValue / (1024f * 1024f);
                if (memoryUsageMB > 1024f)
                {
                    Debug.LogWarning($"Memory usage exceeded limit: {memoryUsageMB:F2}MB");
                    
                    #if UNITY_EDITOR
                    // Force cleanup in editor
                    System.GC.Collect();
                    Resources.UnloadUnusedAssets();
                    #endif

                    // Always try to reduce memory pressure
                    System.GC.Collect(0, System.GCCollectionMode.Optimized);
                }
            }
        }

        private void OnDestroy()
        {
            CleanupRecorders();
        }

        private void OnDisable()
        {
            CleanupRecorders();
        }

        private void CleanupRecorders()
        {
            if (drawCallsRecorder.Valid)
            {
                drawCallsRecorder.Dispose();
            }
            
            if (memoryRecorder.Valid)
            {
                memoryRecorder.Dispose();
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
    }
} 