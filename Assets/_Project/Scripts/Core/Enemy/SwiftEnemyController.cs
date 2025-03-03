using UnityEngine;
using NaughtyAttributes;
using CZ.Core.Logging;
using CZ.Core.Interfaces;
using CZ.Core.Player;
using System.Collections;
using Unity.Profiling;

namespace CZ.Core.Enemy
{
    /// <summary>
    /// Controller for the Swift Enemy type that implements fast, erratic movement patterns 
    /// and dodging behavior.
    /// </summary>
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
        [InfoBox("Randomness of movement (0=direct, 1=erratic)", EInfoBoxType.Normal)]
        private float movementRandomness = 0.3f;
        
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
        [InfoBox("Parameter name for damage trigger", EInfoBoxType.Normal)]
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
        }

        protected override void FixedUpdate()
        {
            if (isDodging) return;
            
            // Update movement
            UpdateMovement();
            
            // Check for dodge opportunities
            if (canDodge && Time.time > lastDodgeTime + dodgeCooldown)
            {
                s_dodgeMarker.Begin();
                try
                {
                    CheckForDodgeOpportunity();
                }
                finally
                {
                    s_dodgeMarker.End();
                }
            }
            
            // Update animator
            if (animator != null)
            {
                animator.SetFloat(speedParameterName, Rb.linearVelocity.magnitude);
            }
        }
        #endregion

        #region Movement Methods
        private void UpdateMovement()
        {
            s_movementMarker.Begin();
            
            try
            {
                // Check if it's time to change direction
                if (Time.time - lastDirectionChangeTime > directionChangeInterval)
                {
                    // Calculate base direction toward target
                    Vector2 toTarget = ((Vector2)transform.position - Rb.position).normalized;
                    
                    // Apply randomness to direction
                    Vector2 randomDirection = Random.insideUnitCircle.normalized;
                    Vector2 newDirection = Vector2.Lerp(toTarget, randomDirection, movementRandomness);
                    
                    // Update current direction
                    currentMoveDirection = newDirection.normalized;
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
            }
            finally
            {
                s_movementMarker.End();
            }
        }
        
        private void CheckForDodgeOpportunity()
        {
            // Find potential threats (projectiles or player) within detection range
            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, dodgeDetectionRange, LayerMask.GetMask("Player", "Projectile"));
            
            foreach (var collider in colliders)
            {
                // Skip if it's another enemy
                if (collider.gameObject.layer == LayerMask.NameToLayer("Enemy"))
                    continue;
                
                // Check if it's a projectile or player
                bool isProjectile = collider.GetComponent<Player.Projectile>() != null;
                bool isPlayer = collider.GetComponent<Player.PlayerController>() != null;
                
                if (isProjectile || isPlayer)
                {
                    // Calculate threat direction
                    Vector2 threatDirection = (collider.transform.position - transform.position).normalized;
                    
                    // Check if threat is coming toward us (dot product)
                    Rigidbody2D threatRb = collider.GetComponent<Rigidbody2D>();
                    
                    if (threatRb != null)
                    {
                        Vector2 threatVelocity = threatRb.linearVelocity.normalized;
                        float dotProduct = Vector2.Dot(threatDirection, threatVelocity);
                        
                        // If threat is moving toward us and random check passes
                        if (dotProduct < -0.5f && Random.value < dodgeProbability)
                        {
                            // Execute dodge
                            PerformDodge(threatDirection);
                            break;
                        }
                    }
                    else if (Random.value < dodgeProbability * 0.5f)
                    {
                        // Occasionally dodge even without velocity info
                        PerformDodge(threatDirection);
                        break;
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
            dodgeCoroutine = StartCoroutine(DodgeCoroutine(dodgeDirection));
            
            CZLogger.LogDebug($"Swift enemy performing dodge in direction: {dodgeDirection}", LogCategory.Enemy);
        }
        
        private IEnumerator DodgeCoroutine(Vector2 dodgeDirection)
        {
            isDodging = true;
            
            // Apply dodge force
            Rb.linearVelocity = Vector2.zero; // Reset velocity first
            Rb.AddForce(dodgeDirection * dodgeForce, ForceMode2D.Impulse);
            
            // Temporarily disable standard movement
            yield return new WaitForSeconds(0.3f);
            
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
                animator.SetTrigger("TakeDamage");
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
                animator.SetTrigger("TakeDamage");
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
            
            // Initialize swift-specific state
            isDodging = false;
            lastDirectionChangeTime = Time.time;
            lastDodgeTime = Time.time - (dodgeCooldown * 0.5f); // Allow dodge after short delay
            
            // Reset animator parameters
            if (animator != null)
            {
                animator.SetFloat(speedParameterName, 0f);
                animator.ResetTrigger(dodgeTriggerName);
                animator.ResetTrigger(damageTriggerName);
            }
        }
        
        /// <summary>
        /// Called when the object is returned to the pool
        /// </summary>
        public override void OnDespawn()
        {
            // Stop any active dodge coroutine
            if (dodgeCoroutine != null)
            {
                StopCoroutine(dodgeCoroutine);
                dodgeCoroutine = null;
            }
            
            base.OnDespawn();
        }
        
        /// <summary>
        /// Helper method for testing - stores a reference to the target transform
        /// </summary>
        /// <param name="target">Target transform to store</param>
        public void SetTargetTransformForTesting(Transform target)
        {
            #if UNITY_EDITOR
            CurrentTarget = target;
            #endif
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
    }
} 