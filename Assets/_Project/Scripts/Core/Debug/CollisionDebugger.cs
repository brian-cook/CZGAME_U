using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using CZ.Core.Logging;
using CZ.Core.Player;
using CZ.Core.Enemy;
using CZ.Core.Interfaces;
using System;
using System.Collections;
using static UnityEngine.Physics2D;

namespace CZ.Core.Debug
{
    /// <summary>
    /// Diagnostic tool to detect and repair collision issues between projectiles and enemies
    /// Attach to a manager object to monitor collisions between key game objects
    /// </summary>
    [AddComponentMenu("CZ/Debug/Collision Debugger")]
    public class CollisionDebugger : MonoBehaviour, ICollisionDebugger
    {
        [SerializeField, Tooltip("Controls detailed logging of collision information")]
        private bool logCollisionInfo = true;
        
        [SerializeField]
        private bool logLayerCollisionMatrixOnStart = true;
        
        [SerializeField]
        private bool fixQueriesHitTriggers = true;
        
        [SerializeField]
        private bool fixProjectileEnemyCollisions = true;
        
        [SerializeField]
        private int fixAttemptsPerSecond = 1;
        
        [SerializeField]
        [Range(1, 30)]
        private int maxFixAttempts = 10;
        
        [SerializeField]
        private bool runVerificationPeriodically = true;
        
        [SerializeField]
        private float verificationInterval = 5f;
        
        [SerializeField]
        private bool findAndFixMisconfigurations = true;
        
        private int currentFixAttempts = 0;
        private float lastFixTime = 0f;
        private bool hasFixedCollisions = false;
        private float lastVerificationTime = 0f;
        
        private int playerLayer;
        private int enemyLayer;
        private int projectileLayer;
        
        private void Start()
        {
            InitializeLayers();
            
            if (logLayerCollisionMatrixOnStart)
            {
                CZLogger.LogInfo("[CollisionDebugger] ----- Physics2D Layer Collision Matrix -----", LogCategory.Debug);
                LogCollisionMatrix();
            }
            
            // Perform initial verification on important settings
            VerifyPhysics2DSettings();
            
            // Run an initial check for all active projectiles and enemies
            if (findAndFixMisconfigurations)
            {
                FindAndFixMisconfigurations();
            }
        }
        
        private void InitializeLayers()
        {
            playerLayer = LayerMask.NameToLayer("Player");
            enemyLayer = LayerMask.NameToLayer("Enemy");
            projectileLayer = LayerMask.NameToLayer("Projectile");
            
            if (playerLayer < 0 || enemyLayer < 0 || projectileLayer < 0)
            {
                CZLogger.LogError("[CollisionDebugger] One or more required layers are missing!", LogCategory.Debug);
                CZLogger.LogError($"[CollisionDebugger] Layer indices - Player: {playerLayer}, Enemy: {enemyLayer}, Projectile: {projectileLayer}", LogCategory.Debug);
            }
        }
        
        private void Update()
        {
            // Ensure queriesHitTriggers is always enabled
            if (fixQueriesHitTriggers && !queriesHitTriggers)
            {
                CZLogger.LogWarning("[CollisionDebugger] 🚨 Physics2D.queriesHitTriggers was disabled! Re-enabling...", LogCategory.Debug);
                queriesHitTriggers = true;
            }
            
            // Attempt to fix projectile-enemy collisions periodically
            if (fixProjectileEnemyCollisions && !hasFixedCollisions)
            {
                if (Time.time - lastFixTime > 1f / fixAttemptsPerSecond && currentFixAttempts < maxFixAttempts)
                {
                    if (AttemptFixProjectileEnemyCollisions())
                    {
                        hasFixedCollisions = true;
                        CZLogger.LogInfo("[CollisionDebugger] Successfully fixed projectile-enemy collisions!", LogCategory.Debug);
                    }
                    else
                    {
                        lastFixTime = Time.time;
                        currentFixAttempts++;
                        
                        if (currentFixAttempts >= maxFixAttempts)
                        {
                            CZLogger.LogError("[CollisionDebugger] Failed to fix projectile-enemy collisions after maximum attempts.", LogCategory.Debug);
                        }
                    }
                }
            }
            
            // Run periodic verification of physics settings
            if (runVerificationPeriodically && Time.time - lastVerificationTime > verificationInterval)
            {
                VerifyPhysics2DSettings();
                
                if (findAndFixMisconfigurations)
                {
                    FindAndFixMisconfigurations();
                }
                
                lastVerificationTime = Time.time;
            }
        }
        
        /// <summary>
        /// Verify all critical Physics2D settings are correctly configured
        /// </summary>
        private void VerifyPhysics2DSettings()
        {
            CZLogger.LogInfo("[CollisionDebugger] Verifying Physics2D settings...", LogCategory.Debug);
            
            // Check critical Physics2D settings
            if (!queriesHitTriggers)
            {
                CZLogger.LogWarning("[CollisionDebugger] 🚨 Physics2D.queriesHitTriggers is disabled! This prevents trigger-based projectile collisions.", LogCategory.Debug);
                queriesHitTriggers = true;
                CZLogger.LogInfo("[CollisionDebugger] ✓ Fixed Physics2D.queriesHitTriggers = true", LogCategory.Debug);
            }
            
            if (queriesStartInColliders)
            {
                CZLogger.LogWarning("[CollisionDebugger] Physics2D.queriesStartInColliders is enabled, which can cause issues with overlapping colliders.", LogCategory.Debug);
                queriesStartInColliders = false;
                CZLogger.LogInfo("[CollisionDebugger] ✓ Fixed Physics2D.queriesStartInColliders = false", LogCategory.Debug);
            }
            
            // Check and fix Physics2D simulation settings
            if (velocityIterations < 8)
            {
                CZLogger.LogWarning("[CollisionDebugger] Physics2D.velocityIterations is set below recommended value (8).", LogCategory.Debug);
                velocityIterations = 8;
                CZLogger.LogInfo("[CollisionDebugger] ✓ Fixed Physics2D.velocityIterations = 8", LogCategory.Debug);
            }
            
            if (positionIterations < 8)
            {
                CZLogger.LogWarning("[CollisionDebugger] Physics2D.positionIterations is only {positionIterations}, which may cause unstable physics.", LogCategory.Debug);
                positionIterations = 8;
                CZLogger.LogInfo("[CollisionDebugger] ✓ Increased Physics2D.positionIterations to 8", LogCategory.Debug);
            }
            
            // Ensure contact pairs mode is set correctly
            if (callbacksOnDisable == false)
            {
                CZLogger.LogWarning("[CollisionDebugger] Physics2D.callbacksOnDisable is disabled, which can cause missed collision events.", LogCategory.Debug);
                callbacksOnDisable = true;
                CZLogger.LogInfo("[CollisionDebugger] ✓ Fixed Physics2D.callbacksOnDisable = true", LogCategory.Debug);
            }
            
            // CRITICAL FIX: Ensure projectile-enemy collisions are enabled
            bool projectileEnemyIgnored = GetIgnoreLayerCollision(projectileLayer, enemyLayer);
            if (projectileEnemyIgnored)
            {
                CZLogger.LogError("[CollisionDebugger] ❌ CRITICAL: Projectile and Enemy layers are set to ignore collisions!", LogCategory.Debug);
                IgnoreLayerCollision(projectileLayer, enemyLayer, false); // Enable collisions
                
                // Double-check the change was applied
                if (GetIgnoreLayerCollision(projectileLayer, enemyLayer))
                {
                    CZLogger.LogError("[CollisionDebugger] ❌ Failed to enable Projectile-Enemy collisions! Manual fix required.", LogCategory.Debug);
                    CZLogger.LogError("CRITICAL COLLISION ERROR: Failed to enable Projectile-Enemy collisions!", LogCategory.Debug);
                }
                else
                {
                    CZLogger.LogInfo("[CollisionDebugger] ✓ Fixed layer collision: Projectiles can now hit Enemies", LogCategory.Debug);
                    CZLogger.LogInfo("Fixed layer collision: Projectiles can now hit Enemies", LogCategory.Debug);
                }
            }
            
            // Verify player-enemy collision (should usually be ENABLED for damage)
            bool playerEnemyIgnored = GetIgnoreLayerCollision(playerLayer, enemyLayer);
            if (playerEnemyIgnored)
            {
                CZLogger.LogWarning("[CollisionDebugger] ⚠ Player and Enemy layers are set to ignore collisions! This prevents damage.", LogCategory.Debug);
                IgnoreLayerCollision(playerLayer, enemyLayer, false); // Enable collisions for damage
                
                // Double-check the change was applied
                if (GetIgnoreLayerCollision(playerLayer, enemyLayer))
                {
                    CZLogger.LogError("[CollisionDebugger] ❌ Failed to enable Player-Enemy collisions for damage!", LogCategory.Debug);
                    CZLogger.LogError("CRITICAL COLLISION ERROR: Failed to enable Player-Enemy collisions!", LogCategory.Debug);
                }
                else
                {
                    CZLogger.LogInfo("[CollisionDebugger] ✓ Fixed layer collision: Player can now be damaged by Enemies", LogCategory.Debug);
                    CZLogger.LogInfo("Fixed layer collision: Player can now be damaged by Enemies", LogCategory.Debug);
                }
            }
            
            // Log the collision matrix for verification
            CZLogger.LogInfo("[CollisionDebugger] Physics settings verification complete. Logging collision matrix:", LogCategory.Debug);
            LogCollisionMatrix();
        }
        
        /// <summary>
        /// Find all active projectiles and enemies and fix any misconfigurations
        /// </summary>
        private void FindAndFixMisconfigurations()
        {
            CZLogger.LogInfo("[CollisionDebugger] Scanning scene for projectiles and enemies to verify configurations...", LogCategory.Debug);
            
            // Find all projectiles
            Projectile[] projectiles = FindObjectsByType<Projectile>(FindObjectsSortMode.None);
            CZLogger.LogInfo($"[CollisionDebugger] Found {projectiles.Length} projectiles in the scene", LogCategory.Debug);
            
            foreach (Projectile projectile in projectiles)
            {
                if (projectile.gameObject.activeInHierarchy)
                {
                    CheckProjectileSetup(projectile.gameObject);
                }
            }
            
            // Find all enemies
            BaseEnemy[] enemies = FindObjectsByType<BaseEnemy>(FindObjectsSortMode.None);
            CZLogger.LogInfo($"[CollisionDebugger] Found {enemies.Length} enemies in the scene", LogCategory.Debug);
            
            foreach (BaseEnemy enemy in enemies)
            {
                if (enemy.gameObject.activeInHierarchy)
                {
                    CheckEnemySetup(enemy.gameObject);
                }
            }
            
            CZLogger.LogInfo("[CollisionDebugger] Scene scan complete.", LogCategory.Debug);
        }
        
        private bool AttemptFixProjectileEnemyCollisions()
        {
            // Get all active projectiles
            var projectiles = FindObjectsByType<CZ.Core.Player.Projectile>(FindObjectsSortMode.None);
            
            // Check if projectile-enemy layer collision is enabled
            int projectileLayer = LayerMask.NameToLayer("Projectile");
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            
            if (projectileLayer < 0 || enemyLayer < 0)
            {
                CZLogger.LogError("[CollisionDebugger] Projectile or Enemy layer not found!", LogCategory.Debug);
                return false;
            }
            
            bool projectileEnemyCollision = !Physics2D.GetIgnoreLayerCollision(projectileLayer, enemyLayer);
            if (!projectileEnemyCollision)
            {
                CZLogger.LogWarning("[CollisionDebugger] Projectile-Enemy layer collision is disabled! Attempting to enable...", LogCategory.Debug);
                Physics2D.IgnoreLayerCollision(projectileLayer, enemyLayer, false);
                
                // Check if it worked
                projectileEnemyCollision = !Physics2D.GetIgnoreLayerCollision(projectileLayer, enemyLayer);
                CZLogger.LogInfo($"[CollisionDebugger] Projectile-Enemy layer collision is now: {(projectileEnemyCollision ? "ENABLED" : "STILL DISABLED")}", LogCategory.Debug);
            }
            else
            {
                CZLogger.LogInfo("[CollisionDebugger] Projectile-Enemy layer collision is correctly enabled", LogCategory.Debug);
            }
            
            // Process each projectile
            foreach (var projectile in projectiles)
            {
                CheckProjectileSetup(projectile.gameObject);
            }
            
            // Check SwiftEnemy objects specifically
            CheckSwiftEnemySetup();
            
            return true;
        }
        
        /// <summary>
        /// Specifically checks and fixes SwiftEnemy objects to ensure they can be hit by projectiles
        /// </summary>
        private void CheckSwiftEnemySetup()
        {
            var swiftEnemies = FindObjectsByType<SwiftEnemyController>(FindObjectsSortMode.None);
            
            if (swiftEnemies.Length == 0)
            {
                CZLogger.LogInfo("[CollisionDebugger] No SwiftEnemy objects found in the scene", LogCategory.Debug);
                return;
            }
            
            CZLogger.LogInfo($"[CollisionDebugger] Found {swiftEnemies.Length} SwiftEnemy objects to check", LogCategory.Debug);
            
            foreach (var swiftEnemy in swiftEnemies)
            {
                if (swiftEnemy == null || !swiftEnemy.gameObject.activeInHierarchy) continue;
                
                // Check layer
                int enemyLayer = LayerMask.NameToLayer("Enemy");
                if (swiftEnemy.gameObject.layer != enemyLayer)
                {
                    CZLogger.LogWarning($"[CollisionDebugger] SwiftEnemy {swiftEnemy.name} is on incorrect layer: {LayerMask.LayerToName(swiftEnemy.gameObject.layer)}. Setting to Enemy layer.", LogCategory.Debug);
                    swiftEnemy.gameObject.layer = enemyLayer;
                }
                
                // Check colliders
                var colliders = swiftEnemy.GetComponents<Collider2D>();
                bool hasEnabledCollider = false;
                
                if (colliders.Length == 0)
                {
                    CZLogger.LogError($"[CollisionDebugger] SwiftEnemy {swiftEnemy.name} has no colliders! Adding a CircleCollider2D.", LogCategory.Debug);
                    var newCollider = swiftEnemy.gameObject.AddComponent<CircleCollider2D>();
                    newCollider.radius = 0.5f;
                    newCollider.isTrigger = false;
                    newCollider.enabled = true;
                    hasEnabledCollider = true;
                }
                else
                {
                    foreach (var collider in colliders)
                    {
                        // Skip damage trigger colliders
                        if (collider.name.Contains("DamageTrigger") || collider.name.Contains("Damage") && collider.isTrigger)
                        {
                            continue;
                        }
                        
                        // Ensure the collider is enabled and not a trigger for physics collisions
                        if (!collider.enabled)
                        {
                            CZLogger.LogWarning($"[CollisionDebugger] SwiftEnemy {swiftEnemy.name} has disabled collider. Enabling it.", LogCategory.Debug);
                            collider.enabled = true;
                        }
                        
                        // For physics collisions with projectiles, we need non-trigger colliders
                        if (collider.isTrigger)
                        {
                            CZLogger.LogWarning($"[CollisionDebugger] SwiftEnemy {swiftEnemy.name} has trigger collider. Setting to non-trigger for physics collisions.", LogCategory.Debug);
                            collider.isTrigger = false;
                        }
                        
                        hasEnabledCollider = true;
                    }
                }
                
                // Check rigidbody
                var rb = swiftEnemy.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    if (!rb.simulated)
                    {
                        CZLogger.LogWarning($"[CollisionDebugger] SwiftEnemy {swiftEnemy.name} has disabled rigidbody simulation. Enabling it.", LogCategory.Debug);
                        rb.simulated = true;
                    }
                }
                else
                {
                    CZLogger.LogError($"[CollisionDebugger] SwiftEnemy {swiftEnemy.name} has no Rigidbody2D! Adding one.", LogCategory.Debug);
                    rb = swiftEnemy.gameObject.AddComponent<Rigidbody2D>();
                    rb.gravityScale = 0f;
                    rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                    rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                    rb.simulated = true;
                }
                
                CZLogger.LogInfo($"[CollisionDebugger] SwiftEnemy {swiftEnemy.name} checked. Layer: {LayerMask.LayerToName(swiftEnemy.gameObject.layer)}, " +
                          $"HasEnabledCollider: {hasEnabledCollider}, RigidbodySimulated: {(rb != null ? rb.simulated.ToString() : "N/A")}", LogCategory.Debug);
            }
        }
        
        private void LogCollisionMatrix()
        {
            // Only log the collision matrix if logging is enabled
            if (!logCollisionInfo) return;
            
            // Get all named layers
            List<string> layerNames = new List<string>();
            for (int i = 0; i <= 31; i++)
            {
                string layerName = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(layerName))
                {
                    layerNames.Add(layerName);
                }
            }
            
            // Header
            CZLogger.LogInfo("[CollisionDebugger] Layer collision matrix:", LogCategory.Debug);
            
            // Row headers
            string header = "Layer | ";
            foreach (string layerName in layerNames)
            {
                header += $"{layerName} | ";
            }
            CZLogger.LogInfo(header, LogCategory.Debug);
            
            // Rows
            foreach (string rowLayer in layerNames)
            {
                int rowLayerIndex = LayerMask.NameToLayer(rowLayer);
                string row = $"{rowLayer} | ";
                
                foreach (string colLayer in layerNames)
                {
                    int colLayerIndex = LayerMask.NameToLayer(colLayer);
                    bool ignored = GetIgnoreLayerCollision(rowLayerIndex, colLayerIndex);
                    
                    // ✓ for collides, ✗ for ignored
                    row += ignored ? "✗ | " : "✓ | ";
                }
                
                CZLogger.LogInfo(row, LogCategory.Debug);
            }
        }
        
        /// <summary>
        /// Check and repair potential collision issues affecting the projectile
        /// </summary>
        public void CheckProjectileSetup(GameObject projectile)
        {
            if (projectile == null) return;
            
            // Use logCollisionInfo to control detailed logging
            if (logCollisionInfo)
            {
                CZLogger.LogInfo($"[CollisionDebugger] Checking projectile {projectile.name}:", LogCategory.Debug);
                CZLogger.LogInfo($"[CollisionDebugger] - Layer: {LayerMask.LayerToName(projectile.layer)} (expected: Projectile)", LogCategory.Debug);
            }
            
            // Check layer
            if (projectile.layer != projectileLayer)
            {
                CZLogger.LogWarning($"[CollisionDebugger] Projectile has wrong layer: {LayerMask.LayerToName(projectile.layer)}. Setting to Projectile layer.", LogCategory.Debug);
                projectile.layer = projectileLayer;
            }
            
            // Check collider
            CircleCollider2D collider = projectile.GetComponent<CircleCollider2D>();
            if (collider != null)
            {
                if (logCollisionInfo)
                {
                    CZLogger.LogInfo($"[CollisionDebugger] - Collider: {(collider.enabled ? "Enabled" : "Disabled")}, IsTrigger: {collider.isTrigger}", LogCategory.Debug);
                }
                
                if (!collider.enabled)
                {
                    CZLogger.LogWarning("[CollisionDebugger] Projectile collider is disabled! Enabling it.", LogCategory.Debug);
                    collider.enabled = true;
                }
                
                if (!collider.isTrigger)
                {
                    CZLogger.LogWarning("[CollisionDebugger] Projectile collider should be a trigger! Setting isTrigger to true.", LogCategory.Debug);
                    collider.isTrigger = true;
                }
            }
            
            // Check rigidbody
            Rigidbody2D rb = projectile.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                if (logCollisionInfo)
                {
                    CZLogger.LogInfo($"[CollisionDebugger] - Rigidbody: Type={rb.bodyType}, Simulated={rb.simulated}, CollisionDetection={rb.collisionDetectionMode}", LogCategory.Debug);
                }
                
                if (!rb.simulated)
                {
                    CZLogger.LogWarning("[CollisionDebugger] Projectile rigidbody is not simulated! Enabling simulation.", LogCategory.Debug);
                    rb.simulated = true;
                }
                
                if (rb.collisionDetectionMode != CollisionDetectionMode2D.Continuous)
                {
                    CZLogger.LogWarning("[CollisionDebugger] Projectile should use Continuous collision detection! Updating setting.", LogCategory.Debug);
                    rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                }
            }
            
            // Verify Projectile script
            Projectile projectileComponent = projectile.GetComponent<Projectile>();
            if (projectileComponent != null)
            {
                // Call VerifyRequiredSettings if it exists
                projectileComponent.VerifyRequiredSettings();
            }
        }
        
        /// <summary>
        /// Check and repair potential collision issues affecting the enemy
        /// </summary>
        public void CheckEnemySetup(GameObject enemy)
        {
            if (enemy == null)
            {
                CZLogger.LogInfo("[CollisionDebugger] Cannot check null enemy", LogCategory.Debug);
                return;
            }
            
            // Use logCollisionInfo to control detailed logging
            if (logCollisionInfo)
            {
                CZLogger.LogInfo($"[CollisionDebugger] Checking enemy {enemy.name}:", LogCategory.Debug);
                CZLogger.LogInfo($"[CollisionDebugger] - Layer: {LayerMask.LayerToName(enemy.layer)} (expected: Enemy)", LogCategory.Debug);
            }
            
            // Check layer
            if (enemy.layer != enemyLayer)
            {
                CZLogger.LogWarning($"[CollisionDebugger] Enemy has wrong layer: {LayerMask.LayerToName(enemy.layer)}. Setting to Enemy layer.", LogCategory.Debug);
                enemy.layer = enemyLayer;
            }
            
            // Check if IDamageable is implemented
            IDamageable damageable = enemy.GetComponent<IDamageable>();
            if (damageable == null)
            {
                CZLogger.LogError($"[CollisionDebugger] ❌ Enemy {enemy.name} does not implement IDamageable interface!", LogCategory.Debug);
            }
            else if (logCollisionInfo)
            {
                CZLogger.LogInfo($"[CollisionDebugger] ✓ Enemy implements IDamageable. MaxHealth: {damageable.MaxHealth}, CurrentHealth: {damageable.CurrentHealth}", LogCategory.Debug);
            }
            
            // Check if the enemy has colliders
            var enemyColliders = enemy.GetComponents<Collider2D>();
            if (enemyColliders == null || enemyColliders.Length == 0)
            {
                CZLogger.LogError($"[CollisionDebugger] Enemy {enemy.name} has no Collider2D components!", LogCategory.Debug);
                return;
            }
            
            // CRITICAL FIX: Check for oversized colliders that might cause player damage issues
            foreach (var currentCollider in enemyColliders)
            {
                // Check if collider is significantly larger than the sprite
                var spriteRenderer = enemy.GetComponentInChildren<SpriteRenderer>();
                if (spriteRenderer != null && currentCollider is BoxCollider2D boxCollider)
                {
                    // Get sprite bounds
                    Bounds spriteBounds = spriteRenderer.bounds;
                    Bounds colliderBounds = boxCollider.bounds;
                    
                    // Check if collider is more than 50% larger than sprite in any dimension
                    float widthRatio = colliderBounds.size.x / spriteBounds.size.x;
                    float heightRatio = colliderBounds.size.y / spriteBounds.size.y;
                    
                    if (widthRatio > 1.5f || heightRatio > 1.5f)
                    {
                        CZLogger.LogWarning($"[CollisionDebugger] Enemy {enemy.name} has oversized collider ({widthRatio:F2}x, {heightRatio:F2}y). This may cause premature player damage.", LogCategory.Debug);
                        CZLogger.LogWarning($"Enemy {enemy.name} has oversized collider: {widthRatio:F2}x, {heightRatio:F2}y. Consider resizing.", LogCategory.Debug);
                        
                        if (findAndFixMisconfigurations)
                        {
                            // Adjust collider size to better match sprite
                            Vector2 newSize = boxCollider.size;
                            if (widthRatio > 1.5f)
                                newSize.x = boxCollider.size.x / widthRatio * 1.2f; // Reduce width, keep 20% buffer
                                
                            if (heightRatio > 1.5f)
                                newSize.y = boxCollider.size.y / heightRatio * 1.2f; // Reduce height, keep 20% buffer
                                
                            boxCollider.size = newSize;
                            CZLogger.LogInfo($"[CollisionDebugger] ✓ Adjusted collider size on {enemy.name} to better match sprite", LogCategory.Debug);
                            CZLogger.LogInfo($"Adjusted collider size on {enemy.name} to better match sprite", LogCategory.Debug);
                        }
                    }
                }
            }
            
            // Check all colliders for issues
            bool allCollidersAreTriggers = true;
            
            foreach (var currentCollider in enemyColliders)
            {
                if (logCollisionInfo)
                {
                    CZLogger.LogInfo($"[CollisionDebugger] - Collider: {(currentCollider.enabled ? "Enabled" : "Disabled")}, IsTrigger: {currentCollider.isTrigger}", LogCategory.Debug);
                }
                
                if (!currentCollider.enabled)
                {
                    CZLogger.LogWarning("[CollisionDebugger] Enemy collider is disabled! Enabling it.", LogCategory.Debug);
                    currentCollider.enabled = true;
                }
                
                // Keep track of trigger state
                if (!currentCollider.isTrigger)
                {
                    allCollidersAreTriggers = false;
                }
                
                // For detailed logging, check capture settings
                if (logCollisionInfo)
                {
                    int projectileMask = 1 << projectileLayer;
                    
                    CZLogger.LogInfo($"[CollisionDebugger] - ContactCaptureLayers: {currentCollider.contactCaptureLayers.value}", LogCategory.Debug);
                    CZLogger.LogInfo($"[CollisionDebugger] - CallbackLayers: {currentCollider.callbackLayers.value}", LogCategory.Debug);
                    
                    if ((currentCollider.contactCaptureLayers.value & projectileMask) == 0)
                    {
                        CZLogger.LogWarning("[CollisionDebugger] Enemy collider cannot capture contacts from Projectile layer! Fixing.", LogCategory.Debug);
                        currentCollider.contactCaptureLayers = Physics2D.AllLayers;
                    }
                    
                    if ((currentCollider.callbackLayers.value & projectileMask) == 0)
                    {
                        CZLogger.LogWarning("[CollisionDebugger] Enemy collider will not receive callbacks from Projectile layer! Fixing.", LogCategory.Debug);
                        currentCollider.callbackLayers = Physics2D.AllLayers;
                    }
                }
            }
            
            // Ensure at least one collider is non-trigger for enemies
            if (allCollidersAreTriggers && enemyColliders.Length > 0 && findAndFixMisconfigurations)
            {
                CZLogger.LogWarning($"[CollisionDebugger] Enemy {enemy.name} has all colliders as triggers! Setting one to solid.", LogCategory.Debug);
                enemyColliders[0].isTrigger = false;
            }
            
            // Check for rigidbody
            Rigidbody2D rb = enemy.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                CZLogger.LogWarning($"[CollisionDebugger] Enemy {enemy.name} has no Rigidbody2D component!", LogCategory.Debug);
            }
            else
            {
                if (logCollisionInfo)
                {
                    CZLogger.LogInfo($"[CollisionDebugger] - Rigidbody: Type={rb.bodyType}, Simulated={rb.simulated}, CollisionDetection={rb.collisionDetectionMode}", LogCategory.Debug);
                }
                
                if (!rb.simulated)
                {
                    CZLogger.LogWarning("[CollisionDebugger] Enemy rigidbody is not simulated! Enabling simulation.", LogCategory.Debug);
                    rb.simulated = true;
                }
                
                if (rb.collisionDetectionMode != CollisionDetectionMode2D.Continuous && findAndFixMisconfigurations)
                {
                    CZLogger.LogWarning("[CollisionDebugger] Setting enemy collision detection mode to Continuous for more reliable collisions.", LogCategory.Debug);
                    rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                }
            }
        }
        
        /// <summary>
        /// Force a verification of the Physics2D settings
        /// </summary>
        public void ForceVerification()
        {
            CZLogger.LogInfo("[CollisionDebugger] Forcing verification of Physics2D settings...", LogCategory.Debug);
            CZLogger.LogInfo("[CollisionDebugger] Force verification triggered - fixing collision issues", LogCategory.Debug);
            
            // Reset physics settings that might be interfering with collisions
            queriesHitTriggers = true;
            queriesStartInColliders = false;
            
            // Explicitly enable critical layer collisions
            IgnoreLayerCollision(projectileLayer, enemyLayer, false); // Enable projectile-enemy collisions
            IgnoreLayerCollision(playerLayer, enemyLayer, false); // Enable player-enemy collisions
            
            // Run standard verification
            VerifyPhysics2DSettings();
            FindAndFixMisconfigurations();
            
            // Reset fix attempt counter
            currentFixAttempts = 0;
            lastFixTime = 0;
            hasFixedCollisions = false;
            
            // Log collision matrix to confirm settings
            CZLogger.LogInfo("[CollisionDebugger] Force verification complete. Current collision matrix:", LogCategory.Debug);
            LogCollisionMatrix();
        }
        
        /// <summary>
        /// Immediately fixes critical collision issues, particularly focusing on projectile-enemy and player-enemy interactions.
        /// This method can be called from other systems (like GameManager) when collision issues are detected during gameplay.
        /// </summary>
        /// <returns>True if all issues were fixed, false if some issues remain</returns>
        public bool FixCriticalCollisionIssues()
        {
            CZLogger.LogInfo("[CollisionDebugger] Emergency collision fix requested", LogCategory.Debug);
            CZLogger.LogInfo("[CollisionDebugger] Emergency collision fix requested - resolving critical issues", LogCategory.Debug);
            
            bool success = true;
            
            // 1. Fix Physics2D global settings
            Physics2D.queriesHitTriggers = true;
            Physics2D.queriesStartInColliders = false;
            Physics2D.callbacksOnDisable = true;
            
            // 2. Fix critical layer collision settings
            if (Physics2D.GetIgnoreLayerCollision(projectileLayer, enemyLayer))
            {
                Physics2D.IgnoreLayerCollision(projectileLayer, enemyLayer, false);
                if (Physics2D.GetIgnoreLayerCollision(projectileLayer, enemyLayer))
                {
                    CZLogger.LogError("CRITICAL: Failed to enable projectile-enemy collisions!", LogCategory.Debug);
                    CZLogger.LogError("CRITICAL COLLISION ERROR: Failed to enable projectile-enemy collisions!", LogCategory.Debug);
                    success = false;
                }
                else
                {
                    CZLogger.LogInfo("Fixed: Projectiles can now collide with enemies", LogCategory.Debug);
                    CZLogger.LogInfo("Fixed: Projectiles can now collide with enemies", LogCategory.Debug);
                }
            }
            
            if (Physics2D.GetIgnoreLayerCollision(playerLayer, enemyLayer))
            {
                Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, false);
                if (Physics2D.GetIgnoreLayerCollision(playerLayer, enemyLayer))
                {
                    CZLogger.LogError("CRITICAL: Failed to enable player-enemy collisions!", LogCategory.Debug);
                    CZLogger.LogError("CRITICAL COLLISION ERROR: Failed to enable player-enemy collisions!", LogCategory.Debug);
                    success = false;
                }
                else
                {
                    CZLogger.LogInfo("Fixed: Player can now be damaged by enemies", LogCategory.Debug);
                    CZLogger.LogInfo("Fixed: Player can now be damaged by enemies", LogCategory.Debug);
                }
            }
            
            // 3. Find and fix all active projectiles
            var projectiles = FindObjectsByType<Projectile>(FindObjectsSortMode.None);
            CZLogger.LogInfo($"Found {projectiles.Length} projectiles to fix", LogCategory.Debug);
            
            foreach (var projectile in projectiles)
            {
                if (projectile != null && projectile.gameObject != null)
                {
                    // CRITICAL FIX: Ensure correct layer
                    if (projectile.gameObject.layer != projectileLayer)
                    {
                        CZLogger.LogWarning($"[CollisionDebugger] Projectile {projectile.name} has incorrect layer: {LayerMask.LayerToName(projectile.gameObject.layer)}. Setting to Projectile layer.", LogCategory.Debug);
                        projectile.gameObject.layer = projectileLayer;
                    }
                    
                    // Ensure colliders are triggers and enabled
                    var colliders = projectile.GetComponents<Collider2D>();
                    bool hasEnabledCollider = false;
                    
                    foreach (var currentCollider in colliders)
                    {
                        // CRITICAL FIX: Ensure collider is a trigger
                        if (!currentCollider.isTrigger)
                        {
                            CZLogger.LogWarning($"[CollisionDebugger] Projectile {projectile.name} collider is not a trigger. Setting isTrigger=true.", LogCategory.Debug);
                            currentCollider.isTrigger = true;
                        }
                        
                        // CRITICAL FIX: Ensure collider is enabled
                        if (!currentCollider.enabled)
                        {
                            CZLogger.LogWarning($"[CollisionDebugger] Projectile {projectile.name} collider is disabled. Enabling it.", LogCategory.Debug);
                            currentCollider.enabled = true;
                        }
                        
                        // Set contact and callback layers to include Enemy layer
                        int enemyMask = 1 << enemyLayer;
                        if ((currentCollider.contactCaptureLayers.value & enemyMask) == 0)
                        {
                            currentCollider.contactCaptureLayers = Physics2D.AllLayers;
                        }
                        
                        if ((currentCollider.callbackLayers.value & enemyMask) == 0)
                        {
                            currentCollider.callbackLayers = Physics2D.AllLayers;
                        }
                        
                        if (currentCollider.enabled)
                        {
                            hasEnabledCollider = true;
                        }
                    }
                    
                    // If no colliders or no enabled colliders, add one
                    if (colliders.Length == 0 || !hasEnabledCollider)
                    {
                        CZLogger.LogWarning($"[CollisionDebugger] Projectile {projectile.name} has no enabled colliders. Adding CircleCollider2D.", LogCategory.Debug);
                        CircleCollider2D newCollider = projectile.gameObject.AddComponent<CircleCollider2D>();
                        newCollider.radius = 0.3f;
                        newCollider.isTrigger = true;
                        newCollider.enabled = true;
                    }
                    
                    // Ensure rigidbody is set up correctly
                    if (projectile.TryGetComponent<Rigidbody2D>(out var rb))
                    {
                        if (!rb.simulated)
                        {
                            CZLogger.LogWarning($"[CollisionDebugger] Projectile {projectile.name} rigidbody is not simulated. Enabling simulation.", LogCategory.Debug);
                            rb.simulated = true;
                        }
                        
                        if (rb.collisionDetectionMode != CollisionDetectionMode2D.Continuous)
                        {
                            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                        }
                    }
                    else
                    {
                        // Add rigidbody if missing
                        CZLogger.LogWarning($"[CollisionDebugger] Projectile {projectile.name} has no Rigidbody2D. Adding one.", LogCategory.Debug);
                        Rigidbody2D newRb = projectile.gameObject.AddComponent<Rigidbody2D>();
                        newRb.gravityScale = 0f;
                        newRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                        newRb.simulated = true;
                        newRb.interpolation = RigidbodyInterpolation2D.Interpolate;
                        newRb.constraints = RigidbodyConstraints2D.FreezeRotation;
                    }
                }
            }
            
            // 4. Find and fix all active enemies
            var enemies = FindObjectsByType<BaseEnemy>(FindObjectsSortMode.None);
            CZLogger.LogInfo($"Found {enemies.Length} enemies to fix", LogCategory.Debug);
            
            foreach (var enemy in enemies)
            {
                if (enemy != null && enemy.gameObject != null)
                {
                    // CRITICAL FIX: Ensure correct layer
                    if (enemy.gameObject.layer != enemyLayer)
                    {
                        CZLogger.LogWarning($"[CollisionDebugger] Enemy {enemy.name} has incorrect layer: {LayerMask.LayerToName(enemy.gameObject.layer)}. Setting to Enemy layer.", LogCategory.Debug);
                        enemy.gameObject.layer = enemyLayer;
                    }
                    
                    // Fix colliders - ensure at least one non-trigger collider for physics
                    var colliders = enemy.GetComponents<Collider2D>();
                    bool hasNonTrigger = false;
                    bool hasEnabledCollider = false;
                    
                    if (colliders.Length > 0)
                    {
                        foreach (var currentCollider in colliders)
                        {
                            // CRITICAL FIX: Ensure collider is enabled
                            if (!currentCollider.enabled)
                            {
                                CZLogger.LogWarning($"[CollisionDebugger] Enemy {enemy.name} collider is disabled. Enabling it.", LogCategory.Debug);
                                currentCollider.enabled = true;
                            }
                            
                            // Set contact and callback layers to include Projectile layer
                            int projectileMask = 1 << projectileLayer;
                            if ((currentCollider.contactCaptureLayers.value & projectileMask) == 0)
                            {
                                currentCollider.contactCaptureLayers = Physics2D.AllLayers;
                            }
                            
                            if ((currentCollider.callbackLayers.value & projectileMask) == 0)
                            {
                                currentCollider.callbackLayers = Physics2D.AllLayers;
                            }
                            
                            // Check if this is a non-trigger collider
                            if (!currentCollider.isTrigger)
                            {
                                hasNonTrigger = true;
                            }
                            
                            if (currentCollider.enabled)
                            {
                                hasEnabledCollider = true;
                            }
                            
                            // Check and fix oversized colliders
                            if (currentCollider is BoxCollider2D boxCollider)
                            {
                                var spriteRenderer = enemy.GetComponentInChildren<SpriteRenderer>();
                                if (spriteRenderer != null)
                                {
                                    Bounds spriteBounds = spriteRenderer.bounds;
                                    Vector2 newSize = boxCollider.size;
                                    
                                    // Adjust oversized dimensions
                                    if (boxCollider.size.x > spriteBounds.size.x * 1.5f)
                                    {
                                        newSize.x = spriteBounds.size.x * 1.2f;
                                    }
                                    if (boxCollider.size.y > spriteBounds.size.y * 1.5f)
                                    {
                                        newSize.y = spriteBounds.size.y * 1.2f;
                                    }
                                    
                                    boxCollider.size = newSize;
                                }
                            }
                        }
                        
                        // CRITICAL FIX: If all colliders are triggers, make one non-trigger for physics
                        if (!hasNonTrigger && colliders.Length > 0)
                        {
                            CZLogger.LogWarning($"[CollisionDebugger] Enemy {enemy.name} has no non-trigger colliders. Setting one to non-trigger.", LogCategory.Debug);
                            colliders[0].isTrigger = false;
                        }
                        
                        // CRITICAL FIX: If no colliders are enabled, enable one
                        if (!hasEnabledCollider && colliders.Length > 0)
                        {
                            CZLogger.LogWarning($"[CollisionDebugger] Enemy {enemy.name} has no enabled colliders. Enabling one.", LogCategory.Debug);
                            colliders[0].enabled = true;
                        }
                    }
                    else
                    {
                        // Add a collider if missing
                        CZLogger.LogWarning($"[CollisionDebugger] Enemy {enemy.name} has no colliders. Adding BoxCollider2D.", LogCategory.Debug);
                        BoxCollider2D newCollider = enemy.gameObject.AddComponent<BoxCollider2D>();
                        newCollider.isTrigger = false;
                        
                        // Size based on sprite if available
                        var spriteRenderer = enemy.GetComponentInChildren<SpriteRenderer>();
                        if (spriteRenderer != null)
                        {
                            Bounds spriteBounds = spriteRenderer.bounds;
                            newCollider.size = new Vector2(spriteBounds.size.x * 0.9f, spriteBounds.size.y * 0.9f);
                        }
                    }
                    
                    // Ensure enemy has a damage dealer component
                    EnemyDamageDealer damageDealer = enemy.GetComponent<EnemyDamageDealer>();
                    if (damageDealer == null)
                    {
                        CZLogger.LogWarning($"[CollisionDebugger] Enemy {enemy.name} has no EnemyDamageDealer component. Adding one.", LogCategory.Debug);
                        damageDealer = enemy.gameObject.AddComponent<EnemyDamageDealer>();
                    }
                    
                    // Ensure enemy has a rigidbody for physics
                    if (!enemy.TryGetComponent<Rigidbody2D>(out var rb))
                    {
                        CZLogger.LogWarning($"[CollisionDebugger] Enemy {enemy.name} has no Rigidbody2D. Adding one.", LogCategory.Debug);
                        rb = enemy.gameObject.AddComponent<Rigidbody2D>();
                        rb.gravityScale = 0f;
                        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                    }
                    
                    // Ensure rigidbody is simulated
                    if (rb != null && !rb.simulated)
                    {
                        CZLogger.LogWarning($"[CollisionDebugger] Enemy {enemy.name} rigidbody is not simulated. Enabling simulation.", LogCategory.Debug);
                        rb.simulated = true;
                    }
                }
            }
            
            CZLogger.LogInfo("[CollisionDebugger] Emergency collision fix complete - " + 
                      (success ? "All issues resolved" : "Some issues could not be fixed"), LogCategory.Debug);
            
            return success;
        }
    }
} 