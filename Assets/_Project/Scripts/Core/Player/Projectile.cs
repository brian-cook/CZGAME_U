using UnityEngine;
using CZ.Core.Pooling;
using NaughtyAttributes;
using CZ.Core.Interfaces;

namespace CZ.Core.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class Projectile : MonoBehaviour, IPoolable
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
                circleCollider.radius = 0.2f;
                circleCollider.isTrigger = true;
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
                Debug.LogWarning("[Projectile] No SpriteRenderer found, projectile may not be visible");
            }
            
            // Set proper layer
            gameObject.layer = LayerMask.NameToLayer("Default");

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
            if (!isActive) return;

            // Skip collision with owner
            if (owner != null && (other.gameObject == owner || other.transform.IsChildOf(owner.transform)))
            {
                Debug.Log($"[Projectile] Ignoring collision with owner: {owner.name}");
                return;
            }

            // Check for IDamageable interface first
            var damageable = other.GetComponent<IDamageable>();
            if (damageable != null)
            {
                Debug.Log($"[Projectile] Hit damageable object: {other.gameObject.name}");
                damageable.TakeDamage(damage);
                ReturnToPool();
                return;
            }

            // Fallback to tag check if needed
            if (other.CompareTag("Enemy"))
            {
                Debug.Log($"[Projectile] Hit enemy with tag: {other.gameObject.name}");
                ReturnToPool();
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
            
            Debug.Log($"[Projectile] OnSpawn - Position: {transform.position}, Sprite visible: {(spriteRenderer != null ? spriteRenderer.enabled : false)}");
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
            
            // Ensure the sprite renderer is visible
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
            }
            
            Debug.Log($"[Projectile] Initialized with direction: {direction}, owner: {(owner != null ? owner.name : "none")}, Position: {transform.position}");
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
        #endregion
    }
} 