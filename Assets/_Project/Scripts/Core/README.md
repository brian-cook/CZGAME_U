# CZ Game Core Assembly Structure

This document outlines the assembly structure and dependencies for the CZ Game project.

## Assembly Hierarchy

The project uses a layered assembly structure to prevent circular dependencies:

```
CZ.Core.Interfaces
    ↑
    ├── CZ.Core.Pooling
    ├── CZ.Core.Configuration
    ├── CZ.Core.Logging
    ├── CZ.Core.Extensions
    |
    ├── CZ.Core.Player
    |   ↑
    |   └── CZ.Core.Enemy (via interfaces only)
    |
    ├── CZ.Core.Debug
    |
    └── CZ.Core (Main)
```

## Key Design Principles

1. **Interface-Based Design**: Core components expose interfaces in the `CZ.Core.Interfaces` assembly, which allows other assemblies to reference functionality without creating circular dependencies.

2. **Dependency Direction**: Dependencies should flow upward in the hierarchy. Lower-level assemblies should not reference higher-level ones.

3. **Shared Interfaces**: When two assemblies need to interact (e.g., Player and Enemy), they should do so through interfaces defined in `CZ.Core.Interfaces`.

4. **Interface Property Sharing**: When a class implements multiple interfaces with similar properties (e.g., both requiring a `Transform`), implement the property once and document that it satisfies multiple interfaces to avoid duplication.

5. **Proper C# Using Directives**: 
   - Use `using namespace;` for importing namespaces
   - Use `using static Class;` for importing static classes (e.g., `using static UnityEngine.Physics2D;`)
   - Use `using Alias = Namespace.Class;` for type aliases

## Recent Changes

### CollisionDebugger Refactoring

The `CollisionDebugger` component has been refactored to implement the `ICollisionDebugger` interface, which is defined in the `CZ.Core.Interfaces` assembly. This allows the `GameManager` to reference the debugger through its interface rather than its concrete implementation, breaking a circular dependency between `CZ.Core` and `CZ.Core.Debug`.

### Player-Enemy Interaction Interfaces

To prevent circular dependencies between the Player and Enemy assemblies, we've implemented the following interfaces:

- `IProjectileIdentifier`: Implemented by the `Projectile` class to allow enemy systems to detect and react to projectiles without directly referencing the Player namespace.
- `IPlayerIdentifier`: Implemented by the `PlayerController` class to allow enemy systems to detect the player without directly referencing the Player namespace.

This approach allows the Enemy systems to interact with Player components through interfaces rather than direct references, maintaining a clean and scalable architecture.

### Interface Property Duplication Fix

The `PlayerController` class implements both `IPlayerReference` and `IPlayerIdentifier` interfaces, which both require a `PlayerTransform` property. To avoid duplication errors, a single property implementation satisfies both interfaces and is documented accordingly.

### Static Class Import Fix

The `CollisionDebugger` class has been updated to use the proper `using static` directive for the Unity `Physics2D` class, following C# language rules for importing static types. This change allows direct access to the static members without qualification while avoiding compilation errors.

### Debug Namespace Logging Fix

Fixed namespace conflicts in the `CZ.Core.Debug` namespace by replacing direct `Debug.Log` calls with `CZLogger` methods. This resolves compilation errors caused by the Unity `Debug` class being confused with the `CZ.Core.Debug` namespace. All debug logging in this namespace now properly uses the project's logging system with `LogCategory.Debug`.

### Collision System Improvements

Enhanced the `CollisionDebugger` component to fix critical issues with collision detection between projectiles, enemies, and the player:

1. **Layer Collision Matrix Management** - Fixed issues with Physics2D layer collision settings that were preventing projectiles from damaging enemies and enemies from damaging the player
2. **Oversized Collider Detection** - Added automatic detection and resizing of enemy colliders that are significantly larger than their sprites, preventing premature player damage
3. **Emergency Collision Fix** - Added a new public method `FixCriticalCollisionIssues()` that can be called by the `GameManager` or other systems when collision problems are detected during gameplay
4. **Direct Debug Output** - Added direct `UnityEngine.Debug` logging alongside the project's `CZLogger` system for critical collision issues to ensure visibility in the console
5. **Variable Naming Fixes** - Resolved variable naming conflicts and references to ensure proper collider processing and automatic fixes
6. **Explicit Debug Alias** - Added an explicit alias for the Unity Debug class (`using Debug = UnityEngine.Debug;`) to avoid any ambiguity with the `CZ.Core.Debug` namespace

These improvements ensure proper collision detection between game objects and resolve the issues with projectiles not colliding with enemies and enemies having excessively large hitboxes that cause premature player damage.

### Assembly Reference Resolver

A new editor utility has been added at `Assets/Editor/ReferenceResolver.cs` to help detect and fix circular dependencies in the project. This tool can be accessed from the Unity menu under "Tools > CZ Game > Fix Assembly References".

## Best Practices

1. When adding new functionality that might be used across multiple assemblies, consider defining an interface for it in `CZ.Core.Interfaces`.

2. Use the Reference Resolver tool to check for circular dependencies before committing changes.

3. Keep assembly references minimal - only reference what you actually need.

4. When in doubt about where to place new code, consider its dependencies and which other components need to access it.

5. For cross-assembly interactions (like Enemy detecting Player objects), always use interfaces defined in a shared assembly rather than direct references.

6. When implementing multiple interfaces with similar properties, use a single implementation with appropriate documentation rather than duplicating properties.

7. Use the correct C# using directive syntax:
   - For static classes like `Physics2D`, use `using static UnityEngine.Physics2D;`
   - For namespaces, use the regular `using UnityEngine;` syntax 

8. When working in a namespace that conflicts with commonly used classes (e.g., `CZ.Core.Debug` vs. Unity's `Debug` class), use either:
   - Fully qualified names: `UnityEngine.Debug.Log()`
   - Proper aliases: `using Debug = UnityEngine.Debug;`
   - Or preferably, use the project's logging system: `CZLogger.LogInfo("message", LogCategory.Debug)` 

### Variable Naming Conventions

When iterating through collections of components, use descriptive variable names that clearly indicate what the loop variable represents. Avoid generic names that could conflict with other variables in the same scope:

```csharp
// Good practice
var colliders = gameObject.GetComponents<Collider2D>();
foreach (var currentCollider in colliders)
{
    // Use currentCollider within the loop
}

// Avoid this pattern
var colliders = gameObject.GetComponents<Collider2D>();
foreach (var collider in colliders) // May conflict with a 'collider' variable elsewhere
{
    // Using the generic 'collider' name may cause conflicts
}
```

This naming convention helps prevent the CS0136 error: "A local or parameter named '{name}' cannot be declared in this scope because that name is used in an enclosing local scope to define a local or parameter." 