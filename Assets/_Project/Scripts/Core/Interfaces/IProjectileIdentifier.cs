using UnityEngine;

namespace CZ.Core.Interfaces
{
    /// <summary>
    /// Interface to identify game objects as projectiles
    /// This allows enemy systems to detect projectiles without directly referencing the Player namespace
    /// </summary>
    public interface IProjectileIdentifier
    {
        /// <summary>
        /// Gets the damage value of this projectile
        /// </summary>
        int Damage { get; }
        
        /// <summary>
        /// Gets the GameObject that fired this projectile
        /// </summary>
        GameObject Owner { get; }
        
        /// <summary>
        /// Gets the current velocity of the projectile
        /// </summary>
        Vector2 Velocity { get; }
    }
} 