using NUnit.Framework;
using CZ.Core;
using UnityEngine;

namespace CZ.Tests.EditMode
{
    public class TestScriptTests
    {
        [Test]
        public void TestScript_WhenInitialized_Exists()
        {
            // Arrange
            var gameObject = new GameObject();
            var testScript = gameObject.AddComponent<TestScript>();

            // Assert
            Assert.IsNotNull(testScript, "TestScript should be added to GameObject");
            Assert.IsTrue(testScript is MonoBehaviour, "TestScript should inherit from MonoBehaviour");
            
            // Cleanup
            Object.DestroyImmediate(gameObject);
        }
    }
} 