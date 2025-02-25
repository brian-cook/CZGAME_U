using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CZ.Core.Player;
using CZ.Core.Enemy;
using CZ.Core.Interfaces;
using CZ.Tests.Utilities;

namespace CZ.Tests.PlayMode.Core.Player
{
    public class PlayerHealthPlayTests : PlayModeTestBase
    {
        private GameObject playerObject;
        private PlayerController playerController;
        private PlayerHealth playerHealth;
        private SpriteRenderer playerSpriteRenderer;
        
        private GameObject enemyObject;
        private EnemyDamageDealer enemyDamageDealer;
        
        [UnitySetUp]
        public IEnumerator Setup()
        {
            // Create test scene
            yield return CreateTestScene();
            
            // Create player object with required components
            playerObject = new GameObject("TestPlayer");
            playerObject.transform.position = Vector3.zero;
            
            // Add required components
            playerSpriteRenderer = playerObject.AddComponent<SpriteRenderer>();
            playerSpriteRenderer.sprite = CreateTestSprite(Color.blue);
            
            var rb = playerObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            
            var collider = playerObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.5f;
            
            playerController = playerObject.AddComponent<PlayerController>();
            playerHealth = playerObject.AddComponent<PlayerHealth>();
            
            // Create enemy object with damage dealer
            enemyObject = new GameObject("TestEnemy");
            enemyObject.transform.position = new Vector3(5f, 0f, 0f); // Start away from player
            
            var enemySpriteRenderer = enemyObject.AddComponent<SpriteRenderer>();
            enemySpriteRenderer.sprite = CreateTestSprite(Color.red);
            
            var enemyCollider = enemyObject.AddComponent<CircleCollider2D>();
            enemyCollider.radius = 0.5f;
            enemyCollider.isTrigger = true;
            
            enemyDamageDealer = enemyObject.AddComponent<EnemyDamageDealer>();
            
            // Wait for initialization
            yield return null;
        }
        
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Clean up
            Object.Destroy(playerObject);
            Object.Destroy(enemyObject);
            
            yield return null;
        }
        
        [UnityTest]
        public IEnumerator PlayerHealth_TakesDamage_WhenCollidingWithEnemy()
        {
            // Setup - ensure player has full health
            int initialHealth = playerHealth.CurrentHealth;
            Assert.Greater(initialHealth, 0, "Player should start with health > 0");
            
            // Move enemy to player position to trigger collision
            enemyObject.transform.position = playerObject.transform.position;
            
            // Wait for collision to be processed
            yield return new WaitForSeconds(0.2f);
            
            // Assert damage was taken
            Assert.Less(playerHealth.CurrentHealth, initialHealth, "Player health should decrease after enemy collision");
        }
        
        [UnityTest]
        public IEnumerator PlayerHealth_IsInvulnerable_AfterTakingDamage()
        {
            // Setup - ensure player has full health
            int initialHealth = playerHealth.CurrentHealth;
            
            // Move enemy to player position to trigger collision
            enemyObject.transform.position = playerObject.transform.position;
            
            // Wait for collision to be processed
            yield return new WaitForSeconds(0.2f);
            
            // Record health after first hit
            int healthAfterFirstHit = playerHealth.CurrentHealth;
            Assert.Less(healthAfterFirstHit, initialHealth, "Player should take damage from first hit");
            
            // Wait a short time (less than invulnerability duration)
            yield return new WaitForSeconds(0.2f);
            
            // Move enemy away and back to trigger another collision
            enemyObject.transform.position = new Vector3(5f, 0f, 0f);
            yield return new WaitForSeconds(0.1f);
            enemyObject.transform.position = playerObject.transform.position;
            
            // Wait for potential collision
            yield return new WaitForSeconds(0.2f);
            
            // Health should not have changed due to invulnerability
            Assert.AreEqual(healthAfterFirstHit, playerHealth.CurrentHealth, "Player should be invulnerable after taking damage");
        }
        
        [UnityTest]
        public IEnumerator PlayerHealth_VisualsChange_WhenTakingDamage()
        {
            // Store original color
            Color originalColor = playerSpriteRenderer.color;
            
            // Move enemy to player position to trigger collision
            enemyObject.transform.position = playerObject.transform.position;
            
            // Wait for collision to be processed
            yield return new WaitForSeconds(0.1f);
            
            // Color should be different during damage flash
            Assert.AreNotEqual(originalColor, playerSpriteRenderer.color, "Player sprite should change color when taking damage");
            
            // Wait for damage flash to end but still in invulnerability period
            yield return new WaitForSeconds(0.3f);
            
            // Color should be flashing during invulnerability
            // We can't test exact color as it's changing each frame, but alpha should be different from 1
            Assert.AreNotEqual(1f, playerSpriteRenderer.color.a, "Player sprite should be flashing during invulnerability");
        }
        
        [UnityTest]
        public IEnumerator PlayerHealth_Death_TriggersDeathSequence()
        {
            // Setup - set health to a low value that will be killed by one hit
            var healthField = typeof(PlayerHealth).GetField("currentHealth", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            healthField.SetValue(playerHealth, 5);
            
            // Configure enemy to deal enough damage to kill
            var damageField = typeof(EnemyDamageDealer).GetField("damageAmount", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            damageField.SetValue(enemyDamageDealer, 10);
            
            bool deathEventTriggered = false;
            playerHealth.OnDeath += () => deathEventTriggered = true;
            
            // Move enemy to player position to trigger collision
            enemyObject.transform.position = playerObject.transform.position;
            
            // Wait for collision to be processed
            yield return new WaitForSeconds(0.2f);
            
            // Assert player is dead
            Assert.IsTrue(playerHealth.IsDead, "Player should be dead after taking fatal damage");
            Assert.IsTrue(deathEventTriggered, "Death event should be triggered");
            
            // Wait for death sequence to progress
            yield return new WaitForSeconds(0.5f);
            
            // Sprite should be fading out
            Assert.Less(playerSpriteRenderer.color.a, 1f, "Player sprite should fade out during death sequence");
        }
        
        private Sprite CreateTestSprite(Color color)
        {
            // Create a simple test sprite
            Texture2D texture = new Texture2D(32, 32);
            Color[] colors = new Color[32 * 32];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = color;
            }
            texture.SetPixels(colors);
            texture.Apply();
            
            return Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
        }
    }
} 