using UnityEngine;
using CZ.Core.Interfaces;
using System.Collections;

namespace CZ.Core.Player
{
    /// <summary>
    /// Simple implementation of IHitEffects for the player that provides basic 
    /// effect handling when taking damage.
    /// </summary>
    [AddComponentMenu("CZ/Player/Player Damage Effects")]
    public class PlayerDamageEffects : MonoBehaviour, IHitEffects
    {
        [SerializeField]
        private float knockbackForce = 5f;
        
        [SerializeField]
        private float knockbackDuration = 0.2f;
        
        private Rigidbody2D rb;
        private Vector2 lastDamageSourcePosition;
        private IDamageable damageable;
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            damageable = GetComponent<IDamageable>();
            
            // Subscribe to damage events if available
            if (damageable != null && damageable is IHasHealthEvents healthEvents)
            {
                healthEvents.OnDamaged += OnDamageReceived;
                Debug.Log("[PlayerDamageEffects] Successfully subscribed to IDamageable.OnDamaged event");
            }
        }
        
        private void OnDestroy()
        {
            // Unsubscribe to prevent memory leaks
            if (damageable != null && damageable is IHasHealthEvents healthEvents)
            {
                healthEvents.OnDamaged -= OnDamageReceived;
            }
        }
        
        private void OnDamageReceived(int damageAmount, int currentHealth)
        {
            // Apply a simple knockback effect
            if (rb != null)
            {
                Vector2 damageDirection;
                
                // If we have a damage source position, use that for direction
                if (lastDamageSourcePosition != Vector2.zero)
                {
                    damageDirection = ((Vector2)transform.position - lastDamageSourcePosition).normalized;
                }
                else
                {
                    // Otherwise use a random direction
                    damageDirection = Random.insideUnitCircle.normalized;
                }
                
                // Apply knockback force
                rb.AddForce(damageDirection * knockbackForce, ForceMode2D.Impulse);
                Debug.Log($"[PlayerDamageEffects] Applied knockback in direction: {damageDirection}");
                
                // Reset velocity after knockbackDuration
                StartCoroutine(ResetKnockbackAfterDelay());
            }
        }
        
        private IEnumerator ResetKnockbackAfterDelay()
        {
            yield return new WaitForSeconds(knockbackDuration);
            
            if (rb != null)
            {
                // Gradually slow down instead of immediate stop
                float elapsed = 0f;
                float slowdownDuration = 0.1f;
                Vector2 currentVelocity = rb.linearVelocity;
                
                while (elapsed < slowdownDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / slowdownDuration;
                    rb.linearVelocity = Vector2.Lerp(currentVelocity, Vector2.zero, t);
                    yield return null;
                }
                
                rb.linearVelocity = Vector2.zero;
                Debug.Log("[PlayerDamageEffects] Reset knockback after duration");
            }
        }

        /// <summary>
        /// Sets the position of the damage source to calculate effect direction
        /// </summary>
        /// <param name="sourcePosition">Position of the damage source</param>
        public void SetDamageSourcePosition(Vector2 sourcePosition)
        {
            lastDamageSourcePosition = sourcePosition;
            Debug.Log($"[PlayerDamageEffects] Damage source position set to {sourcePosition}");
        }
    }
} 