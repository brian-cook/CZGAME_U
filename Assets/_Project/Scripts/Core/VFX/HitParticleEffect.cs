using UnityEngine;

namespace CZ.Core.VFX
{
    /// <summary>
    /// Simple component to handle hit particle effect lifecycle
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class HitParticleEffect : MonoBehaviour
    {
        private ParticleSystem hitParticleSystem;

        private void Awake()
        {
            hitParticleSystem = GetComponent<ParticleSystem>();
        }

        private void OnEnable()
        {
            // Auto-destroy when particles finish playing
            Destroy(gameObject, hitParticleSystem.main.duration + hitParticleSystem.main.startLifetime.constant);
        }
    }
} 