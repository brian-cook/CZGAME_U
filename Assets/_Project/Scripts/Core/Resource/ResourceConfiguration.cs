using UnityEngine;
using NaughtyAttributes;

namespace CZ.Core.Resource
{
    [CreateAssetMenu(fileName = "ResourceConfiguration", menuName = "CZ/Resource/ResourceConfiguration")]
    public class ResourceConfiguration : ScriptableObject
    {
        [Header("Experience Settings")]
        [SerializeField, MinValue(1)]
        public int baseExperienceValue = 1;
        
        [SerializeField]
        public Color experienceColor = Color.yellow;

        [Header("Health Settings")]
        [SerializeField, MinValue(1)]
        public int baseHealthValue = 10;
        
        [SerializeField]
        public Color healthColor = Color.green;

        [Header("PowerUp Settings")]
        [SerializeField, MinValue(1)]
        public float powerUpDuration = 5f;
        
        [SerializeField]
        public Color powerUpColor = Color.blue;

        [Header("Currency Settings")]
        [SerializeField, MinValue(1)]
        public int baseCurrencyValue = 5;
        
        [SerializeField]
        public Color currencyColor = Color.yellow;

        [Header("Collection Settings")]
        [SerializeField, MinValue(0.1f)]
        public float baseCollectionRadius = 1f;
        
        [SerializeField, MinValue(0.1f)]
        public float baseCollectionSpeed = 5f;
        
        [SerializeField, MinValue(1f)]
        public float baseLifetime = 10f;

        [Header("Visual Settings")]
        [SerializeField, MinValue(0.1f)]
        public float basePulseSpeed = 1f;
        
        [SerializeField, Range(0f, 1f)]
        public float basePulseIntensity = 0.2f;
        
        [SerializeField, MinValue(0.1f)]
        public float baseTrailTime = 0.2f;
    }
} 