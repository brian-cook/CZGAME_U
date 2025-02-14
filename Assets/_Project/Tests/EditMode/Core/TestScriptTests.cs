using NUnit.Framework;
using CZ.Core;
using UnityEngine;

namespace CZ.Tests.EditMode
{
    public class TestScriptTests
    {
        [Test]
        public void TestScript_WhenInitialized_LogsMessage()
        {
            // Arrange
            var gameObject = new GameObject();
            var testScript = gameObject.AddComponent<TestScript>();
            var loggedMessage = false;
            
            Application.logMessageReceived += (message, stackTrace, type) => {
                if (message == "Test script initialized")
                    loggedMessage = true;
            };

            // Act
            testScript.Start();

            // Assert
            Assert.IsTrue(loggedMessage, "TestScript should log initialization message");
        }
    }
} 