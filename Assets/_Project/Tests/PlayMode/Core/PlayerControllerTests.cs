using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using CZ.Core.Player;
using CZ.Core.Input;
using CZ.Core;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace CZ.Tests.PlayMode
{
    /// <summary>
    /// Test suite for the PlayerController component.
    /// Follows Unity 6.0 testing guidelines and project requirements.
    /// </summary>
    public class PlayerControllerTests : InputTestFixture
    {
        private GameObject playerObject;
        private PlayerController playerController;
        private Rigidbody2D rb;
        private GameControls controls;
        private Keyboard keyboard;
        private static GameObject persistentRoot;
        private static bool isOneTimeSetupComplete;
        private static bool isStandardSetupComplete;
        private GameManager gameManager;
        private Scene testScene;

        [UnityEngine.TestTools.UnitySetUpAttribute]
        public IEnumerator UnityOneTimeSetup()
        {
            if (!isOneTimeSetupComplete)
            {
                Debug.Log("Starting One Time Setup");
                LogAssert.Expect(LogType.Log, "Starting One Time Setup");
                
                // Expect cleanup messages throughout the test lifecycle
                LogAssert.ignoreFailingMessages = true;  // Ignore cleanup messages globally
                
                // Create a persistent root object to hold our test objects
                persistentRoot = new GameObject("[TestRoot]");
                Object.DontDestroyOnLoad(persistentRoot);
                
                // Create GameManager first
                var gmObject = new GameObject("[GameManager]");
                gmObject.transform.SetParent(persistentRoot.transform);
                gameManager = gmObject.AddComponent<GameManager>();
                
                // Wait a frame to ensure the object is properly initialized
                yield return null;
                
                // Verify root persistence
                Assert.That(persistentRoot, Is.Not.Null, "Test root should be created");
                Assert.That(persistentRoot.activeInHierarchy, Is.True, "Test root should be active");
                Assert.That(gameManager, Is.Not.Null, "GameManager should be created");
                
                isOneTimeSetupComplete = true;
                
                Debug.Log("One Time Setup Complete");
                LogAssert.Expect(LogType.Log, "One Time Setup Complete");
            }
            
            yield return null;
        }

        [SetUp]
        public override void Setup()
        {
            if (!isOneTimeSetupComplete)
            {
                Debug.LogError("One Time Setup must complete before Standard Setup");
                Assert.Fail("One Time Setup must complete before Standard Setup");
                return;
            }

            // Reset state for clean setup
            CleanupTestObjects();
            
            Debug.Log("Starting Standard Setup");
            LogAssert.Expect(LogType.Log, "Starting Standard Setup");
            
            try
            {
                // Call base setup first
                base.Setup();
                
                // Wait for input system initialization
                InputSystem.Update();

                // Create player object with required components
                playerObject = new GameObject("[TestPlayer]");
                playerObject.transform.SetParent(persistentRoot.transform);
                
                rb = playerObject.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                
                keyboard = InputSystem.AddDevice<Keyboard>();
                InputSystem.Update();

                playerController = playerObject.AddComponent<PlayerController>();
                
                // Initialize GameControls
                controls = new GameControls();
                controls.Enable();
                
                // Start the game
                gameManager.StartGame();
                
                isStandardSetupComplete = true;
                Debug.Log("Standard Setup Complete");
                LogAssert.Expect(LogType.Log, "Standard Setup Complete");
            }
            catch (System.Exception e)
            {
                isStandardSetupComplete = false;
                Debug.LogError($"Standard Setup Failed: {e.Message}");
                throw;
            }
        }

        private void CleanupTestObjects()
        {
            if (controls != null)
            {
                controls.Player.Disable();
                controls.Disable();
                controls.Dispose();
                controls = null;
            }

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

            if (keyboard != null)
            {
                InputSystem.RemoveDevice(keyboard);
                keyboard = null;
            }

            // Force immediate cleanup
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            
            isStandardSetupComplete = false;
        }

        [UnitySetUp]
        public IEnumerator UnitySetup()
        {
            // Ensure Setup has completed
            if (!isStandardSetupComplete)
            {
                Setup();
                yield return null;
            }

            Debug.Log("Starting Unity Setup");
            LogAssert.Expect(LogType.Log, "Starting Unity Setup");
            
            // Initial verification before yielding
            try
            {
                // Verify object persistence
                Assert.That(persistentRoot, Is.Not.Null, "Test root should persist between frames");
                Assert.That(persistentRoot.activeInHierarchy, Is.True, "Test root should be active");
                
                // Verify player object before proceeding
                Assert.That(playerObject, Is.Not.Null, "Player object should persist between frames");
                Assert.That(playerObject.activeInHierarchy, Is.True, "Player object should be active");
                Assert.That(playerObject.transform.parent, Is.EqualTo(persistentRoot.transform), "Player should remain child of test root");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Unity Setup Initial Verification Failed: {e.Message}");
                throw;
            }
            
            // Wait for next frame to ensure everything is initialized
            yield return null;
            
            // Post-yield verification and setup
            try
            {
                // Re-cache components to ensure they're still valid
                playerController = playerObject.GetComponent<PlayerController>();
                rb = playerObject.GetComponent<Rigidbody2D>();
                
                // Safe verification of input system state
                Assert.That(playerController, Is.Not.Null, "PlayerController should exist after initialization");
                Assert.That(rb, Is.Not.Null, "Rigidbody2D should exist after initialization");
                Assert.That(keyboard, Is.Not.Null, "Keyboard should exist");
                Assert.That(keyboard.added, Is.True, "Keyboard should be added to Input System");
                
                var actionMap = controls.Player;
                Assert.That(actionMap, Is.Not.Null, "Player action map should exist after initialization");
                Assert.That(actionMap.enabled, Is.True, "Player action map should be enabled");

                Debug.Log("Unity Setup Complete");
                LogAssert.Expect(LogType.Log, "Unity Setup Complete");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Unity Setup Post-Initialization Failed: {e.Message}");
                throw;
            }
        }

        [UnityTest]
        public IEnumerator PlayerController_Movement_RespondsToInput()
        {
            yield return SetupTestEnvironment();
            
            // Verify initial state
            Assert.That(GameManager.Instance.CurrentGameState, Is.EqualTo(GameManager.GameState.Playing));
            
            // Simulate input
            var input = new Vector2(1f, 0f);
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            playerController.TestInput(input);
            #endif
            
            // Wait for physics
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            
            // Verify movement
            var rb = playerController.GetComponent<Rigidbody2D>();
            Assert.That(rb.linearVelocity.x, Is.GreaterThan(0f));
            
            // Cleanup at end of test
            yield return TearDownTestEnvironment();
        }

        [UnityTest]
        public IEnumerator PlayerController_Velocity_RespectsMaxSpeed()
        {
            yield return SetupTestEnvironment();
            
            // Set maximum input
            var input = new Vector2(1f, 1f).normalized;
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            playerController.TestInput(input);
            #endif
            
            // Wait for physics to stabilize
            yield return new WaitForSeconds(0.5f);
            
            // Verify speed limit
            var rb = playerController.GetComponent<Rigidbody2D>();
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.That(rb.linearVelocity.magnitude, Is.LessThanOrEqualTo(playerController.MaxVelocity));
            #endif
            
            // Cleanup at end of test
            yield return TearDownTestEnvironment();
        }

        private IEnumerator SetupTestEnvironment()
        {
            // Create test scene
            testScene = SceneManager.CreateScene("PlayerTestScene");
            SceneManager.SetActiveScene(testScene);
            
            // Only create GameManager if it doesn't exist
            if (GameManager.Instance == null)
            {
                var gameManagerObj = new GameObject("[GameManager]");
                gameManager = gameManagerObj.AddComponent<GameManager>();
                Object.DontDestroyOnLoad(gameManagerObj);
            }
            
            // Create new player if needed
            if (playerObject == null)
            {
                playerObject = new GameObject("[TestPlayer]");
                playerObject.transform.SetParent(persistentRoot.transform);
                
                rb = playerObject.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                
                playerController = playerObject.AddComponent<PlayerController>();
            }
            
            // Wait for initialization
            yield return new WaitForSeconds(0.1f);
            
            // Start game if needed
            if (GameManager.Instance.CurrentGameState != GameManager.GameState.Playing)
            {
                GameManager.Instance.StartGame();
            }
            
            yield return null;
            
            // Verify setup
            Assert.That(playerController, Is.Not.Null, "PlayerController should exist");
            Assert.That(playerController.enabled, Is.True, "PlayerController should be enabled");
            Assert.That(GameManager.Instance.CurrentGameState, Is.EqualTo(GameManager.GameState.Playing));
            Assert.That(playerObject.transform.parent, Is.EqualTo(persistentRoot.transform), "Player should be child of test root");
        }

        private IEnumerator TearDownTestEnvironment()
        {
            // Cleanup player
            if (playerController != null)
            {
                // Ensure input system is properly cleaned up
                playerController.enabled = false;
                yield return null;

                // Get the controls instance using reflection to ensure cleanup
                var controlsField = typeof(PlayerController).GetField("controls", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (controlsField != null)
                {
                    var playerControls = controlsField.GetValue(playerController) as GameControls;
                    if (playerControls != null)
                    {
                        playerControls.Player.Disable();
                        playerControls.Disable();
                        playerControls.Dispose();
                    }
                }
                
                // Destroy player object
                if (playerObject != null)
                {
                    Object.DestroyImmediate(playerObject);
                    playerObject = null;
                }
                playerController = null;
                rb = null;
            }
            
            // Cleanup test scene
            if (testScene.IsValid())
            {
                yield return SceneManager.UnloadSceneAsync(testScene);
            }

            // Force garbage collection after cleanup
            System.GC.Collect();
            yield return null;
        }

        [TearDown]
        public override void TearDown()
        {
            Debug.Log("Starting Standard Teardown");
            LogAssert.Expect(LogType.Log, "Starting Standard Teardown");

            try 
            {
                CleanupTestObjects();
                base.TearDown();
                
                Debug.Log("Standard Teardown Complete");
                LogAssert.Expect(LogType.Log, "Standard Teardown Complete");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Teardown failed: {e.Message}\n{e.StackTrace}");
                throw;
            }
        }

        [UnityEngine.TestTools.UnityTearDownAttribute]
        public IEnumerator UnityOneTimeTearDown()
        {
            if (!isOneTimeSetupComplete) yield break;

            Debug.Log("Starting One Time Teardown");
            LogAssert.Expect(LogType.Log, "Starting One Time Teardown");

            // Re-enable log message checking
            LogAssert.ignoreFailingMessages = false;

            if (persistentRoot != null)
            {
                try
                {
                    Object.DestroyImmediate(persistentRoot);
                    persistentRoot = null;
                    isOneTimeSetupComplete = false;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"One Time Teardown Failed: {e.Message}");
                    throw;
                }
            }
            
            yield return null;

            Debug.Log("One Time Teardown Complete");
            LogAssert.Expect(LogType.Log, "One Time Teardown Complete");
        }
    }
} 