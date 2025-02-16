using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using CZ.Core.Player;
using UnityEditor;

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
        private PlayerInput playerInput;
        private Keyboard keyboard;
        private static GameObject persistentRoot;
        private static bool isOneTimeSetupComplete;
        private static bool isStandardSetupComplete;

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
                
                // Wait a frame to ensure the object is properly initialized
                yield return null;
                
                // Verify root persistence
                Assert.That(persistentRoot, Is.Not.Null, "Test root should be created");
                Assert.That(persistentRoot.activeInHierarchy, Is.True, "Test root should be active");
                
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
                
                // Setup input actions before adding PlayerController
                var inputActionsPath = "Assets/_Project/Input/GameControls.inputactions";
                var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(inputActionsPath);
                Assert.That(inputActions, Is.Not.Null, $"Input actions not found at {inputActionsPath}");

                // Setup keyboard first
                keyboard = InputSystem.AddDevice<Keyboard>();
                Assert.That(keyboard, Is.Not.Null, "Keyboard device should be created");
                Assert.That(keyboard.added, Is.True, "Keyboard should be added to Input System");
                InputSystem.Update(); // Ensure device is properly initialized

                // Setup PlayerInput with proper initialization order
                playerInput = playerObject.AddComponent<PlayerInput>();
                Assert.That(playerInput, Is.Not.Null, "PlayerInput component should be added");
                
                playerInput.actions = inputActions;
                playerInput.defaultActionMap = "Player";
                playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
                
                // Now add PlayerController after input is setup
                playerController = playerObject.AddComponent<PlayerController>();
                Assert.That(playerController, Is.Not.Null, "PlayerController component should be added");

                // Ensure the input actions are properly initialized
                Assert.That(playerInput.actions, Is.Not.Null, "PlayerInput actions should be assigned");
                
                // Force enable the action map and bind the callback
                var actionMap = playerInput.actions.FindActionMap("Player");
                Assert.That(actionMap, Is.Not.Null, "Player action map should exist");
                var moveAction = actionMap.FindAction("Move");
                Assert.That(moveAction, Is.Not.Null, "Move action should exist");
                moveAction.performed += ctx => playerController.OnMovePerformed(ctx);
                moveAction.canceled += ctx => playerController.OnMoveCanceled(ctx);
                actionMap.Enable();
                
                // Final verification of setup
                Assert.That(playerObject.GetComponent<PlayerInput>(), Is.EqualTo(playerInput), "PlayerInput component should be properly attached");
                Assert.That(playerObject.GetComponent<PlayerController>(), Is.EqualTo(playerController), "PlayerController component should be properly attached");
                Assert.That(playerObject.activeInHierarchy, Is.True, "Player object should be active");

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
                playerInput = playerObject.GetComponent<PlayerInput>();
                playerController = playerObject.GetComponent<PlayerController>();
                rb = playerObject.GetComponent<Rigidbody2D>();
                
                // Safe verification of input system state
                Assert.That(playerInput, Is.Not.Null, "PlayerInput should exist after initialization");
                Assert.That(playerInput.actions, Is.Not.Null, "PlayerInput actions should exist");
                Assert.That(keyboard, Is.Not.Null, "Keyboard should exist");
                Assert.That(keyboard.added, Is.True, "Keyboard should be added to Input System");
                
                var actionMap = playerInput.actions.FindActionMap("Player");
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
            // Ensure input is ready
            Assert.That(keyboard, Is.Not.Null, "Keyboard should be available");
            Assert.That(keyboard.dKey, Is.Not.Null, "D key should be available");
            
            // Wait a frame before starting input
            yield return null;
            
            // Simulate right movement input
            Press(keyboard.dKey);
            
            // Wait for physics update
            yield return new WaitForFixedUpdate();
            
            // Verify movement
            Assert.That(rb.linearVelocity.x, Is.GreaterThan(0f), "Player should move right");
            
            // Release key
            Release(keyboard.dKey);
            
            // Wait for physics to settle
            yield return new WaitForSeconds(0.5f);
            
            // Verify player stops
            Assert.That(rb.linearVelocity.magnitude, Is.LessThan(0.1f), "Player should stop when no input");
        }

        [UnityTest]
        public IEnumerator PlayerController_Velocity_RespectsMaxSpeed()
        {
            // Simulate diagonal movement (maximum input)
            Press(keyboard.dKey);
            Press(keyboard.wKey);
            
            // Wait for physics to apply maximum velocity
            yield return new WaitForSeconds(1f);
            
            // Verify velocity is clamped
            Assert.That(rb.linearVelocity.magnitude, Is.LessThanOrEqualTo(10f), "Velocity should be clamped to maxVelocity");
            
            // Release keys
            Release(keyboard.dKey);
            Release(keyboard.wKey);
        }

        private void Press(KeyControl key)
        {
            var eventPtr = new InputEventPtr();
            unsafe
            {
                using (StateEvent.From(keyboard, out eventPtr))
                {
                    ((KeyControl)key).WriteValueIntoEvent(1.0f, eventPtr);
                    InputSystem.QueueEvent(eventPtr);
                    InputSystem.Update();
                }
            }
        }

        private void Release(KeyControl key)
        {
            var eventPtr = new InputEventPtr();
            unsafe
            {
                using (StateEvent.From(keyboard, out eventPtr))
                {
                    ((KeyControl)key).WriteValueIntoEvent(0.0f, eventPtr);
                    InputSystem.QueueEvent(eventPtr);
                    InputSystem.Update();
                }
            }
        }

        [TearDown]
        public override void TearDown()
        {
            if (!isStandardSetupComplete) return;

            Debug.Log("Starting Standard Teardown");
            LogAssert.Expect(LogType.Log, "Starting Standard Teardown");

            try
            {
                if (playerObject != null)
                {
                    // Properly cleanup components before destroying object
                    if (playerInput != null)
                    {
                        playerInput.actions?.Disable();
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