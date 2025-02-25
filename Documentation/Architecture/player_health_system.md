# Player Health System Documentation

## Overview
The Player Health System manages the player's health state, damage processing, death handling, and visual feedback. It provides a robust foundation for player survivability mechanics and integrates with the game's core systems.

## Core Components

### 1. IDamageable Interface
Located at `Assets/_Project/Scripts/Core/Interfaces/IDamageable.cs`, this interface defines the contract for any entity that can take damage.

```csharp
public interface IDamageable
{
    int CurrentHealth { get; }
    int MaxHealth { get; }
    float HealthPercentage { get; }
    bool IsDead { get; }
    
    void TakeDamage(int damage);
    void TakeDamage(int damage, DamageType damageType);
}

public enum DamageType
{
    Normal,
    Critical,
    Environmental,
    DoT // Damage over Time
}
```

### 2. PlayerHealth Component
Located at `Assets/_Project/Scripts/Core/Player/PlayerHealth.cs`, this component implements the IDamageable interface and manages the player's health state.

#### Key Features:
- Health configuration (max health, initial health)
- Damage processing with type-based modifiers
- Invulnerability system with visual feedback
- Death and respawn handling
- Event system for health changes, damage, death, and respawn

#### Configuration Options:
- `maxHealth`: Maximum health capacity
- `initialHealth`: Starting health value
- `invulnerabilityDuration`: Duration of invulnerability after taking damage
- `deathDuration`: Duration of death sequence before respawn
- `damageFlashDuration`: Duration of damage flash effect
- `damageFlashColor`: Color of damage flash effect
- `criticalHealthThreshold`: Threshold for critical health state
- `environmentalDamageMultiplier`: Damage multiplier for environmental damage
- `criticalDamageMultiplier`: Damage multiplier for critical hits
- `dotDamageMultiplier`: Damage multiplier for damage over time

### 3. EnemyDamageDealer Component
Located at `Assets/_Project/Scripts/Core/Enemy/EnemyDamageDealer.cs`, this component allows enemies to deal damage to the player.

#### Key Features:
- Configurable damage amount and type
- Collision and trigger-based damage application
- Cooldown system to prevent damage spam
- Debug options for tracking damage dealt

## Integration with PlayerController

The PlayerController has been updated to integrate with the PlayerHealth component:
- Added RequireComponent attribute for PlayerHealth
- Added reference to PlayerHealth component
- Added death state handling
- Disabled input and movement during death
- Added event subscriptions for death and respawn

## Testing

### Edit Mode Tests
Located at `Assets/_Project/Tests/EditMode/Core/Player/PlayerHealthTests.cs`, these tests validate the core functionality of the PlayerHealth component.

#### Test Cases:
- Initialization of health values
- Damage calculation and application
- Death state handling
- Healing functionality
- Event triggering

### Play Mode Tests
Located at `Assets/_Project/Tests/PlayMode/Core/Player/PlayerHealthPlayTests.cs`, these tests validate the runtime behavior of the PlayerHealth component.

#### Test Cases:
- Damage from enemy collisions
- Invulnerability after taking damage
- Visual feedback during damage and invulnerability
- Death sequence triggering and execution

## Visual Feedback

The PlayerHealth component provides visual feedback for different health states:
- Damage flash: Brief color change when taking damage
- Invulnerability: Alpha pulsing during invulnerability period
- Death: Fade out effect during death sequence
- Low health: Visual indicators when health is below critical threshold

## Performance Considerations

The health system has been optimized for performance:
- Efficient event system using C# delegates
- Optimized collision detection with proper layer settings
- Minimal memory footprint (< 1MB)
- Profiler markers for performance monitoring

## Future Enhancements (Phase 2 & 3)

### Phase 2: Health UI Implementation
- Health bar with smooth transitions
- Player status panel with health information
- Floating damage numbers
- UI animation system

### Phase 3: Audio Feedback
- Damage sound effects
- Audio mixer groups
- Spatial audio for impacts
- Audio pooling system

## Usage Examples

### Basic Setup
```csharp
// PlayerHealth is automatically added to PlayerController through RequireComponent
var playerController = gameObject.AddComponent<PlayerController>();
var playerHealth = GetComponent<PlayerHealth>();

// Configure health settings
playerHealth.MaxHealth = 100;
playerHealth.RestoreFullHealth();
```

### Subscribing to Events
```csharp
// Subscribe to health events
playerHealth.OnHealthChanged += HandleHealthChanged;
playerHealth.OnDamaged += HandleDamaged;
playerHealth.OnDeath += HandleDeath;
playerHealth.OnRespawn += HandleRespawn;
```

### Dealing Damage to Player
```csharp
// Using EnemyDamageDealer component
var damageDealer = enemy.AddComponent<EnemyDamageDealer>();
damageDealer.damageAmount = 10;
damageDealer.damageType = DamageType.Normal;

// Or directly through IDamageable interface
playerHealth.TakeDamage(10, DamageType.Critical);
```

## Conclusion

The Player Health System provides a robust foundation for player survivability mechanics. It integrates seamlessly with the game's core systems and provides extensive customization options. The system is designed to be performant, maintainable, and extensible for future enhancements. 