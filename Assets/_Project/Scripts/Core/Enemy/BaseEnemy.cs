using UnityEngine;
using CZ.Core.Pooling;
using Unity.Profiling;
using NaughtyAttributes;
using CZ.Core.Interfaces;
using CZ.Core.Resource;
using CZ.Core.Logging;
using CZ.Core.Configuration;
using System;

namespace CZ.Core.Enemy
{
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class BaseEnemy : MonoBehaviour, IPoolable, IDamageable
    {
        #region Events
        // Delegate and event for enemy defeat
        public delegate void EnemyDefeatedHandler(BaseEnemy enemy);
        public event EnemyDefeatedHandler OnEnemyDefeated;
        #endregion

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

        [Header("Resource Drop Configuration")]
        [SerializeField, MinValue(1)]
        [InfoBox("Minimum number of experience objects to drop", EInfoBoxType.Normal)]
        private int minExperienceDropCount = 1;
        
        [SerializeField, MinValue(1)]
        [InfoBox("Maximum number of experience objects to drop", EInfoBoxType.Normal)]
        private int maxExperienceDropCount = 1;
        
        [SerializeField, MinValue(1)]
        [InfoBox("Value per experience object", EInfoBoxType.Normal)]
        private int experienceDropValue = 1;
        
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
        
        // IDamageable interface properties
        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        
        // Protected properties for derived classes
        protected Rigidbody2D Rb => rb;
        protected int CurrentHealthValue 
        { 
            get => currentHealth; 
            set => currentHealth = value; 
        }
        protected int MaxHealthValue 
        { 
            get => maxHealth; 
            set => maxHealth = value; 
        }
        protected float MoveSpeedValue 
        { 
            get => moveSpeed; 
            set => moveSpeed = value; 
        }
        protected bool IsDeadValue 
        { 
            get => IsDead; 
            set => isDying = value; 
        }
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // Store original color for flash effect
            if (TryGetComponent(out SpriteRenderer sr))
            {
                originalColor = sr.color;
            }
            else
            {
                originalColor = Color.white;
            }
            
            // Initialize components
            InitializeComponents();
        }

        protected virtual void FixedUpdate()
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
        protected virtual void InitializeComponents()
        {
            if (isInitialized)
            {
                CZLogger.LogDebug("Enemy components already initialized", LogCategory.Enemy);
                return;
            }
            
            try
            {
                // Get required components
                spriteRenderer = GetComponent<SpriteRenderer>();
                circleCollider = GetComponent<CircleCollider2D>();
                rb = GetComponent<Rigidbody2D>();

                // Configure components
                if (circleCollider != null)
                {
                    // CRITICAL: Make sure collider is set up correctly for collision
                    circleCollider.enabled = true;
                    circleCollider.isTrigger = false; // Standard collider, not trigger
                    circleCollider.radius = collisionRadius;
                    
                    // Log collider settings for debugging
                    Debug.Log($"[{GetType().Name}] Collider initialized: Radius={circleCollider.radius}, " +
                              $"IsTrigger={circleCollider.isTrigger}, Enabled={circleCollider.enabled}, " +
                              $"Layer={LayerMask.LayerToName(gameObject.layer)}");
                }

                if (rb != null)
                {
                    rb.gravityScale = 0f;
                    rb.sleepMode = RigidbodySleepMode2D.StartAwake;
                    rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                    rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                    rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                    
                    // Log rigidbody settings for debugging
                    Debug.Log($"[{GetType().Name}] Rigidbody initialized: Type={rb.bodyType}, " +
                              $"Simulated={rb.simulated}, Mass={rb.mass}, " +
                              $"CollisionDetection={rb.collisionDetectionMode}, " +
                              $"Interpolation={rb.interpolation}");
                }

                // Make sure we set the proper layer
                gameObject.layer = LayerMask.NameToLayer("Enemy");

                // Instantiate material if needed for effects
                if (spriteRenderer != null && materialInstance == null)
                {
                    // Create a material instance for this enemy
                    if (sharedMaterial == null)
                    {
                        sharedMaterial = new Material(Shader.Find("Sprites/Default"))
                        {
                            hideFlags = HideFlags.DontSave
                        };
                    }
                    
                    materialInstance = new Material(sharedMaterial);
                    spriteRenderer.material = materialInstance;
                }

                // Ensure material is properly set up
                if (spriteRenderer != null && materialInstance != null)
                {
                    spriteRenderer.material = materialInstance;
                }

                isInitialized = true;
                
                CZLogger.LogInfo($"Enemy {gameObject.name} components initialized successfully", LogCategory.Enemy);
            }
            catch (System.Exception e)
            {
                CZLogger.LogError($"Error initializing enemy components: {e.Message}", LogCategory.Enemy);
                isInitialized = false;
            }
        }
        #endregion

        #region Movement
        private void MoveTowardsTarget()
        {
            if (!isInitialized || !hasValidTarget) return;

            // Safety check for missing rigidbody - this could happen if it was destroyed
            if (rb == null)
            {
                CZLogger.LogError("Rigidbody2D is null in MoveTowardsTarget. Re-initializing components.", LogCategory.Enemy);
                InitializeComponents();
                
                // If we still don't have a rigidbody, we can't move
                if (rb == null)
                {
                    CZLogger.LogError("Failed to re-initialize Rigidbody2D. Cannot move enemy.", LogCategory.Enemy);
                    return;
                }
            }

            try
            {
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
                        CZLogger.LogDebug($"Moving toward stale target ({targetAge:F1}s old) - reducing speed to {stalenessFactor:P0}", LogCategory.Enemy);
                    }
                    #endif
                }
                
                // Apply velocity with acceleration for smoother movement
                rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, targetVelocity, Time.fixedDeltaTime * velocityLerpSpeed);
                
                // Optionally rotate to face movement direction
                if (rb.linearVelocity.sqrMagnitude > 0.01f)
                {
                    float angle = Mathf.Atan2(rb.linearVelocity.y, rb.linearVelocity.x) * Mathf.Rad2Deg;
                    transform.rotation = Quaternion.Euler(0, 0, angle);
                }
            }
            catch (System.Exception e)
            {
                CZLogger.LogError($"Error in MoveTowardsTarget: {e.Message}", LogCategory.Enemy);
            }
        }
        #endregion

        #region Public Methods
        public virtual void SetTarget(Vector3 position)
        {
            if (!isInitialized)
            {
                // Instead of just logging an error, try to initialize
                CZLogger.LogWarning("Enemy not initialized in SetTarget call. Attempting to initialize now.", LogCategory.Enemy);
                InitializeComponents();
                
                // Check if initialization succeeded
                if (!isInitialized)
                {
                    CZLogger.LogError("Failed to initialize enemy during SetTarget call!", LogCategory.Enemy);
                    return;
                }
            }
            
            // Validate the input position to prevent setting invalid values
            if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z) ||
                float.IsInfinity(position.x) || float.IsInfinity(position.y) || float.IsInfinity(position.z))
            {
                CZLogger.LogError($"Cannot set invalid target position: {position} (contains NaN or Infinity)", LogCategory.Enemy);
                return;
            }
            
            // Set the target position and update tracking variables
            hasValidTarget = true;
            targetPosition = position;
            lastKnownTargetPosition = position;
            lastTargetUpdateTime = Time.time;
            
            CZLogger.LogDebug($"Set target position to {position} for {gameObject.name}", LogCategory.Enemy);
        }

        public virtual void TakeDamage(int damage)
        {
            TakeDamage(damage, DamageType.Normal);
        }

        public virtual void TakeDamage(int damage, DamageType damageType)
        {
            if (isDying || !isInitialized) return;

            // Add detailed logging for debugging projectile hits
            Debug.Log($"[{GetType().Name}] Taking {damage} damage of type {damageType}. Current health: {currentHealth}/{maxHealth}, " +
                      $"Position: {transform.position}, Active: {gameObject.activeInHierarchy}, " +
                      $"ColliderEnabled: {(circleCollider != null ? circleCollider.enabled.ToString() : "N/A")}, " +
                      $"Layer: {LayerMask.LayerToName(gameObject.layer)}");

            // Apply damage modifiers based on damage type
            int actualDamage = CalculateDamage(damage, damageType);
            
            currentHealth = Mathf.Max(0, currentHealth - actualDamage);
            
            // Trigger damage flash
            damageFlashTimer = damageFlashDuration;
            
            if (currentHealth <= 0 && !isDying)
            {
                Debug.Log($"[{GetType().Name}] Enemy defeated by damage of type {damageType}");
                StartDeathSequence();
            }
            
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            CZLogger.LogDebug($"Took {actualDamage} damage ({damageType}). Current health: {currentHealth}/{maxHealth}", LogCategory.Enemy);
            #endif
        }

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
                        float expRoll = UnityEngine.Random.value * 100f;
                        if (expRoll <= experienceDropChance)
                        {
                            int experienceDropCount = UnityEngine.Random.Range(minExperienceDropCount, maxExperienceDropCount + 1);
                            for (int i = 0; i < experienceDropCount; i++)
                            {
                                Vector3 expPosition = spawnPosition + new Vector3(UnityEngine.Random.Range(-0.5f, 0.5f), UnityEngine.Random.Range(-0.5f, 0.5f), 0);
                                var expResource = ResourceManager.Instance.SpawnResource(ResourceType.Experience, expPosition);
                                if (expResource != null)
                                {
                                    expResource.SetResourceValue(experienceDropValue);
                                    resourcesSpawned = true;
                                    CZLogger.LogDebug($"Spawned experience resource at {expPosition} with value {experienceDropValue}", LogCategory.Enemy);
                                }
                            }
                        }

                        // Random chance for health drop with validation and offset
                        float healthRoll = UnityEngine.Random.value * 100f;
                        if (healthRoll <= healthDropChance)
                        {
                            Vector3 healthPosition = spawnPosition + new Vector3(UnityEngine.Random.Range(-0.5f, 0.5f), UnityEngine.Random.Range(-0.5f, 0.5f), 0);
                            var healthResource = ResourceManager.Instance.SpawnResource(ResourceType.Health, healthPosition);
                            if (healthResource != null)
                            {
                                healthResource.SetResourceValue(healthDropValue);
                                resourcesSpawned = true;
                                CZLogger.LogDebug($"Spawned health resource at {healthPosition} with value {healthDropValue}", LogCategory.Enemy);
                            }
                        }

                        // Random chance for power-up with validation and offset
                        float powerUpRoll = UnityEngine.Random.value * 100f;
                        if (powerUpRoll <= powerUpDropChance)
                        {
                            Vector3 powerUpPosition = spawnPosition + new Vector3(UnityEngine.Random.Range(-0.5f, 0.5f), UnityEngine.Random.Range(-0.5f, 0.5f), 0);
                            var powerUpResource = ResourceManager.Instance.SpawnResource(ResourceType.PowerUp, powerUpPosition);
                            if (powerUpResource != null)
                            {
                                powerUpResource.SetResourceValue(powerUpDropValue);
                                resourcesSpawned = true;
                                CZLogger.LogDebug($"Spawned power-up resource at {powerUpPosition} with value {powerUpDropValue}", LogCategory.Enemy);
                            }
                        }

                        // Random chance for currency with validation and offset
                        float currencyRoll = UnityEngine.Random.value * 100f;
                        if (currencyRoll <= currencyDropChance)
                        {
                            Vector3 currencyPosition = spawnPosition + new Vector3(UnityEngine.Random.Range(-0.5f, 0.5f), UnityEngine.Random.Range(-0.5f, 0.5f), 0);
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

                // Trigger the OnEnemyDefeated event before returning to pool
                OnEnemyDefeated?.Invoke(this);

                // Always attempt to return to pool
                ReturnToPool();
            }
            catch (System.Exception e)
            {
                CZLogger.LogError($"Error in enemy death sequence: {e.Message}\n{e.StackTrace}", LogCategory.Enemy);
            }
        }

        public virtual void OnSpawn()
        {
            // Ensure we're initialized first
            if (!isInitialized)
            {
                CZLogger.LogInfo($"Enemy {gameObject.name} not initialized during OnSpawn. Initializing now.", LogCategory.Enemy);
                InitializeComponents();
                
                // If initialization failed, log an error but continue
                if (!isInitialized)
                {
                    CZLogger.LogError($"Failed to initialize enemy {gameObject.name} during OnSpawn!", LogCategory.Enemy);
                }
            }
            
            // Reset health and state
            currentHealth = maxHealth;
            isDying = false;
            deathTimer = 0f;
            damageFlashTimer = 0f;
            hasValidTarget = false;
            lastTargetUpdateTime = 0f;
            
            // Make sure colliders are enabled
            if (circleCollider != null)
            {
                circleCollider.enabled = true;
            }
            
            // Reset the sprite color and ensure visibility
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
                spriteRenderer.enabled = true;
                
                // Fix material issues - ensure we're using Sprites/Default shader for proper rendering
                if (spriteRenderer.material == null || spriteRenderer.material.shader.name.Contains("Lit"))
                {
                    spriteRenderer.material = new Material(Shader.Find("Sprites/Default"));
                    CZLogger.LogInfo("[BaseEnemy] Updated sprite material to Sprites/Default", LogCategory.Enemy);
                }
            }
            
            // Reset any velocity and ensure physics are active
            if (rb != null)
            {
                rb.simulated = true;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
            
            // Remove any ColliderAdjustmentMarker components from previous pool usage
            var marker = GetComponent<Physics2DSetup.ColliderAdjustmentMarker>();
            if (marker != null)
            {
                Destroy(marker);
            }
            
            // Ensure the layer is set correctly
            gameObject.layer = LayerMask.NameToLayer("Enemy");
            
            // Activate the game object
            gameObject.SetActive(true);
            
            CZLogger.LogInfo($"Enemy spawned: {gameObject.name} at {transform.position}, Initialized: {isInitialized}", LogCategory.Enemy);
        }

        public virtual void OnDespawn()
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

        #region Collision Handling
        
        /// <summary>
        /// OnCollisionEnter2D is called when this collider/rigidbody has begun touching another rigidbody/collider
        /// </summary>
        /// <param name="collision">The collision data containing information about the collision</param>
        protected virtual void OnCollisionEnter2D(Collision2D collision)
        {
            if (isDying || !isInitialized) return;
            
            // Check if colliding with a projectile
            if (collision.gameObject.layer == LayerMask.NameToLayer("Projectile"))
            {
                // Log for debugging
                CZLogger.LogInfo($"[{GetType().Name}] Hit by projectile: {collision.gameObject.name} at position {transform.position}", LogCategory.Enemy);

                try
                {
                    // Use reflection to get Projectile component type
                    var projectileType = System.Type.GetType("CZ.Core.Player.Projectile, CZ.Core.Player");
                    if (projectileType == null)
                    {
                        CZLogger.LogWarning($"[{GetType().Name}] Could not find Projectile type via reflection", LogCategory.Enemy);
                        return;
                    }
                    
                    var projectileComponent = collision.gameObject.GetComponent(projectileType);
                    if (projectileComponent != null)
                    {
                        // Use reflection to get the Damage property
                        var damageProperty = projectileType.GetProperty("Damage");
                        if (damageProperty == null)
                        {
                            CZLogger.LogWarning($"[{GetType().Name}] Could not find Damage property on Projectile", LogCategory.Enemy);
                            return;
                        }
                        
                        int damage = (int)damageProperty.GetValue(projectileComponent);
                        TakeDamage(damage);
                    }
                }
                catch (System.Exception ex)
                {
                    CZLogger.LogError($"[{GetType().Name}] Error processing projectile collision: {ex.Message}", LogCategory.Enemy);
                }
            }
            else if (collision.gameObject.layer == LayerMask.NameToLayer("Player"))
            {
                // Log collision with player
                Debug.Log($"[{GetType().Name}] Collided with player at position {transform.position}");
                
                // Use reflection to get PlayerController component
                var playerController = collision.gameObject.GetComponent(System.Type.GetType("CZ.Core.Player.PlayerController, CZ.Core.Player"));
                if (playerController != null)
                {
                    // Use reflection to call TakeDamage method
                    playerController.GetType().GetMethod("TakeDamage").Invoke(playerController, new object[] { 10 });
                }
            }
        }
        
        /// <summary>
        /// OnTriggerEnter2D is called when the Collider2D other enters the trigger
        /// </summary>
        /// <param name="other">The other Collider2D involved in this collision</param>
        protected virtual void OnTriggerEnter2D(Collider2D other)
        {
            if (isDying || !isInitialized) return;
            
            // Check if trigger entered by a projectile
            if (other.gameObject.layer == LayerMask.NameToLayer("Projectile"))
            {
                // Log for debugging
                CZLogger.LogInfo($"[{GetType().Name}] Trigger entered by projectile: {other.gameObject.name} at position {transform.position}", LogCategory.Enemy);

                try
                {
                    // Use reflection to get Projectile component type
                    var projectileType = System.Type.GetType("CZ.Core.Player.Projectile, CZ.Core.Player");
                    if (projectileType == null)
                    {
                        CZLogger.LogWarning($"[{GetType().Name}] Could not find Projectile type via reflection", LogCategory.Enemy);
                        return;
                    }
                    
                    var projectileComponent = other.gameObject.GetComponent(projectileType);
                    if (projectileComponent != null)
                    {
                        // Use reflection to get the Damage property
                        var damageProperty = projectileType.GetProperty("Damage");
                        if (damageProperty == null)
                        {
                            CZLogger.LogWarning($"[{GetType().Name}] Could not find Damage property on Projectile", LogCategory.Enemy);
                            return;
                        }
                        
                        int damage = (int)damageProperty.GetValue(projectileComponent);
                        TakeDamage(damage);
                    }
                }
                catch (System.Exception ex)
                {
                    CZLogger.LogError($"[{GetType().Name}] Error processing projectile trigger: {ex.Message}", LogCategory.Enemy);
                }
            }
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