using UnityEngine;

namespace CZ.Core.Configuration
{
    /// <summary>
    /// Configures memory thresholds and settings for the game
    /// Follows Unity 6.0 memory management guidelines
    /// </summary>
    [CreateAssetMenu(fileName = "MemoryConfiguration", menuName = "CZ/Configuration/Memory Configuration")]
    public class MemoryConfiguration : ScriptableObject
    {
        [Header("Memory Thresholds (MB)")]
        [SerializeField] private float baseThreshold = 256f;
        [SerializeField] private float warningThreshold = 384f;
        [SerializeField] private float criticalThreshold = 512f;
        [SerializeField] private float emergencyThreshold = 768f;
        
        [Header("Pool Memory Thresholds (MB)")]
        [SerializeField] private float poolWarningThreshold = 128f;
        [SerializeField] private float poolCriticalThreshold = 256f;
        [SerializeField] private float poolEmergencyThreshold = 384f;

        // Base memory threshold
        public float BaseThreshold
        {
            get => baseThreshold;
            set => baseThreshold = value;
        }

        // General memory thresholds
        public float WarningThreshold
        {
            get => warningThreshold;
            set => warningThreshold = value;
        }

        public float CriticalThreshold
        {
            get => criticalThreshold;
            set => criticalThreshold = value;
        }

        public float EmergencyThreshold
        {
            get => emergencyThreshold;
            set => emergencyThreshold = value;
        }

        // Pool-specific thresholds
        public float PoolWarningThreshold
        {
            get => poolWarningThreshold;
            set => poolWarningThreshold = value;
        }

        public float PoolCriticalThreshold
        {
            get => poolCriticalThreshold;
            set => poolCriticalThreshold = value;
        }

        public float PoolEmergencyThreshold
        {
            get => poolEmergencyThreshold;
            set => poolEmergencyThreshold = value;
        }

        private void OnValidate()
        {
            // Ensure thresholds maintain proper hierarchy
            warningThreshold = Mathf.Max(warningThreshold, baseThreshold);
            criticalThreshold = Mathf.Max(criticalThreshold, warningThreshold);
            emergencyThreshold = Mathf.Max(emergencyThreshold, criticalThreshold);

            poolWarningThreshold = Mathf.Max(poolWarningThreshold, baseThreshold / 2);
            poolCriticalThreshold = Mathf.Max(poolCriticalThreshold, poolWarningThreshold);
            poolEmergencyThreshold = Mathf.Max(poolEmergencyThreshold, poolCriticalThreshold);
        }
    }
} 