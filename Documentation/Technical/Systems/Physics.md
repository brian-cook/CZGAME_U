# Physics System Documentation

## Overview

The physics system in CZGAME is built on Unity's 2D physics engine with significant customizations for optimal gameplay. This document covers key physics configurations, best practices, and recently resolved issues.

## Key Components

### Physics2DSetup

Located at `Assets/_Project/Scripts/Core/Configuration/Physics2DSetup.cs`, this class manages:

- Global Physics2D settings
- Physics layer collisions
- Collider optimization for enemies
- Runtime verification of physics properties

#### Usage

The `Physics2DSetup` component should be attached to a GameObject that persists throughout gameplay, typically the GameManager. It initializes on `Awake()` and performs:

1. Physics2D global configuration
2. Physics layer collision matrix setup
3. Enemy collider optimization
4. Periodic verification of physics properties

#### Configuration Parameters

| Parameter | Description | Default Value |
|-----------|-------------|---------------|
| `contactOffset` | Minimum separation maintained by physics engine | 0.05 |
| `enemyColliderScaleFactor` | Scale factor for enemy colliders | 1.1 |
| `preventRigidbodySleeping` | Whether to prevent rigidbodies from sleeping | true |
| `enforceCollisionsEveryFrame` | Whether to check physics integrity every frame | true |

## Recently Fixed Issues

### Enemy Collider Size Continuous Growth

**Issue**: Enemy collider radiuses were continuously growing during gameplay due to repeated application of the `enemyColliderScaleFactor`.

**Symptoms**:
- Log messages showing continuous collider radius adjustments
- Increasingly large collision sizes for enemies
- Potential performance degradation

**Root Cause**:
The `UpdateEnemyPhysicsProperties()` method was applying the `enemyColliderScaleFactor` to the current radius value every time it was called. Since this method was called periodically in `Update()`, it created a cumulative growth effect:

```csharp
// Original problematic code
float originalRadius = circleCollider.radius;
float newRadius = originalRadius * enemyColliderScaleFactor;

if (Mathf.Abs(circleCollider.radius - newRadius) > 0.001f)
{
    circleCollider.radius = newRadius;
    updatedCount++;
    Debug.Log($"[Physics2DSetup] Adjusted collider radius on {rb.gameObject.name} " +
             $"from {originalRadius:F3} to {newRadius:F3}");
}
```

This resulted in exponential growth pattern:
- First update: 0.089 → 0.097 (× 1.1)
- Second update: 0.097 → 0.107 (× 1.1)
- Third update: 0.107 → 0.118 (× 1.1)
...and so on.

**Solution**:
1. Added a marker component (`ColliderAdjustmentMarker`) to track which objects have been processed
2. Modified the `UpdateEnemyPhysicsProperties()` method to skip objects that have already been processed
3. Added cleanup in the `OnSpawn()` method to remove the marker when objects are reused from the pool

```csharp
// Key parts of the solution
[DisallowMultipleComponent]
public class ColliderAdjustmentMarker : MonoBehaviour { }

// In UpdateEnemyPhysicsProperties
if (rb.gameObject.TryGetComponent<ColliderAdjustmentMarker>(out _))
{
    continue; // Skip already processed objects
}

// After processing
rb.gameObject.AddComponent<ColliderAdjustmentMarker>();

// In BaseEnemy.OnSpawn
var marker = GetComponent<Physics2DSetup.ColliderAdjustmentMarker>();
if (marker != null)
{
    Destroy(marker);
}
```

This ensures each enemy's collider is adjusted exactly once, preventing the continuous growth issue.

## Physics Layer Setup

The project uses specific physics layers for proper collision detection:

| Layer Name | Index | Used For |
|------------|-------|----------|
| Default | 0 | Generic objects |
| Player | 8 | Player character |
| Enemy | 9 | Enemy objects |
| Water | 4 | Water areas |
| UI | 5 | UI elements |

Layer setup is verified through `LayerSetupEditor.cs` which runs in the Editor to ensure the correct layer configuration.

## Best Practices

### Physics2D Configuration

1. **Proper Collision Layers**:
   - Use dedicated layers for different entity types
   - Configure the collision matrix explicitly

2. **Efficient Collision Detection**:
   - Use appropriate collider types (CircleCollider2D for most dynamic objects)
   - Adjust `ContactOffset` based on game scale
   - Consider performance vs. precision trade-offs

3. **Object Pooling Integration**:
   - Reset physics properties when reusing objects from the pool
   - Ensure marker components are properly removed

4. **Performance Considerations**:
   - Limit the number of dynamic rigidbodies
   - Use continuous collision detection only when necessary
   - Adjust physics update frequency based on game needs

## Debugging Physics Issues

### Diagnostic Tools

- Enable physics debug visualization in the Unity Editor
- Monitor the console logs for physics-related messages
- Use the Physics2D Debugger window

### Common Physics Problems

1. **Objects Passing Through Colliders**:
   - Use continuous collision detection
   - Increase contact offset slightly
   - Check for very fast-moving objects

2. **Performance Degradation**:
   - Check for too many active rigidbodies
   - Verify physics material properties
   - Enable rigidbody sleeping when appropriate

## Unity 6 Physics2D Best Practices

Unity 6 introduced several improvements to the 2D physics system:

- More efficient broadphase collision detection
- Improved continuous collision detection
- Better performance with many rigidbodies

For optimal results:

1. Use the appropriate rigidbody type:
   - Dynamic for fully physics-driven objects
   - Kinematic for objects moved by script but affecting physics
   - Static for immovable objects

2. Configure Physics2D settings properly:
   - Set appropriate velocity/position iterations (higher = more stable but more CPU)
   - Use auto-sync transforms with caution (performance vs. precision)

Last Updated: 2025-02-27
Unity Version: 6000.0.38f1 