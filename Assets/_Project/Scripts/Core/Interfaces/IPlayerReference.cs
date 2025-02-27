using UnityEngine;

namespace CZ.Core.Interfaces
{
    /// <summary>
    /// Interface for accessing player information without direct dependencies on the Player assembly
    /// This prevents circular dependencies between assemblies
    /// </summary>
    public interface IPlayerReference
    {
        /// <summary>
        /// The player's transform component
        /// </summary>
        Transform PlayerTransform { get; }
        
        /// <summary>
        /// The current position of the player
        /// </summary>
        Vector3 PlayerPosition { get; }
        
        /// <summary>
        /// Whether the player is currently alive
        /// </summary>
        bool IsPlayerAlive { get; }
    }
} 