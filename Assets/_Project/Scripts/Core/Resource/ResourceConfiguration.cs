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

        [SerializeField, MinValue(1)]
        public int experienceStackSize = 10;

        [Header("Health Settings")]
        [SerializeField, MinValue(1)]
        public int baseHealthValue = 10;
        
        [SerializeField]
        public Color healthColor = Color.green;

        [SerializeField, MinValue(1)]
        public int healthStackSize = 5;

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

        [SerializeField, MinValue(1)]
        public int currencyStackSize = 20;

        [Header("Collection Settings")]
        [SerializeField, MinValue(0.1f)]
        public float baseCollectionRadius = 1f;
        
        [SerializeField, MinValue(0.1f)]
        public float baseCollectionSpeed = 5f;
        
        [SerializeField, MinValue(1f)]
        public float baseLifetime = 10f;

        [Header("Stack Settings")]
        [SerializeField, MinValue(0.1f)]
        public float stackCollectionMultiplier = 1.2f;

        [SerializeField, MinValue(0f)]
        public float stackBonusPerItem = 0.1f;

        [SerializeField, Range(1, 10)]
        public int maxStackBonus = 5;

        [Header("Visual Settings")]
        [SerializeField, MinValue(0.1f)]
        public float basePulseSpeed = 1f;
        
        [SerializeField, Range(0f, 1f)]
        public float basePulseIntensity = 0.2f;
        
        [SerializeField, MinValue(0.1f)]
        public float baseTrailTime = 0.2f;

        [Header("Collection Feedback")]
        [SerializeField]
        public bool enableCollectionEffects = true;

        [SerializeField]
        public GameObject collectionVFXPrefab;

        [SerializeField, MinValue(0.1f)]
        public float collectionEffectDuration = 0.5f;

        [SerializeField]
        public AudioClip standardCollectionSound;

        [SerializeField]
        public AudioClip stackCompleteSound;

        [SerializeField]
        public AudioClip specialResourceSound;

        #region Stack Calculation
        public int CalculateStackBonus(int stackSize)
        {
            float bonus = 1f + Mathf.Min(stackSize * stackBonusPerItem, maxStackBonus);
            return Mathf.RoundToInt(bonus);
        }

        public float GetStackCollectionRadius(int currentStack)
        {
            return baseCollectionRadius * (1f + (currentStack * stackCollectionMultiplier));
        }
        #endregion
    }
} 