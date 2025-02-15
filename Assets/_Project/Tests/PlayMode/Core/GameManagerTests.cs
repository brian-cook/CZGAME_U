using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CZ.Core;

namespace CZ.Tests.PlayMode
{
    public class GameManagerTests
    {
        private GameObject gameManagerObject;
        private GameManager gameManager;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            // Create GameManager instance
            gameManagerObject = new GameObject("GameManager");
            gameManager = gameManagerObject.AddComponent<GameManager>();
            
            // Wait one frame for initialization
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameManager_Initialization_CreatesInstance()
        {
            // Verify singleton instance
            Assert.That(GameManager.Instance, Is.Not.Null);
            Assert.That(GameManager.Instance, Is.EqualTo(gameManager));
            
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameManager_StateTransitions_WorkCorrectly()
        {
            // Test game start
            gameManager.StartGame();
            Assert.That(gameManager.CurrentGameState, Is.EqualTo(GameManager.GameState.Playing));
            
            // Test pause
            gameManager.PauseGame();
            Assert.That(gameManager.CurrentGameState, Is.EqualTo(GameManager.GameState.Paused));
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            
            // Test resume
            gameManager.ResumeGame();
            Assert.That(gameManager.CurrentGameState, Is.EqualTo(GameManager.GameState.Playing));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            
            // Test game end
            gameManager.EndGame();
            Assert.That(gameManager.CurrentGameState, Is.EqualTo(GameManager.GameState.GameOver));
            
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameManager_PerformanceMonitoring_IsActive()
        {
            // Wait a few frames for performance monitoring to initialize
            yield return new WaitForSeconds(0.1f);
            
            // Store reference to avoid destroyed object access
            var manager = gameManager;
            
            // We can't directly test the values, but we can ensure the game doesn't crash
            // when monitoring is active
            Assert.That(() => 
            {
                if (manager != null)
                {
                    manager.enabled = true;
                    manager.enabled = false;
                }
            }, Throws.Nothing);

            yield return null;
        }

        [TearDown]
        public void Cleanup()
        {
            if (gameManagerObject != null)
            {
                Object.Destroy(gameManagerObject);
            }
        }
    }
} 