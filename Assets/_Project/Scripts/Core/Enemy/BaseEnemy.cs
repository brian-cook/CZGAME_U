using UnityEngine;
using CZ.Core.Pooling;
using Unity.Profiling;
using NaughtyAttributes;
using CZ.Core.Interfaces;

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
        
        [SerializeField, MinValue(1)]
        private int health = 100;

        [Header("Collision Configuration")]
        [SerializeField, MinValue(0.1f), MaxValue(1f)]
        [Tooltip("Radius of the collision circle relative to sprite size")]
        private float collisionRadius = 0.25f;

        [SerializeField]
        [Tooltip("Whether the collider should be a trigger")]
        private bool useTriggerCollider = false;
        #endregion

        #region State
        private Vector3 targetPosition;
        private bool isInitialized;
        private Vector3 lastKnownTargetPosition;
        private float lastTargetUpdateTime;
        private bool hasValidTarget;
        #endregion

        #region Properties
        public GameObject GameObject => gameObject;
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
        #endregion

        #region Initialization
        private void InitializeComponents()
        {
            if (isInitialized) return;

            // Get required components
            spriteRenderer = GetComponent<SpriteRenderer>();
            circleCollider = GetComponent<CircleCollider2D>();
            rb = GetComponent<Rigidbody2D>();

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
            health -= damage;
            if (health <= 0)
            {
                // Return to pool instead of destroying
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
            health = 100;
            hasValidTarget = false;
            lastTargetUpdateTime = 0f;
            rb.linearVelocity = Vector2.zero;
            
            gameObject.SetActive(true);
            Debug.Log($"[BaseEnemy] Enemy spawned: {gameObject.name}");
        }

        public void OnDespawn()
        {
            hasValidTarget = false;
            targetPosition = Vector3.zero;
            lastKnownTargetPosition = Vector3.zero;
            rb.linearVelocity = Vector2.zero;
            gameObject.SetActive(false);
            Debug.Log($"[BaseEnemy] Enemy despawned: {gameObject.name}");
        }
        #endregion
    }
} 