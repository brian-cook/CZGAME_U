using UnityEngine;
using UnityEngine.InputSystem;
using NaughtyAttributes;
using CZ.Core;
using CZ.Core.Input;
using CZ.Core.Pooling;
using CZ.Core.Interfaces;
using static NaughtyAttributes.EInfoBoxType;
using System.Linq;

namespace CZ.Core.Player
{
    /// <summary>
    /// Handles player movement, input, and game interactions
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(PlayerHealth))]
    public class PlayerController : MonoBehaviour, IPositionProvider, IPlayerReference
    {
        #region Components
        private Rigidbody2D rb;
        private CircleCollider2D circleCollider;
        private TrailRenderer movementTrail;
        private static Material sharedTrailMaterial;
        private GameControls controls;
        private ObjectPool<Projectile> projectilePool;
        private PlayerHealth playerHealth;
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

        [BoxGroup("Attack Settings")]
        [SerializeField]
        [InfoBox("Projectile prefab for auto-attack", Normal)]
        private Projectile projectilePrefab;

        [BoxGroup("Attack Settings")]
        [SerializeField, MinValue(0.1f), MaxValue(2f)]
        [InfoBox("Time between auto-attacks in seconds", Normal)]
        private float attackCooldown = 0.5f;

        [BoxGroup("Debug Settings")]
        [SerializeField]
        private bool enableDebugLogs = false;
        #endregion

        #region State
        private Vector2 moveInput;
        private bool isMoving;
        private bool isInitialized;
        private bool isInputEnabled;
        private float lastAttackTime;
        private Vector2 lastMoveDirection = Vector2.right;
        private Vector2 mousePosition;
        private Vector2 gamepadAimInput;
        private bool isUsingGamepad;
        private bool isDead;
        #endregion

        #region Test Support
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        public float MaxVelocity => maxVelocity;
        
        /// <summary>
        /// Test method to simulate input. Only available in editor and development builds.
        /// </summary>
        public void TestInput(Vector2 input)
        {
            if (!isInputEnabled) return;
            moveInput = input;
            isMoving = moveInput.sqrMagnitude > 0.01f;
            
            if (movementTrail != null)
            {
                movementTrail.emitting = isMoving;
            }
        }
        #endif
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            Debug.Log("[PlayerController] Awake called");
            SetupComponents();
            controls = new GameControls();
            
            // Subscribe to input events
            controls.Player.Move.performed += OnMove;
            controls.Player.Move.canceled += OnMove;
            controls.Player.Attack.performed += OnAttack;
            controls.Player.MousePosition.performed += OnMousePosition;
            controls.Player.GamepadAim.performed += OnGamepadAim;
            controls.Player.GamepadAim.canceled += OnGamepadAim;
            
            Debug.Log("[PlayerController] Input system initialized");
            
            // Subscribe to game state changes
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
                Debug.Log("[PlayerController] Subscribed to GameManager events");
            }
            else
            {
                Debug.LogError("[PlayerController] GameManager instance not found!");
            }

            // Initialize projectile pool
            InitializeProjectilePool();
            
            // Subscribe to health events
            if (playerHealth != null)
            {
                playerHealth.OnDeath += HandlePlayerDeath;
                playerHealth.OnRespawn += HandlePlayerRespawn;
                Debug.Log("[PlayerController] Subscribed to PlayerHealth events");
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
                
                // Use bodyType instead of the obsolete isKinematic property
                rb.bodyType = RigidbodyType2D.Dynamic;
                
                // Make sure simulation is enabled
                rb.simulated = true;
            }
            
            // Get and configure CircleCollider2D
            circleCollider = GetComponent<CircleCollider2D>();
            if (circleCollider != null)
            {
                circleCollider.radius = 0.5f;
                circleCollider.isTrigger = false;  // Must be false for physical collisions
            }
            
            // Ensure the player is on the Player layer for proper collision matrix handling
            gameObject.layer = LayerMask.NameToLayer("Player");
            
            // Get PlayerHealth component
            playerHealth = GetComponent<PlayerHealth>();
            if (playerHealth == null)
            {
                Debug.LogError("[PlayerController] PlayerHealth component not found!");
                playerHealth = gameObject.AddComponent<PlayerHealth>();
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

        private void InitializeProjectilePool()
        {
            if (projectilePrefab == null)
            {
                Debug.LogError("[PlayerController] Projectile prefab not assigned! Please assign a projectile prefab in the inspector.");
                return;
            }

            try
            {
                if (PoolManager.Instance == null)
                {
                    Debug.LogError("[PlayerController] PoolManager instance is null! Cannot initialize projectile pool.");
                    return;
                }

                // Ensure the prefab is inactive before creating the pool
                bool wasActive = projectilePrefab.gameObject.activeSelf;
                if (wasActive)
                {
                    projectilePrefab.gameObject.SetActive(false);
                }

                projectilePool = PoolManager.Instance.CreatePool(
                    createFunc: () => {
                        var proj = Instantiate(projectilePrefab);
                        proj.gameObject.SetActive(false);
                        return proj;
                    },
                    initialSize: 20,  // Reduced from 100 to improve initialization performance
                    maxSize: 100,     // Reduced from 200 to prevent memory issues
                    poolName: "ProjectilePool"
                );

                // Restore prefab active state if needed
                if (wasActive)
                {
                    projectilePrefab.gameObject.SetActive(true);
                }

                Debug.Log("[PlayerController] Projectile pool initialized successfully with initial size: 20, max size: 100");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PlayerController] Failed to initialize projectile pool: {e.Message}\n{e.StackTrace}");
            }
        }

        private void OnEnable()
        {
            if (!isInitialized)
            {
                SetupComponents();
            }

            // Only enable controls if game is in Playing state
            if (GameManager.Instance?.CurrentGameState == GameManager.GameState.Playing && !isDead)
            {
                EnablePlayerInput();
            }
            else
            {
                isInputEnabled = false;
            }
            
            Debug.Log($"[PlayerController] OnEnable - Input Enabled: {isInputEnabled}, Controls Active: {controls != null && controls.Player.enabled}");
        }

        private void EnablePlayerInput()
        {
            if (controls == null)
            {
                Debug.LogError("[PlayerController] Cannot enable input - controls is null!");
                return;
            }
            
            try
            {
                controls.Enable();
                isInputEnabled = true;
                Debug.Log("[PlayerController] Input system enabled");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PlayerController] Error enabling input: {e.Message}");
                isInputEnabled = false;
            }
        }

        private void DisablePlayerInput()
        {
            if (controls == null) return;
            
            try
            {
                controls.Disable();
                isInputEnabled = false;
                Debug.Log("[PlayerController] Input system disabled");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PlayerController] Error disabling input: {e.Message}");
            }
        }

        private void FixedUpdate()
        {
            if (isInputEnabled)
            {
                HandleMovement();
            }
        }

        private void Update()
        {
            // Debug input system every few seconds
            if (Time.frameCount % 60 == 0) // Check roughly every second at 60 FPS
            {
                DebugInputSystem();
            }
        }

        private void DebugInputSystem()
        {
            if (controls == null)
            {
                Debug.LogError("[PlayerController] Controls object is null!");
                return;
            }

            try
            {
                bool playerActionsEnabled = controls.Player.enabled;
                bool attackActionEnabled = controls.Player.Attack.enabled;
                
                Debug.Log($"[PlayerController] Input System Status - " +
                         $"Controls: {controls != null}, " +
                         $"Player Actions Enabled: {playerActionsEnabled}, " +
                         $"Attack Action Enabled: {attackActionEnabled}, " +
                         $"Input Enabled Flag: {isInputEnabled}, " +
                         $"Is Dead: {isDead}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PlayerController] Error checking input system: {e.Message}");
            }
        }

        private void OnDisable()
        {
            if (controls != null)
            {
                controls.Player.Move.performed -= OnMove;
                controls.Player.Move.canceled -= OnMove;
                controls.Player.Attack.performed -= OnAttack;
                controls.Player.MousePosition.performed -= OnMousePosition;
                controls.Player.GamepadAim.performed -= OnGamepadAim;
                controls.Player.GamepadAim.canceled -= OnGamepadAim;
                controls.Player.Disable();
                controls.Disable();
            }
            
            if (movementTrail != null)
            {
                movementTrail.emitting = false;
            }
            
            isInputEnabled = false;
            moveInput = Vector2.zero;
            isMoving = false;
            
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
            
            Debug.Log("[PlayerController] OnDisable - Input system disabled");
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            }
            
            if (controls != null)
            {
                // Ensure Player actions are disabled first
                controls.Player.Move.performed -= OnMove;
                controls.Player.Move.canceled -= OnMove;
                controls.Player.Attack.performed -= OnAttack;
                controls.Player.MousePosition.performed -= OnMousePosition;
                controls.Player.GamepadAim.performed -= OnGamepadAim;
                controls.Player.GamepadAim.canceled -= OnGamepadAim;
                controls.Player.Disable();
                
                // Then disable and dispose the entire controls
                controls.Disable();
                controls.Dispose();
                controls = null;
            }
            
            // Unsubscribe from health events
            if (playerHealth != null)
            {
                playerHealth.OnDeath -= HandlePlayerDeath;
                playerHealth.OnRespawn -= HandlePlayerRespawn;
            }

            // Cleanup shared material if this is the last instance
            if (sharedTrailMaterial != null)
            {
                var activeControllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
                if (!activeControllers.Any(pc => pc != this && pc.enabled))
                {
                    Destroy(sharedTrailMaterial);
                    sharedTrailMaterial = null;
                }
            }
        }
        #endregion

        #region Movement
        private void HandleMovement()
        {
            if (!isMoving || isDead)
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
            if (!isInputEnabled || isDead)
            {
                Debug.Log("[PlayerController] Input received but not enabled or player is dead");
                return;
            }
            
            moveInput = context.ReadValue<Vector2>();
            isMoving = moveInput.sqrMagnitude > 0.01f;
            
            if (isMoving)
            {
                lastMoveDirection = moveInput.normalized;
            }
            
            if (movementTrail != null)
            {
                movementTrail.emitting = isMoving;
            }
            
            if (enableDebugLogs && isMoving)
            {
                Debug.Log($"[PlayerController] Move input: {moveInput}");
            }
        }

        private void OnAttack(InputAction.CallbackContext context)
        {
            // Add more detailed debug logging to diagnose the issue
            Debug.Log($"[PlayerController] OnAttack called - InputEnabled: {isInputEnabled}, IsDead: {isDead}, IsPerformed: {context.performed}, Controls: {controls != null}");
            
            if (!isInputEnabled || !context.performed || isDead) 
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"[PlayerController] Attack input ignored - InputEnabled: {isInputEnabled}, IsDead: {isDead}");
                }
                return;
            }

            float currentTime = Time.time;
            if (currentTime - lastAttackTime >= attackCooldown)
            {
                try
                {
                    Debug.Log("[PlayerController] Attempting to fire projectile");
                    FireProjectile();
                    lastAttackTime = currentTime;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[PlayerController] Error firing projectile: {e.Message}\n{e.StackTrace}");
                }
            }
            else if (enableDebugLogs)
            {
                Debug.Log($"[PlayerController] Attack on cooldown. Time since last attack: {currentTime - lastAttackTime}s");
            }
        }

        private void OnMousePosition(InputAction.CallbackContext context)
        {
            if (!isInputEnabled) return;
            mousePosition = context.ReadValue<Vector2>();
        }

        private void OnGamepadAim(InputAction.CallbackContext context)
        {
            if (!isInputEnabled) return;
            
            gamepadAimInput = context.ReadValue<Vector2>();
            isUsingGamepad = gamepadAimInput.sqrMagnitude > 0.01f;
            
            if (enableDebugLogs && isUsingGamepad)
            {
                Debug.Log($"[PlayerController] Gamepad aim input: {gamepadAimInput}");
            }
        }

        private void FireProjectile()
        {
            if (isDead)
            {
                Debug.LogWarning("[PlayerController] Cannot fire projectile - player is dead!");
                return;
            }

            if (projectilePool == null)
            {
                Debug.LogError("[PlayerController] Projectile pool not initialized! Attempting to reinitialize...");
                InitializeProjectilePool();
                
                if (projectilePool == null)
                {
                    Debug.LogError("[PlayerController] Failed to reinitialize projectile pool!");
                    return;
                }
            }

            try
            {
                Debug.Log("[PlayerController] Getting projectile from pool");
                var projectile = projectilePool.Get();
                if (projectile != null)
                {
                    projectile.transform.position = transform.position;
                    
                    Vector2 fireDirection;
                    if (isUsingGamepad && gamepadAimInput.sqrMagnitude > 0.01f)
                    {
                        // Use gamepad aim direction
                        fireDirection = gamepadAimInput.normalized;
                        Debug.Log($"[PlayerController] Using gamepad aim direction: {fireDirection}");
                    }
                    else
                    {
                        // Use mouse aim or last move direction as fallback
                        if (Camera.main != null)
                        {
                            Vector2 worldMousePos = Camera.main.ScreenToWorldPoint(mousePosition);
                            fireDirection = (worldMousePos - (Vector2)transform.position).normalized;
                            Debug.Log($"[PlayerController] Using mouse aim direction: {fireDirection}, Mouse position: {mousePosition}, World position: {worldMousePos}");
                        }
                        else
                        {
                            // Fallback to last move direction if camera is null
                            fireDirection = lastMoveDirection;
                            Debug.LogWarning("[PlayerController] Main camera not found, using last move direction for projectile!");
                        }
                    }
                    
                    // Ensure direction is normalized
                    if (fireDirection.sqrMagnitude < 0.01f)
                    {
                        fireDirection = lastMoveDirection.sqrMagnitude > 0.01f ? lastMoveDirection : Vector2.right;
                        Debug.Log($"[PlayerController] Using fallback direction: {fireDirection}");
                    }
                    
                    Debug.Log($"[PlayerController] Initializing projectile with direction: {fireDirection}");
                    projectile.Initialize(fireDirection, this.gameObject);
                    Debug.Log($"[PlayerController] Projectile fired successfully: {projectile.name}");
                }
                else
                {
                    Debug.LogError("[PlayerController] Failed to get projectile from pool!");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PlayerController] Error in FireProjectile: {e.Message}\n{e.StackTrace}");
            }
        }
        #endregion

        #region Health Management
        private void HandlePlayerDeath()
        {
            isDead = true;
            isInputEnabled = false;
            
            // Stop movement
            moveInput = Vector2.zero;
            isMoving = false;
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
            
            // Disable trail
            if (movementTrail != null)
            {
                movementTrail.emitting = false;
            }
            
            // Disable controls during death
            DisablePlayerInput();
            
            Debug.Log("[PlayerController] Player died, input disabled");
        }
        
        private void HandlePlayerRespawn()
        {
            isDead = false;
            
            // Only re-enable input if game is in playing state
            if (GameManager.Instance?.CurrentGameState == GameManager.GameState.Playing)
            {
                EnablePlayerInput();
                Debug.Log("[PlayerController] Player respawned, input re-enabled");
            }
        }
        #endregion

        #region Game State
        private void HandleGameStateChanged(GameManager.GameState newState)
        {
            // Don't enable input if player is dead
            isInputEnabled = newState == GameManager.GameState.Playing && !isDead;
            
            if (isInputEnabled)
            {
                EnablePlayerInput();
                Debug.Log("[PlayerController] Input enabled due to game state change to Playing");
            }
            else
            {
                DisablePlayerInput();
                // Reset movement state when input is disabled
                moveInput = Vector2.zero;
                isMoving = false;
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                }
                if (movementTrail != null)
                {
                    movementTrail.emitting = false;
                }
            }
            
            Debug.Log($"[PlayerController] Game state changed to {newState}, input enabled: {isInputEnabled}");
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Gets the current velocity of the player
        /// </summary>
        /// <returns>Current velocity as Vector2</returns>
        public Vector2 GetVelocity()
        {
            return rb != null ? rb.linearVelocity : Vector2.zero;
        }
        #endregion

        #region IPositionProvider Implementation
        public Vector3 GetPosition()
        {
            return transform.position;
        }
        #endregion

        #region IPlayerReference Implementation
        /// <summary>
        /// The player's transform component
        /// </summary>
        public Transform PlayerTransform => transform;
        
        /// <summary>
        /// The current position of the player
        /// </summary>
        public Vector3 PlayerPosition => transform.position;
        
        /// <summary>
        /// Whether the player is currently alive
        /// </summary>
        public bool IsPlayerAlive => playerHealth == null ? true : !playerHealth.IsDead;
        #endregion
    }
}
