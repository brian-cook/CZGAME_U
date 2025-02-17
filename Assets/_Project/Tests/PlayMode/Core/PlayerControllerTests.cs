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

                // Ensure standard setup runs after one-time setup
                Setup();
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

            Debug.Log("Starting Standard Setup");
            LogAssert.Expect(LogType.Log, "Starting Standard Setup");

            try
            {
                // Reset state for clean setup
                isStandardSetupComplete = false;
                
                // Call base setup first
                base.Setup();
                
                // Wait for input system initialization
                InputSystem.Update();

                // Ensure we have our persistent root
                Assert.That(persistentRoot, Is.Not.Null, "Test root should exist");
                Assert.That(persistentRoot.activeInHierarchy, Is.True, "Test root should be active");

                // Create player object with required components and proper naming
                playerObject = new GameObject("[TestPlayer]");
                Assert.That(playerObject, Is.Not.Null, "Player object should be created");
                
                playerObject.transform.SetParent(persistentRoot.transform, false);
                Assert.That(playerObject.transform.parent, Is.EqualTo(persistentRoot.transform), "Player should be child of test root");
                
                // Configure Rigidbody2D
                rb = playerObject.AddComponent<Rigidbody2D>();
                Assert.That(rb, Is.Not.Null, "Rigidbody2D should be added");
                rb.gravityScale = 0f;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                
                // Setup keyboard first
                keyboard = InputSystem.AddDevice<Keyboard>();
                Assert.That(keyboard, Is.Not.Null, "Keyboard device should be created");
                Assert.That(keyboard.added, Is.True, "Keyboard should be added to Input System");
                InputSystem.Update(); // Ensure device is properly initialized

                // Now add PlayerController
                playerController = playerObject.AddComponent<PlayerController>();
                Assert.That(playerController, Is.Not.Null, "PlayerController component should be added");
                
                // Initialize GameControls
                controls = new GameControls();
                controls.Enable();
                
                // Final verification of setup
                Assert.That(playerObject.GetComponent<PlayerController>(), Is.EqualTo(playerController), "PlayerController component should be properly attached");
                Assert.That(playerObject.activeInHierarchy, Is.True, "Player object should be active");
                Assert.That(controls.Player.enabled, Is.True, "Player controls should be enabled");

                // Start the game
                gameManager.StartGame();
                
                // Set completion flag only after all setup is successful
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

        [UnitySetUp]
        public IEnumerator UnitySetup()
        {
            if (!isStandardSetupComplete)
            {
                var errorMsg = "Standard Setup must complete before Unity Setup";
                Debug.LogError(errorMsg);
                Assert.Fail(errorMsg);
                yield break;
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
            
            // Setup GameManager
            var gameManagerObj = new GameObject("GameManager");
            gameManager = gameManagerObj.AddComponent<GameManager>();
            Object.DontDestroyOnLoad(gameManagerObj);
            
            // Setup Player
            var playerObj = new GameObject("Player");
            playerController = playerObj.AddComponent<PlayerController>();
            
            // Wait for initialization
            yield return new WaitForSeconds(0.5f);
            
            // Start game
            gameManager.StartGame();
            yield return null;
        }

        private IEnumerator TearDownTestEnvironment()
        {
            // Cleanup player
            if (playerController != null)
            {
                playerController.enabled = false;
                Object.DestroyImmediate(playerController.gameObject);
            }
            
            // Cleanup GameManager
            if (gameManager != null)
            {
                Object.DestroyImmediate(gameManager.gameObject);
            }
            
            // Cleanup test scene
            if (testScene.IsValid())
            {
                yield return SceneManager.UnloadSceneAsync(testScene);
            }
        }

        [TearDown]
        public override void TearDown()
        {
            controls?.Disable();
            controls?.Dispose();
            
            if (!isStandardSetupComplete) return;

            Debug.Log("Starting Standard Teardown");
            LogAssert.Expect(LogType.Log, "Starting Standard Teardown");

            try
            {
                if (playerObject != null)
                {
                    // Properly cleanup components before destroying object
                    if (playerController != null)
                    {
                        playerController.enabled = false;
                    }
                    
                    Object.DestroyImmediate(playerObject);
                    playerObject = null;
                }
                
                if (keyboard != null)
                {
                    InputSystem.RemoveDevice(keyboard);
                    keyboard = null;
                }

                isStandardSetupComplete = false;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Standard Teardown Failed: {e.Message}");
                throw;
            }

            base.TearDown();

            Debug.Log("Standard Teardown Complete");
            LogAssert.Expect(LogType.Log, "Standard Teardown Complete");
        }

        [UnityEngine.TestTools.UnityTearDownAttribute]
        public IEnumerator UnityOneTimeTearDown()
        {
            if (!isOneTimeSetupComplete) yield break;

            Debug.Log("Starting One Time Teardown");
            LogAssert.Expect(LogType.Log, "Starting One Time Teardown");

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
            
            // Wait a frame to ensure cleanup
            yield return null;

            Debug.Log("One Time Teardown Complete");
            LogAssert.Expect(LogType.Log, "One Time Teardown Complete");
        }
    }
} 