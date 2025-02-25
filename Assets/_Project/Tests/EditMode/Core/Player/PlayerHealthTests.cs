using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CZ.Core.Player;
using CZ.Core.Interfaces;

namespace CZ.Tests.EditMode.Core.Player
{
    public class PlayerHealthTests
    {
        private GameObject playerObject;
        private PlayerController playerController;
        private PlayerHealth playerHealth;
        private SpriteRenderer spriteRenderer;

        [SetUp]
        public void Setup()
        {
            // Create player object with required components
            playerObject = new GameObject("TestPlayer");
            
            // Add required components
            playerController = playerObject.AddComponent<PlayerController>();
            playerHealth = playerObject.AddComponent<PlayerHealth>();
            spriteRenderer = playerObject.AddComponent<SpriteRenderer>();
            
            // Add required physics components
            playerObject.AddComponent<Rigidbody2D>();
            playerObject.AddComponent<CircleCollider2D>();
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up
            Object.DestroyImmediate(playerObject);
        }

        [Test]
        public void PlayerHealth_Initialization_SetsCorrectValues()
        {
            // Access private fields using reflection
            var maxHealthField = typeof(PlayerHealth).GetField("maxHealth", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var initialHealthField = typeof(PlayerHealth).GetField("initialHealth", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            // Set test values
            maxHealthField.SetValue(playerHealth, 100);
            initialHealthField.SetValue(playerHealth, 80);
            
            // Call initialization methods
            playerHealth.SendMessage("Awake");
            playerHealth.SendMessage("Start");
            
            // Verify initial values
            Assert.AreEqual(80, playerHealth.CurrentHealth);
            Assert.AreEqual(100, playerHealth.MaxHealth);
            Assert.AreEqual(0.8f, playerHealth.HealthPercentage);
            Assert.IsFalse(playerHealth.IsDead);
        }

        [Test]
        public void TakeDamage_ReducesHealth()
        {
            // Setup
            var maxHealthField = typeof(PlayerHealth).GetField("maxHealth", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var initialHealthField = typeof(PlayerHealth).GetField("initialHealth", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            maxHealthField.SetValue(playerHealth, 100);
            initialHealthField.SetValue(playerHealth, 100);
            
            playerHealth.SendMessage("Awake");
            playerHealth.SendMessage("Start");
            
            // Act
            playerHealth.TakeDamage(20);
            
            // Assert
            Assert.AreEqual(80, playerHealth.CurrentHealth);
            Assert.AreEqual(0.8f, playerHealth.HealthPercentage);
        }

        [Test]
        public void TakeDamage_WithDamageType_AppliesModifiers()
        {
            // Setup
            var maxHealthField = typeof(PlayerHealth).GetField("maxHealth", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var initialHealthField = typeof(PlayerHealth).GetField("initialHealth", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            maxHealthField.SetValue(playerHealth, 100);
            initialHealthField.SetValue(playerHealth, 100);
            
            playerHealth.SendMessage("Awake");
            playerHealth.SendMessage("Start");
            
            // Act - Critical damage should do 1.5x damage
            playerHealth.TakeDamage(20, DamageType.Critical);
            
            // Assert - 20 * 1.5 = 30 damage, so 100 - 30 = 70 health
            Assert.AreEqual(70, playerHealth.CurrentHealth);
        }

        [Test]
        public void TakeDamage_WhenHealthReachesZero_PlayerDies()
        {
            // Setup
            var maxHealthField = typeof(PlayerHealth).GetField("maxHealth", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var initialHealthField = typeof(PlayerHealth).GetField("initialHealth", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            maxHealthField.SetValue(playerHealth, 100);
            initialHealthField.SetValue(playerHealth, 100);
            
            playerHealth.SendMessage("Awake");
            playerHealth.SendMessage("Start");
            
            bool deathEventTriggered = false;
            playerHealth.OnDeath += () => deathEventTriggered = true;
            
            // Act
            playerHealth.TakeDamage(100);
            
            // Assert
            Assert.AreEqual(0, playerHealth.CurrentHealth);
            Assert.AreEqual(0f, playerHealth.HealthPercentage);
            Assert.IsTrue(playerHealth.IsDead);
            Assert.IsTrue(deathEventTriggered);
        }

        [Test]
        public void Heal_IncreasesHealth()
        {
            // Setup
            var maxHealthField = typeof(PlayerHealth).GetField("maxHealth", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var initialHealthField = typeof(PlayerHealth).GetField("initialHealth", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            maxHealthField.SetValue(playerHealth, 100);
            initialHealthField.SetValue(playerHealth, 50);
            
            playerHealth.SendMessage("Awake");
            playerHealth.SendMessage("Start");
            
            // Act
            playerHealth.Heal(20);
            
            // Assert
            Assert.AreEqual(70, playerHealth.CurrentHealth);
        }

        [Test]
        public void Heal_DoesNotExceedMaxHealth()
        {
            // Setup
            var maxHealthField = typeof(PlayerHealth).GetField("maxHealth", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var initialHealthField = typeof(PlayerHealth).GetField("initialHealth", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            maxHealthField.SetValue(playerHealth, 100);
            initialHealthField.SetValue(playerHealth, 90);
            
            playerHealth.SendMessage("Awake");
            playerHealth.SendMessage("Start");
            
            // Act
            playerHealth.Heal(20);
            
            // Assert
            Assert.AreEqual(100, playerHealth.CurrentHealth);
        }

        [Test]
        public void RestoreFullHealth_SetsHealthToMax()
        {
            // Setup
            var maxHealthField = typeof(PlayerHealth).GetField("maxHealth", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var initialHealthField = typeof(PlayerHealth).GetField("initialHealth", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            maxHealthField.SetValue(playerHealth, 100);
            initialHealthField.SetValue(playerHealth, 50);
            
            playerHealth.SendMessage("Awake");
            playerHealth.SendMessage("Start");
            
            // Act
            playerHealth.RestoreFullHealth();
            
            // Assert
            Assert.AreEqual(100, playerHealth.CurrentHealth);
        }

        [Test]
        public void HealthEvents_AreTriggeredCorrectly()
        {
            // Setup
            var maxHealthField = typeof(PlayerHealth).GetField("maxHealth", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var initialHealthField = typeof(PlayerHealth).GetField("initialHealth", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            maxHealthField.SetValue(playerHealth, 100);
            initialHealthField.SetValue(playerHealth, 100);
            
            playerHealth.SendMessage("Awake");
            playerHealth.SendMessage("Start");
            
            int damagedEventCount = 0;
            int healthChangedEventCount = 0;
            int lastDamageAmount = 0;
            int lastCurrentHealth = 0;
            int lastMaxHealth = 0;
            
            playerHealth.OnDamaged += (damage, currentHealth) => {
                damagedEventCount++;
                lastDamageAmount = damage;
                lastCurrentHealth = currentHealth;
            };
            
            playerHealth.OnHealthChanged += (currentHealth, maxHealth) => {
                healthChangedEventCount++;
                lastCurrentHealth = currentHealth;
                lastMaxHealth = maxHealth;
            };
            
            // Act
            playerHealth.TakeDamage(30);
            
            // Assert
            Assert.AreEqual(1, damagedEventCount);
            Assert.AreEqual(1, healthChangedEventCount);
            Assert.AreEqual(30, lastDamageAmount);
            Assert.AreEqual(70, lastCurrentHealth);
            Assert.AreEqual(100, lastMaxHealth);
        }
    }
} 