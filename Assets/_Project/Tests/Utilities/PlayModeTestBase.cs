using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CZ.Tests.Utilities
{
    /// <summary>
    /// Base class for play mode tests that provides common functionality.
    /// </summary>
    public abstract class PlayModeTestBase
    {
        protected Scene testScene;
        
        /// <summary>
        /// Creates an empty test scene for isolated testing.
        /// </summary>
        /// <returns>IEnumerator for test coroutine</returns>
        protected IEnumerator CreateTestScene()
        {
            // Create a new empty scene for testing
            testScene = SceneManager.CreateScene("TestScene_" + System.Guid.NewGuid().ToString().Substring(0, 8));
            SceneManager.SetActiveScene(testScene);
            
            // Wait a frame for scene to initialize
            yield return null;
            
            // Create a camera for the test scene
            var cameraObject = new GameObject("TestCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            camera.orthographicSize = 5;
            camera.transform.position = new Vector3(0, 0, -10);
            
            // Add a light
            var lightObject = new GameObject("TestLight");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            
            yield return null;
        }
        
        /// <summary>
        /// Unloads the test scene.
        /// </summary>
        /// <returns>IEnumerator for test coroutine</returns>
        protected IEnumerator UnloadTestScene()
        {
            if (testScene.IsValid())
            {
                yield return SceneManager.UnloadSceneAsync(testScene);
            }
            
            yield return null;
        }
        
        /// <summary>
        /// Waits for a condition to be true with a timeout.
        /// </summary>
        /// <param name="condition">The condition to check</param>
        /// <param name="timeout">Maximum time to wait in seconds</param>
        /// <returns>IEnumerator for test coroutine</returns>
        protected IEnumerator WaitForCondition(System.Func<bool> condition, float timeout = 5f)
        {
            float startTime = Time.time;
            while (!condition() && Time.time - startTime < timeout)
            {
                yield return null;
            }
        }
        
        /// <summary>
        /// Logs a test message with a timestamp.
        /// </summary>
        /// <param name="message">The message to log</param>
        protected void LogTest(string message)
        {
            Debug.Log($"[TEST {Time.time:F2}s] {message}");
        }
    }
} 