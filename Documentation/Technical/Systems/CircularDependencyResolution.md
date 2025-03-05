# Circular Dependency Resolution

## Overview

Circular dependencies occur when two or more assemblies directly or indirectly depend on each other. In Unity, this often happens between assembly definition files (`.asmdef`) when classes in one assembly reference classes in another, and vice versa.

This document outlines strategies for identifying and resolving circular dependencies in Unity projects.

## Identifying Circular Dependencies

Circular dependencies typically manifest as compiler errors like:

```
error CS0234: The type or namespace name 'X' does not exist in the namespace 'Y' (are you missing an assembly reference?)
```

These errors occur because Unity's assembly compilation system cannot resolve the circular reference chain.

## Common Causes

1. **Direct References Between Assemblies**
   - Assembly A references types in Assembly B
   - Assembly B references types in Assembly A

2. **Transitive Dependencies**
   - Assembly A references Assembly B
   - Assembly B references Assembly C
   - Assembly C references Assembly A

3. **Shared Functionality**
   - Core systems that need to interact with multiple subsystems
   - Subsystems that need to reference core functionality

## Resolution Strategies

### 1. Interface Extraction

The most architecturally sound approach is to extract interfaces into a separate assembly:

1. Create a new assembly (e.g., `CZ.Core.Interfaces`)
2. Define interfaces for the functionality needed across assemblies
3. Implement these interfaces in their respective assemblies
4. Reference only the interfaces assembly from other assemblies

Example:
```csharp
// In CZ.Core.Interfaces
public interface IPlayer {
    void TakeDamage(int amount);
}

// In CZ.Core.Player
public class PlayerController : MonoBehaviour, IPlayer {
    public void TakeDamage(int amount) { ... }
}

// In CZ.Core.Enemy
// Reference CZ.Core.Interfaces, not CZ.Core.Player
public void OnCollision(Collision2D collision) {
    var player = collision.gameObject.GetComponent<IPlayer>();
    if (player != null) {
        player.TakeDamage(10);
    }
}
```

### 2. Event-Based Communication

Use events or a message bus to decouple assemblies:

1. Create a shared events assembly
2. Define events that can be raised by any assembly
3. Allow assemblies to subscribe to events without direct references

Example:
```csharp
// In CZ.Core.Events
public static class GameEvents {
    public static event Action<int, GameObject> PlayerDamaged;
    public static void RaisePlayerDamaged(int amount, GameObject source) {
        PlayerDamaged?.Invoke(amount, source);
    }
}

// In CZ.Core.Enemy
// No direct reference to Player needed
GameEvents.RaisePlayerDamaged(10, gameObject);

// In CZ.Core.Player
void OnEnable() {
    GameEvents.PlayerDamaged += HandleDamage;
}
```

### 3. Reflection-Based Access (Last Resort)

When architectural changes are not immediately feasible, reflection can be used as a temporary solution:

```csharp
// Instead of direct reference:
// var player = collision.gameObject.GetComponent<CZ.Core.Player.PlayerController>();

// Use reflection:
var playerType = System.Type.GetType("CZ.Core.Player.PlayerController, CZ.Core.Player");
var player = collision.gameObject.GetComponent(playerType);
if (player != null) {
    playerType.GetMethod("TakeDamage").Invoke(player, new object[] { 10 });
}
```

**Important:** Always add proper error handling when using reflection:

```csharp
try {
    var playerType = System.Type.GetType("CZ.Core.Player.PlayerController, CZ.Core.Player");
    if (playerType == null) {
        Debug.LogWarning("PlayerController type not found");
        return;
    }
    
    var player = collision.gameObject.GetComponent(playerType);
    if (player != null) {
        var takeDamageMethod = playerType.GetMethod("TakeDamage", new[] { typeof(int) });
        if (takeDamageMethod != null) {
            takeDamageMethod.Invoke(player, new object[] { 10 });
        }
    }
} catch (System.Exception ex) {
    Debug.LogError($"Error accessing PlayerController: {ex.Message}");
}
```

## Best Practices

1. **Plan Assembly Structure Early**
   - Design with dependency direction in mind
   - Create interface assemblies for shared contracts
   - Use dependency injection where possible

2. **Minimize Cross-Assembly References**
   - Keep related functionality in the same assembly
   - Only expose what is absolutely necessary

3. **Use Dependency Visualization**
   - Tools like Unity's Assembly Definition References window
   - Visual Studio's Architecture Explorer
   - Custom dependency graphs

4. **Document Assembly Dependencies**
   - Maintain a dependency map
   - Document intentional dependency directions
   - Note where reflection is used as a temporary solution

## Recent Fixes in This Project

### Physics2DSetup.cs - CZ.Core.Enemy Dependency

The `Physics2DSetup` class in `CZ.Core.Configuration` needed to access `SwiftEnemyController` from `CZ.Core.Enemy`, but `CZ.Core.Enemy` already depended on `CZ.Core.Configuration`.

**Solution:**
- Removed direct reference to `CZ.Core.Enemy` namespace
- Used reflection to access `SwiftEnemyController` type
- Added proper error handling

### SwiftEnemyController.cs - CZ.Core.Player Dependency

The `SwiftEnemyController` class in `CZ.Core.Enemy` needed to access `Projectile` and `PlayerController` from `CZ.Core.Player`, but this created a circular dependency.

**Solution:**
- Removed direct references to `CZ.Core.Player` namespace
- Used reflection to access `Projectile` and `PlayerController` types
- Added error handling for reflection calls

## Future Improvements

1. **Extract Interfaces**
   - Create `IProjectile` and `IPlayerController` interfaces
   - Move to a shared interfaces assembly
   - Update code to use interfaces instead of concrete types

2. **Implement Event System**
   - Create a centralized event system for cross-assembly communication
   - Replace direct method calls with events
   - Decouple subsystems through event-based messaging

3. **Dependency Injection**
   - Implement a dependency injection framework
   - Register services at startup
   - Resolve dependencies at runtime 