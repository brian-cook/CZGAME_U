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
        #endregion

        #region State
        private Vector2 moveInput;
        private bool isMoving;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            SetupComponents();
        }

        private void SetupComponents()
        {
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
                playerInput.defaultActionMap = "Player";
                playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            }
            
            // Get and configure CircleCollider2D
            circleCollider = GetComponent<CircleCollider2D>();
            if (circleCollider != null)
            {
                circleCollider.radius = 0.5f;
                circleCollider.isTrigger = false;
            }

            // Setup minimal trail
            movementTrail = gameObject.AddComponent<TrailRenderer>();
            if (movementTrail != null)
            {
                movementTrail.time = 0.1f;
                movementTrail.startWidth = 0.1f;
                movementTrail.endWidth = 0f;
                movementTrail.material = new Material(Shader.Find("Sprites/Default"));
            }
        }

        private void Start()
        {
            VerifyComponents();
        }

        private void VerifyComponents()
        {
            if (playerInput == null)
            {
                Debug.LogError($"[PlayerController] PlayerInput component missing on {gameObject.name}");
                return;
            }

            moveAction = playerInput.actions?.FindAction("Move");
            if (moveAction != null)
            {
                moveAction.performed += OnMovePerformed;
                moveAction.canceled += OnMoveCanceled;
            }
            else
            {
                Debug.LogError($"[PlayerController] Move action not found in input actions asset");
            }
        }

        public void OnMovePerformed(InputAction.CallbackContext context)
        {
            moveInput = context.ReadValue<Vector2>();
            isMoving = moveInput.sqrMagnitude > 0.01f;
        }

        public void OnMoveCanceled(InputAction.CallbackContext context)
        {
            moveInput = Vector2.zero;
            isMoving = false;
        }

        private void OnEnable()
        {
            if (playerInput == null) playerInput = GetComponent<PlayerInput>();
            
            if (playerInput?.actions != null)
            {
                playerInput.currentActionMap = playerInput.actions.FindActionMap("Player");
                moveAction = playerInput.actions.FindAction("Move");
                if (moveAction != null) moveAction.Enable();
            }
        }

        private void OnDisable()
        {
            if (moveAction != null)
            {
                moveAction.performed -= OnMovePerformed;
                moveAction.canceled -= OnMoveCanceled;
                moveAction.Disable();
            }
        }

        private void FixedUpdate()
        {
            HandleMovement();
        }
        #endregion

        #region Movement
        private void HandleMovement()
        {
            if (!isMoving)
            {
                rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, linearDrag * Time.fixedDeltaTime);
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