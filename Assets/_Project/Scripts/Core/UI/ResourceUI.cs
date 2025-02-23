using UnityEngine;
using UnityEngine.UIElements;
using CZ.Core.Resource;
using System.Collections.Generic;
using NaughtyAttributes;
using System.Collections;
using System;
using CZ.Core.Logging;

namespace CZ.Core.UI
{
    public class ResourceUI : MonoBehaviour
    {
        #region Configuration
        [Header("UI Configuration")]
        [SerializeField]
        private UIDocument uiDocument;

        [SerializeField]
        private ResourceConfiguration resourceConfig;

        [Header("UI References")]
        [SerializeField]
        private VisualTreeAsset resourceCounterTemplate;
        #endregion

        #region UI Elements
        private Dictionary<ResourceType, ResourceCounter> resourceCounters;
        private VisualElement root;
        private VisualElement resourceContainer;
        private bool isQuitting;
        private bool isInitialized;
        #endregion

        #region Unity Lifecycle
        private void OnEnable()
        {
            if (!isInitialized)
            {
                InitializeUI();
            }
            SubscribeToResourceManager();
        }

        private void OnDisable()
        {
            if (!isQuitting)
            {
                UnsubscribeFromResourceManager();
            }
        }

        private void OnApplicationQuit()
        {
            isQuitting = true;
            CleanupUI();
        }
        #endregion

        #region Initialization
        private void InitializeUI()
        {
            if (isQuitting || isInitialized) return;

            if (ResourceManager.Instance == null)
            {
                CZLogger.LogWarning("[ResourceUI] ResourceManager not found, will retry connection...", LogCategory.UI);
                StartCoroutine(WaitForResourceManager());
                return;
            }

            if (uiDocument == null)
            {
                CZLogger.LogError("[ResourceUI] UIDocument not assigned!", LogCategory.UI);
                return;
            }

            // Clear existing UI if any
            CleanupUI();

            root = uiDocument.rootVisualElement;
            resourceContainer = root.Q<VisualElement>("ResourceContainer");
            
            if (resourceContainer == null)
            {
                CZLogger.LogError("[ResourceUI] Resource container not found in UI Document!", LogCategory.UI);
                return;
            }

            InitializeResourceCounters(root);
            isInitialized = true;
            CZLogger.LogInfo("[ResourceUI] UI initialized successfully", LogCategory.UI);
        }

        private void ValidateConfiguration()
        {
            if (resourceConfig == null)
            {
                CZLogger.LogError("[ResourceUI] ResourceConfiguration is missing!", LogCategory.UI);
                return;
            }

            if (resourceCounterTemplate == null)
            {
                CZLogger.LogError("[ResourceUI] ResourceCounterTemplate is missing!", LogCategory.UI);
                return;
            }
        }

        private void InitializeResourceCounters(VisualElement root)
        {
            if (resourceCounterTemplate == null)
            {
                CZLogger.LogError("[ResourceUI] Resource counter template is missing!", LogCategory.UI);
                return;
            }

            resourceCounters = new Dictionary<ResourceType, ResourceCounter>();

            foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
            {
                try
                {
                    var instance = resourceCounterTemplate.Instantiate();
                    var counter = new ResourceCounter(type, resourceConfig, instance);
                    
                    if (!counter.IsValid)
                    {
                        CZLogger.LogError($"[ResourceUI] Failed to create valid counter for {type}", LogCategory.UI);
                        continue;
                    }

                    resourceCounters[type] = counter;
                    resourceContainer.Add(counter.Root);
                    CZLogger.LogInfo($"[ResourceUI] Successfully created counter for {type}", LogCategory.UI);
                }
                catch (Exception e)
                {
                    CZLogger.LogError($"[ResourceUI] Failed to create counter for {type}: {e.Message}", LogCategory.UI);
                }
            }
        }

        private ResourceCounter CreateResourceCounter(ResourceType type, VisualElement root)
        {
            if (resourceCounterTemplate == null)
            {
                CZLogger.LogError($"[ResourceCounter] Template is null for {type}", LogCategory.UI);
                return null;
            }

            if (resourceConfig == null)
            {
                CZLogger.LogError($"[ResourceCounter] Config is null for {type}", LogCategory.UI);
                return null;
            }

            var instance = resourceCounterTemplate.CloneTree();
            var counter = new ResourceCounter(type, resourceConfig, instance);

            if (!counter.Initialize())
            {
                CZLogger.LogError($"[ResourceCounter] Failed to find required UI elements for {type}", LogCategory.UI);
                return null;
            }

            root.Add(instance);
            return counter;
        }

        private IEnumerator WaitForResourceManager()
        {
            float timeout = 5f;
            float elapsed = 0f;
            
            while (ResourceManager.Instance == null && elapsed < timeout && !isQuitting)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (ResourceManager.Instance != null && !isQuitting)
            {
                CZLogger.LogInfo("[ResourceUI] ResourceManager found, initializing UI...", LogCategory.UI);
                InitializeUI();
            }
            else if (!isQuitting)
            {
                CZLogger.LogError("[ResourceUI] ResourceManager not found after timeout. UI will not be initialized.", LogCategory.UI);
            }
        }
        #endregion

        #region Resource Manager Integration
        private void SubscribeToResourceManager()
        {
            if (isQuitting) return;

            var resourceManager = ResourceManager.Instance;
            if (resourceManager != null)
            {
                resourceManager.OnResourceCollected += HandleResourceCollected;
                SetResourceCountersInteractable(true);
                CZLogger.LogInfo("[ResourceUI] Successfully subscribed to ResourceManager events", LogCategory.UI);
            }
            else
            {
                CZLogger.LogWarning("[ResourceUI] ResourceManager not available - resource collection events will not be processed", LogCategory.UI);
                SetResourceCountersInteractable(false);
            }
        }

        private void UnsubscribeFromResourceManager()
        {
            if (isQuitting || ResourceManager.Instance == null) return;

            try
            {
                ResourceManager.Instance.OnResourceCollected -= HandleResourceCollected;
                CZLogger.LogInfo("[ResourceUI] Unsubscribed from ResourceManager events", LogCategory.UI);
            }
            catch (System.Exception e)
            {
                CZLogger.LogWarning($"[ResourceUI] Error during unsubscribe: {e.Message}", LogCategory.UI);
            }
        }

        private void CleanupUI()
        {
            if (resourceCounters != null)
            {
                foreach (var counter in resourceCounters.Values)
                {
                    counter.SetInteractable(false);
                }
                resourceCounters.Clear();
            }

            if (resourceContainer != null)
            {
                resourceContainer.Clear();
            }

            isInitialized = false;
        }

        private void SetResourceCountersInteractable(bool interactable)
        {
            if (isQuitting || resourceCounters == null) return;

            foreach (var counter in resourceCounters.Values)
            {
                counter.SetInteractable(interactable);
            }
        }
        #endregion

        #region Resource Handling
        private void HandleResourceCollected(ResourceType type, int value)
        {
            if (!isInitialized || isQuitting)
            {
                CZLogger.LogWarning($"[ResourceUI] Ignoring resource collection - UI not initialized or quitting. Type: {type}, Value: {value}", LogCategory.UI);
                return;
            }

            if (resourceCounters != null && resourceCounters.TryGetValue(type, out var counter))
            {
                counter.AddValue(value);
                CZLogger.LogInfo($"Updated counter for {type} with value {value}. New total: {counter.CurrentValue}", LogCategory.UI);
            }
            else
            {
                CZLogger.LogError($"[ResourceUI] Failed to update counter - Counter not found for type: {type}", LogCategory.UI);
            }
        }

        [Button("Reset Counters")]
        public void ResetAllCounters()
        {
            if (resourceCounters == null) return;
            foreach (var counter in resourceCounters.Values)
            {
                counter.Reset();
            }
        }
        #endregion

        #region Debug
        [Button("Test Resource Collection")]
        private void TestResourceCollection()
        {
            if (!Application.isPlaying) return;
            
            foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
            {
                HandleResourceCollected(type, UnityEngine.Random.Range(1, 10));
            }
        }
        #endregion
    }

    public class ResourceCounter
    {
        private readonly ResourceConfiguration resourceConfig;
        private readonly ResourceType resourceType;
        private Label valueLabel;
        private VisualElement icon;
        private int currentValue;
        
        public VisualElement Root { get; private set; }
        public int CurrentValue => currentValue;
        public bool IsValid => Root != null && valueLabel != null && icon != null;

        public ResourceCounter(ResourceType type, ResourceConfiguration config, VisualElement root)
        {
            resourceType = type;
            resourceConfig = config;
            currentValue = 0;
            Root = root;

            if (config == null)
            {
                CZLogger.LogError($"[ResourceCounter] Config is null for {type}", LogCategory.UI);
                return;
            }

            try
            {
                Root.name = $"{type}Counter";

                // Setup UI elements
                valueLabel = Root.Q<Label>("Value");
                icon = Root.Q<VisualElement>("Icon");

                if (!IsValid)
                {
                    CZLogger.LogError($"[ResourceCounter] Failed to find required UI elements for {type}", LogCategory.UI);
                    return;
                }

                // Configure appearance
                Color color = type switch
                {
                    ResourceType.Health => config.healthColor,
                    ResourceType.Experience => config.experienceColor,
                    ResourceType.PowerUp => config.powerUpColor,
                    ResourceType.Currency => config.currencyColor,
                    _ => Color.white
                };

                icon.style.backgroundColor = color;
                valueLabel.text = "0";
                CZLogger.LogInfo($"[ResourceCounter] Initialized counter for {type} with color: {color}", LogCategory.UI);
            }
            catch (System.Exception e)
            {
                CZLogger.LogError($"[ResourceCounter] Error during initialization of {type}: {e.Message}", LogCategory.UI);
                Root = null;
                valueLabel = null;
                icon = null;
            }
        }

        public void AddValue(int value)
        {
            if (!IsValid)
            {
                CZLogger.LogError($"[ResourceCounter] Cannot add value - counter for {resourceType} is invalid", LogCategory.UI);
                return;
            }

            if (value < 0)
            {
                CZLogger.LogWarning($"[ResourceCounter] Attempted to add negative value: {value} to {resourceType}", LogCategory.UI);
                return;
            }

            currentValue += value;
            UpdateDisplay();
            PlayCollectionAnimation();
            CZLogger.LogInfo($"[ResourceCounter] Added {value} to {resourceType}. New total: {currentValue}", LogCategory.UI);
        }

        public void Reset()
        {
            currentValue = 0;
            UpdateDisplay();
            CZLogger.LogInfo($"[ResourceCounter] Reset counter for {resourceType}", LogCategory.UI);
        }

        private void UpdateDisplay()
        {
            if (!IsValid)
            {
                CZLogger.LogError($"[ResourceCounter] Cannot update display - counter for {resourceType} is invalid", LogCategory.UI);
                return;
            }

            valueLabel.text = currentValue.ToString();
            CZLogger.LogInfo($"[ResourceCounter] Updated display for {resourceType} to: {currentValue}", LogCategory.UI);
        }

        private void PlayCollectionAnimation()
        {
            if (Root != null)
            {
                Root.experimental.animation
                    .Scale(1.2f, 100)
                    .OnCompleted(() => {
                        Root.experimental.animation
                            .Scale(1f, 100);
                    });
            }
        }

        public void SetInteractable(bool interactable)
        {
            if (Root == null) return;

            Root.SetEnabled(interactable);
            
            // Visual feedback for disabled state
            if (!interactable)
            {
                Root.style.opacity = 0.5f;
                if (icon != null) icon.style.backgroundColor = Color.gray;
                if (valueLabel != null) valueLabel.text = "--";
            }
            else
            {
                Root.style.opacity = 1f;
                UpdateDisplay();
            }
        }

        public bool Initialize()
        {
            if (Root == null)
            {
                CZLogger.LogError($"[ResourceCounter] Root is null for {resourceType}", LogCategory.UI);
                return false;
            }

            valueLabel = Root.Q<Label>("Value");
            icon = Root.Q<VisualElement>("Icon");

            if (valueLabel == null || icon == null)
            {
                CZLogger.LogError($"[ResourceCounter] Failed to find required UI elements for {resourceType}", LogCategory.UI);
                return false;
            }

            // Configure appearance
            Color color = resourceType switch
            {
                ResourceType.Health => resourceConfig.healthColor,
                ResourceType.Experience => resourceConfig.experienceColor,
                ResourceType.PowerUp => resourceConfig.powerUpColor,
                ResourceType.Currency => resourceConfig.currencyColor,
                _ => Color.white
            };

            icon.style.backgroundColor = color;
            valueLabel.text = "0";
            CZLogger.LogInfo($"[ResourceCounter] Initialized counter for {resourceType} with color: {color}", LogCategory.UI);
            return true;
        }
    }
} 