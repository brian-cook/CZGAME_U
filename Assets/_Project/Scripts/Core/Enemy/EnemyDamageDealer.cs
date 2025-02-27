using UnityEngine;
using CZ.Core.Interfaces;
using NaughtyAttributes;
using CZ.Core.Logging;
using System.Linq;

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
        [InfoBox("Type of damage to apply", EInfoBoxType.Normal)]
        private DamageType damageType = DamageType.Normal;
        
        [BoxGroup("Damage Settings")]
        [SerializeField, Range(0.1f, 5f)]
        [InfoBox("Cooldown between damage applications (seconds)", EInfoBoxType.Normal)]
        private float damageCooldown = 1f;
        
        [BoxGroup("Damage Settings")]
        [SerializeField]
        [Layer]
        [InfoBox("Which layers this enemy can damage", EInfoBoxType.Normal)]
        private LayerMask damageableLayers = -1; // Default to everything
        
        [BoxGroup("Collision Settings")]
        [SerializeField]
        [InfoBox("Whether to use a separate damage trigger collider", EInfoBoxType.Normal)]
        private bool useSeparateTriggerCollider = true;
        
        [BoxGroup("Debug Settings")]
        [SerializeField]
        private bool enableDebugLogs = true;
        
        private float lastDamageTime;
        private CircleCollider2D damageTriggerCollider;
        private Collider2D physicsCollider;
        private BaseEnemy ownEnemyComponent;
        
        private void Awake()
        {
            // Cache own enemy component
            ownEnemyComponent = GetComponent<BaseEnemy>();
            
            // Find existing physics collider
            physicsCollider = GetComponent<Collider2D>();
            if (physicsCollider == null)
            {
                Debug.LogError("[EnemyDamageDealer] No Collider2D found on GameObject!");
                return;
            }
            
            // IMPORTANT: Do not modify the physics collider's isTrigger property
            // This ensures physical collisions continue to work properly
            
            if (useSeparateTriggerCollider)
            {
                // Create a separate trigger collider for damage detection if it doesn't exist
                damageTriggerCollider = GetComponents<CircleCollider2D>()
                    .FirstOrDefault(c => c != physicsCollider);
                
                if (damageTriggerCollider == null)
                {
                    damageTriggerCollider = gameObject.AddComponent<CircleCollider2D>();
                    
                    // Match size of the main collider if possible
                    if (physicsCollider is CircleCollider2D physicsCircleCollider)
                    {
                        damageTriggerCollider.radius = physicsCircleCollider.radius;
                    }
                    else
                    {
                        // Fallback for non-circle colliders
                        damageTriggerCollider.radius = 0.5f;
                    }
                }
                
                // Mark this as a trigger collider
                damageTriggerCollider.isTrigger = true;
                
                if (enableDebugLogs)
                {
                    CZLogger.LogInfo($"[EnemyDamageDealer] Created separate trigger collider for damage", LogCategory.Enemy);
                }
                
                // CRITICAL: Make sure the physics collider is not a trigger
                if (physicsCollider.isTrigger)
                {
                    physicsCollider.isTrigger = false;
                    CZLogger.LogWarning($"[EnemyDamageDealer] Fixed physics collider that was incorrectly set as trigger", LogCategory.Enemy);
                }
            }
            
            lastDamageTime = -damageCooldown; // Allow immediate first damage
            
            if (enableDebugLogs)
            {
                CZLogger.LogInfo($"[EnemyDamageDealer] Initialized on {gameObject.name} with damage: {damageAmount}, type: {damageType}, useSeparateTrigger: {useSeparateTriggerCollider}", LogCategory.Enemy);
            }
        }
        
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (useSeparateTriggerCollider) return; // Skip if using separate trigger
            
            CZLogger.LogDebug($"[EnemyDamageDealer] OnCollisionEnter2D with {collision.gameObject.name}", LogCategory.Enemy);
            TryDealDamage(collision.gameObject);
        }
        
        private void OnCollisionStay2D(Collision2D collision)
        {
            if (useSeparateTriggerCollider) return; // Skip if using separate trigger
            
            TryDealDamage(collision.gameObject);
        }
        
        private void OnTriggerEnter2D(Collider2D collider)
        {
            // Always process trigger events, as they could be from either the main collider 
            // (if we're not using a separate trigger) or the damage trigger collider
            
            CZLogger.LogDebug($"[EnemyDamageDealer] OnTriggerEnter2D with {collider.gameObject.name}", LogCategory.Enemy);
            TryDealDamage(collider.gameObject);
        }
        
        private void OnTriggerStay2D(Collider2D collider)
        {
            // Always process trigger events, as they could be from either the main collider 
            // (if we're not using a separate trigger) or the damage trigger collider
            
            TryDealDamage(collider.gameObject);
        }
        
        private void TryDealDamage(GameObject target)
        {
            // Never process null targets
            if (target == null) return;
            
            // Check if the cooldown has elapsed
            if (Time.time < lastDamageTime + damageCooldown)
            {
                return;
            }
            
            // Check if we're allowed to damage this layer
            if (!LayerInMask(target.layer, damageableLayers))
            {
                return;
            }
            
            // IMPORTANT: Skip damage to other enemies
            // Check if the target is another enemy (on the Enemy layer or has BaseEnemy component)
            if (target.layer == LayerMask.NameToLayer("Enemy") || target.GetComponent<BaseEnemy>() != null)
            {
                if (enableDebugLogs)
                {
                    CZLogger.LogDebug($"[EnemyDamageDealer] Ignoring damage to another enemy: {target.name}", LogCategory.Enemy);
                }
                return;
            }
            
            // Try to get damageable component
            IDamageable damageable = target.GetComponent<IDamageable>();
            if (damageable != null)
            {
                // Apply damage using the correct method signature from IDamageable interface
                damageable.TakeDamage(damageAmount, damageType);
                
                // Update last damage time and log
                lastDamageTime = Time.time;
                if (enableDebugLogs)
                {
                    CZLogger.LogInfo($"[EnemyDamageDealer] Dealt {damageAmount} {damageType} damage to {target.name}", LogCategory.Enemy);
                }
            }
        }
        
        private bool LayerInMask(int layer, LayerMask mask)
        {
            return ((1 << layer) & mask) != 0;
        }
        
        private void OnValidate()
        {
            // When useSeparateTriggerCollider is turned off in the editor,
            // we need to find and potentially remove the extra collider
            if (!Application.isPlaying && !useSeparateTriggerCollider)
            {
                // This only runs in the editor, not during gameplay
                CircleCollider2D[] colliders = GetComponents<CircleCollider2D>();
                if (colliders.Length > 1)
                {
                    Debug.LogWarning("[EnemyDamageDealer] Multiple colliders found. Consider removing excess colliders if not using separate trigger.");
                }
            }
        }
    }
} 