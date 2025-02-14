using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CZ.Core;

namespace CZ.Tests.PlayMode
{
    public class TestScriptPlayModeTests
    {
        [UnityTest]
        public IEnumerator TestScript_InPlayMode_LogsMessageOnStart()
        {
            // Arrange
            var gameObject = new GameObject();
            var testScript = gameObject.AddComponent<TestScript>();
            var loggedMessage = false;
            
            Application.logMessageReceived += (message, stackTrace, type) => {
                if (message == "Test script initialized")
                    loggedMessage = true;
            };

            // Act - Wait for start to be called
            yield return null;

            // Assert
            Assert.IsTrue(loggedMessage, "TestScript should log initialization message in play mode");
            
            // Cleanup
            Object.Destroy(gameObject);
        }
    }
} 