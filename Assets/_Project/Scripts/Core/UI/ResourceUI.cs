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
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            InitializeUI();
        }

        private void Start()
        {
            if (ResourceManager.Instance == null)
            {
                Debug.LogWarning("[ResourceUI] ResourceManager not found, will retry connection...");
                StartCoroutine(WaitForResourceManager());
                return;
            }
            
            InitializeUI();
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

        private void OnEnable()
        {
            if (!isQuitting)
            {
                SubscribeToResourceManager();
            }
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
            if (isQuitting) return;

            if (ResourceManager.Instance == null)
            {
                Debug.LogError("[ResourceUI] Cannot initialize UI without ResourceManager!");
                return;
            }

            if (uiDocument == null)
            {
                Debug.LogError("[ResourceUI] UIDocument not assigned!");
                return;
            }

            root = uiDocument.rootVisualElement;
            resourceContainer = root.Q<VisualElement>("ResourceContainer");
            
            if (resourceContainer == null)
            {
                Debug.LogError("[ResourceUI] Resource container not found in UI Document!");
                return;
            }

            InitializeResourceCounters();
            SubscribeToResourceManager();
            
            Debug.Log("[ResourceUI] UI initialized successfully");
        }

        private void InitializeResourceCounters()
        {
            if (isQuitting) return;

            resourceCounters = new Dictionary<ResourceType, ResourceCounter>();

            // Create counters for each resource type
            foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
            {
                var counter = new ResourceCounter(type, resourceConfig, resourceCounterTemplate);
                resourceContainer.Add(counter.Root);
                resourceCounters.Add(type, counter);
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
            // Skip if already quitting or ResourceManager is null
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
            if (resourceCounters.TryGetValue(type, out var counter))
            {
                counter.AddValue(value);
            }
        }

        [Button("Reset Counters")]
        public void ResetAllCounters()
        {
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
        private ResourceType resourceType;

        public ResourceCounter(ResourceType type, ResourceConfiguration config, VisualTreeAsset template)
        {
            resourceType = type;
            Root = template.Instantiate();
            Root.name = $"{type}Counter";

            // Setup UI elements
            valueLabel = Root.Q<Label>("Value");
            icon = Root.Q<VisualElement>("Icon");

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
            UpdateDisplay();
        }

        public void AddValue(int value)
        {
            currentValue += value;
            UpdateDisplay();
            PlayCollectionAnimation();
        }

        public void Reset()
        {
            currentValue = 0;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            valueLabel.text = currentValue.ToString();
        }

        private void PlayCollectionAnimation()
        {
            Root.experimental.animation
                .Scale(1.2f, 100)
                .OnCompleted(() => {
                    Root.experimental.animation
                        .Scale(1f, 100);
                });
        }

        public void SetInteractable(bool interactable)
        {
            Root.SetEnabled(interactable);
            
            // Visual feedback for disabled state
            if (!interactable)
            {
                Root.style.opacity = 0.5f;
                icon.style.backgroundColor = Color.gray;
                valueLabel.text = "--";
            }
            else
            {
                Root.style.opacity = 1f;
                UpdateDisplay();
            }
        }
    }
} 