using UnityEngine;

namespace CZ.Core.Interfaces
{
    /// <summary>
    /// Interface for components that provide collision debugging functionality
    /// </summary>
    public interface ICollisionDebugger
    {
        /// <summary>
        /// Force a verification of physics settings and attempt to fix any issues
        /// </summary>
        void ForceVerification();
        
        /// <summary>
        /// Check and repair potential issues with a projectile
        /// </summary>
        /// <param name="projectile">The projectile GameObject to check</param>
        void CheckProjectileSetup(GameObject projectile);
        
        /// <summary>
        /// Check and repair potential issues with an enemy
        /// </summary>
        /// <param name="enemy">The enemy GameObject to check</param>
        void CheckEnemySetup(GameObject enemy);
        
        /// <summary>
        /// Immediately fixes critical collision issues, particularly focusing on projectile-enemy and player-enemy interactions.
        /// This method can be called from other systems (like GameManager) when collision issues are detected during gameplay.
        /// </summary>
        /// <returns>True if all issues were fixed, false if some issues remain</returns>
        bool FixCriticalCollisionIssues();
    }
} 