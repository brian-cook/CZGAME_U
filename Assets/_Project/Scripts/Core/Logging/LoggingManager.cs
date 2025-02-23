using UnityEngine;
using System;
using System.Collections.Generic;

namespace CZ.Core.Logging
{
    public enum LogCategory
    {
        System,
        Performance,
        Gameplay,
        UI,
        Resource,
        Enemy,
        Player,
        Pool,
        Debug
    }

    public enum LogPriority
    {
        Critical,   // Always logged
        Warning,    // Logged in development and specific builds
        Info,       // Logged in development
        Debug      // Only logged when explicitly enabled
    }

    public class LoggingManager : MonoBehaviour
    {
        private static LoggingManager instance;
        private static bool isQuitting;
        
        public static LoggingManager Instance
        {
            get
            {
                if (isQuitting)
                {
                    Debug.LogWarning("[LoggingManager] Instance requested during application quit, returning null");
                    return null;
                }

                if (instance == null && !isQuitting)
                {
                    #if UNITY_EDITOR
                    if (!UnityEditor.EditorApplication.isPlaying)
                    {
                        // In editor mode, find or create a temporary instance
                        instance = FindAnyObjectByType<LoggingManager>();
                        if (instance == null)
                        {
                            var go = new GameObject("LoggingManager_EditorOnly");
                            instance = go.AddComponent<LoggingManager>();
                            instance.InitializeLoggingSettings();
                        }
                        return instance;
                    }
                    #endif

                    // In play mode, create a persistent instance
                    var gameObject = new GameObject("LoggingManager");
                    instance = gameObject.AddComponent<LoggingManager>();
                    DontDestroyOnLoad(gameObject);
                }
                return instance;
            }
        }

        private Dictionary<LogCategory, bool> categoryEnabled = new Dictionary<LogCategory, bool>();
        private Dictionary<LogPriority, bool> priorityEnabled = new Dictionary<LogPriority, bool>();

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            isQuitting = false;
            InitializeLoggingSettings();
            
            #if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
            #else
            DontDestroyOnLoad(gameObject);
            #endif
        }

        private void InitializeLoggingSettings()
        {
            // Initialize all categories as enabled in development, disabled in release
            foreach (LogCategory category in Enum.GetValues(typeof(LogCategory)))
            {
                categoryEnabled[category] = Debug.isDebugBuild;
            }

            // Initialize priorities
            priorityEnabled[LogPriority.Critical] = true; // Always enabled
            priorityEnabled[LogPriority.Warning] = Debug.isDebugBuild;
            priorityEnabled[LogPriority.Info] = Debug.isDebugBuild;
            priorityEnabled[LogPriority.Debug] = false; // Explicitly enabled only
        }

        public void SetCategoryEnabled(LogCategory category, bool enabled)
        {
            categoryEnabled[category] = enabled;
        }

        public void SetPriorityEnabled(LogPriority priority, bool enabled)
        {
            priorityEnabled[priority] = enabled;
        }

        public bool ShouldLog(LogCategory category, LogPriority priority)
        {
            // Critical logs are always logged regardless of category
            if (priority == LogPriority.Critical) return true;

            // Check if both category and priority are enabled
            return categoryEnabled.TryGetValue(category, out bool categoryIsEnabled) &&
                   priorityEnabled.TryGetValue(priority, out bool priorityIsEnabled) &&
                   categoryIsEnabled && priorityIsEnabled;
        }

        public void Log(string message, LogCategory category, LogPriority priority = LogPriority.Info)
        {
            if (!ShouldLog(category, priority)) return;

            string formattedMessage = $"[{category}] {message}";
            
            switch (priority)
            {
                case LogPriority.Critical:
                    Debug.LogError(formattedMessage);
                    break;
                case LogPriority.Warning:
                    Debug.LogWarning(formattedMessage);
                    break;
                default:
                    Debug.Log(formattedMessage);
                    break;
            }
        }

        public void LogError(string message, LogCategory category)
        {
            Log(message, category, LogPriority.Critical);
        }

        public void LogWarning(string message, LogCategory category)
        {
            Log(message, category, LogPriority.Warning);
        }

        public void LogInfo(string message, LogCategory category)
        {
            Log(message, category, LogPriority.Info);
        }

        public void LogDebug(string message, LogCategory category)
        {
            Log(message, category, LogPriority.Debug);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            isQuitting = true;
            if (instance == this)
            {
                // Clear instance and dictionaries
                categoryEnabled.Clear();
                priorityEnabled.Clear();
                instance = null;
            }
        }

        private void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        }

        private void HandleSceneUnloaded(UnityEngine.SceneManagement.Scene scene)
        {
            // Reinitialize logging settings on scene unload to ensure proper state
            if (instance == this && !isQuitting)
            {
                InitializeLoggingSettings();
            }
        }
    }
} 