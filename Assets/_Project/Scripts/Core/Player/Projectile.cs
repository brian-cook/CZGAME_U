using UnityEngine;
using CZ.Core.Pooling;
using NaughtyAttributes;
using CZ.Core.Interfaces;

namespace CZ.Core.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class Projectile : MonoBehaviour, IPoolable, IProjectileIdentifier
    {
        #region Components
        private Rigidbody2D rb;
        private CircleCollider2D circleCollider;
        private TrailRenderer projectileTrail;
        private static Material sharedProjectileMaterial;
        private SpriteRenderer spriteRenderer;
        #endregion

        #region Configuration
        [BoxGroup("Projectile Settings")]
        [SerializeField, MinValue(1f), MaxValue(50f)]
        [InfoBox("Speed of the projectile", EInfoBoxType.Normal)]
        private float projectileSpeed = 15f;

        [BoxGroup("Projectile Settings")]
        [SerializeField, MinValue(0.1f), MaxValue(5f)]
        [InfoBox("Lifetime of the projectile in seconds", EInfoBoxType.Normal)]
        private float lifetime = 2f;

        [BoxGroup("Projectile Settings")]
        [SerializeField, MinValue(1), MaxValue(50)]
        [InfoBox("Damage dealt by the projectile", EInfoBoxType.Normal)]
        private int damage = 50;
        #endregion

        #region State
        private float currentLifetime;
        private bool isInitialized;
        private bool isActive;
        private GameObject owner; // Reference to the object that fired this projectile
        private bool warnedAboutMissingLayer = false;
        #endregion

        #region IProjectileIdentifier Implementation
        public int Damage => damage;
        public GameObject Owner => owner;
        public Vector2 Velocity => rb != null ? rb.linearVelocity : Vector2.zero;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            SetupComponents();
        }

        private void SetupComponents()
        {
            if (isInitialized) return;

            rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.gravityScale = 0f;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            }

            circleCollider = GetComponent<CircleCollider2D>();
            if (circleCollider != null)
            {
                circleCollider.radius = 0.3f;
                circleCollider.isTrigger = true;
                
                // Make sure the collider is enabled
                circleCollider.enabled = true;
                
                Debug.Log($"[Projectile] Collider set up with radius: {circleCollider.radius}, isTrigger: {circleCollider.isTrigger}");
            }

            if (projectileTrail == null)
            {
                projectileTrail = gameObject.AddComponent<TrailRenderer>();
                if (projectileTrail != null)
                {
                    projectileTrail.time = 0.1f;
                    projectileTrail.startWidth = 0.2f;
                    projectileTrail.endWidth = 0f;
                    projectileTrail.emitting = false;

                    if (sharedProjectileMaterial == null)
                    {
                        sharedProjectileMaterial = new Material(Shader.Find("Sprites/Default"))
                        {
                            hideFlags = HideFlags.DontSave
                        };
                    }
                    projectileTrail.material = sharedProjectileMaterial;
                }
            }
            
            // Get sprite renderer component
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                Debug.LogWarning("[Projectile] No SpriteRenderer found, adding one");
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                
                // Set default sprite if none exists
                if (spriteRenderer.sprite == null)
                {
                    spriteRenderer.sprite = Resources.Load<Sprite>("DefaultProjectile");
                    Debug.Log("[Projectile] Added default sprite to projectile");
                }
            }
            
            // Ensure sprite is visible with proper material
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
                spriteRenderer.color = Color.white; // Ensure full visibility
                
                // Use appropriate shader for 2D rendering
                spriteRenderer.material = new Material(Shader.Find("Sprites/Default"));
                Debug.Log($"[Projectile] Set sprite material to Sprites/Default");
            }
            
            // Set proper layer for visibility and collision
            // Try to use Projectile layer but fall back to Default if it doesn't exist
            int projectileLayer = LayerMask.NameToLayer("Projectile");
            if (projectileLayer >= 0)
            {
                gameObject.layer = projectileLayer;
                Debug.Log($"[Projectile] Layer set to: Projectile");
            }
            else
            {
                gameObject.layer = LayerMask.NameToLayer("Default");
                Debug.LogWarning($"[Projectile] Projectile layer not found, using Default layer instead. Please add a Projectile layer in Project Settings.");
            }
            
            // Ensure the projectile is active
            gameObject.SetActive(true);

            isInitialized = true;
        }

        private void FixedUpdate()
        {
            if (!isActive) return;

            currentLifetime += Time.fixedDeltaTime;
            if (currentLifetime >= lifetime)
            {
                ReturnToPool();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isActive) 
            {
                Debug.Log($"[Projectile] Ignoring collision as projectile is not active");
                return;
            }

            // Skip collision with owner
            if (owner != null && (other.gameObject == owner || other.transform.IsChildOf(owner.transform)))
            {
                Debug.Log($"[Projectile] Ignoring collision with owner: {owner.name}");
                return;
            }
            
            // Enhanced logging with more details for debugging
            Debug.Log($"[Projectile] Collision with {other.gameObject.name}, Layer: {other.gameObject.layer} ({LayerMask.LayerToName(other.gameObject.layer)}), " +
                $"Tag: {other.tag}, IsTrigger: {other.isTrigger}, Enabled: {other.enabled}, " +
                $"Transform position: {other.transform.position}, My position: {transform.position}");

            // Force update the collider in case it wasn't enabled
            CircleCollider2D enemyCollider = other.GetComponent<CircleCollider2D>();
            if (enemyCollider != null && !enemyCollider.enabled)
            {
                Debug.LogWarning($"[Projectile] Enemy collider was disabled! Enabling it.");
                enemyCollider.enabled = true;
            }

            // Get IDamageable component more aggressively
            IDamageable damageable = null;
            
            // Try direct component access
            damageable = other.GetComponent<IDamageable>();
            
            // Try parent if not found
            if (damageable == null)
            {
                damageable = other.GetComponentInParent<IDamageable>();
            }
            
            // Try root object if still not found
            if (damageable == null && other.transform.root != other.transform)
            {
                damageable = other.transform.root.GetComponent<IDamageable>();
            }
            
            // Try all components in parent chain
            if (damageable == null)
            {
                Transform current = other.transform;
                while (current != null && damageable == null)
                {
                    damageable = current.GetComponent<IDamageable>();
                    current = current.parent;
                }
            }
            
            if (damageable != null)
            {
                Debug.Log($"[Projectile] Hit damageable object: {other.gameObject.name}, applying {damage} damage");
                damageable.TakeDamage(damage);
                ReturnToPool();
                return;
            }
            else
            {
                Debug.LogWarning($"[Projectile] Object {other.gameObject.name} does not implement IDamageable interface");
            }

            // Fallback to tag check if needed
            if (other.CompareTag("Enemy"))
            {
                Debug.Log($"[Projectile] Hit enemy with tag: {other.gameObject.name}");
                ReturnToPool();
                return;
            }
            
            // Check if we hit other layers that should destroy the projectile
            if (other.gameObject.layer == LayerMask.NameToLayer("Default") ||
                other.gameObject.layer == LayerMask.NameToLayer("Obstacle"))
            {
                Debug.Log($"[Projectile] Hit obstacle layer: {LayerMask.LayerToName(other.gameObject.layer)}");
                ReturnToPool();
                return;
            }
        }

        private void OnValidate()
        {
            CircleCollider2D col = GetComponent<CircleCollider2D>();
            if (col != null && !col.isTrigger)
            {
                Debug.LogError("[Projectile] CircleCollider2D MUST be a trigger for projectiles!");
                col.isTrigger = true;
            }
            
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null && rb.collisionDetectionMode != CollisionDetectionMode2D.Continuous)
            {
                Debug.LogWarning("[Projectile] Rigidbody2D should use Continuous collision detection!");
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }
        }
        #endregion

        #region IPoolable Implementation
        public GameObject GameObject => gameObject;

        public void OnSpawn()
        {
            isActive = true;
            currentLifetime = 0f;
            owner = null; // Reset owner reference
            
            // Ensure the game object is active
            gameObject.SetActive(true);
            
            // Enable trail renderer
            if (projectileTrail != null)
            {
                projectileTrail.emitting = true;
            }
            
            // Ensure sprite renderer is visible
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
                spriteRenderer.color = new Color(spriteRenderer.color.r, spriteRenderer.color.g, spriteRenderer.color.b, 1f);
            }
            
            // Make sure collider is enabled
            if (circleCollider != null)
            {
                circleCollider.enabled = true;
            }
            
            Debug.Log($"[Projectile] OnSpawn - Position: {transform.position}, Sprite visible: {(spriteRenderer != null ? spriteRenderer.enabled : false)}, Collider enabled: {(circleCollider != null ? circleCollider.enabled : false)}");
        }

        public void OnDespawn()
        {
            isActive = false;
            owner = null; // Clear owner reference
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
            if (projectileTrail != null)
            {
                projectileTrail.emitting = false;
            }
            
            Debug.Log("[Projectile] OnDespawn - Projectile returned to pool");
        }
        #endregion

        #region Public Methods
        public void Initialize(Vector2 direction, GameObject projectileOwner = null)
        {
            owner = projectileOwner;
            
            if (rb != null)
            {
                rb.linearVelocity = direction.normalized * projectileSpeed;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0, 0, angle);
            }
            
            // Ensure the sprite renderer is visible with proper settings
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
                spriteRenderer.color = Color.white; // Full opacity
                
                // Use appropriate shader for 2D rendering
                if (spriteRenderer.material == null || spriteRenderer.material.shader.name.Contains("Lit"))
                {
                    spriteRenderer.material = new Material(Shader.Find("Sprites/Default"));
                }
            }
            
            // Ensure projectile trail is active
            if (projectileTrail != null)
            {
                projectileTrail.emitting = true;
            }
            
            // Make sure collider is enabled for collision detection
            if (circleCollider != null)
            {
                circleCollider.enabled = true;
            }
            
            // Set proper layer - try to use Projectile layer but fall back to Default if it doesn't exist
            int projectileLayer = LayerMask.NameToLayer("Projectile");
            if (projectileLayer >= 0)
            {
                gameObject.layer = projectileLayer;
            }
            else
            {
                gameObject.layer = LayerMask.NameToLayer("Default");
                // Only log warning once per session
                if (!warnedAboutMissingLayer)
                {
                    Debug.LogWarning($"[Projectile] Projectile layer not found, using Default layer instead. Please add a Projectile layer in Project Settings.");
                    warnedAboutMissingLayer = true;
                }
            }
            
            Debug.Log($"[Projectile] Initialized with direction: {direction}, owner: {(owner != null ? owner.name : "none")}, Position: {transform.position}, Sprite visible: {(spriteRenderer != null ? spriteRenderer.enabled : false)}, Layer: {LayerMask.LayerToName(gameObject.layer)}");
        }

        private void ReturnToPool()
        {
            var pool = PoolManager.Instance.GetPool<Projectile>();
            if (pool != null)
            {
                pool.Return(this);
            }
            else
            {
                Debug.LogError("[Projectile] Failed to return to pool - pool not found!");
                gameObject.SetActive(false);
            }
        }

        public void VerifyRequiredSettings()
        {
            // Log global Physics2D settings that are critical
            Debug.Log($"[Projectile] Physics2D settings: queriesHitTriggers={Physics2D.queriesHitTriggers}, " +
                      $"queriesStartInColliders={Physics2D.queriesStartInColliders}, " +
                      $"callbacksOnDisable={Physics2D.callbacksOnDisable}");
            
            // Ensure layer is set to Projectile
            int projectileLayer = LayerMask.NameToLayer("Projectile");
            if (projectileLayer >= 0 && gameObject.layer != projectileLayer)
            {
                Debug.LogWarning($"[Projectile] Layer is not set to Projectile! Current: {LayerMask.LayerToName(gameObject.layer)}");
                gameObject.layer = projectileLayer;
            }
            
            // Verify collider settings
            CircleCollider2D col = GetComponent<CircleCollider2D>();
            if (col != null)
            {
                if (!col.enabled)
                {
                    Debug.LogWarning("[Projectile] Collider was disabled! Enabling it.");
                    col.enabled = true;
                }
                
                if (!col.isTrigger)
                {
                    Debug.LogWarning("[Projectile] Collider must be a trigger! Setting isTrigger to true.");
                    col.isTrigger = true;
                }
            }
            
            // Force enable Physics2D.queriesHitTriggers
            Physics2D.queriesHitTriggers = true;
        }

        public void Fire(Vector3 startPosition, Vector2 direction, GameObject projectileOwner)
        {
            // ... existing code ...
            
            owner = projectileOwner;
            
            if (rb != null)
            {
                rb.linearVelocity = direction.normalized * projectileSpeed;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0, 0, angle);
            }
            
            // Ensure the sprite renderer is visible with proper settings
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
                spriteRenderer.color = Color.white; // Full opacity
                
                // Use appropriate shader for 2D rendering
                if (spriteRenderer.material == null || spriteRenderer.material.shader.name.Contains("Lit"))
                {
                    spriteRenderer.material = new Material(Shader.Find("Sprites/Default"));
                }
            }
            
            // Ensure projectile trail is active
            if (projectileTrail != null)
            {
                projectileTrail.emitting = true;
            }
            
            // Make sure collider is enabled for collision detection
            if (circleCollider != null)
            {
                circleCollider.enabled = true;
            }
            
            // Set proper layer - try to use Projectile layer but fall back to Default if it doesn't exist
            int projectileLayer = LayerMask.NameToLayer("Projectile");
            if (projectileLayer >= 0)
            {
                gameObject.layer = projectileLayer;
            }
            
            // NEW: Verify physics settings
            VerifyRequiredSettings();
            
            // ... existing code ...
        }
        #endregion
    }
} 