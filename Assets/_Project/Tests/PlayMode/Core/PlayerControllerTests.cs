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

        [SetUp]
        public override void Setup()
        {
            base.Setup();

            // Create player object with required components
            playerObject = new GameObject("Player");
            rb = playerObject.AddComponent<Rigidbody2D>();
            
            // Setup input actions before adding PlayerController
            var inputActionsPath = "Assets/_Project/Input/GameControls.inputactions";
            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(inputActionsPath);
            if (inputActions == null)
            {
                Debug.LogError($"Failed to load input actions at path: {inputActionsPath}");
                Assert.Fail("Input actions asset not found");
            }

            // Setup keyboard first
            keyboard = InputSystem.AddDevice<Keyboard>();

            // Setup PlayerInput
            playerInput = playerObject.AddComponent<PlayerInput>();
            playerInput.actions = inputActions;
            playerInput.defaultActionMap = "Player";
            
            // Enable the action map
            playerInput.actions.Enable();
            
            // Now add PlayerController after input is setup
            playerController = playerObject.AddComponent<PlayerController>();
            
            // We no longer expect this error since we properly initialized input
            //LogAssert.Expect(LogType.Error, "PlayerInput or its actions are null");
        }

        [UnitySetUp]
        public IEnumerator UnitySetup()
        {
            // Wait for next frame to ensure everything is initialized
            yield return null;
            
            // Verify input system is ready
            Assert.That(playerInput.actions.enabled, Is.True, "Input actions should be enabled");
            Assert.That(keyboard.added, Is.True, "Keyboard should be added to Input System");
        }

        [UnityTest]
        public IEnumerator PlayerController_Movement_RespondsToInput()
        {
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
            Set(key, 1.0f);
        }

        private void Release(KeyControl key)
        {
            Set(key, 0.0f);
        }

        private void Set(KeyControl key, float value)
        {
            InputState.Change(key, value);
            InputSystem.Update();
        }

        [TearDown]
        public override void TearDown()
        {
            if (playerObject != null)
            {
                Object.Destroy(playerObject);
            }
            
            if (keyboard != null)
            {
                InputSystem.RemoveDevice(keyboard);
            }

            base.TearDown();
        }
    }
} 