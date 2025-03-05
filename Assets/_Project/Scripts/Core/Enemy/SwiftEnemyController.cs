using UnityEngine;
using NaughtyAttributes;
using CZ.Core.Logging;
using CZ.Core.Interfaces;
using System.Collections;
using Unity.Profiling;

namespace CZ.Core.Enemy
{
    /// <summary>
    /// SwiftEnemyController implements a fast-moving enemy type that can dodge player attacks
    /// </summary>
    /// <remarks>
    /// This class extends BaseEnemy and adds:
    /// - Fast, agile movement with randomness
    /// - Dodge capability to avoid player and projectiles
    /// - Animation control through Animator
    /// - Screen boundary correction to prevent enemies moving offscreen
    /// </remarks>
    [RequireComponent(typeof(Animator))]
    public class SwiftEnemyController : BaseEnemy
    {
        #region Performance Profiling
        private static readonly ProfilerMarker s_movementMarker = 
            new ProfilerMarker(ProfilerCategory.Scripts, "SwiftEnemy.UpdateMovement");
        private static readonly ProfilerMarker s_dodgeMarker = 
            new ProfilerMarker(ProfilerCategory.Scripts, "SwiftEnemy.DodgeBehavior");
        #endregion

        #region Components
        private Animator animator;
        #endregion

        #region Testing Support
        /// <summary>
        /// Target transform for testing purposes. This is null in gameplay but available for tests.
        /// </summary>
        public Transform CurrentTarget { get; private set; }
        #endregion

        #region Configuration
        [Header("Swift Enemy Configuration")]
        
        [BoxGroup("Movement Settings")]
        [SerializeField, MinValue(5f), MaxValue(15f)]
        [InfoBox("Base movement speed multiplier", EInfoBoxType.Normal)]
        private float speedMultiplier = 1.5f;
        
        [BoxGroup("Movement Settings")]
        [SerializeField, MinValue(0.1f), MaxValue(5f)]
        [InfoBox("How quickly the enemy can change direction", EInfoBoxType.Normal)]
        private float agility = 1.2f;
        
        [BoxGroup("Movement Settings")]
        [SerializeField, Range(0f, 1f)]
        [InfoBox("How random the movement direction should be (0 = direct, 1 = random)", EInfoBoxType.Normal)]
        private float movementRandomness = 0.5f;
        
        [BoxGroup("Movement Settings")]
        [SerializeField, MinValue(0.1f), MaxValue(2f)]
        [InfoBox("How frequently direction changes occur", EInfoBoxType.Normal)]
        private float directionChangeInterval = 0.5f;
        
        [BoxGroup("Dodge Behavior")]
        [SerializeField]
        [InfoBox("Whether this enemy can dodge", EInfoBoxType.Normal)]
        private bool canDodge = true;
        
        [BoxGroup("Dodge Behavior")]
        [SerializeField, MinValue(0.1f), MaxValue(10f)]
        [InfoBox("Force applied during dodge", EInfoBoxType.Normal)]
        private float dodgeForce = 8f;
        
        [BoxGroup("Dodge Behavior")]
        [SerializeField, MinValue(0.1f), MaxValue(5f)]
        [InfoBox("Cooldown between dodge attempts", EInfoBoxType.Normal)]
        private float dodgeCooldown = 1.5f;
        
        [BoxGroup("Dodge Behavior")]
        [SerializeField, Range(0f, 1f)]
        [InfoBox("Probability of dodge when conditions are met", EInfoBoxType.Normal)]
        private float dodgeProbability = 0.7f;
        
        [BoxGroup("Dodge Behavior")]
        [SerializeField, MinValue(1f), MaxValue(10f)]
        [InfoBox("Maximum distance to initiate dodge", EInfoBoxType.Normal)]
        private float dodgeDetectionRange = 5f;
        
        [BoxGroup("Health Settings")]
        [SerializeField, Range(0.5f, 1.0f)]
        [InfoBox("Health multiplier for swift enemies (lower than base)", EInfoBoxType.Normal)]
        private float healthMultiplier = 0.75f;
        
        [BoxGroup("Animation")]
        [SerializeField]
        [InfoBox("Parameter name for movement speed", EInfoBoxType.Normal)]
        private string speedParameterName = "Speed";
        
        [BoxGroup("Animation")]
        [SerializeField]
        [InfoBox("Parameter name for dodge trigger", EInfoBoxType.Normal)]
        private string dodgeTriggerName = "Dodge";
        
        [BoxGroup("Animation")]
        [SerializeField]
        [InfoBox("Parameter name for taking damage", EInfoBoxType.Normal)]
        private string damageTriggerName = "TakeDamage";
        #endregion

        #region State Variables
        private Vector2 currentMoveDirection;
        private float lastDirectionChangeTime;
        private float lastDodgeTime;
        private bool isDodging;
        private Coroutine dodgeCoroutine;
        private System.Random randomGenerator;
        private int seed;

        // Add property to store target
        private Transform targetTransform;

        // Property to access the target transform
        protected Transform TargetTransform => targetTransform;

        // Add helpful property method to check if we have a valid target
        protected bool HasValidTarget => TargetTransform != null;
        #endregion

        #region Unity Lifecycle
        protected virtual void Awake()
        {
            // Get required components
            animator = GetComponent<Animator>();
            
            // Set unique seed for this instance
            seed = System.Guid.NewGuid().GetHashCode();
            randomGenerator = new System.Random(seed);
            
            // Initialize state
            lastDirectionChangeTime = Time.time;
            lastDodgeTime = -dodgeCooldown; // Allow immediate first dodge
            currentMoveDirection = Random.insideUnitCircle.normalized;
            
            // Ensure the sprite renderer is enabled
            SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
            }
            
            // Ensure proper layer assignment
            gameObject.layer = LayerMask.NameToLayer("Enemy");
        }
        
        protected virtual void Start()
        {
            // Customize physics for swift enemies
            if (Rb != null)
            {
                Rb.mass = 1.0f;  // Lower mass than standard enemies
                Rb.linearDamping = 0.2f;  // Lower drag for more responsive movement
                Rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }
            
            // CRITICAL FIX: Ensure proper layer and collider settings
            gameObject.layer = LayerMask.NameToLayer("Enemy");
            
            // Ensure colliders are properly set up for collision detection
            var colliders = GetComponents<Collider2D>();
            bool hasEnabledCollider = false;
            
            if (colliders != null && colliders.Length > 0)
            {
                foreach (var currentCollider in colliders)
                {
                    // Swift enemies should use non-trigger colliders to detect projectile hits
                    currentCollider.isTrigger = false;
                    currentCollider.enabled = true;
                    hasEnabledCollider = true;
                    
                    // Log for debugging
                    Debug.Log($"[SwiftEnemy] Set up collider: {currentCollider.GetType().Name}, enabled: {currentCollider.enabled}, trigger: {currentCollider.isTrigger}");
                }
                
                // Make sure at least one collider is enabled
                if (!hasEnabledCollider && colliders.Length > 0)
                {
                    // If no colliders were enabled, enable the first one
                    colliders[0].enabled = true;
                    Debug.LogWarning($"[SwiftEnemy] No enabled colliders found, enabling collider: {colliders[0].GetType().Name}");
                }
            }
        }

        // Override the base class's FixedUpdate method
        protected override void FixedUpdate()
        {
            // Call base implementation first
            base.FixedUpdate();
            
            if (IsDead) return;

            try {
                // Update enemy position based on current direction and speed
                UpdateMovement();
                
                // Check for dodge opportunities if not already dodging
                if (!isDodging && Time.time - lastDodgeTime > dodgeCooldown)
                {
                    CheckForDodgeOpportunity();
                }
                
                // Update animator if available
                if (animator != null)
                {
                    try
                    {
                        float currentSpeed = Rb.linearVelocity.magnitude / MoveSpeedValue;
                        animator.SetFloat(speedParameterName, currentSpeed);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[SwiftEnemy] Error updating animator: {e.Message}");
                    }
                }
                
                // Ensure enemy stays within reasonable boundaries
                KeepEnemyOnScreen();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SwiftEnemy] Error in FixedUpdate: {e.Message}");
            }
        }

        // Override SetTarget from BaseEnemy to also store the transform
        public override void SetTarget(Vector3 position)
        {
            // Set transform if we have a valid CurrentTarget
            if (CurrentTarget != null)
            {
                targetTransform = CurrentTarget;
            }
            
            // Call base implementation
            base.SetTarget(position);
        }

        // Make sure this is implemented to update targetTransform
        public void SetTargetTransformForTesting(Transform target)
        {
            targetTransform = target;
            
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            // In editor or development builds, we can use the testing mechanism
            // This might be a Transform that isn't part of a typical Entity
            CurrentTarget = target; 
            CZLogger.LogDebug($"Target transform set via testing method: {(target != null ? target.name : "null")}", LogCategory.Enemy);
            #endif
        }
        #endregion

        #region Movement Methods
        private void UpdateMovement()
        {
            using (s_movementMarker.Auto())
            {
                try
                {
                    // Don't update movement if dodging
                    if (isDodging) return;
                    
                    // Change direction periodically with some randomness
                    if (Time.time - lastDirectionChangeTime > directionChangeInterval)
                    {
                        // Get direction toward target if we have one
                        Vector2 targetDirection = Vector2.zero;
                        if (TargetTransform != null)
                        {
                            // Calculate direction toward player (this is the key fix - we want to move TOWARD the player)
                            targetDirection = ((Vector2)TargetTransform.position - (Vector2)transform.position).normalized;
                            
                            // Add some randomness but still favor moving toward player
                            Vector2 randomOffset = Random.insideUnitCircle * movementRandomness;
                            currentMoveDirection = (targetDirection * (1f - movementRandomness) + randomOffset).normalized;
                            
                            Debug.Log($"[SwiftEnemy] Updated movement direction toward target: {currentMoveDirection}");
                        }
                        else
                        {
                            // No target, move randomly with some persistence
                            Vector2 randomDirection = Random.insideUnitCircle.normalized;
                            currentMoveDirection = (currentMoveDirection * (1f - movementRandomness) + randomDirection * movementRandomness).normalized;
                        }
                        
                        // Set timer for next direction change
                        lastDirectionChangeTime = Time.time;
                    }
                    
                    // Calculate target velocity based on current direction
                    Vector2 targetVelocity = currentMoveDirection * MoveSpeedValue;
                    
                    // Apply movement with appropriate agility
                    Rb.linearVelocity = Vector2.Lerp(Rb.linearVelocity, targetVelocity, Time.fixedDeltaTime * agility);
                    
                    // Update facing direction based on movement
                    if (Rb.linearVelocity.sqrMagnitude > 0.1f)
                    {
                        float angle = Mathf.Atan2(Rb.linearVelocity.y, Rb.linearVelocity.x) * Mathf.Rad2Deg;
                        transform.rotation = Quaternion.Euler(0, 0, angle);
                    }
                    
                    // Update animator speed parameter
                    if (animator != null)
                    {
                        float speed = Rb.linearVelocity.magnitude / MoveSpeedValue;
                        animator.SetFloat(speedParameterName, speed);
                    }
                }
                finally
                {
                    s_movementMarker.End();
                }
            }
        }
        
        private void CheckForDodgeOpportunity()
        {
            using (s_dodgeMarker.Auto())
            {
                if (!canDodge) return;
                
                // Find potential threats (projectiles or player) within detection range
                Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, dodgeDetectionRange, LayerMask.GetMask("Player", "Projectile"));
                
                foreach (var collider in colliders)
                {
                    // Skip if the collider is part of this enemy
                    if (collider.gameObject == gameObject) continue;
                    
                    Vector2 threatPosition = collider.transform.position;
                    Vector2 threatDirection = (threatPosition - (Vector2)transform.position).normalized;
                    
                    // Check if it's a projectile or player using interfaces
                    bool isProjectile = collider.GetComponent<IProjectileIdentifier>() != null;
                    bool isPlayer = collider.GetComponent<IPlayerIdentifier>() != null;
                    
                    if (isProjectile || isPlayer)
                    {
                        // Roll a random chance to dodge
                        if (Random.value < dodgeProbability)
                        {
                            PerformDodge(threatDirection);
                            break;
                        }
                    }
                }
            }
        }
        
        private void PerformDodge(Vector2 threatDirection)
        {
            // Already dodging
            if (isDodging) return;
            
            // Mark dodge time
            lastDodgeTime = Time.time;
            
            // Calculate dodge direction (perpendicular to threat)
            Vector2 dodgeDirection = new Vector2(-threatDirection.y, threatDirection.x);
            
            // Randomly choose between the two perpendicular directions
            if (Random.value < 0.5f)
            {
                dodgeDirection = -dodgeDirection;
            }
            
            // Trigger animation
            if (animator != null)
            {
                animator.SetTrigger(dodgeTriggerName);
            }
            
            // Start dodge coroutine
            if (dodgeCoroutine != null)
            {
                StopCoroutine(dodgeCoroutine);
            }
            
            // Use reduced dodge force to prevent going too far off screen
            float adjustedDodgeForce = Mathf.Min(dodgeForce, 8f);
            
            dodgeCoroutine = StartCoroutine(DodgeCoroutine(dodgeDirection, adjustedDodgeForce));
            
            CZLogger.LogDebug($"Swift enemy performing dodge in direction: {dodgeDirection} with force: {adjustedDodgeForce}", LogCategory.Enemy);
        }
        
        private IEnumerator DodgeCoroutine(Vector2 dodgeDirection, float force)
        {
            isDodging = true;
            
            // Apply dodge force
            Rb.linearVelocity = Vector2.zero; // Reset velocity first
            Rb.AddForce(dodgeDirection * force, ForceMode2D.Impulse);
            
            // Temporary disable standard movement
            yield return new WaitForSeconds(0.2f); // Reduced from 0.3f for quicker recovery
            
            // Gradual slow down after dodge
            float slowdownTime = 0.2f;
            float startTime = Time.time;
            while (Time.time < startTime + slowdownTime)
            {
                float t = (Time.time - startTime) / slowdownTime;
                Rb.linearVelocity = Vector2.Lerp(Rb.linearVelocity, Vector2.zero, t);
                yield return null;
            }
            
            // Reset state
            Rb.linearVelocity = Vector2.zero;
            isDodging = false;
            dodgeCoroutine = null;
            
            // Force an update of movement direction toward player
            if (TargetTransform != null)
            {
                Vector2 toPlayer = ((Vector2)TargetTransform.position - (Vector2)transform.position).normalized;
                currentMoveDirection = toPlayer;
                lastDirectionChangeTime = Time.time;
            }
        }
        #endregion

        #region Damage Override
        /// <summary>
        /// Handle damage and trigger visual feedback
        /// </summary>
        public override void TakeDamage(int damage)
        {
            base.TakeDamage(damage);
            
            // Play damage animation if not dead
            if (!IsDead && animator != null)
            {
                animator.SetTrigger(damageTriggerName);
            }
        }
        
        /// <summary>
        /// Handle damage with DamageType and trigger visual feedback
        /// </summary>
        public override void TakeDamage(int damage, DamageType damageType)
        {
            base.TakeDamage(damage, damageType);
            
            // Play damage animation if not dead
            if (!IsDead && animator != null)
            {
                animator.SetTrigger(damageTriggerName);
            }
        }
        #endregion

        #region Pooling
        /// <summary>
        /// Initialize the enemy when it's spawned from the pool
        /// </summary>
        public void Initialize(int baseHealthValue, float moveSpeedValue)
        {
            // Set base properties
            MaxHealthValue = Mathf.RoundToInt(baseHealthValue * healthMultiplier);
            CurrentHealthValue = MaxHealthValue;
            MoveSpeedValue = moveSpeedValue * speedMultiplier;
            
            // Reset state
            IsDeadValue = false;
            isDodging = false;
            lastDodgeTime = -dodgeCooldown; // Allow immediate first dodge
            currentMoveDirection = Random.insideUnitCircle.normalized;
            
            // Initialize with random seed for consistent behavior in tests
            seed = UnityEngine.Random.Range(1, 10000);
            randomGenerator = new System.Random(seed);
            
            // Reset any active coroutines
            if (dodgeCoroutine != null)
            {
                StopCoroutine(dodgeCoroutine);
                dodgeCoroutine = null;
            }
            
            // Reset velocity
            if (Rb != null)
            {
                Rb.linearVelocity = Vector2.zero;
            }
            
            gameObject.SetActive(true);
        }
        
        /// <summary>
        /// Called when the object is retrieved from the pool
        /// </summary>
        public override void OnSpawn()
        {
            base.OnSpawn();
            
            // Ensure the game object and sprite renderer are enabled when spawned
            gameObject.SetActive(true);
            
            // Update target transform if we have a CurrentTarget
            if (CurrentTarget != null)
            {
                targetTransform = CurrentTarget.transform;
            }
            
            // CRITICAL FIX: Ensure proper layer is set
            gameObject.layer = LayerMask.NameToLayer("Enemy");
            
            // CRITICAL FIX: Ensure all colliders are set up correctly
            var colliders = GetComponents<Collider2D>();
            bool hasEnabledCollider = false;
            
            if (colliders != null && colliders.Length > 0)
            {
                foreach (var currentCollider in colliders)
                {
                    // Swift enemies should use non-trigger colliders to detect projectile hits
                    currentCollider.isTrigger = false;
                    currentCollider.enabled = true;
                    hasEnabledCollider = true;
                    
                    // Log for debugging
                    Debug.Log($"[SwiftEnemy] Set up collider: {currentCollider.GetType().Name}, enabled: {currentCollider.enabled}, trigger: {currentCollider.isTrigger}");
                }
                
                // Make sure at least one collider is enabled
                if (!hasEnabledCollider && colliders.Length > 0)
                {
                    // If no colliders were enabled, enable the first one
                    colliders[0].enabled = true;
                    Debug.LogWarning($"[SwiftEnemy] No enabled colliders found, enabling collider: {colliders[0].GetType().Name}");
                }
            }
            
            SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
                spriteRenderer.color = new Color(spriteRenderer.color.r, spriteRenderer.color.g, spriteRenderer.color.b, 1f); // Ensure full opacity
                
                // Fix material issues - ensure we're using Sprites/Default shader for proper rendering
                if (spriteRenderer.material == null || spriteRenderer.material.shader.name.Contains("Lit"))
                {
                    spriteRenderer.material = new Material(Shader.Find("Sprites/Default"));
                    Debug.Log("[SwiftEnemy] Updated sprite material to Sprites/Default");
                }
            }
            
            // Reset state variables
            isDodging = false;
            if (dodgeCoroutine != null)
            {
                StopCoroutine(dodgeCoroutine);
                dodgeCoroutine = null;
            }
            
            // Ensure the animator is in the proper state if we have one
            if (animator != null)
            {
                animator.SetFloat(speedParameterName, 0);
                animator.enabled = true;
                animator.Rebind(); // Reset animator state
                
                // Check if there's a controller and warn if not
                if (animator.runtimeAnimatorController == null)
                {
                    Debug.LogWarning("[SwiftEnemy] Animator has no controller assigned!");
                }
            }
            
            Debug.Log($"[SwiftEnemy] OnSpawn complete - Sprite visible: {(spriteRenderer ? spriteRenderer.enabled : false)}, Layer: {LayerMask.LayerToName(gameObject.layer)}, Material: {(spriteRenderer ? spriteRenderer.material.shader.name : "none")}");
        }
        
        /// <summary>
        /// Called when the object is returned to the pool
        /// </summary>
        public override void OnDespawn()
        {
            base.OnDespawn();
            
            // Ensure we stop any active coroutines
            if (dodgeCoroutine != null)
            {
                StopCoroutine(dodgeCoroutine);
                dodgeCoroutine = null;
            }
            
            isDodging = false;
            
            // Log despawn for debugging
            Debug.Log("[SwiftEnemy] Successfully despawned");
        }
        #endregion

        #region Testing Support
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Expose properties for testing
        public bool IsDodging => isDodging;
        public float LastDodgeTime => lastDodgeTime;
        public Vector2 CurrentMoveDirection => currentMoveDirection;
        
        // Visualize dodge radius in editor
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, dodgeDetectionRange);
        }
        #endif
        #endregion

        // Add a method to keep the enemy on screen
        private void KeepEnemyOnScreen()
        {
            // Get screen boundaries with some padding
            if (Camera.main == null) return;
            
            float padding = 2f; // Units beyond screen edge before correcting
            
            Vector3 viewportPosition = Camera.main.WorldToViewportPoint(transform.position);
            bool needsCorrection = false;
            
            // Check if we're too far outside the viewport
            if (viewportPosition.x < -padding || viewportPosition.x > 1 + padding || 
                viewportPosition.y < -padding || viewportPosition.y > 1 + padding)
            {
                needsCorrection = true;
            }
            
            if (needsCorrection)
            {
                // Calculate direction toward center of screen
                Vector3 screenCenter = new Vector3(0.5f, 0.5f, viewportPosition.z);
                Vector3 worldCenter = Camera.main.ViewportToWorldPoint(screenCenter);
                Vector2 directionToCenter = ((Vector2)worldCenter - (Vector2)transform.position).normalized;
                
                // Set movement direction toward center
                currentMoveDirection = directionToCenter;
                lastDirectionChangeTime = Time.time + directionChangeInterval; // Prevent immediate direction change
                
                Debug.Log($"[SwiftEnemy] Correcting position to stay on screen. Current position: {transform.position}, Direction to center: {directionToCenter}");
            }
        }

        #region Collision Handling

        /// <summary>
        /// OnCollisionEnter2D is called when this collider/rigidbody has begun touching another rigidbody/collider
        /// </summary>
        /// <param name="collision">The collision data containing information about the collision</param>
        protected override void OnCollisionEnter2D(Collision2D collision)
        {
            // Check if colliding with a projectile
            if (collision.gameObject.layer == LayerMask.NameToLayer("Projectile"))
            {
                // Log for debugging
                Debug.Log($"[SwiftEnemy] Hit by projectile: {collision.gameObject.name} at position {transform.position}");

                // Use reflection to get Projectile component type
                var projectileComponent = collision.gameObject.GetComponent(System.Type.GetType("CZ.Core.Player.Projectile, CZ.Core.Player"));
                if (projectileComponent != null)
                {
                    // Use reflection to get the DamageValue property
                    int damage = (int)projectileComponent.GetType().GetProperty("DamageValue").GetValue(projectileComponent);
                    TakeDamage(damage);
                    
                    // If you need to handle the projectile differently, you can do so here
                }
            }
            else if (collision.gameObject.layer == LayerMask.NameToLayer("Player"))
            {
                // Log collision with player
                Debug.Log($"[SwiftEnemy] Collided with player at position {transform.position}");
                
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
        protected override void OnTriggerEnter2D(Collider2D other)
        {
            // This serves as a backup method in case projectiles use triggers
            if (other.gameObject.layer == LayerMask.NameToLayer("Projectile"))
            {
                // Log for debugging
                Debug.Log($"[SwiftEnemy] Trigger entered by projectile: {other.gameObject.name} at position {transform.position}");

                // Use reflection to get Projectile component
                var projectileComponent = other.gameObject.GetComponent(System.Type.GetType("CZ.Core.Player.Projectile, CZ.Core.Player"));
                if (projectileComponent != null)
                {
                    // Use reflection to get the DamageValue property
                    int damage = (int)projectileComponent.GetType().GetProperty("DamageValue").GetValue(projectileComponent);
                    TakeDamage(damage);
                }
            }
        }
        
        #endregion
    }
} 