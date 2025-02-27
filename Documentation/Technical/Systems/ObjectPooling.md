# Object Pooling System

## Overview

The Object Pooling system in CZGAME provides an efficient way to reuse frequently instantiated and destroyed game objects. This system significantly reduces garbage collection overhead and improves performance, especially for objects that are created and destroyed frequently during gameplay, such as projectiles, enemies, and visual effects.

## Implementation Details

The pooling system is implemented in the `CZ.Core.Pooling` assembly and consists of several key components:

### Core Components

#### ObjectPool<T>

The generic `ObjectPool<T>` class manages pools of specific object types. It's responsible for:
- Pre-instantiating objects at initialization
- Providing objects when requested
- Reclaiming objects when they're no longer needed
- Expanding the pool dynamically when necessary
- Memory monitoring to prevent excessive allocation

```csharp
public class ObjectPool<T> where T : IPoolable
{
    // Creates a new object pool
    public ObjectPool(Func<T> createFunc, int initialSize, int maxSize, string poolName, float memoryThresholdMB = 1024f);
    
    // Get an object from the pool
    public T Get();
    
    // Return an object to the pool
    public void Return(T obj);
    
    // Stats properties
    public int CurrentCount { get; }
    public int ActiveCount { get; }
    public int TotalCount { get; }
    public int PeakCount { get; }
    public bool IsExpanding { get; }
    public int MaxSize { get; }
}
```

#### PoolManager

The `PoolManager` is a singleton that manages all individual object pools. It:
- Creates and initializes pools based on configuration
- Provides a centralized access point for all pools
- Handles pool cleanup and shutdown
- Monitors memory usage across all pools
- Implements automatic cleanup during memory pressure

```csharp
public class PoolManager : MonoBehaviour
{
    public static PoolManager Instance { get; }
    
    // Create a new pool
    public ObjectPool<T> CreatePool<T>(Func<T> createFunc, int initialSize, int maxSize, string poolName) where T : MonoBehaviour, IPoolable;
    
    // Get an existing pool
    public ObjectPool<T> GetPool<T>() where T : MonoBehaviour, IPoolable;
    
    // Get an object from a pool
    public T Get<T>() where T : IPoolable;
    
    // Return an object to its pool
    public void Return<T>(T obj) where T : IPoolable;
    
    // Clear all pools
    public void ClearAllPools();
    
    // Get the total number of active objects
    public int ActiveCount { get; }
}
```

#### IPoolable Interface

The `IPoolable` interface must be implemented by any object that can be pooled:

```csharp
public interface IPoolable
{
    // Called when the object is retrieved from the pool
    void OnSpawn();
    
    // Called when the object is returned to the pool
    void OnDespawn();
    
    // Gets the GameObject associated with this poolable object
    GameObject GameObject { get; }
}
```

This interface ensures that objects can properly prepare themselves when taken from or returned to the pool.

### Memory Management

The pooling system includes sophisticated memory management:

1. **Memory Monitoring**: Uses `ProfilerRecorder` to track memory usage
2. **Expansion Control**: Prevents pool expansion when memory usage is high
3. **Dynamic Thresholds**:
   - Pool Warning: 256MB (25% of base memory)
   - Pool Critical: 358.40MB (35% of base memory)
   - Pool Emergency: 460.80MB (45% of base memory)
4. **Cleanup Strategy**: Implements memory cleanup when thresholds are exceeded

## Usage

### Setup

1. Configure pool settings in the `MemoryConfiguration` ScriptableObject
2. Ensure the `PoolManager` is initialized at game start (handled by the `GameManager`)
3. Implement the `IPoolable` interface on objects that will be pooled

### Basic Usage Example

```csharp
// Getting an object from a pool
var projectile = PoolManager.Instance.Get<Projectile>();

// Returning an object to its pool
PoolManager.Instance.Return(projectile);
```

### Creating a New Poolable Object

1. Implement the `IPoolable` interface:
   ```csharp
   public class Projectile : MonoBehaviour, IPoolable
   {
       public GameObject GameObject => gameObject;
       
       public void OnSpawn()
       {
           // Reset state when taken from pool
           transform.position = Vector3.zero;
           transform.rotation = Quaternion.identity;
           
           // Additional reset logic
           if (TryGetComponent<Rigidbody2D>(out var rb))
           {
               rb.velocity = Vector2.zero;
               rb.angularVelocity = 0f;
           }
       }
       
       public void OnDespawn()
       {
           // Clean up when returned to pool
           StopAllCoroutines();
           
           // Reset any state that could affect behavior when reused
           if (TryGetComponent<Collider2D>(out var collider))
           {
               collider.enabled = false;
           }
       }
       
       // Return to pool when done
       public void OnProjectileComplete()
       {
           PoolManager.Instance.Return(this);
       }
   }
   ```

2. Create and register the pool:
   ```csharp
   // Create a pool in initialization code
   PoolManager.Instance.CreatePool<Projectile>(
       createFunc: () => Instantiate(projectilePrefab).GetComponent<Projectile>(),
       initialSize: 100,
       maxSize: 200,
       poolName: "Projectiles"
   );
   ```

## Configuration

The pooling system uses the `MemoryConfiguration` ScriptableObject for centralized settings:

```csharp
[Serializable]
public class PoolConfig
{
    public string Name;
    public GameObject Prefab;
    public int InitialSize = 10;
    public int MaxSize = 100;
    public float MemoryBudgetMB = 10f;
}

public class MemoryConfiguration : ScriptableObject
{
    public List<PoolConfig> Pools = new List<PoolConfig>();
    
    // Memory Thresholds
    public float BaseMemoryMB = 1024f;
    public float WarningMemoryMB = 1536f;
    public float CriticalMemoryMB = 1792f;
    public float EmergencyMemoryMB = 2048f;
}
```

## Best Practices

1. **Choose appropriate initial pool sizes**:
   - Too small: Frequent expansion during gameplay (performance spikes)
   - Too large: Excessive memory usage
   - Recommended:
     * Projectiles: 100 initial, 200 max
     * Enemies: 50 initial, 100 max
     * VFX/Particles: 25 initial, 50 max
     * UI Elements: 50 initial, 100 max

2. **Never destroy pooled objects manually**:
   - Always use `PoolManager.Instance.Return(obj)` instead of `Destroy()`
   - Set up automatic return for time-based objects

3. **Reset object state completely**:
   - Ensure `OnSpawn()` resets all object properties
   - Clear any references or callbacks in `OnDespawn()`
   - Pay special attention to physics components

4. **Use object pools for frequently created/destroyed objects**:
   - Projectiles, particles, enemies, UI elements
   - Not recommended for rare or long-lived objects

5. **Handle expansion settings carefully**:
   - Enable expansion for objects with unpredictable quantity needs
   - Set reasonable maximum sizes based on gameplay scenarios
   - Monitor memory usage during expansion

6. **Profile and optimize**:
   - Use Unity Profiler to track pool usage
   - Adjust pool sizes based on real gameplay data
   - Watch for spikes in pool expansion during intense gameplay

## Integration with Other Systems

### Physics System Integration

The pooling system integrates with the [Physics System](Physics.md) by properly handling collider reuse:

```csharp
public void OnDespawn()
{
    // Disable colliders when returned to pool
    foreach (Collider2D collider in GetComponentsInChildren<Collider2D>())
    {
        collider.enabled = false;
    }
}

public void OnSpawn()
{
    // Re-enable colliders when taken from pool
    foreach (Collider2D collider in GetComponentsInChildren<Collider2D>())
    {
        collider.enabled = true;
    }
    
    // Ensure physics properties are updated
    if (TryGetComponent<Rigidbody2D>(out var rb))
    {
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }
}
```

### Performance Infrastructure

The pooling system is a critical part of the overall [Performance Guidelines](../Performance/PerformanceGuidelines.md), particularly:

1. Memory budgeting
2. Garbage collection optimization
3. Frame rate stabilization

## Performance Considerations

### Memory Usage

- Each pool reserves memory for its initial capacity
- Pools expand only when memory thresholds allow
- Memory budgets per pool:
  * Projectiles: 10MB total (50KB per instance)
  * Enemies: 10MB total (100KB per instance)
  * VFX: 10MB total (200KB per instance)
  * UI: 5MB total (50KB per instance)

### CPU Performance

- Pooling significantly reduces GC overhead
- Getting/returning objects is very fast (O(1) operations)
- Expansion during gameplay can cause frame drops if not managed correctly

### Recent Optimizations

The pooling system was recently optimized to:

1. Use thread-safe operations for concurrent access
2. Implement memory monitoring to prevent excessive allocation
3. Add validation to prevent pool type mismatches
4. Improve logging for better diagnostics

## Common Issues and Troubleshooting

### Pooled Objects Not Being Reclaimed

**Symptoms**:
- Pool consistently grows to maximum size
- Objects appear to leak

**Possible Causes**:
- Missing `Return()` calls
- References preventing proper cleanup

**Solutions**:
- Ensure all objects are returned when no longer needed
- Check for persistent references in other systems

### Unexpected Object Behavior

**Symptoms**:
- Objects have incorrect state when taken from pool
- Visual artifacts or physics anomalies

**Possible Causes**:
- Incomplete reset in `OnSpawn()`
- External systems modifying pooled objects

**Solutions**:
- Implement comprehensive reset logic in `OnSpawn()`
- Clear all references and state in `OnDespawn()`

### Pool Expansion Performance Spikes

**Symptoms**:
- Frame drops during intense gameplay
- Consistent memory allocation spikes

**Possible Causes**:
- Initial pool sizes too small
- Excessive simultaneous requests

**Solutions**:
- Increase initial pool sizes based on profiling
- Implement staggered initialization during loading

## Unity 6 Compatibility

Unity 6 introduces several performance improvements that enhance the pooling system:

1. `ProfilerRecorder` API for efficient memory monitoring
2. Improved GameObject activation performance
3. Enhanced component access optimizations
4. Better memory management and GC control

The CZGAME pooling system takes full advantage of these Unity 6 features while maintaining compatibility with the project's performance targets.

## Related Documentation

- [Performance Guidelines](../Performance/PerformanceGuidelines.md)
- [Physics System](Physics.md)
- [Infrastructure Plan](../Architecture/Infrastructure.md)

## Revision History

| Date | Version | Changes |
|------|---------|---------|
| 2025-02-27 | 1.0 | Initial document creation |
| 2025-02-27 | 1.1 | Updated with memory management details |
| 2025-02-27 | 1.2 | Added Unity 6 compatibility section |

Last Updated: 2025-02-27
Unity Version: 6000.0.38f1 