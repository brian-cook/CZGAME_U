using UnityEngine;
using NUnit.Framework;
using CZ.Core.Enemy;
using CZ.Core.Interfaces;
using UnityEditor;
using UnityEngine.TestTools;
using System.Collections;
using System.Collections.Generic;

namespace CZ.Tests.EditMode.Core.Enemy
{
    /// <summary>
    /// Tests for the Swift Enemy implementation
    /// </summary>
    public class SwiftEnemyTests
    {
        private GameObject testEnemy;
        private SwiftEnemyController swiftEnemy;
        private MockAnimator mockAnimator;

        /// <summary>
        /// Setup for each test - creates a test enemy object with required components
        /// </summary>
        [SetUp]
        public void Setup()
        {
            // Create test enemy game object
            testEnemy = new GameObject("TestSwiftEnemy");
            
            // Add required components
            testEnemy.AddComponent<SpriteRenderer>();
            testEnemy.AddComponent<CircleCollider2D>();
            
            // Add Rigidbody2D with specific settings
            var rb = testEnemy.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            
            // Add mock animator
            mockAnimator = new MockAnimator();
            testEnemy.AddComponent<MockAnimatorProvider>().Initialize(mockAnimator);
            
            // Add swift enemy controller
            swiftEnemy = testEnemy.AddComponent<SwiftEnemyController>();
            
            // Create a target for the enemy to move towards
            var target = new GameObject("Target").transform;
            target.position = Vector3.right * 10; // Place target to the right
            swiftEnemy.SetTarget(target.position);
            
            // Store target reference for cleanup in teardown
            swiftEnemy.SetTargetTransformForTesting(target);
            
            // Initialize the enemy
            swiftEnemy.Initialize(10, 5f);
        }

        /// <summary>
        /// Teardown after each test - destroys test objects
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(testEnemy);
            if (swiftEnemy.CurrentTarget != null)
            {
                Object.DestroyImmediate(swiftEnemy.CurrentTarget.gameObject);
            }
        }

        /// <summary>
        /// Test to verify the swift enemy has the required components
        /// </summary>
        [Test]
        public void SwiftEnemy_HasRequiredComponents()
        {
            // Assert that all required components exist
            Assert.IsNotNull(testEnemy.GetComponent<SpriteRenderer>(), "Should have SpriteRenderer");
            Assert.IsNotNull(testEnemy.GetComponent<CircleCollider2D>(), "Should have CircleCollider2D");
            Assert.IsNotNull(testEnemy.GetComponent<Rigidbody2D>(), "Should have Rigidbody2D");
            Assert.IsNotNull(testEnemy.GetComponent<SwiftEnemyController>(), "Should have SwiftEnemyController");
        }

        /// <summary>
        /// Test to verify the swift enemy has customized physics properties
        /// </summary>
        [Test]
        public void SwiftEnemy_HasCustomizedPhysics()
        {
            // Get rigidbody
            var rb = testEnemy.GetComponent<Rigidbody2D>();
            
            // Assert that physics are customized for swift movement
            Assert.AreEqual(0f, rb.gravityScale, "Gravity scale should be 0");
            Assert.IsTrue(rb.mass <= 1.0f, "Mass should be 1.0 or less for swift enemies");
            Assert.IsTrue(rb.linearDamping <= 0.3f, "Drag should be low for responsive movement");
            Assert.AreEqual(RigidbodyConstraints2D.FreezeRotation, rb.constraints, "Rotation should be constrained");
        }

        /// <summary>
        /// Test to verify the swift enemy's health is modified correctly
        /// </summary>
        [Test]
        public void SwiftEnemy_HasModifiedHealth()
        {
            // Swift enemies should have health that's 75% of base health
            int expectedHealth = Mathf.RoundToInt(10 * 0.75f); // 10 = base health from Initialize()
            Assert.AreEqual(expectedHealth, swiftEnemy.CurrentHealth, "Swift enemy should have 75% of base health");
        }

        /// <summary>
        /// Test to verify taking damage triggers animation
        /// </summary>
        [Test]
        public void SwiftEnemy_DamageTriggerAnimation()
        {
            // Reset trigger counter
            mockAnimator.ResetTriggers();
            
            // Take damage
            swiftEnemy.TakeDamage(1);
            
            // Verify animation trigger was set
            Assert.IsTrue(mockAnimator.WasTriggerCalled("TakeDamage"), "TakeDamage trigger should be called on animator");
        }

        /// <summary>
        /// Mock animator provider component to attach to test objects
        /// </summary>
        private class MockAnimatorProvider : MonoBehaviour
        {
            private MockAnimator mockAnimator;
            
            public void Initialize(MockAnimator animator)
            {
                mockAnimator = animator;
            }
            
            public void SetTrigger(string name)
            {
                mockAnimator.SetTrigger(name);
            }
            
            public void SetFloat(string name, float value)
            {
                mockAnimator.SetFloat(name, value);
            }
        }

        /// <summary>
        /// Mock Animator class for testing animation triggers
        /// </summary>
        private class MockAnimator
        {
            private HashSet<string> triggeredAnimations = new HashSet<string>();
            private Dictionary<string, float> floatParameters = new Dictionary<string, float>();
            
            public void SetTrigger(string name)
            {
                triggeredAnimations.Add(name);
            }
            
            public void SetFloat(string name, float value)
            {
                floatParameters[name] = value;
            }
            
            public bool WasTriggerCalled(string name)
            {
                return triggeredAnimations.Contains(name);
            }
            
            public float GetFloat(string name)
            {
                return floatParameters.TryGetValue(name, out float value) ? value : 0f;
            }
            
            public void ResetTriggers()
            {
                triggeredAnimations.Clear();
            }
        }
    }
} 