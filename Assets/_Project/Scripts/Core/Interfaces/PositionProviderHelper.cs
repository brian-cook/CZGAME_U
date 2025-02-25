using UnityEngine;

namespace CZ.Core.Interfaces
{
    /// <summary>
    /// Helper class to find IPositionProvider instances without direct references to specific implementations
    /// </summary>
    public static class PositionProviderHelper
    {
        /// <summary>
        /// Finds the first IPositionProvider in the scene
        /// </summary>
        /// <returns>The first IPositionProvider found, or null if none exists</returns>
        public static IPositionProvider FindPositionProvider()
        {
            // Find all MonoBehaviours in the scene
            MonoBehaviour[] allMonoBehaviours = Object.FindObjectsOfType<MonoBehaviour>();
            
            // Check each one to see if it implements IPositionProvider
            foreach (MonoBehaviour mb in allMonoBehaviours)
            {
                if (mb is IPositionProvider provider)
                {
                    return provider;
                }
            }
            
            return null;
        }
    }
} 