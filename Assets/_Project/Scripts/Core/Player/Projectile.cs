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
        private int damage = 10;
        #endregion

        #region State
        private float currentLifetime;
        private bool isInitialized;
        private bool isActive;
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

            // Check if we hit an enemy
            if (other.CompareTag("Enemy"))
            {
                var damageable = other.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(damage);
                }
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
            if (projectileTrail != null)
            {
                projectileTrail.emitting = true;
            }
        }

        public void OnDespawn()
        {
            isActive = false;
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
            if (projectileTrail != null)
            {
                projectileTrail.emitting = false;
            }
        }
        #endregion

        #region Public Methods
        public void Initialize(Vector2 direction)
        {
            if (rb != null)
            {
                rb.linearVelocity = direction.normalized * projectileSpeed;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0, 0, angle);
            }
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