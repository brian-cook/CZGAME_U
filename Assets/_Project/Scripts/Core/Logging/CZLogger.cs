using UnityEngine;

namespace CZ.Core.Logging
{
    public static class CZLogger
    {
        public static void LogError(string message, LogCategory category)
        {
            var manager = LoggingManager.Instance;
            if (manager != null)
            {
                manager.LogError(message, category);
            }
            else
            {
                Debug.LogError($"[{category}] {message} (Fallback: LoggingManager not available)");
            }
        }

        public static void LogWarning(string message, LogCategory category)
        {
            var manager = LoggingManager.Instance;
            if (manager != null)
            {
                manager.LogWarning(message, category);
            }
            else
            {
                Debug.LogWarning($"[{category}] {message} (Fallback: LoggingManager not available)");
            }
        }

        public static void LogInfo(string message, LogCategory category)
        {
            var manager = LoggingManager.Instance;
            if (manager != null)
            {
                manager.LogInfo(message, category);
            }
            else if (Debug.isDebugBuild)
            {
                Debug.Log($"[{category}] {message} (Fallback: LoggingManager not available)");
            }
        }

        public static void LogDebug(string message, LogCategory category)
        {
            var manager = LoggingManager.Instance;
            if (manager != null)
            {
                manager.LogDebug(message, category);
            }
            else if (Debug.isDebugBuild)
            {
                Debug.Log($"[{category}] {message} (Fallback: LoggingManager not available)");
            }
        }

        public static void SetCategoryEnabled(LogCategory category, bool enabled)
        {
            var manager = LoggingManager.Instance;
            if (manager != null)
            {
                manager.SetCategoryEnabled(category, enabled);
            }
        }

        public static void SetPriorityEnabled(LogPriority priority, bool enabled)
        {
            var manager = LoggingManager.Instance;
            if (manager != null)
            {
                manager.SetPriorityEnabled(priority, enabled);
            }
        }
    }
} 