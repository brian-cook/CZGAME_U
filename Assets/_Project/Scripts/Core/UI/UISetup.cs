using UnityEngine;
using UnityEngine.UIElements;
using NaughtyAttributes;
using CZ.Core.Resource;
using CZ.Core.Logging;

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
                CZLogger.LogWarning("UIDocument was not assigned, found component on GameObject", LogCategory.UI);
            }

            if (resourceUI == null)
            {
                resourceUI = GetComponent<ResourceUI>();
                CZLogger.LogWarning("ResourceUI was not assigned, found component on GameObject", LogCategory.UI);
            }

            // Load default assets if not assigned
            if (panelSettings == null)
            {
                panelSettings = Resources.Load<PanelSettings>("UI/DefaultPanelSettings");
                CZLogger.LogWarning("PanelSettings was not assigned, loaded from Resources", LogCategory.UI);
            }

            if (resourceConfig == null)
            {
                resourceConfig = Resources.Load<ResourceConfiguration>("Configuration/DefaultResourceConfiguration");
                CZLogger.LogWarning("ResourceConfiguration was not assigned, loaded from Resources", LogCategory.UI);
            }

            if (resourceCounterTemplate == null)
            {
                resourceCounterTemplate = Resources.Load<VisualTreeAsset>("UI/ResourceCounter");
                CZLogger.LogWarning("ResourceCounterTemplate was not assigned, loaded from Resources", LogCategory.UI);
            }

            // Validate required components
            if (uiDocument == null) throw new MissingComponentException("UIDocument component is required!");
            if (resourceUI == null) throw new MissingComponentException("ResourceUI component is required!");
            if (panelSettings == null) throw new MissingReferenceException("PanelSettings asset is required!");
            if (resourceConfig == null) throw new MissingReferenceException("ResourceConfiguration asset is required!");
            if (resourceCounterTemplate == null) throw new MissingReferenceException("ResourceCounterTemplate asset is required!");
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

                CZLogger.LogInfo("UI Components configured successfully", LogCategory.UI);
            }
            else
            {
                CZLogger.LogWarning("ResourceUI component not found, skipping configuration", LogCategory.UI);
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