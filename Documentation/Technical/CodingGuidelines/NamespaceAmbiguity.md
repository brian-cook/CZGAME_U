# Avoiding Namespace Ambiguity in CZGAME

## Overview
Namespace ambiguities occur when the compiler can't determine which type to use because multiple types with the same name exist in different namespaces that are both being referenced. This document provides guidance on how to prevent and resolve these issues in the CZGAME project.

## Common Ambiguity Issues

### 1. Random Class Ambiguity
One of the most common ambiguities in Unity projects occurs between `UnityEngine.Random` and `System.Random`:

```csharp
// This causes ambiguity if both System and UnityEngine are imported
float value = Random.value; // Which Random? System or UnityEngine?
```

### 2. Debug Class Ambiguity
Ambiguity between `UnityEngine.Debug` and `System.Diagnostics.Debug`:

```csharp
// Which Debug is being used?
Debug.Log("Message");
```

### 3. Input Class Ambiguity
With the new Input System, there can be ambiguity between `UnityEngine.Input` and `UnityEngine.InputSystem.Input`:

```csharp
// Which Input is this?
var input = Input.GetAxis("Horizontal");
```

## Best Practices

### 1. Use Fully Qualified Names for Ambiguous Types
Always use the full namespace when dealing with types that might be ambiguous:

```csharp
// Explicitly specify which Random to use
float randomValue = UnityEngine.Random.value;
int randomNumber = new System.Random().Next(1, 100);
```

### 2. Use Namespace Aliases
Use the `using` directive with an alias to create a shorter name for a fully qualified name:

```csharp
using SystemRandom = System.Random;
using UnityRandom = UnityEngine.Random;

// Now use the aliases
float unityRandomValue = UnityRandom.value;
int systemRandomNumber = new SystemRandom().Next(1, 100);
```

### 3. Avoid Importing Unnecessary Namespaces
Only import the namespaces you actually need:

```csharp
// Only import what you need
using UnityEngine;
// Avoid using System if you only need specific System namespaces
using System.Collections.Generic;
using System.Linq;
```

### 4. Be Cautious with 'using System;'
The `System` namespace contains many common types that might conflict with Unity types. Consider only importing specific System namespaces as needed.

## Resolution Strategies for Existing Ambiguities

### 1. Identify Ambiguous References
When you encounter a CS0104 error ("X is an ambiguous reference between 'A.X' and 'B.X'"), identify all occurrences of the ambiguous type.

### 2. Apply Fully Qualified Names
Modify all occurrences to use the fully qualified name:

```csharp
// Before
float value = Random.value;

// After
float value = UnityEngine.Random.value;
```

### 3. Consider Codebase Consistency
If one usage pattern is dominant throughout the codebase, consider standardizing on that pattern for consistency.

## Example: Fixing Random Ambiguity

```csharp
// Before - with ambiguity
using System;
using UnityEngine;

public class MyClass
{
    void MyMethod()
    {
        // This is ambiguous - System.Random or UnityEngine.Random?
        float value = Random.value;
    }
}

// After - fixed
using System;
using UnityEngine;

public class MyClass
{
    void MyMethod()
    {
        // Explicitly qualified - no ambiguity
        float value = UnityEngine.Random.value;
    }
}
```

## Conclusion
Resolving namespace ambiguities is a common task in Unity development. By following these guidelines, you can prevent ambiguity issues and maintain clean, error-free code. When in doubt, use fully qualified names to eliminate any potential confusion for both the compiler and other developers reading your code. 