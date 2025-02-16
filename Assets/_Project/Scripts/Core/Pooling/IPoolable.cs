using UnityEngine;

namespace CZ.Core.Pooling
{
    /// <summary>
    /// Interface for objects that can be pooled.
    /// Implements Unity 6.0 best practices for object pooling.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// Called when the object is retrieved from the pool
        /// </summary>
        void OnSpawn();

        /// <summary>
        /// Called when the object is returned to the pool
        /// </summary>
        void OnDespawn();

        /// <summary>
        /// Gets the GameObject associated with this poolable object
        /// </summary>
        GameObject GameObject { get; }
    }
} 