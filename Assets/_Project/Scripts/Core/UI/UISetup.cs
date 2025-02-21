using UnityEngine;
using UnityEngine.UIElements;
using NaughtyAttributes;
using CZ.Core.Resource;

namespace CZ.Core.UI
{
    [RequireComponent(typeof(UIDocument))]
    [RequireComponent(typeof(ResourceUI))]
    public class UISetup : MonoBehaviour
    {
        #region Configuration
        [Header("UI Components")]
        [SerializeField, Required]
        private UIDocument uiDocument;
        
        [SerializeField, Required]
        private ResourceUI resourceUI;

        [Header("UI Assets")]
        [SerializeField, Required]
        private PanelSettings panelSettings;

        [SerializeField, Required]
        private ResourceConfiguration resourceConfig;

        [SerializeField, Required]
        private VisualTreeAsset resourceCounterTemplate;

        private bool isInitialized;
        #endregion

        private void Awake()
        {
            if (isInitialized) return;
            ValidateComponents();
            SetupUIComponents();
            isInitialized = true;
        }

        private void ValidateComponents()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
                Debug.LogWarning("[UISetup] UIDocument was not assigned, found component on GameObject");
            }

            if (resourceUI == null)
            {
                resourceUI = GetComponent<ResourceUI>();
                Debug.LogWarning("[UISetup] ResourceUI was not assigned, found component on GameObject");
            }

            // Load default assets if not assigned
            if (panelSettings == null)
            {
                panelSettings = Resources.Load<PanelSettings>("UI/DefaultPanelSettings");
                Debug.LogWarning("[UISetup] PanelSettings was not assigned, loaded from Resources");
            }

            if (resourceConfig == null)
            {
                resourceConfig = Resources.Load<ResourceConfiguration>("Configuration/DefaultResourceConfiguration");
                Debug.LogWarning("[UISetup] ResourceConfiguration was not assigned, loaded from Resources");
            }

            if (resourceCounterTemplate == null)
            {
                resourceCounterTemplate = Resources.Load<VisualTreeAsset>("UI/ResourceCounter");
                Debug.LogWarning("[UISetup] ResourceCounterTemplate was not assigned, loaded from Resources");
            }

            // Validate required components
            if (uiDocument == null) throw new MissingComponentException("[UISetup] UIDocument component is required!");
            if (resourceUI == null) throw new MissingComponentException("[UISetup] ResourceUI component is required!");
            if (panelSettings == null) throw new MissingReferenceException("[UISetup] PanelSettings asset is required!");
            if (resourceConfig == null) throw new MissingReferenceException("[UISetup] ResourceConfiguration asset is required!");
            if (resourceCounterTemplate == null) throw new MissingReferenceException("[UISetup] ResourceCounterTemplate asset is required!");
        }

        private void SetupUIComponents()
        {
            // Configure UIDocument
            if (uiDocument != null && panelSettings != null)
            {
                uiDocument.panelSettings = panelSettings;
            }
            
            // Set up ResourceUI with required references
            if (resourceUI != null)
            {
                var resourceUIType = resourceUI.GetType();
                
                // Set UIDocument reference
                var uiDocField = resourceUIType.GetField("uiDocument", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (uiDocField != null)
                {
                    uiDocField.SetValue(resourceUI, uiDocument);
                }

                // Set ResourceConfiguration reference
                var configField = resourceUIType.GetField("resourceConfig", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (configField != null)
                {
                    configField.SetValue(resourceUI, resourceConfig);
                }

                // Set ResourceCounterTemplate reference
                var templateField = resourceUIType.GetField("resourceCounterTemplate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (templateField != null)
                {
                    templateField.SetValue(resourceUI, resourceCounterTemplate);
                }

                // Disable automatic initialization in ResourceUI
                resourceUI.enabled = false;
                resourceUI.enabled = true;

                Debug.Log("[UISetup] UI Components configured successfully");
            }
            else
            {
                Debug.LogWarning("[UISetup] ResourceUI component not found, skipping configuration");
            }
        }

        [Button("Setup UI Components")]
        private void EditorSetupUIComponents()
        {
            if (Application.isPlaying) return;
            ValidateComponents();
            SetupUIComponents();
        }

        private void OnValidate()
        {
            if (Application.isPlaying) return;

            // Auto-assign components if they exist
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            if (resourceUI == null) resourceUI = GetComponent<ResourceUI>();
        }
    }
} 