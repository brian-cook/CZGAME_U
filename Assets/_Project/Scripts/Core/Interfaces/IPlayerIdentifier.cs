using UnityEngine;

namespace CZ.Core.Interfaces
{
    /// <summary>
    /// Interface to identify game objects as player-controlled entities
    /// This allows enemy systems to detect players without directly referencing the Player namespace
    /// </summary>
    public interface IPlayerIdentifier
    {
        /// <summary>
        /// Gets the player's transform
        /// </summary>
        Transform PlayerTransform { get; }
        
        /// <summary>
        /// Gets the player's current position
        /// </summary>
        Vector3 Position { get; }
        
        /// <summary>
        /// Gets the player's current velocity
        /// </summary>
        Vector2 Velocity { get; }
    }
} 