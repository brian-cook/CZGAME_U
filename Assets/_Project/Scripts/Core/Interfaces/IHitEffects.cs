using UnityEngine;

namespace CZ.Core.Interfaces
{
    /// <summary>
    /// Interface for components that handle visual effects when an entity takes damage
    /// </summary>
    public interface IHitEffects
    {
        /// <summary>
        /// Sets the position of the damage source to calculate effect direction
        /// </summary>
        /// <param name="sourcePosition">Position of the damage source</param>
        void SetDamageSourcePosition(Vector2 sourcePosition);
    }
} 