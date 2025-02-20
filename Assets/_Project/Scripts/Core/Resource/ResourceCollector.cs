using UnityEngine;
using System;
using System.Collections.Generic;
using NaughtyAttributes;
using CZ.Core.Interfaces;
using CZ.Core.Extensions;

namespace CZ.Core.Resource
{
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(AudioSource))]
    public class ResourceCollector : MonoBehaviour
    {
        #region Components
        private CircleCollider2D collectionTrigger;
        private AudioSource audioSource;
        #endregion

        #region Configuration
        [Header("Collection Settings")]
        [SerializeField]
        private ResourceConfiguration resourceConfig;

        [SerializeField]
        private LayerMask resourceLayer;

        [Header("Debug")]
        [SerializeField]
        private bool showDebugInfo = false;
        #endregion

        #region State
        private Dictionary<ResourceType, ResourceStack> resourceStacks;
        private bool isInitialized;
        #endregion

        #region Events
        public event Action<ResourceType, int> OnResourceCollected;
        public event Action<ResourceType, int> OnStackCompleted;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            SetupComponents();
            InitializeStacks();
        }

        private void SetupComponents()
        {
            if (isInitialized) return;

            collectionTrigger = GetComponent<CircleCollider2D>();
            if (collectionTrigger != null)
            {
                collectionTrigger.isTrigger = true;
                collectionTrigger.radius = resourceConfig.baseCollectionRadius;
            }
            else
            {
                Debug.LogError("[ResourceCollector] CircleCollider2D component not found!");
            }

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f; // 2D sound
            }

            isInitialized = true;
        }

        private void InitializeStacks()
        {
            resourceStacks = new Dictionary<ResourceType, ResourceStack>();

            // Initialize stacks for stackable resources
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                var stack = new ResourceStack(type, resourceConfig);
                stack.OnResourceAdded += HandleResourceAdded;
                stack.OnStackCompleted += HandleStackCompleted;
                resourceStacks.Add(type, stack);
            }
        }

        private void OnDestroy()
        {
            if (resourceStacks != null)
            {
                foreach (var stack in resourceStacks.Values)
                {
                    stack.OnResourceAdded -= HandleResourceAdded;
                    stack.OnStackCompleted -= HandleStackCompleted;
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.gameObject.IsInLayerMask(resourceLayer))
                return;

            var resource = other.GetComponent<BaseResource>();
            if (resource != null)
            {
                CollectResource(resource);
            }
        }
        #endregion

        #region Resource Collection
        private void CollectResource(BaseResource resource)
        {
            if (!resourceStacks.TryGetValue(resource.ResourceType, out var stack))
            {
                Debug.LogError($"[ResourceCollector] No stack found for resource type: {resource.ResourceType}");
                return;
            }

            if (stack.TryAddResource(resource))
            {
                // Play collection sound
                if (resourceConfig.enableCollectionEffects)
                {
                    PlayCollectionSound(resource.ResourceType, stack.IsFull);
                }

                // Spawn collection VFX
                if (resourceConfig.collectionVFXPrefab != null)
                {
                    SpawnCollectionVFX(resource.transform.position);
                }

                // Update collection radius based on stack size
                UpdateCollectionRadius(stack);
            }
        }

        private void HandleResourceAdded(ResourceType type, int value)
        {
            OnResourceCollected?.Invoke(type, value);
            
            if (showDebugInfo)
            {
                Debug.Log($"[ResourceCollector] Collected {type} with value {value}");
            }
        }

        private void HandleStackCompleted(ResourceType type, int totalValue)
        {
            OnStackCompleted?.Invoke(type, totalValue);
            
            if (showDebugInfo)
            {
                Debug.Log($"[ResourceCollector] Stack completed for {type} with total value {totalValue}");
            }

            // Play stack completion sound
            if (resourceConfig.enableCollectionEffects && resourceConfig.stackCompleteSound != null)
            {
                audioSource.PlayOneShot(resourceConfig.stackCompleteSound);
            }

            // Reset collection radius for this type
            if (resourceStacks.TryGetValue(type, out var stack))
            {
                UpdateCollectionRadius(stack);
            }
        }

        private void UpdateCollectionRadius(ResourceStack stack)
        {
            float newRadius = stack.GetCurrentCollectionRadius();
            SetCollectionRadius(newRadius);
        }

        private void PlayCollectionSound(ResourceType type, bool isStackComplete)
        {
            if (!resourceConfig.enableCollectionEffects || audioSource == null) return;

            AudioClip clipToPlay = type switch
            {
                ResourceType.PowerUp => resourceConfig.specialResourceSound,
                _ => isStackComplete ? resourceConfig.stackCompleteSound : resourceConfig.standardCollectionSound
            };

            if (clipToPlay != null)
            {
                audioSource.PlayOneShot(clipToPlay);
            }
        }

        private void SpawnCollectionVFX(Vector3 position)
        {
            if (!resourceConfig.enableCollectionEffects || resourceConfig.collectionVFXPrefab == null) return;

            var vfx = Instantiate(resourceConfig.collectionVFXPrefab, position, Quaternion.identity);
            Destroy(vfx, resourceConfig.collectionEffectDuration);
        }
        #endregion

        #region Public Methods
        public void SetCollectionRadius(float radius)
        {
            if (collectionTrigger != null)
            {
                collectionTrigger.radius = Mathf.Max(0.1f, radius);
            }
        }

        public float GetCollectionRadius()
        {
            return collectionTrigger != null ? collectionTrigger.radius : resourceConfig.baseCollectionRadius;
        }

        public void ClearStacks()
        {
            foreach (var stack in resourceStacks.Values)
            {
                stack.Clear();
            }
            SetCollectionRadius(resourceConfig.baseCollectionRadius);
        }
        #endregion
    }
} 