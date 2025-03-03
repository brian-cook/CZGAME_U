# Unit Testing Guidelines for CZGAME

## Overview
This document outlines best practices for writing and maintaining unit tests in the CZGAME project using Unity's Test Framework. Following these guidelines ensures consistent, maintainable tests that effectively validate our code while remaining resilient to code changes.

## Test Organization

### Test Location
- Edit Mode Tests: `Assets/_Project/Tests/EditMode/`
- Play Mode Tests: `Assets/_Project/Tests/PlayMode/`
- Mirror the project structure within these directories (e.g., Core/Enemy tests should be in `Tests/EditMode/Core/Enemy/`)

### Naming Conventions
- Test Classes: `[ClassName]Tests.cs` (e.g., `SwiftEnemyTests.cs`)
- Test Methods: `[ClassName]_[TestScenario]` (e.g., `SwiftEnemy_HasRequiredComponents`)
- Test Assemblies: Match project assemblies with `.Tests` suffix

## Test Structure

### Class Setup
```csharp
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CZ.Core.Enemy; // Import the namespace being tested

namespace CZ.Tests.EditMode.Core.Enemy 
{
    public class SwiftEnemyTests
    {
        // Test class implementation
    }
}
```

### Test Method Structure
Each test should follow the Arrange-Act-Assert pattern:
```csharp
[Test]
public void SwiftEnemy_HasRequiredComponents()
{
    // Arrange - set up the test conditions
    var testObject = new GameObject();
    var component = testObject.AddComponent<SwiftEnemyController>();
    
    // Act - perform the action being tested
    var result = component.Initialize(10, 5f);
    
    // Assert - verify the expected outcome
    Assert.IsTrue(result);
    Assert.IsNotNull(component.GetComponent<Rigidbody2D>());
}
```

### Setup and Teardown
Use `[SetUp]` and `[TearDown]` for common initialization and cleanup:

```csharp
private GameObject testObject;
private SwiftEnemyController component;

[SetUp]
public void Setup()
{
    testObject = new GameObject("TestEnemy");
    component = testObject.AddComponent<SwiftEnemyController>();
}

[TearDown]
public void TearDown()
{
    // IMPORTANT: Always clean up ANY GameObject created in tests
    Object.DestroyImmediate(testObject);
    
    // Clean up any additional GameObjects created during tests
    if (component.CurrentTarget != null)
    {
        Object.DestroyImmediate(component.CurrentTarget.gameObject);
    }
}
```

## Testing Practices

### 1. Test Isolation
- Each test should be completely independent
- Never rely on the state from another test
- Reset all static data between tests

### 2. Deterministic Tests
- Tests should produce the same result every time they run
- Seed random number generators when randomness is needed
- Avoid timing-dependent logic in Edit Mode tests

### 3. Mock Dependencies
- Use mock or stub implementations for external dependencies
- Create test-specific implementations of interfaces
- Example:

```csharp
private class MockAnimator
{
    private HashSet<string> triggeredAnimations = new HashSet<string>();
    
    public void SetTrigger(string name)
    {
        triggeredAnimations.Add(name);
    }
    
    public bool WasTriggerCalled(string name)
    {
        return triggeredAnimations.Contains(name);
    }
}
```

### 4. Testing MonoBehaviours
When testing MonoBehaviours, consider these special requirements:
- Instantiate GameObject instances for components
- Clean up all GameObjects in TearDown
- Test in isolation from other systems when possible
- Implement test-specific properties and methods (marked with `#if UNITY_EDITOR`)

```csharp
// In your component class
#if UNITY_EDITOR
public Vector3 ExposedTargetPosition => targetPosition;
#endif
```

### 5. Supporting Testability
Design your classes with testing in mind:
- Make methods `protected virtual` when they may need to be overridden in tests
- Create testing interfaces for complex dependencies
- Add editor-only properties to expose internal state
- Consider adding separate test-support methods when needed:

```csharp
#if UNITY_EDITOR
public void SetTargetTransformForTesting(Transform target) 
{
    CurrentTarget = target;
}
#endif
```

## Common Pitfalls

### 1. Scene Contamination
- Always clean up ALL game objects created during tests
- Be careful with persistent singletons or managers

### 2. Test Interdependence
- Tests should never depend on each other's execution
- Each test should set up its own complete environment

### 3. Time-Dependent Tests
- Edit Mode tests have no concept of Unity's Time
- Use explicit timekeeping or input simulation for time-dependent tests

### 4. Missing Cleanup
- Failing to destroy objects can lead to memory leaks and test interference
- Always use TearDown to clean up resources

## Test Coverage Guidelines

### Minimum Test Coverage
- All public methods should have at least one test
- Core game mechanics should have comprehensive tests
- Edge cases and error handling should be tested

### What to Test
1. Core functionality and business logic
2. Critical user interactions
3. Edge cases and error handling
4. Regressions for fixed bugs

### What NOT to Test
1. Simple property getters/setters
2. Unity-provided functionality
3. External library functionality
4. Visual/UI appearance (except for functional aspects)

## Conclusion
Following these guidelines ensures that our tests remain valuable, maintainable, and effective. Remember that well-written tests are an investment in the long-term stability and quality of our codebase. 