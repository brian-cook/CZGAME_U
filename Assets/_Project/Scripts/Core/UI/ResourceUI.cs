using UnityEngine;
using UnityEngine.UIElements;
using CZ.Core.Resource;
using System.Collections.Generic;
using NaughtyAttributes;

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
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            InitializeUI();
        }

        private void Start()
        {
            // Try to find ResourceManager after all Awake calls are complete
            if (ResourceManager.Instance == null)
            {
                Debug.LogError("[ResourceUI] ResourceManager not found in scene. Please ensure ResourceManager is set up in the initial scene.");
                SetResourceCountersInteractable(false);
            }
        }

        private void OnEnable()
        {
            SubscribeToResourceManager();
        }

        private void OnDisable()
        {
            UnsubscribeFromResourceManager();
        }
        #endregion

        #region Initialization
        private void InitializeUI()
        {
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
        }

        private void InitializeResourceCounters()
        {
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
            var resourceManager = ResourceManager.Instance;
            if (resourceManager != null)
            {
                resourceManager.OnResourceCollected -= HandleResourceCollected;
                Debug.Log("[ResourceUI] Unsubscribed from ResourceManager events");
            }
        }

        private void SetResourceCountersInteractable(bool interactable)
        {
            if (resourceCounters != null)
            {
                foreach (var counter in resourceCounters.Values)
                {
                    counter.SetInteractable(interactable);
                }
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