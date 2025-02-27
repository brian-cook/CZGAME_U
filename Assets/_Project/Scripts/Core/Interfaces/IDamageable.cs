namespace CZ.Core.Interfaces
{
    /// <summary>
    /// Interface for any entity that can take damage
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// Current health value
        /// </summary>
        int CurrentHealth { get; }
        
        /// <summary>
        /// Maximum health value
        /// </summary>
        int MaxHealth { get; }
        
        /// <summary>
        /// Health percentage (0-1 range)
        /// </summary>
        float HealthPercentage { get; }
        
        /// <summary>
        /// Whether the entity is dead (health <= 0)
        /// </summary>
        bool IsDead { get; }
        
        /// <summary>
        /// Apply damage to the entity
        /// </summary>
        /// <param name="damage">Amount of damage to apply</param>
        void TakeDamage(int damage);
        
        /// <summary>
        /// Apply damage to the entity with damage type
        /// </summary>
        /// <param name="damage">Amount of damage to apply</param>
        /// <param name="damageType">Type of damage being applied</param>
        void TakeDamage(int damage, DamageType damageType);
    }
    
    /// <summary>
    /// Interface for entities that expose health-related events
    /// </summary>
    public interface IHasHealthEvents
    {
        /// <summary>
        /// Event triggered when entity takes damage
        /// </summary>
        event System.Action<int, int> OnDamaged; // damage amount, current health
        
        /// <summary>
        /// Event triggered when entity health changes
        /// </summary>
        event System.Action<int, int> OnHealthChanged; // current health, max health
        
        /// <summary>
        /// Event triggered when entity dies
        /// </summary>
        event System.Action OnDeath;
    }
    
    /// <summary>
    /// Types of damage that can be applied
    /// </summary>
    public enum DamageType
    {
        /// <summary>
        /// Standard damage with no special effects
        /// </summary>
        Normal,
        
        /// <summary>
        /// Critical damage that may bypass defenses or have increased effect
        /// </summary>
        Critical,
        
        /// <summary>
        /// Environmental damage (traps, hazards, etc.)
        /// </summary>
        Environmental,
        
        /// <summary>
        /// Damage over time effects
        /// </summary>
        DoT
    }
} 