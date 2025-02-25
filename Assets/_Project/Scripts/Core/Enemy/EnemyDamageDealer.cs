using UnityEngine;
using CZ.Core.Interfaces;
using NaughtyAttributes;

namespace CZ.Core.Enemy
{
    /// <summary>
    /// Component that deals damage to IDamageable entities on collision
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class EnemyDamageDealer : MonoBehaviour
    {
        [BoxGroup("Damage Settings")]
        [SerializeField, MinValue(1)]
        [InfoBox("Amount of damage to deal on collision", EInfoBoxType.Normal)]
        private int damageAmount = 10;
        
        [BoxGroup("Damage Settings")]
        [SerializeField]
        [InfoBox("Type of damage to deal", EInfoBoxType.Normal)]
        private DamageType damageType = DamageType.Normal;
        
        [BoxGroup("Collision Settings")]
        [SerializeField]
        [InfoBox("Whether to use OnTriggerEnter2D instead of OnCollisionEnter2D", EInfoBoxType.Normal)]
        private bool useTrigger = true;
        
        [BoxGroup("Collision Settings")]
        [SerializeField, MinValue(0f)]
        [InfoBox("Cooldown between damage applications (seconds)", EInfoBoxType.Normal)]
        private float damageCooldown = 0.5f;
        
        [BoxGroup("Debug Settings")]
        [SerializeField]
        private bool enableDebugLogs = false;
        
        private float lastDamageTime;
        
        private void Awake()
        {
            // Ensure we have a Collider2D
            Collider2D collider = GetComponent<Collider2D>();
            if (collider == null)
            {
                Debug.LogError("[EnemyDamageDealer] No Collider2D found on GameObject!");
                return;
            }
            
            // Set trigger property based on configuration
            collider.isTrigger = useTrigger;
            
            lastDamageTime = -damageCooldown; // Allow immediate first damage
        }
        
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (useTrigger) return; // Skip if using triggers
            
            TryDealDamage(collision.gameObject);
        }
        
        private void OnCollisionStay2D(Collision2D collision)
        {
            if (useTrigger) return; // Skip if using triggers
            
            TryDealDamage(collision.gameObject);
        }
        
        private void OnTriggerEnter2D(Collider2D collider)
        {
            if (!useTrigger) return; // Skip if using collisions
            
            TryDealDamage(collider.gameObject);
        }
        
        private void OnTriggerStay2D(Collider2D collider)
        {
            if (!useTrigger) return; // Skip if using collisions
            
            TryDealDamage(collider.gameObject);
        }
        
        private void TryDealDamage(GameObject target)
        {
            // Check cooldown
            if (Time.time - lastDamageTime < damageCooldown)
            {
                return;
            }
            
            // Try to get IDamageable
            IDamageable damageable = target.GetComponent<IDamageable>();
            if (damageable == null)
            {
                return;
            }
            
            // Deal damage
            damageable.TakeDamage(damageAmount, damageType);
            lastDamageTime = Time.time;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[EnemyDamageDealer] Dealt {damageAmount} damage to {target.name}");
            }
        }
    }
} 