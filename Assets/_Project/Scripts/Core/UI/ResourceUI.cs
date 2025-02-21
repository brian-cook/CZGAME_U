using UnityEngine;
using UnityEngine.UIElements;
using CZ.Core.Resource;
using System.Collections.Generic;
using NaughtyAttributes;
using System.Collections;

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
                Debug.LogWarning("[ResourceUI] ResourceManager not found, will retry connection...");
                StartCoroutine(WaitForResourceManager());
                return;
            }

            if (uiDocument == null)
            {
                Debug.LogError("[ResourceUI] UIDocument not assigned!");
                return;
            }

            // Clear existing UI if any
            CleanupUI();

            root = uiDocument.rootVisualElement;
            resourceContainer = root.Q<VisualElement>("ResourceContainer");
            
            if (resourceContainer == null)
            {
                Debug.LogError("[ResourceUI] Resource container not found in UI Document!");
                return;
            }

            InitializeResourceCounters();
            isInitialized = true;
            Debug.Log("[ResourceUI] UI initialized successfully");
        }

        private void InitializeResourceCounters()
        {
            if (isQuitting) return;

            if (resourceConfig == null)
            {
                Debug.LogError("[ResourceUI] ResourceConfiguration is missing!");
                return;
            }

            if (resourceCounterTemplate == null)
            {
                Debug.LogError("[ResourceUI] ResourceCounterTemplate is missing!");
                return;
            }

            // Create new dictionary if null
            resourceCounters = new Dictionary<ResourceType, ResourceCounter>();

            // Create counters for each resource type
            foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
            {
                try
                {
                    var counter = new ResourceCounter(type, resourceConfig, resourceCounterTemplate);
                    
                    if (counter.Root == null)
                    {
                        Debug.LogError($"[ResourceUI] Failed to create counter for {type} - Root is null");
                        continue;
                    }

                    if (counter.IsValid)
                    {
                        resourceContainer.Add(counter.Root);
                        resourceCounters.Add(type, counter);
                        Debug.Log($"[ResourceUI] Created and added counter for {type} with initial value: {counter.CurrentValue}");
                    }
                    else
                    {
                        Debug.LogError($"[ResourceUI] Counter for {type} is invalid - skipping");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[ResourceUI] Error creating counter for {type}: {e.Message}");
                }
            }

            Debug.Log($"[ResourceUI] Initialized {resourceCounters.Count} resource counters");
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
                Debug.Log("[ResourceUI] ResourceManager found, initializing UI...");
                InitializeUI();
            }
            else if (!isQuitting)
            {
                Debug.LogError("[ResourceUI] ResourceManager not found after timeout. UI will not be initialized.");
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
                Debug.Log("[ResourceUI] Successfully subscribed to ResourceManager events");
            }
            else
            {
                Debug.LogWarning("[ResourceUI] ResourceManager not available - resource collection events will not be processed");
                SetResourceCountersInteractable(false);
            }
        }

        private void UnsubscribeFromResourceManager()
        {
            if (isQuitting || ResourceManager.Instance == null) return;

            try
            {
                ResourceManager.Instance.OnResourceCollected -= HandleResourceCollected;
                Debug.Log("[ResourceUI] Unsubscribed from ResourceManager events");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ResourceUI] Error during unsubscribe: {e.Message}");
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
                Debug.LogWarning($"[ResourceUI] Ignoring resource collection - UI not initialized or quitting. Type: {type}, Value: {value}");
                return;
            }

            if (resourceCounters != null && resourceCounters.TryGetValue(type, out var counter))
            {
                counter.AddValue(value);
                Debug.Log($"[ResourceUI] Updated counter for {type} with value {value}. New total: {counter.CurrentValue}");
            }
            else
            {
                Debug.LogError($"[ResourceUI] Failed to update counter - Counter not found for type: {type}");
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
                HandleResourceCollected(type, Random.Range(1, 10));
            }
        }
        #endregion
    }

    public class ResourceCounter
    {
        public VisualElement Root { get; private set; }
        private Label valueLabel;
        private VisualElement icon;
        private int currentValue;
        public int CurrentValue => currentValue;
        private ResourceType resourceType;
        public bool IsValid => Root != null && valueLabel != null && icon != null;

        public ResourceCounter(ResourceType type, ResourceConfiguration config, VisualTreeAsset template)
        {
            if (template == null)
            {
                Debug.LogError($"[ResourceCounter] Template is null for {type}");
                return;
            }

            if (config == null)
            {
                Debug.LogError($"[ResourceCounter] Config is null for {type}");
                return;
            }

            resourceType = type;
            currentValue = 0;

            try
            {
                Root = template.Instantiate();
                Root.name = $"{type}Counter";

                // Setup UI elements
                valueLabel = Root.Q<Label>("Value");
                icon = Root.Q<VisualElement>("Icon");

                if (!IsValid)
                {
                    Debug.LogError($"[ResourceCounter] Failed to find required UI elements for {type}");
                    return;
                }

                // Configure appearance
                Color color = type switch
                {
                    ResourceType.Experience => config.experienceColor,
                    ResourceType.Health => config.healthColor,
                    ResourceType.PowerUp => config.powerUpColor,
                    ResourceType.Currency => config.currencyColor,
                    _ => Color.white
                };

                icon.style.backgroundColor = color;
                valueLabel.text = "0";
                Debug.Log($"[ResourceCounter] Initialized counter for {type} with color: {color}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ResourceCounter] Error during initialization of {type}: {e.Message}");
                Root = null;
                valueLabel = null;
                icon = null;
            }
        }

        public void AddValue(int value)
        {
            if (!IsValid)
            {
                Debug.LogError($"[ResourceCounter] Cannot add value - counter for {resourceType} is invalid");
                return;
            }

            if (value < 0)
            {
                Debug.LogWarning($"[ResourceCounter] Attempted to add negative value: {value} to {resourceType}");
                return;
            }

            currentValue += value;
            UpdateDisplay();
            PlayCollectionAnimation();
            Debug.Log($"[ResourceCounter] Added {value} to {resourceType}. New total: {currentValue}");
        }

        public void Reset()
        {
            currentValue = 0;
            UpdateDisplay();
            Debug.Log($"[ResourceCounter] Reset counter for {resourceType}");
        }

        private void UpdateDisplay()
        {
            if (!IsValid)
            {
                Debug.LogError($"[ResourceCounter] Cannot update display - counter for {resourceType} is invalid");
                return;
            }

            valueLabel.text = currentValue.ToString();
            Debug.Log($"[ResourceCounter] Updated display for {resourceType} to: {currentValue}");
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
    }
} 