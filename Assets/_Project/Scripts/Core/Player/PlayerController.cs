using UnityEngine;
using UnityEngine.InputSystem;
using NaughtyAttributes;
using CZ.Core;
using CZ.Core.Input;
using static NaughtyAttributes.EInfoBoxType;

namespace CZ.Core.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class PlayerController : MonoBehaviour
    {
        #region Components
        private Rigidbody2D rb;
        private CircleCollider2D circleCollider;
        private TrailRenderer movementTrail;
        private static Material sharedTrailMaterial;
        private GameControls controls;
        #endregion

        #region Configuration
        [BoxGroup("Movement Settings")]
        [SerializeField, MinValue(0f), MaxValue(20f)]
        [InfoBox("Base movement speed of the player", Normal)]
        private float moveSpeed = 5f;

        [BoxGroup("Movement Settings")]
        [SerializeField, MinValue(0f), MaxValue(20f)]
        [InfoBox("Maximum velocity the player can reach", Normal)]
        private float maxVelocity = 8f;
        
        [BoxGroup("Physics Settings")]
        [SerializeField, MinValue(0f)]
        [InfoBox("Linear drag applied to slow down movement", Normal)]
        private float linearDrag = 3f;

        [BoxGroup("Debug Settings")]
        [SerializeField]
        private bool enableDebugLogs = false;
        #endregion

        #region State
        private Vector2 moveInput;
        private bool isMoving;
        private bool isInitialized;
        private bool isInputEnabled;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            SetupComponents();
            controls = new GameControls();
            
            // Subscribe to input events
            controls.Player.Move.performed += OnMove;
            controls.Player.Move.canceled += OnMove;
            
            // Subscribe to game state changes
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
            }
            else
            {
                Debug.LogError("[PlayerController] GameManager instance not found!");
            }
        }

        private void SetupComponents()
        {
            if (isInitialized) return;
            
            // Get and configure Rigidbody2D
            rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.gravityScale = 0f;
                rb.linearDamping = linearDrag;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }
            
            // Get and configure CircleCollider2D
            circleCollider = GetComponent<CircleCollider2D>();
            if (circleCollider != null)
            {
                circleCollider.radius = 0.5f;
                circleCollider.isTrigger = false;
            }

            // Setup minimal trail with shared material
            if (movementTrail == null)
            {
                movementTrail = gameObject.AddComponent<TrailRenderer>();
                if (movementTrail != null)
                {
                    movementTrail.time = 0.1f;
                    movementTrail.startWidth = 0.1f;
                    movementTrail.endWidth = 0f;
                    movementTrail.emitting = false; // Start with trail disabled
                    
                    // Use shared material
                    if (sharedTrailMaterial == null)
                    {
                        sharedTrailMaterial = new Material(Shader.Find("Sprites/Default"))
                        {
                            hideFlags = HideFlags.DontSave
                        };
                    }
                    movementTrail.material = sharedTrailMaterial;
                }
            }
            
            isInitialized = true;
            if (enableDebugLogs) Debug.Log("[PlayerController] Components initialized successfully");
        }

        private void OnEnable()
        {
            if (!isInitialized)
            {
                SetupComponents();
            }
            
            controls?.Enable();
            isInputEnabled = GameManager.Instance?.CurrentGameState == GameManager.GameState.Playing;
            
            if (enableDebugLogs) Debug.Log("[PlayerController] Input system enabled");
        }

        private void OnDisable()
        {
            controls?.Disable();
            
            if (movementTrail != null)
            {
                movementTrail.emitting = false;
            }
            
            isInputEnabled = false;
            if (enableDebugLogs) Debug.Log("[PlayerController] Input system disabled");
        }

        private void FixedUpdate()
        {
            if (isInputEnabled)
            {
                HandleMovement();
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            }
            
            if (controls != null)
            {
                controls.Player.Move.performed -= OnMove;
                controls.Player.Move.canceled -= OnMove;
                controls.Dispose();
            }
        }
        #endregion

        #region Movement
        private void HandleMovement()
        {
            if (!isMoving)
            {
                // Apply stronger deceleration when stopping
                float stopThreshold = 0.01f;
                if (rb.linearVelocity.sqrMagnitude < stopThreshold)
                {
                    rb.linearVelocity = Vector2.zero;
                }
                else
                {
                    rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, linearDrag * 2f * Time.fixedDeltaTime);
                }
                return;
            }

            Vector2 targetVelocity = moveInput.normalized * moveSpeed;
            rb.linearVelocity = Vector2.ClampMagnitude(targetVelocity, maxVelocity);
            
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerController] Moving with velocity: {rb.linearVelocity}");
            }
        }
        #endregion

        #region Input Handling
        private void OnMove(InputAction.CallbackContext context)
        {
            if (!isInputEnabled) return;
            
            moveInput = context.ReadValue<Vector2>();
            isMoving = moveInput.sqrMagnitude > 0.01f;
            
            // Only enable trail when moving
            if (movementTrail != null)
            {
                movementTrail.emitting = isMoving;
            }
            
            if (enableDebugLogs && isMoving)
            {
                Debug.Log($"[PlayerController] Move input: {moveInput}");
            }
        }
        #endregion

        #region Game State
        private void HandleGameStateChanged(GameManager.GameState newState)
        {
            isInputEnabled = newState == GameManager.GameState.Playing;
            
            if (!isInputEnabled)
            {
                // Reset movement when input is disabled
                moveInput = Vector2.zero;
                rb.linearVelocity = Vector2.zero;
                isMoving = false;
                
                if (movementTrail != null)
                {
                    movementTrail.emitting = false;
                }
            }
            
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerController] Game state changed to {newState}, input enabled: {isInputEnabled}");
            }
        }
        #endregion

        #region Public Methods
        public Vector3 GetPosition()
        {
            return transform.position;
        }
        #endregion
    }
}