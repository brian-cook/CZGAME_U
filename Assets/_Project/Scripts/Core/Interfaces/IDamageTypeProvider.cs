using UnityEngine;

namespace CZ.Core.Interfaces
{
    /// <summary>
    /// Interface for components that provide information about damage types
    /// </summary>
    public interface IDamageTypeProvider
    {
        /// <summary>
        /// Gets the last damage type that was applied to the entity
        /// </summary>
        DamageType? LastDamageType { get; }
    }
} 