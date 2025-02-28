using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using NaughtyAttributes;
using Unity.Profiling;
using CZ.Core.Interfaces;
using CZ.Core.Logging;
// No direct VFX namespace import to avoid circular dependency

namespace CZ.Core.Player
{
    /// <summary>
    /// Handles player health, damage processing, and death state
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerHealth : MonoBehaviour, IDamageable, IHasHealthEvents
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
        private IHitEffects hitEffects; // Use interface instead of concrete implementation
        private Vector2 lastDamageSourcePosition;
        private static readonly ProfilerMarker s_HealthSystemMarker = new ProfilerMarker("PlayerHealth.System");
        #endregion

        #region Events
        /// <summary>
        /// Event fired when player takes damage
        /// </summary>
        public event Action<int, int> OnDamaged; // damage amount, current health

        /// <summary>
        /// Event fired when player takes damage (with damage type)
        /// </summary>
        public event Action<int, int, DamageType> OnDamagedWithType; // damage amount, current health, damage type

        /// <summary>
        /// Event fired when player health changes
        /// </summary>
        public event Action<int, int> OnHealthChanged; // current health, max health

        /// <summary>
        /// Event fired when player dies
        /// </summary>
        public event Action OnDeath;

        /// <summary>
        /// Event fired when player respawns
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
        /// Whether the player is currently invulnerable to damage
        /// </summary>
        public bool IsInvulnerable => isInvulnerable;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            InitializeComponents();
            InitializeHealth();
        }

        private void Start()
        {
            // Nothing needed in Start as initialization is done in Awake
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
            // No need to re-initialize components as we only reset health values
            if (currentHealth <= 0 && isInitialized)
            {
                InitializeHealth();
            }
        }

        private void OnDisable()
        {
            // Reset state
            isInvulnerable = false;
            invulnerabilityTimer = 0f;
            damageFlashTimer = 0f;
            isDying = false;

            // Reset sprite if it exists
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

            // Get player controller
            playerController = GetComponent<PlayerController>();
            if (playerController == null)
            {
                Debug.LogError("[PlayerHealth] PlayerController component not found!");
                playerController = gameObject.AddComponent<PlayerController>();
            }

            // Get sprite renderer
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                // Try finding sprite renderer in children
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
                
                if (spriteRenderer == null)
                {
                    Debug.LogWarning("[PlayerHealth] SpriteRenderer not found on this GameObject or its children. Damage flash effect will not work.");
                }
                else
                {
                    if (enableDebugLogs)
                    {
                        Debug.Log("[PlayerHealth] Found SpriteRenderer on child GameObject.");
                    }
                }
            }

            if (spriteRenderer != null)
            {
                originalColor = spriteRenderer.color;
            }

            // Get hit effects component using interface
            hitEffects = GetComponent<IHitEffects>();
            if (hitEffects == null)
            {
                // Try finding in children
                var childHitEffects = GetComponentInChildren<IHitEffects>();
                if (childHitEffects != null)
                {
                    hitEffects = childHitEffects;
                    if (enableDebugLogs)
                    {
                        Debug.Log("[PlayerHealth] Found IHitEffects component on child GameObject.");
                    }
                }
                else
                {
                    Debug.LogWarning("[PlayerHealth] No IHitEffects component found. Hit effects will be disabled.");
                }
            }

            isInitialized = true;
            if (enableDebugLogs)
            {
                Debug.Log("[PlayerHealth] Components initialized successfully");
            }
        }

        private void InitializeHealth()
        {
            currentHealth = initialHealth;
            isInvulnerable = false;
            invulnerabilityTimer = 0f;
            damageFlashTimer = 0f;
            isDying = false;

            // Reset sprite color
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerHealth] Health initialized to {currentHealth}/{maxHealth}");
            }
        }
        #endregion

        #region Damage Handling
        public void TakeDamage(int damage)
        {
            TakeDamage(damage, DamageType.Normal);
        }

        public void TakeDamage(int damage, DamageType damageType)
        {
            if (isDying || isInvulnerable || damage <= 0) return;

            // Apply any damage modifiers
            int actualDamage = CalculateDamage(damage, damageType);
            
            // Apply damage
            currentHealth = Mathf.Max(0, currentHealth - actualDamage);
            
            // Set damage source position for hit effects
            Vector2 damageSourcePos = GetDamageSourcePosition();
            lastDamageSourcePosition = damageSourcePos;
            
            // Apply hit effects if available
            if (hitEffects != null)
            {
                hitEffects.SetDamageSourcePosition(damageSourcePos);
            }
            
            // Start invulnerability period
            isInvulnerable = true;
            invulnerabilityTimer = invulnerabilityDuration;
            
            // Start damage flash effect
            damageFlashTimer = damageFlashDuration;
            
            // Invoke events
            OnDamaged?.Invoke(actualDamage, currentHealth);
            OnDamagedWithType?.Invoke(actualDamage, currentHealth, damageType);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerHealth] Took {actualDamage} damage ({damageType}). Current health: {currentHealth}/{maxHealth}");
            }
            
            // Check for death
            if (currentHealth <= 0 && !isDying)
            {
                StartCoroutine(HandleDeath());
            }
        }

        private Vector2 GetDamageSourcePosition()
        {
            // Use last damage source position if available
            if (lastDamageSourcePosition != Vector2.zero)
            {
                return lastDamageSourcePosition;
            }
            
            // Find closest enemy if possible
            var enemies = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .OfType<IDamageable>()
                .Where(e => e != (object)this && e is MonoBehaviour mb && mb.gameObject.activeInHierarchy)
                .Select(e => (e as MonoBehaviour).transform.position)
                .ToArray();
            
            if (enemies.Length > 0)
            {
                // Find closest enemy
                Vector2 playerPos = transform.position;
                Vector2 closestPos = enemies
                    .OrderBy(pos => Vector2.Distance(playerPos, pos))
                    .FirstOrDefault();
                
                return closestPos;
            }
            
            // Fallback to random direction
            Vector2 randomDir = UnityEngine.Random.insideUnitCircle.normalized;
            return (Vector2)transform.position + randomDir * 2f;
        }

        private int CalculateDamage(int baseDamage, DamageType damageType)
        {
            // Apply any damage modifiers based on damage type
            switch (damageType)
            {
                case DamageType.Critical:
                    // Critical damage is multiplied by 2
                    return baseDamage * 2;
                    
                case DamageType.Environmental:
                    // Environmental damage bypasses armor (if implemented)
                    return baseDamage;
                    
                case DamageType.DoT:
                    // DoT might be reduced slightly
                    return Mathf.Max(1, Mathf.FloorToInt(baseDamage * 0.8f));
                    
                case DamageType.Normal:
                default:
                    return baseDamage;
            }
        }
        #endregion

        #region Healing
        public void Heal(int amount)
        {
            if (isDying || amount <= 0) return;
            
            // Apply healing
            int previousHealth = currentHealth;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            
            // Only invoke events if health actually changed
            if (currentHealth != previousHealth)
            {
                OnHealthChanged?.Invoke(currentHealth, maxHealth);
                
                if (enableDebugLogs)
                {
                    Debug.Log($"[PlayerHealth] Healed {amount} health. Current health: {currentHealth}/{maxHealth}");
                }
            }
            else if (enableDebugLogs)
            {
                Debug.Log($"[PlayerHealth] Healing had no effect. Already at max health: {currentHealth}/{maxHealth}");
            }
        }

        public void RestoreFullHealth()
        {
            if (isDying) return;
            
            // Store previous health to check if it changed
            int previousHealth = currentHealth;
            
            // Set health to max
            currentHealth = maxHealth;
            
            // Only invoke events if health actually changed
            if (currentHealth != previousHealth)
            {
                OnHealthChanged?.Invoke(currentHealth, maxHealth);
                
                if (enableDebugLogs)
                {
                    Debug.Log($"[PlayerHealth] Restored to full health: {currentHealth}/{maxHealth}");
                }
            }
        }
        #endregion

        #region Status Effects
        private void UpdateInvulnerabilityState()
        {
            if (!isInvulnerable) return;
            
            // Decrease timer
            invulnerabilityTimer -= Time.deltaTime;
            
            // Check if invulnerability has expired
            if (invulnerabilityTimer <= 0f)
            {
                isInvulnerable = false;
                invulnerabilityTimer = 0f;
                
                if (enableDebugLogs)
                {
                    Debug.Log("[PlayerHealth] Invulnerability period ended");
                }
            }
        }

        private void UpdateDamageFlash()
        {
            if (spriteRenderer == null || damageFlashTimer <= 0f) return;
            
            // Decrease timer
            damageFlashTimer -= Time.deltaTime;
            
            // Calculate flash intensity using sine wave for smoother effect
            float flashIntensity = Mathf.Clamp01(damageFlashTimer / damageFlashDuration);
            flashIntensity *= Mathf.Sin(flashIntensity * Mathf.PI); // Sine curve for smoother falloff
            
            // Apply flash color with intensity
            spriteRenderer.color = Color.Lerp(originalColor, damageFlashColor, flashIntensity);
            
            // Reset color when effect ends
            if (damageFlashTimer <= 0f)
            {
                spriteRenderer.color = originalColor;
                
                if (enableDebugLogs)
                {
                    Debug.Log("[PlayerHealth] Damage flash effect ended");
                }
            }
        }
        #endregion

        #region Death and Respawn
        private IEnumerator HandleDeath()
        {
            if (isDying) yield break;
            
            isDying = true;
            currentHealth = 0;
            
            // Invoke death event first so listeners can prepare
            OnDeath?.Invoke();
            
            if (enableDebugLogs)
            {
                Debug.Log("[PlayerHealth] Player has died, starting death sequence");
            }
            
            // Play death effects/animation here
            if (spriteRenderer != null)
            {
                // Fade out sprite
                float elapsedTime = 0f;
                Color startColor = spriteRenderer.color;
                Color targetColor = new Color(startColor.r, startColor.g, startColor.b, 0f);
                
                while (elapsedTime < deathDuration)
                {
                    float t = elapsedTime / deathDuration;
                    spriteRenderer.color = Color.Lerp(startColor, targetColor, t);
                    
                    elapsedTime += Time.deltaTime;
                    yield return null;
                }
                
                // Ensure we end at fully transparent
                spriteRenderer.color = targetColor;
            }
            else
            {
                // If no sprite renderer, just wait for death duration
                yield return new WaitForSeconds(deathDuration);
            }
            
            // Disable GameObject temporarily (until respawn)
            gameObject.SetActive(false);
            
            if (enableDebugLogs)
            {
                Debug.Log("[PlayerHealth] Death sequence completed");
            }
        }

        public void Respawn()
        {
            if (!IsDead)
            {
                Debug.LogWarning("[PlayerHealth] Cannot respawn player that is not dead");
                return;
            }
            
            // Re-enable GameObject if it was disabled
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
            
            // Reset health and state
            isDying = false;
            InitializeHealth();
            
            // Reset sprite if it exists
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }
            
            // Invoke respawn event
            OnRespawn?.Invoke();
            
            if (enableDebugLogs)
            {
                Debug.Log("[PlayerHealth] Player respawned successfully");
            }
        }
        #endregion

        #region Debug Methods
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
            TakeDamage(99999);
        }

        [Button("Restore Full Health")]
        private void DebugRestoreHealth()
        {
            RestoreFullHealth();
        }
        #endregion
    }
} 