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
            // Find all MonoBehaviours in the scene using the non-deprecated method
            // Using FindObjectsSortMode.None for better performance since we don't need sorted results
            MonoBehaviour[] allMonoBehaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            
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