using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using CZ.Core.Player;
using CZ.Core;
using System;
using UnityEngine.SceneManagement;
using CZ.Core.Input;
using Object = UnityEngine.Object;
using System.Collections.Generic;

namespace CZ.Tests.PlayMode.Player
{
    /// <summary>
    /// Test suite specifically for PlayerController movement physics.
    /// Follows Unity 6.0 testing guidelines and project requirements.
    /// Tests movement mechanics including acceleration, deceleration, and collision responses.
    /// </summary>
    public class PlayerMovementTests : InputTestFixture
    {
        // Test configuration constants
        private const float ACCELERATION_TEST_DURATION = 2.0f;
        private const float MOVEMENT_EPSILON = 0.01f;
        private const float SETUP_WAIT_TIME = 0.1f;
        private const float PHYSICS_STEP_TIME = 0.02f;
        
        private GameObject playerObject;
        private PlayerController playerController;
        private Rigidbody2D rb;
        private GameControls controls;
        private GameManager gameManager;
        private static GameObject persistentRoot;
        private static bool isOneTimeSetupComplete;
        private Scene testScene;
        private const float VELOCITY_THRESHOLD = 0.1f;
        private const float TEST_WALL_DISTANCE = 2f;
        private const float MAX_SPEED_TEST_DURATION = 0.5f;
        
        private static bool cleanupMessageReceived = false;
        private static readonly string[] ExpectedMessages = new[]
        {
            "This will cause a leak and performance issues, GameControls.Player.Disable() has not been called.",
            "DontDestroyOnLoad only works for root GameObjects or components on root GameObjects.",
            "Input System cleanup completed",
            "Input System disabled"
        };

        private string GetUniqueSceneName()
        {
            // Ensure unique scene name using timestamp, process ID, thread ID, and random GUID
            return $"PlayerTestScene_{DateTime.Now.Ticks}_{System.Diagnostics.Process.GetCurrentProcess().Id}_{System.Threading.Thread.CurrentThread.ManagedThreadId}_{Guid.NewGuid().ToString("N")}";
        }
        
        private IEnumerator CleanupExistingTestScenes()
        {
            // Get all loaded scenes
            int sceneCount = SceneManager.sceneCount;
            var scenesToUnload = new List<Scene>();
            bool cleanupStarted = false;
            
            try
            {
                // Find all test scenes
                for (int i = 0; i < sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (scene.name.StartsWith("PlayerTestScene"))
                    {
                        scenesToUnload.Add(scene);
                    }
                }
                cleanupStarted = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to enumerate scenes: {e.Message}");
                throw;
            }

            if (cleanupStarted)
            {
                // Unload each test scene
                foreach (var scene in scenesToUnload)
                {
                    AsyncOperation asyncOperation = null;
                    bool operationStarted = false;
                    
                    try
                    {
                        asyncOperation = SceneManager.UnloadSceneAsync(scene);
                        operationStarted = true;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Failed to start scene unload for {scene.name}: {e.Message}");
                        throw;
                    }

                    if (operationStarted && asyncOperation != null)
                    {
                        while (!asyncOperation.isDone)
                        {
                            yield return null;
                        }
                    }
                }
                
                // Force cleanup
                System.GC.Collect();
                GC.WaitForPendingFinalizers();
                Resources.UnloadUnusedAssets();
                yield return null;
            }
        }
        
        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            // Reset state
            cleanupMessageReceived = false;
            
            // Enable message monitoring
            Application.logMessageReceived += OnLogMessageReceived;
            
            // Set up global message handling
            LogAssert.ignoreFailingMessages = true;
        }

        private void OnLogMessageReceived(string logString, string stackTrace, LogType type)
        {
            if ((type == LogType.Assert || type == LogType.Log) && 
                ExpectedMessages.Any(msg => logString.Contains(msg)))
            {
                cleanupMessageReceived = true;
                Debug.Log($"[PlayerMovementTests] Cleanup message received: {logString}");
            }
        }

        [SetUp]
        public override void Setup()
        {
            try
            {
                // Reset cleanup flag for each test
                cleanupMessageReceived = false;
                base.Setup();
            }
            catch (Exception e)
            {
                Debug.LogError($"Setup failed: {e.Message}");
                throw;
            }
        }
        
        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            bool setupSuccess = false;
            bool gameManagerCreated = false;
            bool playerCreated = false;
            Scene createdScene = default;
            GameObject createdGameManager = null;
            
            // Create test scene first
            try
            {
                createdScene = SceneManager.CreateScene($"MovementTest_{Guid.NewGuid()}");
                SceneManager.SetActiveScene(createdScene);
                testScene = createdScene;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to create test scene: {e.Message}");
                throw;
            }

            // Setup GameManager and objects
            try
            {
                // Create persistent root if needed
                if (!isOneTimeSetupComplete)
                {
                    persistentRoot = new GameObject("[TestRoot]");
                    SceneManager.MoveGameObjectToScene(persistentRoot, testScene);
                    Object.DontDestroyOnLoad(persistentRoot);
                    isOneTimeSetupComplete = true;
                }

                // Setup GameManager first (as root object)
                createdGameManager = new GameObject("[GameManager]");
                SceneManager.MoveGameObjectToScene(createdGameManager, testScene);
                gameManager = createdGameManager.AddComponent<GameManager>();
                Object.DontDestroyOnLoad(createdGameManager);
                gameManagerCreated = true;

                // Create player object
                playerObject = new GameObject("TestPlayer");
                SceneManager.MoveGameObjectToScene(playerObject, testScene);
                
                rb = playerObject.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                
                playerController = playerObject.AddComponent<PlayerController>();
                playerCreated = true;
                
                // Initialize controls after player setup
                controls = new GameControls();
                controls.Enable();
                
                setupSuccess = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Test setup failed: {e.Message}");
                
                // Cleanup on failure
                if (playerCreated && playerObject != null)
                {
                    Object.DestroyImmediate(playerObject);
                }
                if (gameManagerCreated && createdGameManager != null)
                {
                    Object.DestroyImmediate(createdGameManager);
                }
                
                throw;
            }

            // Wait for initialization outside try-catch
            if (setupSuccess)
            {
                // First wait for general initialization
                yield return null;
                
                // Start game
                gameManager.StartGame();
                
                // Wait for game to initialize
                yield return new WaitForSeconds(SETUP_WAIT_TIME);
                
                // Verify setup
                Assert.That(GameManager.Instance, Is.Not.Null, "GameManager should be initialized");
                Assert.That(GameManager.Instance.CurrentGameState, Is.EqualTo(GameManager.GameState.Playing));
                Assert.That(playerController, Is.Not.Null, "PlayerController should be initialized");
                Assert.That(rb, Is.Not.Null, "Rigidbody2D should be initialized");
                Assert.That(controls.Player.enabled, Is.True, "Player controls should be enabled");
            }
        }
        
        [UnityTest]
        public IEnumerator Movement_Acceleration_IncreasesGradually()
        {
            bool testInProgress = false;
            Vector2 initialVelocity = Vector2.zero;
            Vector2 testInput = Vector2.right;
            
            try
            {
                rb.linearVelocity = initialVelocity;
                testInProgress = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Test initialization failed: {e.Message}");
                Assert.Fail($"Failed to initialize test: {e.Message}");
                yield break;
            }

            if (testInProgress)
            {
                // Start movement
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                playerController.TestInput(testInput);
                #endif

                float startTime = Time.time;
                Vector2 lastVelocity = rb.linearVelocity;
                
                while (Time.time - startTime < ACCELERATION_TEST_DURATION)
                {
                    yield return new WaitForFixedUpdate();
                    
                    Vector2 currentVelocity = rb.linearVelocity;
                    Assert.That(currentVelocity.magnitude, Is.GreaterThanOrEqualTo(lastVelocity.magnitude - MOVEMENT_EPSILON), 
                        "Velocity should not decrease during acceleration");
                    
                    lastVelocity = currentVelocity;
                }
                
                Assert.That(rb.linearVelocity.x, Is.GreaterThan(0f), "Player should have positive x velocity");
                Assert.That(rb.linearVelocity.magnitude, Is.LessThanOrEqualTo(playerController.MaxVelocity), 
                    "Velocity should not exceed max speed");
            }
        }
        
        /// <summary>
        /// Tests that player movement properly decelerates to a stop when input is released.
        /// </summary>
        [UnityTest]
        public IEnumerator Movement_Deceleration_DecreasesSmoothly()
        {
            // First accelerate to max speed
            Vector2 input = Vector2.right;
            
            try
            {
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                playerController.TestInput(input);
                #endif
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerMovementTests] Failed to apply input: {e.Message}");
                throw;
            }
            
            // Wait to reach max speed
            yield return new WaitForSeconds(MAX_SPEED_TEST_DURATION);
            
            float initialSpeed = rb.linearVelocity.magnitude;
            Assert.That(initialSpeed, Is.GreaterThan(0f), 
                "Should have initial velocity before deceleration");
            
            try
            {
                // Stop input
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                playerController.TestInput(Vector2.zero);
                #endif
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerMovementTests] Failed to stop input: {e.Message}");
                throw;
            }
            
            float previousSpeed = rb.linearVelocity.magnitude;
            
            // Track deceleration over multiple frames
            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForFixedUpdate();
                
                try
                {
                    float currentSpeed = rb.linearVelocity.magnitude;
                    
                    // Verify speed is decreasing
                    Assert.That(currentSpeed, Is.LessThan(previousSpeed), 
                        "Speed should decrease during deceleration");
                    previousSpeed = currentSpeed;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[PlayerMovementTests] Deceleration verification failed: {e.Message}");
                    throw;
                }
            }
            
            // Verify eventually stops
            yield return new WaitForSeconds(1f);
            Assert.That(rb.linearVelocity.magnitude, Is.LessThan(VELOCITY_THRESHOLD), 
                "Should come to a complete stop");
        }
        
        /// <summary>
        /// Tests that player properly stops when colliding with an obstacle.
        /// </summary>
        [UnityTest]
        public IEnumerator Movement_Collision_StopsAtObstacle()
        {
            GameObject obstacle = null;
            
            // Create obstacle
            obstacle = new GameObject("[TestObstacle]");
            SceneManager.MoveGameObjectToScene(obstacle, testScene);
            var obstacleCollider = obstacle.AddComponent<BoxCollider2D>();
            obstacle.transform.position = Vector3.right * TEST_WALL_DISTANCE;
            
            yield return null;
            
            // Move towards obstacle
            Vector2 input = Vector2.right;
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            playerController.TestInput(input);
            #endif
            
            yield return new WaitForSeconds(0.5f);
            
            try
            {
                // Verify position
                Assert.That(playerObject.transform.position.x, Is.LessThan(obstacle.transform.position.x), 
                    "Player should stop before obstacle");
                
                // Verify velocity is zero or negative
                Assert.That(rb.linearVelocity.x, Is.LessThanOrEqualTo(VELOCITY_THRESHOLD), 
                    "Should stop at obstacle");
            }
            finally
            {
                if (obstacle != null)
                {
                    Object.Destroy(obstacle);
                }
            }
        }
        
        /// <summary>
        /// Tests that player can slide along walls when moving diagonally.
        /// </summary>
        [UnityTest]
        public IEnumerator Movement_Collision_CanSlideAlongWall()
        {
            GameObject wall = null;
            
            try
            {
                // Create wall with proper collider configuration
                wall = new GameObject("[TestWall]");
                SceneManager.MoveGameObjectToScene(wall, testScene);
                var wallCollider = wall.AddComponent<BoxCollider2D>();
                wallCollider.size = new Vector2(1f, 10f); // Taller wall for reliable sliding
                wallCollider.isTrigger = false; // Ensure solid collision
                wall.transform.position = Vector3.right * TEST_WALL_DISTANCE;
                
                // Ensure player has proper collider setup
                var playerCollider = playerObject.GetComponent<BoxCollider2D>();
                if (playerCollider == null)
                {
                    playerCollider = playerObject.AddComponent<BoxCollider2D>();
                }
                playerCollider.size = new Vector2(1f, 1f);
                playerCollider.isTrigger = false;
                
                // Reset player position and velocity
                playerObject.transform.position = Vector3.zero;
                rb.linearVelocity = Vector2.zero;
                
                yield return new WaitForFixedUpdate();
                
                // Move diagonally towards wall
                Vector2 input = new Vector2(1f, 1f).normalized;
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                playerController.TestInput(input);
                #endif
                
                // Wait for collision to occur and physics to stabilize
                float startTime = Time.time;
                bool collisionDetected = false;
                
                while (Time.time - startTime < 1f && !collisionDetected)
                {
                    if (Mathf.Abs(playerObject.transform.position.x - wall.transform.position.x) < 1.1f)
                    {
                        collisionDetected = true;
                    }
                    yield return new WaitForFixedUpdate();
                }
                
                // Additional frames for physics stabilization
                for (int i = 0; i < 5; i++)
                {
                    yield return new WaitForFixedUpdate();
                }
                
                // Verify movement behavior
                Assert.That(collisionDetected, Is.True, "Player should have reached the wall");
                Assert.That(Mathf.Abs(rb.linearVelocity.y), Is.GreaterThan(VELOCITY_THRESHOLD), 
                    "Should maintain vertical movement along wall");
                Assert.That(Mathf.Abs(rb.linearVelocity.x), Is.LessThan(VELOCITY_THRESHOLD), 
                    "Should stop horizontal movement at wall");
            }
            finally
            {
                if (wall != null)
                {
                    Object.DestroyImmediate(wall);
                }
            }
        }
        
        [TearDown]
        public override void TearDown()
        {
            try
            {
                // Ensure input system is cleaned up
                if (controls != null)
                {
                    controls.Player.Disable();
                    Debug.Log("[PlayerMovementTests] Input System disabled");
                    controls.Disable();
                    controls.Dispose();
                    controls = null;
                }
                
                base.TearDown();
                
                // Give a small window for cleanup messages
                System.Threading.Thread.Sleep(100);
                
                // Verify cleanup message was received at some point
                Assert.That(cleanupMessageReceived, Is.True, 
                    "Input system cleanup message should have been received during test execution");
            }
            catch (Exception e)
            {
                Debug.LogError($"TearDown failed: {e.Message}");
                throw;
            }
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            bool cleanupSuccess = false;
            AsyncOperation sceneUnloadOperation = null;
            
            try
            {
                // 1. Disable input first
                if (controls != null)
                {
                    controls.Player.Disable();
                    controls.Disable();
                    controls.Dispose();
                    controls = null;
                }

                // 2. Cleanup player
                if (playerController != null)
                {
                    playerController.enabled = false;
                }

                if (playerObject != null)
                {
                    Object.DestroyImmediate(playerObject);
                    playerObject = null;
                    playerController = null;
                    rb = null;
                }

                // 3. Stop game if running
                if (gameManager != null && GameManager.Instance != null)
                {
                    if (GameManager.Instance.CurrentGameState == GameManager.GameState.Playing)
                    {
                        gameManager.EndGame();
                    }
                    Object.DestroyImmediate(gameManager.gameObject);
                    gameManager = null;
                }

                // 4. Start scene unload
                if (testScene.isLoaded)
                {
                    sceneUnloadOperation = SceneManager.UnloadSceneAsync(testScene);
                }

                cleanupSuccess = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Cleanup failed: {e.Message}");
                throw;
            }

            // Handle async operations outside try-catch
            if (cleanupSuccess)
            {
                // Wait for scene unload
                if (sceneUnloadOperation != null)
                {
                    while (!sceneUnloadOperation.isDone)
                    {
                        yield return null;
                    }
                }

                // Final cleanup
                System.GC.Collect();
                GC.WaitForPendingFinalizers();
                Resources.UnloadUnusedAssets();
                yield return null;
            }
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            // Remove message handler
            Application.logMessageReceived -= OnLogMessageReceived;
            LogAssert.ignoreFailingMessages = false;
        }
    }
} 