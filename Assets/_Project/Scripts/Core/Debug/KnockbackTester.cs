using UnityEngine;
using CZ.Core.Player;
using CZ.Core.Interfaces;
using CZ.Core.Logging;
using NaughtyAttributes;

namespace CZ.Core.Debug
{
    /// <summary>
    /// Debug tool to test player knockback functionality
    /// </summary>
    [AddComponentMenu("CZ/Debug/Knockback Tester")]
    public class KnockbackTester : MonoBehaviour
    {
        [BoxGroup("Test Settings")]
        [SerializeField]
        [InfoBox("Player to test knockback on", EInfoBoxType.Normal)]
        private PlayerHealth playerHealth;

        [BoxGroup("Test Settings")]
        [SerializeField]
        [InfoBox("Damage amount to apply", EInfoBoxType.Normal)]
        [MinValue(1), MaxValue(50)]
        private int damageAmount = 10;

        [BoxGroup("Test Settings")]
        [SerializeField]
        [InfoBox("Type of damage to apply", EInfoBoxType.Normal)]
        private DamageType damageType = DamageType.Normal;

        [BoxGroup("Test Settings")]
        [SerializeField]
        [InfoBox("Direction to apply knockback from", EInfoBoxType.Normal)]
        private Vector2 knockbackDirection = Vector2.right;

        private void Start()
        {
            // Find player if not assigned
            if (playerHealth == null)
            {
                var player = Object.FindFirstObjectByType<PlayerController>();
                if (player != null)
                {
                    playerHealth = player.GetComponent<PlayerHealth>();
                }
            }
        }

        [Button("Apply Normal Damage")]
        private void ApplyNormalDamage()
        {
            if (playerHealth == null)
            {
                CZLogger.LogError("[KnockbackTester] No player health component assigned!", LogCategory.Debug);
                return;
            }

            // Set position for knockback direction
            var hitEffects = playerHealth.GetComponent<IHitEffects>();
            if (hitEffects != null)
            {
                Vector2 playerPos = playerHealth.transform.position;
                Vector2 sourcePos = playerPos - knockbackDirection.normalized * 2f;
                hitEffects.SetDamageSourcePosition(sourcePos);
                CZLogger.LogInfo($"[KnockbackTester] Set damage source position to {sourcePos}", LogCategory.Debug);
            }

            // Apply damage
            playerHealth.TakeDamage(damageAmount, DamageType.Normal);
            CZLogger.LogInfo($"[KnockbackTester] Applied {damageAmount} normal damage to player", LogCategory.Debug);
        }

        [Button("Apply Critical Damage")]
        private void ApplyCriticalDamage()
        {
            if (playerHealth == null)
            {
                CZLogger.LogError("[KnockbackTester] No player health component assigned!", LogCategory.Debug);
                return;
            }

            // Set position for knockback direction
            var hitEffects = playerHealth.GetComponent<IHitEffects>();
            if (hitEffects != null)
            {
                Vector2 playerPos = playerHealth.transform.position;
                Vector2 sourcePos = playerPos - knockbackDirection.normalized * 2f;
                hitEffects.SetDamageSourcePosition(sourcePos);
                CZLogger.LogInfo($"[KnockbackTester] Set damage source position to {sourcePos}", LogCategory.Debug);
            }

            // Apply critical damage (double damage and stronger knockback)
            playerHealth.TakeDamage(damageAmount * 2, DamageType.Critical);
            CZLogger.LogInfo($"[KnockbackTester] Applied {damageAmount * 2} critical damage to player", LogCategory.Debug);
        }

        [Button("Test Knockback From Random Direction")]
        private void TestRandomKnockback()
        {
            if (playerHealth == null)
            {
                CZLogger.LogError("[KnockbackTester] No player health component assigned!", LogCategory.Debug);
                return;
            }

            // Generate random direction
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            
            // Set position for knockback direction
            var hitEffects = playerHealth.GetComponent<IHitEffects>();
            if (hitEffects != null)
            {
                Vector2 playerPos = playerHealth.transform.position;
                Vector2 sourcePos = playerPos - randomDir * 2f;
                hitEffects.SetDamageSourcePosition(sourcePos);
                CZLogger.LogInfo($"[KnockbackTester] Set random damage source position to {sourcePos}", LogCategory.Debug);
            }

            // Apply damage
            playerHealth.TakeDamage(damageAmount, damageType);
            CZLogger.LogInfo($"[KnockbackTester] Applied {damageAmount} {damageType} damage to player from random direction", LogCategory.Debug);
        }
    }
} 