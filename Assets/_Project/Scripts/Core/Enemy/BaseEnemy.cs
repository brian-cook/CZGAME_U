using UnityEngine;
using CZ.Core.Pooling;
using Unity.Profiling;
using NaughtyAttributes;
using CZ.Core.Interfaces;
using CZ.Core.Resource;

namespace CZ.Core.Enemy
{
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class BaseEnemy : MonoBehaviour, IPoolable, IDamageable
    {
        #region Components
        private SpriteRenderer spriteRenderer;
        private CircleCollider2D circleCollider;
        private Rigidbody2D rb;
        private Material materialInstance;
        #endregion

        #region Configuration
        [Header("Enemy Configuration")]
        [SerializeField, MinValue(1f)] 
        private float moveSpeed = 5f;
        
        [SerializeField, MinValue(0.1f)]
        private float stoppingDistance = 1f;
        
        [SerializeField, MinValue(0.1f)]
        private float velocityLerpSpeed = 8f;
        
        [SerializeField, MinValue(0.1f)]
        private float targetUpdateTimeout = 2f;
        
        [Header("Health Configuration")]
        [SerializeField, MinValue(1)]
        [InfoBox("Initial health of the enemy", EInfoBoxType.Normal)]
        private int maxHealth = 100;

        [SerializeField, MinValue(0f), MaxValue(2f)]
        [InfoBox("Duration of damage flash effect", EInfoBoxType.Normal)]
        private float damageFlashDuration = 0.2f;

        [SerializeField]
        [InfoBox("Color to flash when taking damage", EInfoBoxType.Normal)]
        private Color damageFlashColor = Color.red;

        [Header("Death Configuration")]
        [SerializeField, MinValue(0f), MaxValue(2f)]
        [InfoBox("Duration of death fade effect", EInfoBoxType.Normal)]
        private float deathFadeDuration = 0.5f;

        [Header("Collision Configuration")]
        [SerializeField, MinValue(0.1f), MaxValue(1f)]
        [Tooltip("Radius of the collision circle relative to sprite size")]
        private float collisionRadius = 0.25f;

        [SerializeField]
        [Tooltip("Whether the collider should be a trigger")]
        private bool useTriggerCollider = false;

        [Header("Resource Drop Configuration")]
        [SerializeField, MinValue(0)]
        private int minExperienceDrop = 1;
        
        [SerializeField, MinValue(0)]
        private int maxExperienceDrop = 3;
        
        [SerializeField, Range(0f, 1f)]
        private float healthDropChance = 0.1f;
        
        [SerializeField, Range(0f, 1f)]
        private float powerUpDropChance = 0.05f;
        #endregion

        #region State
        private Vector3 targetPosition;
        private bool isInitialized;
        private Vector3 lastKnownTargetPosition;
        private float lastTargetUpdateTime;
        private bool hasValidTarget;
        private int currentHealth;
        private bool isDying;
        private float deathTimer;
        private float damageFlashTimer;
        private Color originalColor;
        #endregion

        #region Properties
        public GameObject GameObject => gameObject;
        public bool IsDead => currentHealth <= 0;
        public float HealthPercentage => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            InitializeComponents();
        }

        private void FixedUpdate()
        {
            if (!isInitialized) return;
            MoveTowardsTarget();
        }

        private void Update()
        {
            if (!isInitialized) return;

            // Handle damage flash effect
            if (damageFlashTimer > 0)
            {
                damageFlashTimer -= Time.deltaTime;
                float flashIntensity = Mathf.Clamp01(damageFlashTimer / damageFlashDuration);
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = Color.Lerp(originalColor, damageFlashColor, flashIntensity);
                }
            }

            // Handle death effect
            if (isDying)
            {
                deathTimer += Time.deltaTime;
                float fadeProgress = deathTimer / deathFadeDuration;
                
                if (spriteRenderer != null)
                {
                    Color fadeColor = spriteRenderer.color;
                    fadeColor.a = Mathf.Lerp(1f, 0f, fadeProgress);
                    spriteRenderer.color = fadeColor;
                }

                if (fadeProgress >= 1f)
                {
                    CompleteDeathSequence();
                }
            }
        }
        #endregion

        #region Initialization
        private void InitializeComponents()
        {
            if (isInitialized) return;

            // Get required components
            spriteRenderer = GetComponent<SpriteRenderer>();
            circleCollider = GetComponent<CircleCollider2D>();
            rb = GetComponent<Rigidbody2D>();

            // Store original color and create material instance
            if (spriteRenderer != null)
            {
                originalColor = spriteRenderer.color;
                materialInstance = new Material(spriteRenderer.material);
                materialInstance.hideFlags = HideFlags.HideAndDontSave;
                spriteRenderer.material = materialInstance;
            }

            // Configure Rigidbody2D
            if (rb != null)
            {
                rb.gravityScale = 0f;
                rb.linearDamping = 0.5f;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }

            // Configure CircleCollider2D
            if (circleCollider != null)
            {
                // Set collision radius based on sprite size and configuration
                if (spriteRenderer != null)
                {
                    float spriteSize = Mathf.Max(spriteRenderer.sprite.bounds.size.x, spriteRenderer.sprite.bounds.size.y);
                    circleCollider.radius = spriteSize * collisionRadius;
                }
                else
                {
                    circleCollider.radius = collisionRadius;
                }
                
                circleCollider.isTrigger = useTriggerCollider;
                
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[BaseEnemy] Configured collider - Radius: {circleCollider.radius:F2}, IsTrigger: {useTriggerCollider}");
                #endif
            }

            isInitialized = true;
            Debug.Log($"[BaseEnemy] Initialized enemy: {gameObject.name}");
        }
        #endregion

        #region Movement
        private void MoveTowardsTarget()
        {
            if (!isInitialized || !hasValidTarget) return;

            // Calculate distance to target
            Vector2 toTarget = ((Vector2)targetPosition - rb.position);
            float distance = toTarget.magnitude;
            
            // Calculate direction and desired velocity
            Vector2 direction = toTarget.normalized;
            Vector2 targetVelocity = direction * moveSpeed;
            
            // Adjust velocity based on distance to target
            if (distance < stoppingDistance)
            {
                float speedMultiplier = Mathf.Clamp01(distance / stoppingDistance);
                targetVelocity *= speedMultiplier;
            }
            
            // Check if target is stale
            float targetAge = Time.time - lastTargetUpdateTime;
            bool isTargetStale = targetAge > targetUpdateTimeout;
            
            if (isTargetStale)
            {
                // For stale targets, maintain some movement but reduce speed over time
                float stalenessFactor = Mathf.Clamp01(1.0f - (targetAge - targetUpdateTimeout) / targetUpdateTimeout);
                stalenessFactor = Mathf.Max(0.3f, stalenessFactor); // Never go below 30% speed
                targetVelocity *= stalenessFactor;
                
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (rb.linearVelocity.magnitude > 0.1f)
                {
                    Debug.LogWarning($"[BaseEnemy] Using stale target data. Age: {targetAge:F2}s, Speed: {rb.linearVelocity.magnitude:F2}");
                }
                #endif
            }
            
            // Apply movement with improved responsiveness
            float lerpFactor = Time.fixedDeltaTime * velocityLerpSpeed;
            if (isTargetStale)
            {
                // Reduce lerp speed for stale targets to maintain more momentum
                lerpFactor *= 0.5f;
            }
            
            // Apply velocity change
            rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, targetVelocity, lerpFactor);
            
            // Update facing direction when moving
            if (rb.linearVelocity.sqrMagnitude > 0.1f)
            {
                float angle = Mathf.Atan2(rb.linearVelocity.y, rb.linearVelocity.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0, 0, angle);
            }
        }
        #endregion

        #region Public Methods
        public void SetTarget(Vector3 position)
        {
            if (!isInitialized)
            {
                Debug.LogError("[BaseEnemy] Cannot set target - enemy not initialized!");
                return;
            }
            
            // Always update the target time to prevent false staleness
            lastTargetUpdateTime = Time.time;
            
            // Only update position if it's actually different
            if (position != lastKnownTargetPosition || !hasValidTarget)
            {
                targetPosition = position;
                lastKnownTargetPosition = position;
                hasValidTarget = true;
                
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[BaseEnemy] Target updated to: {position}, Distance: {((Vector2)position - rb.position).magnitude:F2}");
                #endif
            }
        }

        public void TakeDamage(int damage)
        {
            if (isDying || !isInitialized) return;

            currentHealth = Mathf.Max(0, currentHealth - damage);
            
            // Trigger damage flash
            damageFlashTimer = damageFlashDuration;
            
            if (currentHealth <= 0 && !isDying)
            {
                StartDeathSequence();
            }
            
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[BaseEnemy] Took {damage} damage. Current health: {currentHealth}/{maxHealth}");
            #endif
        }

        private void StartDeathSequence()
        {
            if (isDying) return;
            
            isDying = true;
            deathTimer = 0f;
            
            // Disable physics and collision
            if (rb != null)
            {
                rb.simulated = false;
            }
            if (circleCollider != null)
            {
                circleCollider.enabled = false;
            }
            
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[BaseEnemy] Starting death sequence for: {gameObject.name}");
            #endif
        }

        private void CompleteDeathSequence()
        {
            if (ResourceManager.Instance != null)
            {
                // Spawn experience
                int experienceDrop = Random.Range(minExperienceDrop, maxExperienceDrop + 1);
                ResourceManager.Instance.SpawnResource(ResourceType.Experience, transform.position, experienceDrop);

                // Random chance for health drop
                if (Random.value < healthDropChance)
                {
                    ResourceManager.Instance.SpawnResource(ResourceType.Health, transform.position);
                }

                // Random chance for power-up
                if (Random.value < powerUpDropChance)
                {
                    ResourceManager.Instance.SpawnResource(ResourceType.PowerUp, transform.position);
                }
            }

            // Return to pool
            var pool = PoolManager.Instance.GetPool<BaseEnemy>();
            if (pool != null)
            {
                pool.Return(this);
            }
            else
            {
                Debug.LogError("[BaseEnemy] Failed to return to pool - pool not found!");
                gameObject.SetActive(false);
            }
        }

        public void OnSpawn()
        {
            if (!isInitialized)
            {
                InitializeComponents();
            }
            
            // Reset state
            currentHealth = maxHealth;
            isDying = false;
            deathTimer = 0f;
            damageFlashTimer = 0f;
            hasValidTarget = false;
            lastTargetUpdateTime = 0f;
            
            // Reset physics
            if (rb != null)
            {
                rb.simulated = true;
                rb.linearVelocity = Vector2.zero;
            }
            
            // Reset collider
            if (circleCollider != null)
            {
                circleCollider.enabled = true;
            }
            
            // Reset visuals
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }
            
            gameObject.SetActive(true);
            Debug.Log($"[BaseEnemy] Enemy spawned: {gameObject.name}");
        }

        public void OnDespawn()
        {
            // Clean up material instance if it exists
            if (materialInstance != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(materialInstance);
                }
                else
                {
                    DestroyImmediate(materialInstance);
                }
                materialInstance = null;
            }
            
            hasValidTarget = false;
            targetPosition = Vector3.zero;
            lastKnownTargetPosition = Vector3.zero;
            isDying = false;
            currentHealth = maxHealth;
            
            if (rb != null)
            {
                rb.simulated = true;
                rb.linearVelocity = Vector2.zero;
            }
            
            if (circleCollider != null)
            {
                circleCollider.enabled = true;
            }
            
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }
            
            gameObject.SetActive(false);
            Debug.Log($"[BaseEnemy] Enemy despawned: {gameObject.name}");
        }
        #endregion

        private void OnDestroy()
        {
            // Clean up material instance
            if (materialInstance != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(materialInstance);
                }
                else
                {
                    DestroyImmediate(materialInstance);
                }
                materialInstance = null;
            }
        }
    }
} 