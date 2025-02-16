using UnityEngine;
using UnityEngine.InputSystem;
using NaughtyAttributes;

namespace CZ.Core.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class PlayerController : MonoBehaviour
    {
        #region Components
        private Rigidbody2D rb;
        private PlayerInput playerInput;
        private InputAction moveAction;
        private CircleCollider2D circleCollider;
        private TrailRenderer movementTrail;
        private static Material sharedTrailMaterial;
        #endregion

        #region Configuration
        [BoxGroup("Movement Settings")]
        [SerializeField, MinValue(0f), MaxValue(20f)]
        [InfoBox("Base movement speed of the player", EInfoBoxType.Normal)]
        private float moveSpeed = 5f;

        [BoxGroup("Movement Settings")]
        [SerializeField, MinValue(0f), MaxValue(20f)]
        [InfoBox("Maximum velocity the player can reach", EInfoBoxType.Normal)]
        private float maxVelocity = 8f;
        
        [BoxGroup("Physics Settings")]
        [SerializeField, MinValue(0f)]
        [InfoBox("Linear drag applied to slow down movement", EInfoBoxType.Normal)]
        private float linearDrag = 3f;

        [BoxGroup("Debug Settings")]
        [SerializeField]
        private bool enableDebugLogs = false;
        #endregion

        #region State
        private Vector2 moveInput;
        private bool isMoving;
        private bool isInitialized;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            SetupComponents();
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
            
            // Get PlayerInput
            playerInput = GetComponent<PlayerInput>();
            if (playerInput != null)
            {
                if (enableDebugLogs) Debug.Log("[PlayerController] Setting up PlayerInput");
                playerInput.defaultActionMap = "Player";
                playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
                
                // Ensure actions asset is assigned
                if (playerInput.actions == null)
                {
                    Debug.LogError("[PlayerController] No input actions asset assigned");
                    return;
                }

                var actionMap = playerInput.actions.FindActionMap("Player", true);
                if (actionMap != null)
                {
                    actionMap.Enable();
                    moveAction = actionMap.FindAction("Move");
                    if (moveAction != null)
                    {
                        moveAction.performed += OnMovePerformed;
                        moveAction.canceled += OnMoveCanceled;
                        moveAction.Enable();
                        if (enableDebugLogs) Debug.Log("[PlayerController] Move action enabled");
                    }
                }
            }
            else
            {
                Debug.LogError("[PlayerController] PlayerInput component missing");
                return;
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
        }

        public void OnMovePerformed(InputAction.CallbackContext context)
        {
            moveInput = context.ReadValue<Vector2>();
            isMoving = moveInput.sqrMagnitude > 0.01f;
            
            // Only enable trail when moving
            if (movementTrail != null)
            {
                movementTrail.emitting = isMoving;
            }
            
            if (enableDebugLogs && isMoving)
            {
                Debug.Log($"[PlayerController] Move: {moveInput:F2}");
            }
        }

        public void OnMoveCanceled(InputAction.CallbackContext context)
        {
            moveInput = Vector2.zero;
            isMoving = false;
            
            if (movementTrail != null)
            {
                movementTrail.emitting = false;
            }
            
            if (enableDebugLogs)
            {
                Debug.Log("[PlayerController] Move canceled");
            }
        }

        private void OnEnable()
        {
            if (!isInitialized)
            {
                SetupComponents();
            }
            else if (moveAction != null)
            {
                moveAction.Enable();
            }
        }

        private void OnDisable()
        {
            if (moveAction != null)
            {
                moveAction.Disable();
            }
            
            if (movementTrail != null)
            {
                movementTrail.emitting = false;
            }
        }

        private void FixedUpdate()
        {
            HandleMovement();
        }

        private void OnDestroy()
        {
            if (moveAction != null)
            {
                moveAction.performed -= OnMovePerformed;
                moveAction.canceled -= OnMoveCanceled;
                moveAction.Disable();
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
        }
        #endregion

        #region Debug Methods
        [Button("Reset Velocity")]
        private void ResetVelocity()
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
        }
        #endregion
    }
}