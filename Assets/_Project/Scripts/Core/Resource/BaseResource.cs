using UnityEngine;
using Unity.Profiling;
using NaughtyAttributes;
using CZ.Core.Interfaces;

namespace CZ.Core.Resource
{
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class BaseResource : MonoBehaviour, IPoolable
    {
        #region Components
        private SpriteRenderer spriteRenderer;
        private CircleCollider2D circleCollider;
        private Rigidbody2D rb;
        private TrailRenderer resourceTrail;
        private static Material sharedTrailMaterial;
        #endregion

        #region Configuration
        [Header("Resource Configuration")]
        [SerializeField] 
        private ResourceType resourceType;
        public ResourceType ResourceType => resourceType;

        [SerializeField, MinValue(1)]
        [InfoBox("Value of this resource", EInfoBoxType.Normal)]
        private int resourceValue = 1;
        public int ResourceValue => resourceValue;

        [SerializeField, MinValue(0.1f)]
        [InfoBox("Collection radius for magnetic effect", EInfoBoxType.Normal)]
        private float collectionRadius = 1f;

        [SerializeField, MinValue(0f)]
        [InfoBox("Speed when moving towards player", EInfoBoxType.Normal)]
        private float collectionSpeed = 5f;

        [SerializeField, MinValue(0f)]
        [InfoBox("Time before resource despawns", EInfoBoxType.Normal)]
        private float lifetime = 10f;

        [Header("Visual Configuration")]
        [SerializeField]
        private Color resourceColor = Color.white;

        [SerializeField, MinValue(0f)]
        private float pulseSpeed = 1f;

        [SerializeField, Range(0f, 1f)]
        private float pulseIntensity = 0.2f;
        #endregion

        #region State
        private bool isInitialized;
        private bool isBeingCollected;
        private float currentLifetime;
        private Transform target;
        private Vector3 initialScale;
        private Color baseColor;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            SetupComponents();
        }

        private void SetupComponents()
        {
            if (isInitialized) return;

            // Get and configure components
            spriteRenderer = GetComponent<SpriteRenderer>();
            circleCollider = GetComponent<CircleCollider2D>();
            rb = GetComponent<Rigidbody2D>();

            if (spriteRenderer != null)
            {
                // Store the original color from the prefab
                baseColor = resourceColor;
                spriteRenderer.color = baseColor;
                Debug.Log($"[BaseResource] Initialized {resourceType} with color: {baseColor}");
            }

            if (circleCollider != null)
            {
                circleCollider.isTrigger = true;
                circleCollider.radius = 0.25f;
            }

            if (rb != null)
            {
                rb.gravityScale = 0f;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }

            // Add trail renderer if needed
            if (resourceTrail == null)
            {
                resourceTrail = gameObject.AddComponent<TrailRenderer>();
                if (resourceTrail != null)
                {
                    resourceTrail.time = 0.2f;
                    resourceTrail.startWidth = 0.2f;
                    resourceTrail.endWidth = 0f;
                    resourceTrail.emitting = false;

                    // Use the same color as the sprite
                    if (sharedTrailMaterial == null)
                    {
                        sharedTrailMaterial = new Material(Shader.Find("Sprites/Default"));
                    }
                    resourceTrail.material = sharedTrailMaterial;
                    resourceTrail.startColor = baseColor;
                    resourceTrail.endColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
                }
            }

            initialScale = transform.localScale;
            isInitialized = true;
        }

        private void Update()
        {
            if (!isInitialized) return;

            // Handle lifetime
            if (!isBeingCollected)
            {
                currentLifetime += Time.deltaTime;
                if (currentLifetime >= lifetime)
                {
                    ReturnToPool();
                    return;
                }
            }

            // Visual effects
            if (spriteRenderer != null)
            {
                float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseIntensity;
                transform.localScale = initialScale * pulse;

                if (isBeingCollected)
                {
                    float collectionProgress = Vector3.Distance(transform.position, target.position) / collectionRadius;
                    spriteRenderer.color = Color.Lerp(baseColor, Color.white, 1f - collectionProgress);
                }
            }

            // Collection movement
            if (isBeingCollected && target != null)
            {
                Vector3 direction = (target.position - transform.position).normalized;
                float distance = Vector3.Distance(transform.position, target.position);
                float speedMultiplier = Mathf.Lerp(1f, 2f, 1f - (distance / collectionRadius));
                
                rb.linearVelocity = direction * collectionSpeed * speedMultiplier;

                if (distance < 0.1f)
                {
                    OnCollected();
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isInitialized || isBeingCollected) return;

            if (other.CompareTag("Player"))
            {
                target = other.transform;
                StartCollection();
            }
        }

        private void OnEnable()
        {
            // Ensure type and color are preserved when object is enabled
            if (spriteRenderer != null)
            {
                spriteRenderer.color = resourceColor;
                if (resourceTrail != null)
                {
                    var gradient = new Gradient();
                    gradient.SetKeys(
                        new GradientColorKey[] { new GradientColorKey(resourceColor, 0.0f), new GradientColorKey(resourceColor, 1.0f) },
                        new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
                    );
                    resourceTrail.colorGradient = gradient;
                }
            }
            Debug.Log($"Resource enabled: Type={resourceType}, Color={resourceColor}");
        }
        #endregion

        #region Collection
        private void StartCollection()
        {
            if (!isInitialized || isBeingCollected) return;

            isBeingCollected = true;
            if (resourceTrail != null)
            {
                resourceTrail.emitting = true;
            }
        }

        private void OnCollected()
        {
            if (ResourceManager.Instance != null)
            {
                try
                {
                    ResourceManager.Instance.CollectResource(resourceType, resourceValue);
                    Debug.Log($"[BaseResource] Resource collected: {resourceType} with value {resourceValue}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[BaseResource] Error during resource collection: {e.Message}");
                }
                finally
                {
                    ReturnToPool();
                }
            }
            else
            {
                Debug.LogError("[BaseResource] Failed to collect resource - ResourceManager not found!");
                ReturnToPool();
            }
        }

        private void ReturnToPool()
        {
            if (ResourceManager.Instance == null)
            {
                Debug.LogError("[BaseResource] Failed to return to pool - ResourceManager not found!");
                gameObject.SetActive(false);
                return;
            }

            if (!ResourceManager.Instance.ReturnResourceToPool(this))
            {
                Debug.LogError($"[BaseResource] Failed to return {resourceType} resource to pool!");
                gameObject.SetActive(false);
            }
        }
        #endregion

        #region IPoolable Implementation
        public GameObject GameObject => gameObject;

        public void OnSpawn()
        {
            if (!isInitialized)
            {
                SetupComponents();
            }

            currentLifetime = 0f;
            isBeingCollected = false;
            target = null;
            transform.localScale = initialScale;

            // Ensure color and type are correctly set on spawn
            if (spriteRenderer != null)
            {
                // Use the resource's configured color
                spriteRenderer.color = resourceColor;
                baseColor = resourceColor;
                
                Debug.Log($"[BaseResource] Spawned {resourceType} with color: {baseColor}");
            }

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            if (resourceTrail != null)
            {
                resourceTrail.emitting = false;
                // Update trail colors
                resourceTrail.startColor = baseColor;
                resourceTrail.endColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
            }

            gameObject.SetActive(true);
        }

        public void OnDespawn()
        {
            isBeingCollected = false;
            target = null;

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            if (resourceTrail != null)
            {
                resourceTrail.emitting = false;
            }

            gameObject.SetActive(false);
        }
        #endregion

        #region Resource Configuration
        public void SetResourceType(ResourceType type)
        {
            resourceType = type;
            Debug.Log($"[BaseResource] Set resource type to: {type}");
        }

        public void SetResourceValue(int value)
        {
            resourceValue = Mathf.Max(1, value);
            Debug.Log($"[BaseResource] Set resource value to: {resourceValue}");
        }
        #endregion
    }
} 