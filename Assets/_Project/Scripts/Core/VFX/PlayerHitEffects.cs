using System.Collections;
using UnityEngine;
using NaughtyAttributes;
using CZ.Core.Interfaces;
using CZ.Core.Logging;

namespace CZ.Core.VFX
{
    /// <summary>
    /// Handles visual effects when the player takes damage
    /// </summary>
    [AddComponentMenu("CZ/VFX/Player Hit Effects")]
    public class PlayerHitEffects : MonoBehaviour, IHitEffects
    {
        #region Configuration
        [BoxGroup("Visual Effects")]
        [SerializeField]
        [InfoBox("Particle system prefab for hit effect", EInfoBoxType.Normal)]
        private ParticleSystem hitParticlesPrefab;

        [BoxGroup("Visual Effects")]
        [SerializeField]
        [InfoBox("Screen shake amount when taking damage", EInfoBoxType.Normal)]
        [MinValue(0f), MaxValue(1f)]
        private float screenShakeAmount = 0.2f;

        [BoxGroup("Visual Effects")]
        [SerializeField]
        [InfoBox("Duration of screen shake", EInfoBoxType.Normal)]
        [MinValue(0f), MaxValue(0.5f)]
        private float screenShakeDuration = 0.1f;

        [BoxGroup("Visual Effects")]
        [SerializeField]
        [InfoBox("Knockback force when taking damage", EInfoBoxType.Normal)]
        [MinValue(0f), MaxValue(10f)]
        private float knockbackForce = 2f;

        [BoxGroup("Visual Effects")]
        [SerializeField]
        [InfoBox("Duration of knockback effect", EInfoBoxType.Normal)]
        [MinValue(0f), MaxValue(0.5f)]
        private float knockbackDuration = 0.1f;

        [BoxGroup("Visual Effects")]
        [SerializeField]
        [InfoBox("Scale pulse amount when taking damage", EInfoBoxType.Normal)]
        [MinValue(0f), MaxValue(0.5f)]
        private float scalePulseAmount = 0.1f;

        [BoxGroup("Visual Effects")]
        [SerializeField]
        [InfoBox("Duration of scale pulse effect", EInfoBoxType.Normal)]
        [MinValue(0f), MaxValue(0.5f)]
        private float scalePulseDuration = 0.1f;

        [BoxGroup("Debug Settings")]
        [SerializeField]
        private bool enableDebugLogs = false;
        #endregion

        #region State
        private Camera mainCamera;
        private Transform playerTransform;
        private Rigidbody2D playerRigidbody;
        private Vector3 originalScale;
        private bool isInitialized;
        private Vector2 lastDamageDirection = Vector2.zero;
        private Vector2 damageSourcePosition;
        private bool hasDamageSourcePosition;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            InitializeComponents();
        }

        private void Start()
        {
            // Subscribe to player damage events
            var damageable = GetComponent<IDamageable>();
            if (damageable != null)
            {
                if (damageable is MonoBehaviour mb)
                {
                    if (damageable is IHasHealthEvents healthEvents)
                    {
                        healthEvents.OnDamaged += HandlePlayerDamaged;
                        if (enableDebugLogs)
                        {
                            Debug.Log("[PlayerHitEffects] Successfully subscribed to IDamageable.OnDamaged event");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[PlayerHitEffects] IDamageable component doesn't implement IHasHealthEvents");
                    }
                }
                else
                {
                    Debug.LogWarning("[PlayerHitEffects] Found IDamageable but it's not a MonoBehaviour component");
                }
            }
            else
            {
                Debug.LogError("[PlayerHitEffects] No IDamageable component found on this GameObject");
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from player damage events
            var damageable = GetComponent<IDamageable>();
            if (damageable != null && damageable is MonoBehaviour mb)
            {
                if (damageable is IHasHealthEvents healthEvents)
                {
                    healthEvents.OnDamaged -= HandlePlayerDamaged;
                }
            }
        }
        #endregion

        #region Initialization
        private void InitializeComponents()
        {
            if (isInitialized) return;

            mainCamera = Camera.main;
            playerTransform = transform;
            playerRigidbody = GetComponent<Rigidbody2D>();
            originalScale = playerTransform.localScale;

            if (mainCamera == null)
            {
                Debug.LogWarning("[PlayerHitEffects] Main camera not found. Screen shake effects will not work.");
            }

            if (playerRigidbody == null)
            {
                Debug.LogWarning("[PlayerHitEffects] Rigidbody2D not found. Knockback effects will not work.");
            }

            // If no hit particles prefab is assigned, create one at runtime
            if (hitParticlesPrefab == null)
            {
                Debug.Log("[PlayerHitEffects] Hit particles prefab not assigned. Creating one at runtime.");
                hitParticlesPrefab = HitParticleFactory.CreateHitParticlesPrefab();
            }

            isInitialized = true;
            if (enableDebugLogs)
            {
                Debug.Log("[PlayerHitEffects] Components initialized successfully");
            }
        }
        #endregion

        #region Effect Handlers
        private void HandlePlayerDamaged(int damageAmount, int currentHealth)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerHitEffects] Entity took {damageAmount} damage. Current health: {currentHealth}");
            }

            // Calculate damage direction (could be improved with actual damage source position)
            CalculateDamageDirection();

            // Apply all hit effects
            SpawnHitParticles();
            StartCoroutine(ApplyScreenShake());
            ApplyKnockback();
            StartCoroutine(ApplyScalePulse());
        }

        private void CalculateDamageDirection()
        {
            if (hasDamageSourcePosition)
            {
                // Calculate direction from damage source to player
                Vector2 playerPos = transform.position;
                lastDamageDirection = (playerPos - damageSourcePosition).normalized;
                
                if (enableDebugLogs)
                {
                    Debug.Log($"[PlayerHitEffects] Calculated damage direction: {lastDamageDirection} from source at {damageSourcePosition}");
                }
                
                // Reset for next damage
                hasDamageSourcePosition = false;
            }
            else
            {
                // Fallback to random direction if no source position is available
                lastDamageDirection = Random.insideUnitCircle.normalized;
                
                if (enableDebugLogs)
                {
                    Debug.Log($"[PlayerHitEffects] Using random damage direction: {lastDamageDirection}");
                }
            }
        }

        private void SpawnHitParticles()
        {
            if (hitParticlesPrefab == null) return;

            // Instantiate and play the particle effect
            var hitParticles = Instantiate(hitParticlesPrefab, transform.position, Quaternion.identity);
            hitParticles.gameObject.SetActive(true);
            
            // Set the particle color based on damage type (could be extended)
            var mainModule = hitParticles.main;
            mainModule.startColor = new Color(1f, 0.3f, 0.3f, 1f); // Red-ish color for damage
            
            // Play the particle system
            hitParticles.Play();
            
            if (enableDebugLogs)
            {
                Debug.Log("[PlayerHitEffects] Spawned hit particles");
            }
        }

        private IEnumerator ApplyScreenShake()
        {
            if (mainCamera == null || screenShakeAmount <= 0f) yield break;

            Vector3 originalPosition = mainCamera.transform.position;
            float elapsed = 0f;

            while (elapsed < screenShakeDuration)
            {
                float strength = screenShakeAmount * (1f - (elapsed / screenShakeDuration));
                mainCamera.transform.position = originalPosition + Random.insideUnitSphere * strength;
                
                elapsed += Time.deltaTime;
                yield return null;
            }

            mainCamera.transform.position = originalPosition;
            
            if (enableDebugLogs)
            {
                Debug.Log("[PlayerHitEffects] Applied screen shake effect");
            }
        }

        private void ApplyKnockback()
        {
            if (playerRigidbody == null || knockbackForce <= 0f) return;
            
            // Apply knockback force in the direction of the damage
            playerRigidbody.AddForce(lastDamageDirection * knockbackForce, ForceMode2D.Impulse);
            
            // Reset velocity after duration
            StartCoroutine(ResetKnockback());
            
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerHitEffects] Applied knockback in direction: {lastDamageDirection}");
            }
        }

        private IEnumerator ResetKnockback()
        {
            yield return new WaitForSeconds(knockbackDuration);
            
            if (playerRigidbody != null)
            {
                playerRigidbody.linearVelocity = Vector2.zero;
            }
        }

        private IEnumerator ApplyScalePulse()
        {
            if (playerTransform == null || scalePulseAmount <= 0f) yield break;

            float elapsed = 0f;
            
            // Initial pulse outward
            Vector3 targetScale = originalScale * (1f + scalePulseAmount);
            
            while (elapsed < scalePulseDuration * 0.5f)
            {
                float t = elapsed / (scalePulseDuration * 0.5f);
                playerTransform.localScale = Vector3.Lerp(originalScale, targetScale, t);
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // Return to normal
            elapsed = 0f;
            while (elapsed < scalePulseDuration * 0.5f)
            {
                float t = elapsed / (scalePulseDuration * 0.5f);
                playerTransform.localScale = Vector3.Lerp(targetScale, originalScale, t);
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // Ensure we end at the original scale
            playerTransform.localScale = originalScale;
            
            if (enableDebugLogs)
            {
                Debug.Log("[PlayerHitEffects] Applied scale pulse effect");
            }
        }
        #endregion

        #region Public Methods (Interface Implementation)
        /// <summary>
        /// Sets the position of the damage source to calculate direction
        /// </summary>
        /// <param name="sourcePosition">Position of the damage source</param>
        public void SetDamageSourcePosition(Vector2 sourcePosition)
        {
            damageSourcePosition = sourcePosition;
            hasDamageSourcePosition = true;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerHitEffects] Damage source position set to {sourcePosition}");
            }
        }
        #endregion

        #region Debug Methods
        [Button("Test Hit Effects")]
        private void TestHitEffects()
        {
            HandlePlayerDamaged(10, 90);
        }
        #endregion
    }
} 