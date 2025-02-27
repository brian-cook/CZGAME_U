using UnityEngine;
using Unity.Profiling;
using NaughtyAttributes;
using CZ.Core.Interfaces;
using System.Collections;

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

        [SerializeField]
        [InfoBox("Enable trail effects", EInfoBoxType.Normal)]
        private bool enableTrailEffects = false; // Default to off for now
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

            // Add trail renderer if needed and enabled
            if (resourceTrail == null && enableTrailEffects)
            {
                resourceTrail = gameObject.AddComponent<TrailRenderer>();
                if (resourceTrail != null)
                {
                    // Extremely minimal trail configuration
                    resourceTrail.time = 0.05f; // Very short trail
                    resourceTrail.startWidth = 0.1f;  // Very thin trail
                    resourceTrail.endWidth = 0f;
                    resourceTrail.emitting = false;
                    resourceTrail.minVertexDistance = 0.025f;
                    resourceTrail.widthMultiplier = 0.25f; // Very thin multiplier
                    resourceTrail.autodestruct = false;
                    
                    // Set a minimal width curve
                    AnimationCurve widthCurve = new AnimationCurve();
                    widthCurve.AddKey(0f, 1f);
                    widthCurve.AddKey(1f, 0f);
                    resourceTrail.widthCurve = widthCurve;

                    // Use a very transparent material
                    if (sharedTrailMaterial == null)
                    {
                        sharedTrailMaterial = new Material(Shader.Find("Sprites/Default"));
                    }
                    resourceTrail.material = sharedTrailMaterial;
                    
                    // Very transparent colors
                    Color trailStartColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.3f); // Very transparent
                    Color trailEndColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
                    resourceTrail.startColor = trailStartColor;
                    resourceTrail.endColor = trailEndColor;
                }
            }
            else if (resourceTrail != null && !enableTrailEffects)
            {
                // If trail effects are disabled, destroy the component
                Destroy(resourceTrail);
                resourceTrail = null;
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
                    
                    // Scale effect - smoothly scale down as it gets closer to player
                    if (collectionProgress < 0.5f)
                    {
                        // Smoothly scale down to zero as it reaches the player
                        float scaleProgress = collectionProgress / 0.5f;
                        transform.localScale = initialScale * (pulse * scaleProgress);
                    }
                }
            }

            // Collection movement
            if (isBeingCollected && target != null)
            {
                Vector3 direction = (target.position - transform.position).normalized;
                float distance = Vector3.Distance(transform.position, target.position);
                
                // Smoother acceleration curve - eases in at start and eases out at end
                float normalizedDistance = Mathf.Clamp01(distance / collectionRadius);
                float accelerationCurve = 0.5f - 0.5f * Mathf.Cos(normalizedDistance * Mathf.PI);
                
                // Use a more gradual speed multiplier with a maximum cap
                float speedMultiplier = Mathf.Lerp(3f, 1f, accelerationCurve);
                
                // Apply damping when very close to avoid jittering
                if (distance < 0.3f)
                {
                    // When very close, disable trail completely
                    if (resourceTrail != null && enableTrailEffects)
                    {
                        resourceTrail.emitting = false;
                        resourceTrail.Clear();
                    }
                    
                    // Smooth direct interpolation when very close
                    transform.position = Vector3.Lerp(transform.position, target.position, Time.deltaTime * collectionSpeed * 2f);
                    rb.linearVelocity = Vector3.zero; // Clear velocity to prevent overshooting
                }
                else
                {
                    // Normal movement with capped maximum velocity
                    Vector3 targetVelocity = direction * collectionSpeed * speedMultiplier;
                    rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVelocity, Time.deltaTime * 8f);
                }

                // Check for collection completion
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
                if (resourceTrail != null && enableTrailEffects)
                {
                    // Create gradient with reduced alpha for subtle effect
                    var gradient = new Gradient();
                    
                    gradient.SetKeys(
                        new GradientColorKey[] { new GradientColorKey(resourceColor, 0.0f), new GradientColorKey(resourceColor, 1.0f) },
                        new GradientAlphaKey[] { new GradientAlphaKey(0.3f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
                    );
                    resourceTrail.colorGradient = gradient;
                    
                    // Ensure trail is not emitting until collection
                    resourceTrail.emitting = false;
                    resourceTrail.Clear();
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
            
            if (resourceTrail != null && enableTrailEffects)
            {
                // Only turn on trail for very fast-moving resources
                resourceTrail.emitting = true;
                resourceTrail.time = 0.05f; // Keep it very short
                resourceTrail.widthMultiplier = 0.25f; // Keep it very thin
            }
            
            // Add a small initial force in a random direction for visual flair
            if (rb != null && target != null)
            {
                // Calculate a random offset perpendicular to the direction to the player
                Vector2 directionToPlayer = (target.position - transform.position).normalized;
                Vector2 perpendicular = new Vector2(-directionToPlayer.y, directionToPlayer.x);
                
                // Add randomness to the perpendicular direction
                float randomAngle = UnityEngine.Random.Range(-30f, 30f) * Mathf.Deg2Rad;
                Vector2 randomDir = new Vector2(
                    perpendicular.x * Mathf.Cos(randomAngle) - perpendicular.y * Mathf.Sin(randomAngle),
                    perpendicular.x * Mathf.Sin(randomAngle) + perpendicular.y * Mathf.Cos(randomAngle)
                );
                
                // Apply a very small impulse
                rb.AddForce(randomDir * collectionSpeed * 0.2f, ForceMode2D.Impulse);
                
                // Start a coroutine to delay the actual collection movement to give the pop effect time to show
                StartCoroutine(DelayedCollection(0.1f));
            }
        }
        
        private System.Collections.IEnumerator DelayedCollection(float delay)
        {
            // Temporarily disable collection movement logic
            isBeingCollected = false;
            
            // Wait for the delay
            yield return new WaitForSeconds(delay);
            
            // Re-enable collection
            isBeingCollected = true;
        }

        private void OnCollected()
        {
            // Important: Stop emitting the trail immediately to prevent it from following the player
            if (resourceTrail != null && enableTrailEffects)
            {
                resourceTrail.emitting = false;
                resourceTrail.Clear(); // Clear the existing trail
                
                // Double-check: Set trail time to zero and clear again
                resourceTrail.time = 0f;
                resourceTrail.widthMultiplier = 0f;
                resourceTrail.Clear();
            }

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
                    // Clear trail one last time before returning to pool
                    if (resourceTrail != null)
                    {
                        resourceTrail.Clear();
                    }
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

            if (resourceTrail != null && enableTrailEffects)
            {
                resourceTrail.emitting = false;
                resourceTrail.Clear(); // Clear any existing trail
                // Reset trail properties to minimal values
                resourceTrail.time = 0.05f;
                resourceTrail.widthMultiplier = 0.25f;
                
                // Update trail colors with reduced alpha
                Color trailStartColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.3f);
                Color trailEndColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
                resourceTrail.startColor = trailStartColor;
                resourceTrail.endColor = trailEndColor;
                
                // Set up proper gradient
                var gradient = new Gradient();
                gradient.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(baseColor, 0.0f), new GradientColorKey(baseColor, 1.0f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(0.3f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
                );
                resourceTrail.colorGradient = gradient;
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

            if (resourceTrail != null && enableTrailEffects)
            {
                resourceTrail.emitting = false;
                resourceTrail.Clear(); // Ensure trail is completely cleared on despawn
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