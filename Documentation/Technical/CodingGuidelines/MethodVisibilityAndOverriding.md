# Method Visibility and Overriding Best Practices in CZGAME

## Overview
Properly defining method visibility (public, protected, private) and inheritance capabilities (virtual, abstract, override) is crucial for creating maintainable and extensible code in Unity projects. This document outlines best practices for the CZGAME project.

## Unity Lifecycle Methods

### Special Consideration for Unity Message Methods
Unity automatically calls certain methods like `Start()`, `Update()`, `FixedUpdate()`, etc. These methods have special considerations for inheritance:

1. **When These Methods Should Be Virtual:**
   - When the base class implementation provides default behavior that should be customizable in child classes
   - When derived classes need to extend or replace the behavior
   - When you're creating test mocks or need to support unit testing

2. **When These Methods Should NOT Be Virtual:**
   - When the behavior is fundamental to the object's functioning and should not be changed
   - When the implementation contains core logic that should not be modified

### Recommended Pattern for Unity Message Methods

```csharp
// Preferred pattern for extensible lifecycle methods
protected virtual void Update()
{
    // Base implementation that can be extended
    if (!IsActive) return;
    PerformBaseUpdates();
}

// Private helper methods for implementation details
private void PerformBaseUpdates()
{
    // Implementation details
}
```

## Method Visibility Guidelines

### Public Methods
- Should represent the public API of your class
- Stable interface that shouldn't change frequently
- Well-documented with XML comments
- Consider making them virtual if they might need to be overridden

```csharp
/// <summary>
/// Sets the target position for this enemy to move towards.
/// </summary>
/// <param name="position">The world position to target</param>
public virtual void SetTarget(Vector3 position)
{
    targetPosition = position;
    hasValidTarget = true;
}
```

### Protected Methods
- Used for functionality that should be available to derived classes
- Not part of the public API
- Used for extensibility points in the class hierarchy
- Often marked as virtual to allow overriding

```csharp
/// <summary>
/// Core movement logic that can be overridden by specialized enemy types.
/// </summary>
protected virtual void MoveTowardsTarget()
{
    // Default movement implementation
}
```

### Private Methods
- Implementation details only used within the class
- Cannot be overridden (accessibility would need to change to protected/public and be virtual)
- Used for breaking down complex methods into smaller, manageable pieces

```csharp
private void InitializeComponents()
{
    // Internal setup code not meant to be overridden
}
```

## Virtual Methods and Inheritance

### When to Use Virtual Methods
- When a base class provides default behavior that derived classes may need to customize
- When implementing the Template Method pattern
- For all methods that might need specialized implementations in child classes

### When to Use Abstract Methods
- When a base class cannot provide a meaningful default implementation
- When derived classes must implement the functionality
- In abstract base classes that define a contract for derived classes

### Best Practices for Method Overriding

1. **Base Method Calls**
   - Consider whether derived classes should call the base implementation
   - Document whether calling the base method is required, optional, or should be avoided

```csharp
// Example where base call is recommended
protected override void FixedUpdate()
{
    // Custom preprocessing
    
    // Call base implementation to maintain core behavior
    base.FixedUpdate();
    
    // Custom postprocessing
}
```

2. **Documentation**
   - Document the expected behavior of virtual methods
   - Specify what aspects are customizable
   - Note any side effects or state changes

## Common Pitfalls

### 1. Private Unity Message Methods
Marking Unity message methods as `private` prevents derived classes from customizing behavior:

```csharp
// Problematic: can't be overridden
private void Update() 
{ 
    // Implementation
}

// Better: allows derived classes to extend functionality
protected virtual void Update() 
{ 
    // Implementation
}
```

### 2. Missing base.Method() Calls
Forgetting to call the base implementation can break expected behavior:

```csharp
// Could cause issues if base implementation is important
protected override void OnEnable()
{
    // Custom code, but missing base.OnEnable();
}

// Better pattern
protected override void OnEnable()
{
    base.OnEnable(); // Maintain base behavior
    // Additional custom code
}
```

### 3. Overriding Without the `override` Keyword
Creates a new method that shadows the base method rather than overriding it:

```csharp
// Incorrect: shadows the base method instead of overriding it
protected void Update() 
{
    // This is NOT an override
}

// Correct: properly overrides the base method
protected override void Update() 
{
    // This IS an override
}
```

## Testing Considerations

### Making Methods Testable
- Consider making methods virtual to support mocking in tests
- Maintain a balance between testability and encapsulation
- Design classes with testing in mind from the start

```csharp
// Better for testing - allows mocking in test classes
protected virtual void CalculatePath() 
{
    // Implementation
}

// Difficult to test directly
private void CalculatePath() 
{
    // Implementation
}
```

## Conclusion
Proper method visibility and virtual method usage are essential for creating maintainable, extensible code. By following these guidelines, you can create a codebase that is both robust and flexible, allowing for customization through inheritance while maintaining core functionality. 