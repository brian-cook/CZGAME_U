# Resolving Circular Dependencies

## Overview

Circular dependencies occur when two or more modules depend on each other, either directly or indirectly. In Unity projects using assembly definitions (.asmdef files), circular dependencies lead to compilation errors. This document outlines strategies for identifying and resolving such dependencies in the CZGAME project.

## Recent Issue: Physics2DSetup and SwiftEnemyController

### Problem Description

A circular dependency was identified between:
- `CZ.Core.Configuration` assembly (containing `Physics2DSetup.cs`)
- `CZ.Core.Enemy` assembly (containing `SwiftEnemyController.cs`)

The specific error was:
```
Assets\_Project\Scripts\Core\Configuration\Physics2DSetup.cs(3,15): error CS0234: The type or namespace name 'Enemy' does not exist in the namespace 'CZ.Core' (are you missing an assembly reference?)
```

This occurred because:
1. `Physics2DSetup.cs` was trying to directly reference `SwiftEnemyController` from the `CZ.Core.Enemy` namespace
2. The `CZ.Core.Enemy.asmdef` already had a reference to `CZ.Core.Configuration`
3. This created a circular dependency that Unity's compiler couldn't resolve

### Solution Implemented

The solution involved using reflection to reference the `SwiftEnemyController` type indirectly:

1. Removed the direct namespace import:
   ```csharp
   // Removed: using CZ.Core.Enemy;
   ```

2. Modified the `CheckSwiftEnemyCollisions` method to use `System.Type.GetType()` and non-generic `FindObjectsByType()`:
   ```csharp
   System.Type swiftEnemyType = System.Type.GetType("CZ.Core.Enemy.SwiftEnemyController, CZ.Core.Enemy");
   var swiftEnemies = FindObjectsByType(swiftEnemyType, FindObjectsSortMode.None);
   ```

3. Used `MonoBehaviour` as a common base type to access GameObject-related properties without needing direct type reference:
   ```csharp
   MonoBehaviour swiftEnemy = obj as MonoBehaviour;
   ```

This approach maintains the functionality while breaking the circular dependency.

## General Strategies for Resolving Circular Dependencies

### 1. Dependency Inversion

Create a shared interface in a third assembly that both dependent assemblies reference:

```
Before:  A → B → A  (circular)
After:   A → I ← B  (non-circular)
```

### 2. Reflection-Based Access

Use reflection to access types without direct references:

```csharp
// Instead of: var obj = new DependentType();
var type = Type.GetType("Namespace.DependentType, AssemblyName");
var obj = Activator.CreateInstance(type);
```

### 3. Message-Based Communication

Use a message bus or event system to communicate between modules:

```
Before:  A → B → A  (circular)
After:   A → EventSystem ← B  (non-circular)
```

### 4. Service Locator Pattern

Register services in a central locator that doesn't depend on the implementations:

```csharp
// Registration
ServiceLocator.Register<IEnemyService>(new ConcreteEnemyService());

// Usage
var service = ServiceLocator.Resolve<IEnemyService>();
```

### 5. Restructuring Dependencies

Sometimes the best solution is to restructure the code to avoid the dependency altogether:

- Move shared functionality to a common base class or utility
- Extract the dependency-causing functionality to a new module
- Rethink the architecture to reduce coupling

## Best Practices for Preventing Circular Dependencies

1. **Visualize Dependencies**: Create and maintain a dependency graph for your assemblies
2. **Design Before Implementation**: Plan your module structure before implementing
3. **SOLID Principles**: Follow the Single Responsibility and Dependency Inversion principles
4. **Regular Audits**: Periodically review assembly dependencies to identify potential issues
5. **Bottom-Up Development**: Build low-level modules first, then higher-level ones that depend on them

## Tools for Dependency Management

- **Unity Assembly Definition Tools**: Visual tools that show assembly references
- **NDepend**: Advanced static analysis tool for .NET projects
- **Custom Scripts**: Simple scripts that parse .asmdef files to visualize dependencies

## Conclusion

Resolving circular dependencies often requires thinking about alternative design patterns and architectures. While the reflection-based approach used in the `Physics2DSetup` example is effective, consider if a more fundamental restructuring of the code might lead to a cleaner solution in the future.

Remember that circular dependencies often indicate design issues in your architecture. When encountering them, take the time to evaluate if the current design is optimal or if it could benefit from restructuring. 