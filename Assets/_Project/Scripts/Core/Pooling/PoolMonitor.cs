using UnityEngine;
using System.Text;
using Unity.Profiling;
using System.Collections;

namespace CZ.Core.Pooling
{
    /// <summary>
    /// Monitors and displays pool usage and memory statistics
    /// Implements Unity 6.0 profiling best practices
    /// </summary>
    public class PoolMonitor : MonoBehaviour
    {
        private StringBuilder statsBuilder;
        private GUIStyle guiStyle;
        private bool showDebugInfo = false;
        private Vector2 scrollPosition;
        
        // Performance monitoring
        private ProfilerRecorder drawCallsRecorder;
        private ProfilerRecorder totalMemoryRecorder;
        
        private void Start()
        {
            statsBuilder = new StringBuilder();
            
            // Initialize GUI style
            guiStyle = new GUIStyle
            {
                fontSize = 14,
                normal = { textColor = Color.white }
            };
            
            // Initialize profiler recorders
            drawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            totalMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
            
            StartCoroutine(MonitorPerformance());
        }
        
        private void Update()
        {
            // Toggle debug info with F3
            if (Input.GetKeyDown(KeyCode.F3))
            {
                showDebugInfo = !showDebugInfo;
            }
        }
        
        private void OnGUI()
        {
            if (!showDebugInfo) return;
            
            // Create a semi-transparent background
            GUI.Box(new Rect(10, 10, 300, 400), "Pool Monitor");
            
            // Create a scrollview for the stats
            scrollPosition = GUI.BeginScrollView(
                new Rect(10, 30, 300, 380),
                scrollPosition,
                new Rect(0, 0, 280, GetContentHeight())
            );
            
            // Update and display stats
            UpdateStats();
            GUI.Label(new Rect(5, 5, 290, GetContentHeight()), statsBuilder.ToString(), guiStyle);
            
            GUI.EndScrollView();
        }
        
        private void UpdateStats()
        {
            statsBuilder.Clear();
            
            // Add general performance stats
            var drawCalls = drawCallsRecorder.LastValue;
            var totalMemoryMB = totalMemoryRecorder.LastValue / (1024 * 1024);
            
            statsBuilder.AppendLine($"FPS: {1.0f / Time.smoothDeltaTime:F1}");
            statsBuilder.AppendLine($"Draw Calls: {drawCalls}");
            statsBuilder.AppendLine($"Total Memory: {totalMemoryMB}MB");
            statsBuilder.AppendLine("--------------------");
            
            // Add pool stats
            var poolStats = PoolManager.Instance.GetAllPoolStats();
            foreach (var stat in poolStats)
            {
                statsBuilder.AppendLine($"Pool: {stat.Key}");
                statsBuilder.AppendLine($"  Current: {stat.Value.current}");
                statsBuilder.AppendLine($"  Peak: {stat.Value.peak}");
                statsBuilder.AppendLine($"  Memory: {stat.Value.memory / 1024}KB");
                statsBuilder.AppendLine("--------------------");
            }
        }
        
        private float GetContentHeight()
        {
            // Estimate content height based on number of pools
            var poolStats = PoolManager.Instance.GetAllPoolStats();
            return 100 + (poolStats.Count * 100); // Base height + 100 pixels per pool
        }
        
        private IEnumerator MonitorPerformance()
        {
            while (enabled)
            {
                // Check performance thresholds
                var drawCalls = drawCallsRecorder.LastValue;
                var totalMemoryMB = totalMemoryRecorder.LastValue / (1024 * 1024);
                
                if (drawCalls > 100)
                {
                    Debug.LogWarning($"Draw calls exceeded threshold: {drawCalls}");
                }
                
                if (totalMemoryMB > 1024)
                {
                    Debug.LogWarning($"Total memory exceeded threshold: {totalMemoryMB}MB");
                }
                
                yield return new WaitForSeconds(0.5f); // Check every 500ms
            }
        }
        
        private void OnDestroy()
        {
            drawCallsRecorder.Dispose();
            totalMemoryRecorder.Dispose();
        }
    }
} 