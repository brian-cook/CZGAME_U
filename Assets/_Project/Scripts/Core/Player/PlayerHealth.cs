using System;
using System.Collections;
using UnityEngine;
using NaughtyAttributes;
using Unity.Profiling;
using CZ.Core.Interfaces;
using CZ.Core.Logging;

namespace CZ.Core.Player
{
    /// <summary>
    /// Handles player health, damage processing, and death state
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        #region Configuration
        [BoxGroup("Health Settings")]
        [SerializeField, MinValue(1)]
        [InfoBox("Maximum health of the player", EInfoBoxType.Normal)]
        private int maxHealth = 100;

        [BoxGroup("Health Settings")]
        [SerializeField, MinValue(1)]
        [InfoBox("Initial health when spawned", EInfoBoxType.Normal)]
        private int initialHealth = 100;

        [BoxGroup("Damage Settings")]
        [SerializeField, MinValue(0f), MaxValue(2f)]
        [InfoBox("Duration of invulnerability after taking damage", EInfoBoxType.Normal)]
        private float invulnerabilityDuration = 0.5f;

        [BoxGroup("Damage Settings")]
        [SerializeField, MinValue(0f), MaxValue(2f)]
        [InfoBox("Duration of damage flash effect", EInfoBoxType.Normal)]
        private float damageFlashDuration = 0.2f;

        [BoxGroup("Damage Settings")]
        [SerializeField]
        [InfoBox("Color to flash when taking damage", EInfoBoxType.Normal)]
        private Color damageFlashColor = Color.red;

        [BoxGroup("Death Settings")]
        [SerializeField, MinValue(0f), MaxValue(5f)]
        [InfoBox("Duration of death sequence", EInfoBoxType.Normal)]
        private float deathDuration = 1.5f;

        [BoxGroup("Debug Settings")]
        [SerializeField]
        private bool enableDebugLogs = false;
        #endregion

        #region State
        private int currentHealth;
        private bool isInvulnerable;
        private float invulnerabilityTimer;
        private float damageFlashTimer;
        private bool isDying;
        private bool isInitialized;
        private SpriteRenderer spriteRenderer;
        private Color originalColor;
        private PlayerController playerController;
        private static readonly ProfilerMarker s_HealthSystemMarker = new ProfilerMarker("PlayerHealth.System");
        #endregion

        #region Events
        /// <summary>
        /// Event triggered when player takes damage
        /// </summary>
        public event Action<int, int> OnDamaged; // damage amount, current health

        /// <summary>
        /// Event triggered when player health changes
        /// </summary>
        public event Action<int, int> OnHealthChanged; // current health, max health

        /// <summary>
        /// Event triggered when player dies
        /// </summary>
        public event Action OnDeath;

        /// <summary>
        /// Event triggered when player respawns
        /// </summary>
        public event Action OnRespawn;
        #endregion

        #region Properties
        /// <summary>
        /// Current health value
        /// </summary>
        public int CurrentHealth => currentHealth;

        /// <summary>
        /// Maximum health value
        /// </summary>
        public int MaxHealth => maxHealth;

        /// <summary>
        /// Health percentage (0-1 range)
        /// </summary>
        public float HealthPercentage => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;

        /// <summary>
        /// Whether the player is dead (health <= 0)
        /// </summary>
        public bool IsDead => currentHealth <= 0;

        /// <summary>
        /// Whether the player is currently invulnerable
        /// </summary>
        public bool IsInvulnerable => isInvulnerable;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            InitializeComponents();
        }

        private void Start()
        {
            InitializeHealth();
        }

        private void Update()
        {
            using (s_HealthSystemMarker.Auto())
            {
                UpdateInvulnerabilityState();
                UpdateDamageFlash();
            }
        }

        private void OnEnable()
        {
            if (!isInitialized)
            {
                InitializeComponents();
            }
        }

        private void OnDisable()
        {
            // Reset state when disabled
            isInvulnerable = false;
            invulnerabilityTimer = 0f;
            damageFlashTimer = 0f;
            
            // Reset sprite color
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }
        }
        #endregion

        #region Initialization
        private void InitializeComponents()
        {
            if (isInitialized) return;
            
            playerController = GetComponent<PlayerController>();
            if (playerController == null)
            {
                Debug.LogError("[PlayerHealth] PlayerController component not found!");
                return;
            }
            
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
                if (spriteRenderer == null)
                {
                    Debug.LogError("[PlayerHealth] SpriteRenderer component not found!");
                    return;
                }
            }
            
            originalColor = spriteRenderer.color;
            isInitialized = true;
            
            if (enableDebugLogs)
            {
                Debug.Log("[PlayerHealth] Components initialized successfully");
            }
        }

        private void InitializeHealth()
        {
            currentHealth = Mathf.Clamp(initialHealth, 0, maxHealth);
            isInvulnerable = false;
            invulnerabilityTimer = 0f;
            damageFlashTimer = 0f;
            isDying = false;
            
            // Trigger health changed event
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerHealth] Health initialized: {currentHealth}/{maxHealth}");
            }
        }
        #endregion

        #region Health Management
        /// <summary>
        /// Apply damage to the player
        /// </summary>
        /// <param name="damage">Amount of damage to apply</param>
        public void TakeDamage(int damage)
        {
            TakeDamage(damage, DamageType.Normal);
        }

        /// <summary>
        /// Apply damage to the player with damage type
        /// </summary>
        /// <param name="damage">Amount of damage to apply</param>
        /// <param name="damageType">Type of damage being applied</param>
        public void TakeDamage(int damage, DamageType damageType)
        {
            using (s_HealthSystemMarker.Auto())
            {
                // Check if we can take damage
                if (isDying || isInvulnerable || !isInitialized || damage <= 0)
                {
                    return;
                }

                // Apply damage modifiers based on damage type
                int actualDamage = CalculateDamage(damage, damageType);
                
                // Apply damage
                int previousHealth = currentHealth;
                currentHealth = Mathf.Max(0, currentHealth - actualDamage);
                
                // Trigger damage flash
                damageFlashTimer = damageFlashDuration;
                
                // Start invulnerability
                isInvulnerable = true;
                invulnerabilityTimer = invulnerabilityDuration;
                
                // Trigger events
                OnDamaged?.Invoke(actualDamage, currentHealth);
                OnHealthChanged?.Invoke(currentHealth, maxHealth);
                
                if (enableDebugLogs)
                {
                    Debug.Log($"[PlayerHealth] Took {actualDamage} damage ({damageType}). Health: {currentHealth}/{maxHealth}");
                }
                
                // Check for death
                if (currentHealth <= 0 && !isDying)
                {
                    StartCoroutine(HandleDeath());
                }
            }
        }

        /// <summary>
        /// Calculate actual damage based on damage type
        /// </summary>
        private int CalculateDamage(int baseDamage, DamageType damageType)
        {
            switch (damageType)
            {
                case DamageType.Critical:
                    return Mathf.RoundToInt(baseDamage * 1.5f); // Critical hits do 50% more damage
                
                case DamageType.Environmental:
                    return baseDamage; // Environmental damage is not modified
                
                case DamageType.DoT:
                    return baseDamage; // DoT damage is not modified
                
                case DamageType.Normal:
                default:
                    return baseDamage;
            }
        }

        /// <summary>
        /// Heal the player by the specified amount
        /// </summary>
        /// <param name="amount">Amount to heal</param>
        public void Heal(int amount)
        {
            if (amount <= 0 || isDying || !isInitialized)
            {
                return;
            }
            
            int previousHealth = currentHealth;
            currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
            
            // Only trigger event if health actually changed
            if (currentHealth != previousHealth)
            {
                OnHealthChanged?.Invoke(currentHealth, maxHealth);
                
                if (enableDebugLogs)
                {
                    Debug.Log($"[PlayerHealth] Healed for {amount}. Health: {currentHealth}/{maxHealth}");
                }
            }
        }

        /// <summary>
        /// Fully restore player health
        /// </summary>
        public void RestoreFullHealth()
        {
            if (isDying || !isInitialized)
            {
                return;
            }
            
            int previousHealth = currentHealth;
            currentHealth = maxHealth;
            
            // Only trigger event if health actually changed
            if (currentHealth != previousHealth)
            {
                OnHealthChanged?.Invoke(currentHealth, maxHealth);
                
                if (enableDebugLogs)
                {
                    Debug.Log($"[PlayerHealth] Fully restored health: {currentHealth}/{maxHealth}");
                }
            }
        }
        #endregion

        #region State Management
        private void UpdateInvulnerabilityState()
        {
            if (isInvulnerable)
            {
                invulnerabilityTimer -= Time.deltaTime;
                
                // Flash the sprite while invulnerable
                if (spriteRenderer != null)
                {
                    float alpha = Mathf.PingPong(Time.time * 10f, 1f);
                    spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
                }
                
                if (invulnerabilityTimer <= 0f)
                {
                    isInvulnerable = false;
                    
                    // Reset sprite color
                    if (spriteRenderer != null)
                    {
                        spriteRenderer.color = originalColor;
                    }
                }
            }
        }

        private void UpdateDamageFlash()
        {
            if (damageFlashTimer > 0f)
            {
                damageFlashTimer -= Time.deltaTime;
                
                // Apply damage flash color
                if (spriteRenderer != null)
                {
                    float t = damageFlashTimer / damageFlashDuration;
                    spriteRenderer.color = Color.Lerp(originalColor, damageFlashColor, t);
                }
                
                if (damageFlashTimer <= 0f)
                {
                    // Reset sprite color if not invulnerable
                    if (!isInvulnerable && spriteRenderer != null)
                    {
                        spriteRenderer.color = originalColor;
                    }
                }
            }
        }

        private IEnumerator HandleDeath()
        {
            isDying = true;
            
            // Trigger death event
            OnDeath?.Invoke();
            
            if (enableDebugLogs)
            {
                Debug.Log("[PlayerHealth] Player died");
            }
            
            // Disable player input
            if (playerController != null)
            {
                // TODO: Disable player input
            }
            
            // Death animation/effect
            if (spriteRenderer != null)
            {
                // Fade out
                float timer = 0f;
                while (timer < deathDuration)
                {
                    timer += Time.deltaTime;
                    float t = timer / deathDuration;
                    spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 1f - t);
                    yield return null;
                }
                
                // Ensure fully transparent
                spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);
            }
            else
            {
                // Wait for death duration if no sprite renderer
                yield return new WaitForSeconds(deathDuration);
            }
            
            // Notify GameManager of player death
            if (GameManager.Instance != null)
            {
                GameManager.Instance.EndGame();
            }
        }

        /// <summary>
        /// Respawn the player with full health
        /// </summary>
        public void Respawn()
        {
            if (!isDying || !isInitialized)
            {
                return;
            }
            
            isDying = false;
            currentHealth = initialHealth;
            isInvulnerable = true;
            invulnerabilityTimer = invulnerabilityDuration;
            
            // Reset sprite color and visibility
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }
            
            // Trigger events
            OnRespawn?.Invoke();
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerHealth] Player respawned with health: {currentHealth}/{maxHealth}");
            }
        }
        #endregion

        #region Debug
        [Button("Take 10 Damage")]
        private void DebugTakeDamage()
        {
            TakeDamage(10);
        }

        [Button("Take Critical Damage")]
        private void DebugTakeCriticalDamage()
        {
            TakeDamage(20, DamageType.Critical);
        }

        [Button("Heal 10 Health")]
        private void DebugHeal()
        {
            Heal(10);
        }

        [Button("Kill Player")]
        private void DebugKillPlayer()
        {
            TakeDamage(currentHealth);
        }

        [Button("Restore Full Health")]
        private void DebugRestoreHealth()
        {
            RestoreFullHealth();
        }
        #endregion
    }
} 