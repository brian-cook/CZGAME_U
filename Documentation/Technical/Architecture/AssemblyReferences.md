# Unity Assembly Definitions Guide

## Introduction
This document provides a guide for working with Assembly Definitions (.asmdef) in the CZGAME project. Assembly Definitions allow us to organize our code into separate assemblies, improving compilation times and enforcing proper architectural boundaries.

## Project Assembly Structure

Our project uses the following assembly structure:

- **CZ.Core** - Core functionality shared across all systems
- **CZ.Core.Enemy** - Enemy-specific implementations
- **CZ.Core.Player** - Player-specific implementations
- **CZ.Core.Pooling** - Object pooling system
- **CZ.Core.Interfaces** - Shared interfaces
- **CZ.Core.Resource** - Resource management
- **CZ.Core.Configuration** - Game configuration
- **CZ.Core.Logging** - Logging system
- **CZ.Core.UI** - User interface components
- **CZ.Core.VFX** - Visual effects
- **CZ.Core.Debug** - Debug utilities

## Working with Assembly References

### Adding a Reference Between Assemblies

When a script in one assembly needs to use types from another assembly, you must add a reference in the .asmdef file. For example, if code in **CZ.Core.Enemy** needs to reference types in **CZ.Core.Player**, you must:

1. Open `Assets/_Project/Scripts/Core/Enemy/CZ.Core.Enemy.asmdef`
2. Add "CZ.Core.Player" to the references list
3. Save the file

```json
{
    "name": "CZ.Core.Enemy",
    "rootNamespace": "CZ.Core.Enemy",
    "references": [
        "CZ.Core",
        "CZ.Core.Player",  // Added reference
        // Other references...
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

### Common Assembly Reference Errors

#### "The type or namespace name 'X' does not exist in the namespace 'Y'"
This error typically means you're missing an assembly reference. Add the necessary assembly to the references list in your .asmdef file.

#### Circular Dependencies
Unity does not allow circular dependencies between assemblies. If Assembly A references Assembly B, then Assembly B cannot reference Assembly A. To fix this:

1. Move shared code to a common assembly that both can reference
2. Refactor code to break the circular dependency
3. Use interfaces to decouple implementations

## Best Practices

1. **Keep Dependencies Minimal**: Only reference assemblies you actually need
2. **Avoid Circular Dependencies**: Design your architecture to prevent circular dependencies
3. **Use Interface Assemblies**: Place interfaces in separate assemblies to facilitate loose coupling
4. **Test Assembly References**: Create separate test assemblies for each production assembly
5. **Document Dependencies**: Update this document when adding new assemblies or significant dependencies

## Validating Assembly References

When you encounter namespace-related errors, check:

1. That you have the proper `using` statement for the namespace
2. That the assembly containing the namespace is referenced in your .asmdef file
3. That there are no circular dependencies between assemblies

## Commonly Used Unity Assemblies

- **UnityEngine.CoreModule** - Core Unity functionality (automatically referenced)
- **Unity.InputSystem** - For new Input System
- **Unity.TextMeshPro** - For TextMeshPro UI components
- **Unity.Profiling.Core** - For profiling utilities 