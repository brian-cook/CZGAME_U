using UnityEngine;
using CZ.Core.Resource;

namespace CZ.Core.VFX
{
    [RequireComponent(typeof(ParticleSystem))]
    public class ResourceCollectionVFX : MonoBehaviour
    {
        private new ParticleSystem particleSystem;
        private ParticleSystem.MainModule mainModule;

        private void Awake()
        {
            particleSystem = GetComponent<ParticleSystem>();
            mainModule = particleSystem.main;
        }

        public void SetResourceType(ResourceType resourceType, ResourceConfiguration config)
        {
            Color color = resourceType switch
            {
                ResourceType.Health => config.healthColor,
                ResourceType.Experience => config.experienceColor,
                ResourceType.PowerUp => config.powerUpColor,
                ResourceType.Currency => config.currencyColor,
                _ => Color.white
            };

            var startColor = mainModule.startColor;
            startColor.color = color;
            mainModule.startColor = startColor;

            // Adjust particle size based on resource type
            var startSize = mainModule.startSize;
            float sizeMultiplier = resourceType switch
            {
                ResourceType.PowerUp => 1.5f,
                ResourceType.Currency => 0.8f,
                _ => 1f
            };
            startSize.constant *= sizeMultiplier;
            mainModule.startSize = startSize;

            // Start the particle system
            particleSystem.Clear();
            particleSystem.Play();
        }

        private void OnParticleSystemStopped()
        {
            // Return to pool or destroy when finished
            if (gameObject != null)
            {
                Destroy(gameObject);
            }
        }
    }
} 