using UnityEngine;
using CZ.Core.Pooling;
using Unity.Profiling;
using NaughtyAttributes;
using CZ.Core.Interfaces;
using CZ.Core.Resource;
using CZ.Core.Logging;

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
        private static Material sharedMaterial;
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
        [SerializeField, MinValue(1)]
        [InfoBox("Minimum experience points dropped", EInfoBoxType.Normal)]
        private int minExperienceDrop = 1;
        
        [SerializeField, MinValue(1)]
        [InfoBox("Maximum experience points dropped", EInfoBoxType.Normal)]
        private int maxExperienceDrop = 1;
        
        [SerializeField, Range(0, 100)]
        [InfoBox("Chance to drop experience (0-100%)", EInfoBoxType.Normal)]
        private float experienceDropChance = 0;
        
        [SerializeField, MinValue(1)]
        [InfoBox("Amount of health to drop", EInfoBoxType.Normal)]
        private int healthDropValue = 1;
        
        [SerializeField, Range(0, 100)]
        [InfoBox("Chance to drop health (0-100%)", EInfoBoxType.Normal)]
        private float healthDropChance = 0;
        
        [SerializeField, MinValue(1)]
        [InfoBox("Amount of power up to drop", EInfoBoxType.Normal)]
        private int powerUpDropValue = 1;
        
        [SerializeField, Range(0, 100)]
        [InfoBox("Chance to drop power up (0-100%)", EInfoBoxType.Normal)]
        private float powerUpDropChance = 0;
        
        [SerializeField, MinValue(1)]
        [InfoBox("Amount of currency to drop", EInfoBoxType.Normal)]
        private int currencyDropValue = 1;
        
        [SerializeField, Range(0, 100)]
        [InfoBox("Chance to drop currency (0-100%)", EInfoBoxType.Normal)]
        private float currencyDropChance = 0;
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
                originalColor = Color.white; // Default color
                if (sharedMaterial == null)
                {
                    sharedMaterial = new Material(Shader.Find("Sprites/Default"))
                    {
                        hideFlags = HideFlags.DontSave
                    };
                }
                materialInstance = new Material(sharedMaterial);
                materialInstance.hideFlags = HideFlags.DontSave;
                spriteRenderer.material = materialInstance;
                spriteRenderer.color = originalColor;
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
                CZLogger.LogInfo($"Configured collider - Radius: {circleCollider.radius:F2}, IsTrigger: {useTriggerCollider}", LogCategory.Enemy);
                #endif
            }

            isInitialized = true;
            CZLogger.LogInfo($"Initialized enemy: {gameObject.name}", LogCategory.Enemy);
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
                    CZLogger.LogWarning($"Using stale target data. Age: {targetAge:F2}s, Speed: {rb.linearVelocity.magnitude:F2}", LogCategory.Enemy);
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
                CZLogger.LogError("Cannot set target - enemy not initialized!", LogCategory.Enemy);
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
                CZLogger.LogDebug($"Target updated to: {position}, Distance: {((Vector2)position - rb.position).magnitude:F2}", LogCategory.Enemy);
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
            CZLogger.LogDebug($"Took {damage} damage. Current health: {currentHealth}/{maxHealth}", LogCategory.Enemy);
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
            CZLogger.LogInfo($"Starting death sequence for: {gameObject.name}", LogCategory.Enemy);
            #endif
        }

        private void CompleteDeathSequence()
        {
            try
            {
                Vector3 spawnPosition = transform.position;
                bool resourcesSpawned = false;

                // Only attempt to spawn resources if ResourceManager is properly initialized
                if (ResourceManager.Instance != null)
                {
                    try
                    {
                        // Spawn experience with validation and offset
                        float expRoll = Random.value * 100f;
                        if (expRoll <= experienceDropChance)
                        {
                            int experienceDrop = Random.Range(minExperienceDrop, maxExperienceDrop + 1);
                            Vector3 expPosition = spawnPosition + new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f), 0);
                            var expResource = ResourceManager.Instance.SpawnResource(ResourceType.Experience, expPosition);
                            if (expResource != null)
                            {
                                expResource.SetResourceValue(experienceDrop);
                                resourcesSpawned = true;
                                CZLogger.LogDebug($"Spawned experience resource at {expPosition} with value {experienceDrop}", LogCategory.Enemy);
                            }
                        }

                        // Random chance for health drop with validation and offset
                        float healthRoll = Random.value * 100f;
                        if (healthRoll <= healthDropChance)
                        {
                            Vector3 healthPosition = spawnPosition + new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f), 0);
                            var healthResource = ResourceManager.Instance.SpawnResource(ResourceType.Health, healthPosition);
                            if (healthResource != null)
                            {
                                healthResource.SetResourceValue(healthDropValue);
                                resourcesSpawned = true;
                                CZLogger.LogDebug($"Spawned health resource at {healthPosition} with value {healthDropValue}", LogCategory.Enemy);
                            }
                        }

                        // Random chance for power-up with validation and offset
                        float powerUpRoll = Random.value * 100f;
                        if (powerUpRoll <= powerUpDropChance)
                        {
                            Vector3 powerUpPosition = spawnPosition + new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f), 0);
                            var powerUpResource = ResourceManager.Instance.SpawnResource(ResourceType.PowerUp, powerUpPosition);
                            if (powerUpResource != null)
                            {
                                powerUpResource.SetResourceValue(powerUpDropValue);
                                resourcesSpawned = true;
                                CZLogger.LogDebug($"Spawned power-up resource at {powerUpPosition} with value {powerUpDropValue}", LogCategory.Enemy);
                            }
                        }

                        // Random chance for currency with validation and offset
                        float currencyRoll = Random.value * 100f;
                        if (currencyRoll <= currencyDropChance)
                        {
                            Vector3 currencyPosition = spawnPosition + new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f), 0);
                            var currencyResource = ResourceManager.Instance.SpawnResource(ResourceType.Currency, currencyPosition);
                            if (currencyResource != null)
                            {
                                currencyResource.SetResourceValue(currencyDropValue);
                                resourcesSpawned = true;
                                CZLogger.LogDebug($"Spawned currency resource at {currencyPosition} with value {currencyDropValue}", LogCategory.Enemy);
                            }
                        }

                        CZLogger.LogDebug($"Death sequence resource spawning complete. Resources spawned: {resourcesSpawned}", LogCategory.Enemy);
                    }
                    catch (System.Exception e)
                    {
                        CZLogger.LogError($"Error spawning resources: {e.Message}\nStack trace: {e.StackTrace}", LogCategory.Enemy);
                    }
                }
                else
                {
                    CZLogger.LogError("ResourceManager not available for spawning resources", LogCategory.Enemy);
                }
            }
            finally
            {
                // Always attempt to return to pool
                ReturnToPool();
            }
        }

        public void OnSpawn()
        {
            CZLogger.LogDebug($"Enemy spawned: {gameObject.name}", LogCategory.Enemy);
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
                // Ensure material is properly set
                if (materialInstance == null)
                {
                    if (sharedMaterial == null)
                    {
                        sharedMaterial = new Material(Shader.Find("Sprites/Default"))
                        {
                            hideFlags = HideFlags.DontSave
                        };
                    }
                    materialInstance = new Material(sharedMaterial);
                    materialInstance.hideFlags = HideFlags.DontSave;
                }
                spriteRenderer.material = materialInstance;
                spriteRenderer.color = originalColor;
            }
            
            gameObject.SetActive(true);
            CZLogger.LogInfo($"Enemy spawned: {gameObject.name}", LogCategory.Enemy);
        }

        public void OnDespawn()
        {
            CZLogger.LogDebug($"Enemy despawning: {gameObject.name}", LogCategory.Enemy);
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
            CZLogger.LogInfo($"Enemy despawned: {gameObject.name}", LogCategory.Enemy);
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

        private void ReturnToPool()
        {
            try
            {
                if (PoolManager.Instance != null)
                {
                    var pool = PoolManager.Instance.GetPool<BaseEnemy>();
                    if (pool != null)
                    {
                        pool.Return(this);
                        CZLogger.LogInfo($"Enemy despawned: {gameObject.name}", LogCategory.Enemy);
                    }
                    else
                    {
                        CZLogger.LogError($"Failed to return to pool - pool not found for: {gameObject.name}", LogCategory.Enemy);
                        gameObject.SetActive(false);
                    }
                }
                else
                {
                    CZLogger.LogError("PoolManager not available for returning to pool", LogCategory.Enemy);
                    gameObject.SetActive(false);
                }
            }
            catch (System.Exception e)
            {
                CZLogger.LogError($"Error returning to pool: {e.Message}\nStack trace: {e.StackTrace}", LogCategory.Enemy);
                gameObject.SetActive(false);
            }
        }
    }
} 