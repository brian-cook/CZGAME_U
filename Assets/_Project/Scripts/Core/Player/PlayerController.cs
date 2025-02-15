using UnityEngine;
using UnityEngine.InputSystem;

namespace CZ.Core.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        #region Components
        private Rigidbody2D rb;
        private PlayerInput playerInput;
        private InputAction moveAction;
        #endregion

        #region Configuration
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float maxVelocity = 10f;
        
        [Header("Physics")]
        [SerializeField] private float linearDrag = 5f;
        #endregion

        #region State
        private Vector2 moveInput;
        private bool isMoving;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            playerInput = GetComponent<PlayerInput>();
            
            // Configure Rigidbody
            rb.gravityScale = 0f;
            rb.linearDamping = linearDrag;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        private void OnEnable()
        {
            // Get reference to move action
            if (playerInput != null && playerInput.actions != null)
            {
                moveAction = playerInput.actions["Move"];
                if (moveAction != null)
                {
                    moveAction.Enable();
                }
                else
                {
                    Debug.LogError("Move action not found in Input Actions asset");
                }
            }
            else
            {
                Debug.LogError("PlayerInput or its actions are null");
            }
        }

        private void OnDisable()
        {
            moveAction?.Disable();
        }

        private void FixedUpdate()
        {
            HandleMovement();
        }
        #endregion

        #region Input Handling
        public void OnMove(InputAction.CallbackContext context)
        {
            moveInput = context.ReadValue<Vector2>();
            isMoving = moveInput.sqrMagnitude > 0.01f;
        }
        #endregion

        #region Movement
        private void HandleMovement()
        {
            if (isMoving)
            {
                // Apply movement force
                Vector2 moveForce = moveInput * moveSpeed;
                rb.AddForce(moveForce, ForceMode2D.Force);

                // Clamp velocity
                rb.linearVelocity = Vector2.ClampMagnitude(rb.linearVelocity, maxVelocity);
            }
        }
        #endregion
    }
} 