using UnityEngine;
using System.Collections;

namespace CZ.Core.Configuration
{
    /// <summary>
    /// Handles Physics2D setup to ensure proper collisions between game objects
    /// This component ensures collision layers are set up correctly at runtime
    /// </summary>
    public class Physics2DSetup : MonoBehaviour
    {
        [SerializeField] 
        private bool logLayerSetup = true;
        
        [SerializeField]
        private bool enforceCollisionsEveryFrame = true;
        
        [SerializeField, Range(0.01f, 0.2f)]
        [Tooltip("The minimum separation distance maintained by the physics engine")]
        private float contactOffset = 0.05f;
        
        [SerializeField, Range(1.0f, 1.5f)]
        [Tooltip("Scale factor to apply to enemy collider radius to prevent overlap")]
        private float enemyColliderScaleFactor = 1.1f;
        
        [SerializeField]
        [Tooltip("Whether to prevent rigidbodies from sleeping for more reliable collisions")]
        private bool preventRigidbodySleeping = true;
        
        private int playerLayer;
        private int enemyLayer;
        private int projectileLayer;

        private void Awake()
        {
            // Configure global Physics2D settings first
            ConfigurePhysics2DSettings();
            
            // Set up Physics2D collision matrix programmatically
            ConfigurePhysicsLayers();
        }
        
        private void Start()
        {
            // Additional enforcement after all objects are initialized
            StartCoroutine(EnforceCollisionsAfterDelay());
        }
        
        /// <summary>
        /// Configure global Physics2D settings to optimize collision detection
        /// </summary>
        private void ConfigurePhysics2DSettings()
        {
            // Set the default contact offset (minimum separation distance)
            // Increased to provide more space between colliding objects
            Physics2D.defaultContactOffset = contactOffset;
            
            // Set velocity and position iteration counts for better stability
            // More iterations = more stable but more CPU intensive
            Physics2D.velocityIterations = 12; // Increased from 8 for better stability
            Physics2D.positionIterations = 8;  // Increased from 3 for better positioning
            
            // CRITICAL: Force enable these settings for better collision detection
            // This ensures that triggers work properly across all objects
            Physics2D.queriesHitTriggers = true;
            Physics2D.queriesStartInColliders = false; // This often helps with overlap issues
            Physics2D.reuseCollisionCallbacks = true;
            Physics2D.callbacksOnDisable = true;
            
            // Make sure this is explicitly set to true to ensure triggers work correctly
            Physics2D.queriesHitTriggers = true;
            
            // Auto-sync transforms ensures transform positions match physics positions
            Physics2D.autoSyncTransforms = true;
            
            Debug.Log($"[Physics2DSetup] Configured global Physics2D settings: " +
                      $"ContactOffset={Physics2D.defaultContactOffset}, " +
                      $"VelocityIterations={Physics2D.velocityIterations}, " +
                      $"PositionIterations={Physics2D.positionIterations}, " +
                      $"PreventSleeping={preventRigidbodySleeping}, " +
                      $"QueriesHitTriggers={Physics2D.queriesHitTriggers}, " +
                      $"EnemyColliderScaleFactor={enemyColliderScaleFactor}");
        }
        
        private IEnumerator EnforceCollisionsAfterDelay()
        {
            // First pass - initial setup
            yield return new WaitForSeconds(0.1f);
            ConfigurePhysicsLayers();
            UpdateEnemyPhysicsProperties();
            
            // Second pass - after objects have had a chance to initialize
            yield return new WaitForSeconds(0.3f);
            ConfigurePhysicsLayers();
            UpdateEnemyPhysicsProperties();
            
            // Final pass - ensure everything is properly set up
            yield return new WaitForSeconds(0.5f);
            ConfigurePhysicsLayers();
            UpdateEnemyPhysicsProperties();
            
            // Log the details about how many enemies and their current collision settings
            int enemyCount = 0;
            Rigidbody2D[] allRigidbodies = FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None);
            foreach (Rigidbody2D rb in allRigidbodies)
            {
                if (rb.gameObject.layer == enemyLayer)
                {
                    enemyCount++;
                    CircleCollider2D circleCollider = rb.GetComponent<CircleCollider2D>();
                    float radius = circleCollider != null ? circleCollider.radius : 0;
                    
                    Debug.Log($"[Physics2DSetup] Enemy #{enemyCount}: {rb.gameObject.name}, " +
                              $"Simulated: {rb.simulated}, " +
                              $"BodyType: {rb.bodyType}, " +
                              $"Radius: {radius}, " +
                              $"Mass: {rb.mass}, " +
                              $"CollisionDetectionMode: {rb.collisionDetectionMode}, " +
                              $"SleepMode: {rb.sleepMode}");
                }
            }
            
            Debug.Log($"[Physics2DSetup] Found {enemyCount} enemies in the scene");
        }
        
        /// <summary>
        /// Determines if a collider is a damage trigger (used for damage detection, not physics)
        /// </summary>
        private bool IsDamageTriggerCollider(Collider2D collider)
        {
            // Check based on naming conventions
            if (collider.name.Contains("DamageTrigger") || 
                collider.name.Contains("Damage") && collider.isTrigger)
            {
                return true;
            }
            
            // Additional check for secondary colliders that might be used for damage
            if (collider != collider.GetComponentInParent<Rigidbody2D>()?.GetComponent<Collider2D>() && 
                collider.isTrigger)
            {
                // This is not the main physics collider and is a trigger, so it's likely a damage trigger
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Marks a collider as already processed to prevent repeated adjustments
        /// </summary>
        [DisallowMultipleComponent]
        public class ColliderAdjustmentMarker : MonoBehaviour { }

        private void UpdateEnemyPhysicsProperties()
        {
            Rigidbody2D[] allRigidbodies = FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None);
            int updatedCount = 0;
            
            foreach (Rigidbody2D rb in allRigidbodies)
            {
                if (rb.gameObject.layer == enemyLayer)
                {
                    // Skip objects that have already been processed
                    if (rb.gameObject.TryGetComponent<ColliderAdjustmentMarker>(out _))
                    {
                        continue;
                    }
                    
                    // Ensure the rigidbody is properly configured for collisions
                    if (rb.bodyType != RigidbodyType2D.Dynamic)
                    {
                        rb.bodyType = RigidbodyType2D.Dynamic;
                        updatedCount++;
                    }
                    
                    // Ensure continuous collision detection for better precision
                    if (rb.collisionDetectionMode != CollisionDetectionMode2D.Continuous)
                    {
                        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                        updatedCount++;
                    }
                    
                    // Prevent rigidbodies from sleeping for more reliable collisions
                    if (preventRigidbodySleeping && rb.sleepMode != RigidbodySleepMode2D.NeverSleep)
                    {
                        rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
                        updatedCount++;
                    }
                    
                    // Ensure mass is sufficient to prevent easy pushing (but not too heavy)
                    if (rb.mass < 1.0f || rb.mass > 2.0f)
                    {
                        rb.mass = 1.5f;
                        updatedCount++;
                    }
                    
                    // Set linear drag to prevent excessive movement
                    if (rb.linearDamping < 0.5f)
                    {
                        rb.linearDamping = 0.5f;
                        updatedCount++;
                    }
                    
                    // Fix any enemy colliders that might be set as triggers
                    // And ensure collider radius is appropriate to prevent overlap
                    Collider2D[] colliders = rb.GetComponents<Collider2D>();
                    bool hasPhysicsCollider = false;
                    
                    foreach (var collider in colliders)
                    {
                        // Skip damage trigger colliders which should remain as triggers
                        if (IsDamageTriggerCollider(collider))
                        {
                            // This is specifically for damage detection, so keep it as a trigger
                            continue;
                        }
                        
                        // Main physics collider should never be a trigger
                        if (collider.isTrigger)
                        {
                            collider.isTrigger = false;
                            updatedCount++;
                            Debug.Log($"[Physics2DSetup] Fixed collider on {rb.gameObject.name} that was incorrectly set as trigger");
                        }
                        
                        // If this is a circle collider, apply the scale factor only once
                        if (collider is CircleCollider2D circleCollider)
                        {
                            float originalRadius = circleCollider.radius;
                            float baseRadius = originalRadius / enemyColliderScaleFactor; // Calculate what the base radius would be
                            float targetRadius = baseRadius * enemyColliderScaleFactor;   // Then apply the scale factor correctly
                            
                            // Only adjust if not already at target value (with some tolerance)
                            if (Mathf.Abs(circleCollider.radius - targetRadius) > 0.001f)
                            {
                                circleCollider.radius = targetRadius;
                                updatedCount++;
                                Debug.Log($"[Physics2DSetup] Adjusted collider radius on {rb.gameObject.name} " +
                                          $"from {originalRadius:F3} to {targetRadius:F3} (one-time adjustment)");
                            }
                            
                            hasPhysicsCollider = true;
                        }
                    }
                    
                    // If no physics collider was found, add one
                    if (!hasPhysicsCollider)
                    {
                        CircleCollider2D newCollider = rb.gameObject.AddComponent<CircleCollider2D>();
                        newCollider.radius = 0.5f * enemyColliderScaleFactor;
                        newCollider.isTrigger = false;
                        
                        // Create a physics material for the new collider
                        PhysicsMaterial2D physicsMaterial = new PhysicsMaterial2D("EnemyPhysicsMaterial");
                        physicsMaterial.friction = 0.1f;
                        physicsMaterial.bounciness = 0.3f;
                        newCollider.sharedMaterial = physicsMaterial;
                        
                        updatedCount++;
                        Debug.Log($"[Physics2DSetup] Added new physics collider to {rb.gameObject.name} with radius {newCollider.radius:F3}");
                    }
                    
                    // Mark this object as processed so we don't adjust it again
                    rb.gameObject.AddComponent<ColliderAdjustmentMarker>();
                }
            }
            
            if (updatedCount > 0)
            {
                Debug.Log($"[Physics2DSetup] Updated physics properties on {updatedCount} enemy components");
            }
        }
        
        private void Update()
        {
            if (enforceCollisionsEveryFrame)
            {
                // Enforce collision matrix every frame to prevent issues
                ConfigurePhysicsLayers();
                
                // We still need to check periodically, but only for new objects
                if (Time.frameCount % 120 == 0) // Every 120 frames (reduced from 60)
                {
                    UpdateEnemyPhysicsProperties();
                    
                    // Check for SwiftEnemy objects specifically
                    CheckSwiftEnemyCollisions();
                }
            }
        }
        
        /// <summary>
        /// Specifically checks and fixes collision settings for SwiftEnemy objects
        /// </summary>
        private void CheckSwiftEnemyCollisions()
        {
            try
            {
                // Use Type.GetType to find the SwiftEnemyController type without direct namespace reference
                System.Type swiftEnemyType = System.Type.GetType("CZ.Core.Enemy.SwiftEnemyController, CZ.Core.Enemy");
                
                if (swiftEnemyType == null)
                {
                    Debug.LogWarning("[Physics2DSetup] SwiftEnemyController type not found. Skipping SwiftEnemy collision check.");
                    return;
                }
                
                // Use FindObjectsOfType with Type parameter instead of generic version
                var swiftEnemies = FindObjectsByType(swiftEnemyType, FindObjectsSortMode.None);
                
                if (swiftEnemies.Length > 0)
                {
                    Debug.Log($"[Physics2DSetup] Found {swiftEnemies.Length} SwiftEnemy objects to check");
                    
                    foreach (var obj in swiftEnemies)
                    {
                        // Cast to MonoBehaviour to access common properties
                        MonoBehaviour swiftEnemy = obj as MonoBehaviour;
                        if (swiftEnemy == null || !swiftEnemy.gameObject.activeInHierarchy) continue;
                        
                        // Ensure the layer is set to Enemy
                        if (swiftEnemy.gameObject.layer != enemyLayer)
                        {
                            Debug.LogWarning($"[Physics2DSetup] SwiftEnemy {swiftEnemy.name} was on incorrect layer: {LayerMask.LayerToName(swiftEnemy.gameObject.layer)}. Setting to Enemy layer.");
                            swiftEnemy.gameObject.layer = enemyLayer;
                        }
                        
                        // Check all colliders
                        var colliders = swiftEnemy.GetComponents<Collider2D>();
                        bool hasEnabledCollider = false;
                        
                        foreach (var collider in colliders)
                        {
                            // Skip damage trigger colliders
                            if (IsDamageTriggerCollider(collider)) continue;
                            
                            // Ensure the collider is enabled and not a trigger
                            if (!collider.enabled)
                            {
                                Debug.LogWarning($"[Physics2DSetup] SwiftEnemy {swiftEnemy.name} has disabled collider. Enabling it.");
                                collider.enabled = true;
                            }
                            
                            // For physics collisions, we need non-trigger colliders
                            if (collider.isTrigger)
                            {
                                Debug.LogWarning($"[Physics2DSetup] SwiftEnemy {swiftEnemy.name} has trigger collider. Setting to non-trigger for physics collisions.");
                                collider.isTrigger = false;
                            }
                            
                            hasEnabledCollider = true;
                        }
                        
                        // If no enabled colliders were found, log a critical error
                        if (!hasEnabledCollider)
                        {
                            Debug.LogError($"[Physics2DSetup] CRITICAL: SwiftEnemy {swiftEnemy.name} has no enabled colliders for physics!");
                        }
                        
                        // Check rigidbody settings
                        var rb = swiftEnemy.GetComponent<Rigidbody2D>();
                        if (rb != null)
                        {
                            if (!rb.simulated)
                            {
                                Debug.LogWarning($"[Physics2DSetup] SwiftEnemy {swiftEnemy.name} has disabled rigidbody simulation. Enabling it.");
                                rb.simulated = true;
                            }
                            
                            if (rb.collisionDetectionMode != CollisionDetectionMode2D.Continuous)
                            {
                                Debug.LogWarning($"[Physics2DSetup] SwiftEnemy {swiftEnemy.name} not using continuous collision detection. Updating it.");
                                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Physics2DSetup] Error in CheckSwiftEnemyCollisions: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void ConfigurePhysicsLayers()
        {
            // Get layer indices
            int defaultLayer = LayerMask.NameToLayer("Default");
            playerLayer = LayerMask.NameToLayer("Player");
            enemyLayer = LayerMask.NameToLayer("Enemy");
            projectileLayer = LayerMask.NameToLayer("Projectile");
            int waterLayer = LayerMask.NameToLayer("Water");
            int uiLayer = LayerMask.NameToLayer("UI");

            // Make sure the layers were found
            if (playerLayer < 0)
            {
                Debug.LogError("[Physics2DSetup] Player layer not found. Please create this layer in the project settings.");
                Debug.LogError("[Physics2DSetup] To create the Player layer: Go to Edit > Project Settings > Tags and Layers > Add Layer > Set slot 8 to 'Player'");
            }

            if (enemyLayer < 0)
            {
                Debug.LogError("[Physics2DSetup] Enemy layer not found. Please create this layer in the project settings.");
                Debug.LogError("[Physics2DSetup] To create the Enemy layer: Go to Edit > Project Settings > Tags and Layers > Add Layer > Set slot 9 to 'Enemy'");
            }
            
            // Check if Projectile layer exists
            if (projectileLayer < 0)
            {
                Debug.LogError("[Physics2DSetup] Projectile layer not found. Please create this layer in the project settings.");
                Debug.LogError("[Physics2DSetup] To create the Projectile layer: Go to Edit > Project Settings > Tags and Layers > Add Layer > Set slot 10 to 'Projectile'");
                // If projectile layer doesn't exist, use Default layer as fallback
                projectileLayer = defaultLayer;
            }

            // Log the layer configuration if enabled
            if (logLayerSetup)
            {
                Debug.Log($"[Physics2DSetup] Layer setup - Default: {defaultLayer}, Player: {playerLayer}, Enemy: {enemyLayer}, " +
                          $"Projectile: {projectileLayer}, Water: {waterLayer}, UI: {uiLayer}");
            }

            // Force layer collision to be enabled when possible
            if (playerLayer >= 0 && enemyLayer >= 0)
            {
                // Ensure Player-Enemy collisions are enabled
                if (Physics2D.GetIgnoreLayerCollision(playerLayer, enemyLayer))
                {
                    Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, false);
                    Debug.Log("[Physics2DSetup] Explicitly enabled Player-Enemy collisions");
                }
                
                // CRITICAL: Ensure Enemy-Enemy collisions are explicitly enabled
                Physics2D.IgnoreLayerCollision(enemyLayer, enemyLayer, false);
                Debug.Log("[Physics2DSetup] Explicitly enforced Enemy-Enemy collisions");
                
                // Projectile collision setup
                if (projectileLayer >= 0 && projectileLayer != defaultLayer)
                {
                    // CRITICAL FIX: Force-enable Projectile-Enemy collisions and log status
                    Physics2D.IgnoreLayerCollision(projectileLayer, enemyLayer, false);
                    bool projectileEnemyCollision = !Physics2D.GetIgnoreLayerCollision(projectileLayer, enemyLayer);
                    Debug.Log($"[Physics2DSetup] CRITICAL: Projectile-Enemy collisions: {(projectileEnemyCollision ? "ENABLED" : "DISABLED")}");
                    
                    if (!projectileEnemyCollision)
                    {
                        // Try a second time with stronger logging if it didn't work
                        Physics2D.IgnoreLayerCollision(projectileLayer, enemyLayer, false);
                        projectileEnemyCollision = !Physics2D.GetIgnoreLayerCollision(projectileLayer, enemyLayer);
                        Debug.LogWarning($"[Physics2DSetup] ⚠️ Second attempt - Projectile-Enemy collisions: {(projectileEnemyCollision ? "ENABLED" : "STILL DISABLED")}");
                    }
                    
                    // Projectiles should not collide with player (source)
                    Physics2D.IgnoreLayerCollision(projectileLayer, playerLayer, true);
                    Debug.Log("[Physics2DSetup] Explicitly disabled Projectile-Player collisions");
                    
                    // Projectiles should not collide with other projectiles
                    Physics2D.IgnoreLayerCollision(projectileLayer, projectileLayer, true);
                    Debug.Log("[Physics2DSetup] Explicitly disabled Projectile-Projectile collisions");
                }
                
                // Force-enable contact pairs between enemies
                Physics2D.queriesHitTriggers = true;
                Physics2D.queriesStartInColliders = false; // This often helps with overlapping
                Physics2D.reuseCollisionCallbacks = true;
                Physics2D.callbacksOnDisable = true;
                
                // Ensure Player-Player collisions are enabled
                if (Physics2D.GetIgnoreLayerCollision(playerLayer, playerLayer))
                {
                    Physics2D.IgnoreLayerCollision(playerLayer, playerLayer, false);
                    Debug.Log("[Physics2DSetup] Explicitly enabled Player-Player collisions");
                }
            }

            if (logLayerSetup)
            {
                // Check if player-player collisions are enabled
                bool playerPlayerCollision = !Physics2D.GetIgnoreLayerCollision(playerLayer, playerLayer);
                Debug.Log($"[Physics2DSetup] Player-Player collisions: {(playerPlayerCollision ? "Enabled" : "Disabled")}");

                // Check if enemy-enemy collisions are enabled
                bool enemyEnemyCollision = !Physics2D.GetIgnoreLayerCollision(enemyLayer, enemyLayer);
                Debug.Log($"[Physics2DSetup] Enemy-Enemy collisions: {(enemyEnemyCollision ? "Enabled" : "Disabled")}");

                // Check if player-enemy collisions are enabled
                bool playerEnemyCollision = !Physics2D.GetIgnoreLayerCollision(playerLayer, enemyLayer);
                Debug.Log($"[Physics2DSetup] Player-Enemy collisions: {(playerEnemyCollision ? "Enabled" : "Disabled")}");
                
                // Log projectile collisions
                if (projectileLayer >= 0 && projectileLayer != defaultLayer)
                {
                    bool projectileEnemyCollision = !Physics2D.GetIgnoreLayerCollision(projectileLayer, enemyLayer);
                    Debug.Log($"[Physics2DSetup] Projectile-Enemy collisions: {(projectileEnemyCollision ? "Enabled" : "Disabled")}");
                    
                    bool projectilePlayerCollision = !Physics2D.GetIgnoreLayerCollision(projectileLayer, playerLayer);
                    Debug.Log($"[Physics2DSetup] Projectile-Player collisions: {(projectilePlayerCollision ? "Enabled" : "Disabled")}");
                }
            }
        }
    }
} 