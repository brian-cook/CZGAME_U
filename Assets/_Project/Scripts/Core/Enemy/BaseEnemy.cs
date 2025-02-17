using UnityEngine;
using CZ.Core.Pooling;
using Unity.Profiling;
using NaughtyAttributes;

namespace CZ.Core.Enemy
{
    public class BaseEnemy : MonoBehaviour, IPoolable
    {
        [Header("Enemy Configuration")]
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float health = 100f;
        
        [Header("References")]
        [Required("SpriteRenderer required for enemy visibility")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        
        // Pooling interface implementation
        public GameObject GameObject => gameObject;
        
        // Performance monitoring
        private static readonly ProfilerMarker s_updateMarker = 
            new(ProfilerCategory.Scripts, "BaseEnemy.Update");
        
        private Vector3 targetPosition;
        private bool isInitialized;
        private ObjectPool<BaseEnemy> currentPool;
        
        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }
        }
        
        public void OnSpawn()
        {
            health = 100f;
            isInitialized = true;
            gameObject.SetActive(true);
            
            // Random color variation
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Random.ColorHSV(0f, 1f, 0.8f, 1f, 0.8f, 1f);
            }
        }
        
        public void OnDespawn()
        {
            isInitialized = false;
            gameObject.SetActive(false);
        }
        
        private void Update()
        {
            if (!isInitialized) return;
            
            using var _ = s_updateMarker.Auto();
            
            // Simple follow behavior (to be expanded)
            if (targetPosition != Vector3.zero)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    targetPosition,
                    moveSpeed * Time.deltaTime
                );
            }
        }
        
        public void SetTarget(Vector3 position)
        {
            targetPosition = position;
        }
        
        public void TakeDamage(float amount)
        {
            health -= amount;
            
            if (health <= 0)
            {
                // Try local pool first, then PoolManager
                var pool = currentPool ?? PoolManager.Instance?.GetPool<BaseEnemy>();
                pool?.Return(this);
            }
        }
        
        public void SetPool(ObjectPool<BaseEnemy> pool)
        {
            currentPool = pool;
        }
    }
} 