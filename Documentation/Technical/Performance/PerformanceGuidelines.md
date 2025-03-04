# Unity 6 Performance Guidelines for CZGAME

## Overview

This document outlines the performance guidelines and optimization strategies for the CZGAME project built with Unity 6. These guidelines help maintain consistent performance across all target platforms.

## Performance Targets

- **Target FPS**: 60 (as per Unity 6 guidelines)
- **Max Draw Calls**: 100 (verified with ProfilerRecorder)
- **Max Memory Usage**: 1024MB (monitored via Memory Profiler)
- **Target Platform**: Windows (Primary)
- **Resolution**: 1920x1080

## Unity 6 Optimizations

### Graphics Performance

1. **URP 17.0.3 Best Practices**
   - Use Sprite Atlas for 2D textures
   - Enable GPU Instancing where possible
   - Implement proper batching strategies
   - Use appropriate Quality Settings
   - Utilize URP Asset configuration
   - Implement proper LOD strategies

2. **Memory Management**
   - Object Pooling for frequently spawned objects
   - Addressable Assets for resource loading
   - Proper Asset Bundle strategy
   - Scene loading optimization
   - Use ProfilerRecorder for memory tracking
   - Implement proper disposal patterns

3. **Physics Optimization**
   - Use 2D physics layers effectively
   - Implement proper collision detection
   - Optimize physics update intervals
   - Use composite colliders where appropriate
   - Utilize Physics2D settings optimization
   - Implement proper collision matrix

## Performance Monitoring

### Unity 6 Profiler

- Regular profiling sessions using ProfilerRecorder API
- Memory profiling with Unity Memory Profiler
- CPU usage monitoring with Profiler Window
- GPU performance analysis
- Custom performance markers
- Timeline profiling for sequences

### Frame Debugger

- Draw call analysis
- Batching verification
- Shader variant tracking
- URP render pipeline analysis
- Material property optimization
- Texture streaming monitoring

## Best Practices

### Asset Management

- Texture compression settings
- Audio compression profiles
- Mesh optimization techniques
- Asset loading strategies
- Addressables content management
- Asset bundle compression

### Code Optimization

- Use object pooling (PoolManager implementation)
- Implement proper garbage collection
- Optimize Update() calls
- Use coroutines effectively
- Implement proper IDisposable patterns
- Utilize Unity's new Job System

### Unity 6 Features

- Utilize Burst Compiler for performance
- Implement DOTS where beneficial
- Use Unity's new memory profiler
- Leverage Sentis for AI optimization
- Implement URP custom render features
- Use Input System for efficient input handling

### Monitoring Implementation

```csharp
public class PerformanceMonitor : MonoBehaviour
{
    private ProfilerRecorder drawCallsRecorder;
    private ProfilerRecorder memoryRecorder;
    
    private void OnEnable()
    {
        drawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        memoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
    }
    
    private void Update()
    {
        var drawCalls = drawCallsRecorder.LastValue;
        var memoryUsage = memoryRecorder.LastValue / (1024 * 1024); // Convert to MB
        
        if (drawCalls > 100)
            Debug.LogWarning($"Draw calls exceeded threshold: {drawCalls}");
            
        if (memoryUsage > 1024)
            Debug.LogWarning($"Memory usage exceeded threshold: {memoryUsage}MB");
    }
    
    private void OnDisable()
    {
        drawCallsRecorder.Dispose();
        memoryRecorder.Dispose();
    }
}
```

## Memory Management Strategy

### Core Principles

1. **Dynamic Memory Management** (HIGH PRIORITY)
   - System-aware thresholds based on available memory:
     * Base: 1024MB (1GB base memory)
     * Warning: 1536MB (1.5GB - 150% of base)
     * Critical: 1792MB (1.75GB - 175% of base)
     * Emergency: 2048MB (2GB - 200% of base)
   - Pool Memory Thresholds:
     * Pool Warning: 256MB (25% of base)
     * Pool Critical: 358.40MB (35% of base)
     * Pool Emergency: 460.80MB (45% of base)
   - Memory Monitoring:
     * Track absolute and relative memory changes
     * Monitor memory delta against baseline
     * Implement graduated cleanup responses
   - Cleanup Strategy:
     * Preemptive: Light cleanup at warning threshold (1536MB)
     * Aggressive: Full cleanup at critical threshold (1792MB)
     * Emergency: Scene restart if cleanup fails (2048MB)

2. **Pooling Strategy** (HIGH PRIORITY)
   - Pool Configurations:
     * Projectiles: 100 initial, 200 max
     * Enemies: 50 initial, 100 max
     * VFX/Particles: 25 initial, 50 max
     * UI Elements: 50 initial, 100 max
   - Pool Memory Budgets:
     * Projectiles: 10MB total (50KB per instance)
     * Enemies: 10MB total (100KB per instance)
     * VFX: 10MB total (200KB per instance)
     * UI: 5MB total (50KB per instance)
   - Monitoring:
     * Track pool utilization
     * Log pool expansion events
     * Monitor memory impact
     * Implement emergency cleanup

3. **Asset Loading** (HIGH PRIORITY)
   - Use Addressables for:
     * Character variants
     * Weapon prefabs
     * Zone prefabs
     * Large texture assets
   - Implementation:
     * Load on demand
     * Unload unused assets
     * Track reference counts
     * Monitor memory footprint

4. **Scene Management**
   - Single scene approach:
     * Main gameplay scene
     * Additive UI scene
     * No runtime scene loading
   - Asset organization:
     * Preload essential assets
     * Async load non-critical assets
     * Clear reference cache on restart

### Memory Budgets

1. **Runtime Memory** (2048MB Emergency Total)
   - Managed Memory: 1024MB (Base)
     * Game Logic: 256MB
     * Unity Systems: 512MB
     * Asset Memory: 256MB
   - Pool Memory: 460.80MB (45% of base)
     * Projectile Pool: 100MB
     * Enemy Pool: 150MB
     * VFX Pool: 150MB
     * UI Pool: 60.80MB
   - Reserved Memory: 563.20MB
     * System: 256MB
     * Graphics: 204.80MB
     * Audio: 102.40MB

2. **Asset Memory**
   - Textures: 256MB max
   - Audio: 128MB max
   - Meshes: 64MB max
   - Materials: 64MB max
   - Prefabs: 64MB max

### Monitoring Implementation

```csharp
public class MemoryMonitor : MonoBehaviour
{
    private ProfilerRecorder totalMemoryRecorder;
    private ProfilerRecorder managedMemoryRecorder;
    private ProfilerRecorder gcMemoryRecorder;
    private float startupBaseline;
    private float memoryBaseline;
    
    private void OnEnable()
    {
        totalMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
        managedMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Managed Memory");
        gcMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Reserved Memory");
        
        // Initialize with dynamic thresholds
        startupBaseline = totalMemoryRecorder.LastValue / (1024 * 1024);
        memoryBaseline = startupBaseline;
        float thresholdScale = Math.Max(1f, startupBaseline / 512f);
        
        StartCoroutine(MonitorMemory(thresholdScale));
    }
    
    private IEnumerator MonitorMemory(float thresholdScale)
    {
        while (enabled)
        {
            var totalMemory = totalMemoryRecorder.LastValue / (1024 * 1024);
            var managedMemory = managedMemoryRecorder.LastValue / (1024 * 1024);
            var gcMemory = gcMemoryRecorder.LastValue / (1024 * 1024);
            
            // Calculate relative thresholds
            float adjustedWarning = 768f * thresholdScale;
            float adjustedCritical = 896f * thresholdScale;
            float adjustedEmergency = 1024f * thresholdScale;
            
            // Calculate relative changes
            float memoryDelta = totalMemory - memoryBaseline;
            float relativeDelta = memoryDelta / memoryBaseline;
            
            // Log warnings with relative context
            if (totalMemory > adjustedWarning)
                Debug.LogWarning($"Memory usage high: {totalMemory:F2}MB (Delta: {memoryDelta:F2}MB, Relative: {relativeDelta:P2})");
            if (totalMemory > adjustedCritical)
                Debug.LogError($"Memory usage critical: {totalMemory:F2}MB (Delta: {memoryDelta:F2}MB, Relative: {relativeDelta:P2})");
                
            yield return new WaitForSeconds(0.5f);
        }
    }
    
    private void OnDisable()
    {
        totalMemoryRecorder.Dispose();
        managedMemoryRecorder.Dispose();
        gcMemoryRecorder.Dispose();
    }
}
```

### Memory Optimization Checklist

1. **Asset Configuration**
   - [ ] Texture compression appropriate for 2D
   - [ ] Audio compression (Vorbis, quality setting 70%)
   - [ ] Sprite atlas implementation
   - [ ] Material sharing strategy
   - [ ] Shader variant stripping

2. **Runtime Optimization**
   - [ ] Object pooling for all spawned objects
   - [ ] Addressables loading patterns
   - [ ] Garbage collection optimization
   - [ ] Reference cleanup on scene changes
   - [ ] Async loading implementation

3. **Development Practices**
   - [ ] Regular memory profiling
   - [ ] Pool utilization monitoring
   - [ ] GC allocation tracking
   - [ ] Asset reference validation
   - [ ] Memory leak detection

### Critical Memory Considerations for Core Loop

1. **Enemy System**
   - Pool Size: 100 enemies max
   - Memory per Enemy: ~100KB
   - Total Budget: 10MB
   - Monitoring: Pool expansion events

2. **Projectile System**
   - Pool Size: 200 projectiles max
   - Memory per Projectile: ~50KB
   - Total Budget: 10MB
   - Cleanup: Automatic return to pool

3. **VFX System**
   - Pool Size: 50 effects max
   - Memory per Effect: ~200KB
   - Total Budget: 10MB
   - Optimization: Particle system batching

4. **Comfort Zone System**
   - Active Zones: 5 max
   - Memory per Zone: ~500KB
   - Total Budget: 2.5MB
   - Optimization: Shared materials

### Memory Profiling Schedule

1. **Development Phase**
   - Daily: Basic memory snapshot
   - Weekly: Deep memory analysis
   - Per Feature: Memory impact assessment

2. **Testing Phase**
   - Every Build: Memory baseline check
   - Load Testing: Peak memory validation
   - Stress Testing: Memory leak detection

### Unity Memory Profiler Usage

1. **Key Metrics to Monitor**
   - Total Memory Used
   - Managed Heap Size
   - GC Reserved Memory
   - Texture Memory
   - Mesh Memory
   - Asset Memory

2. **Warning Thresholds**
   - Total Memory: 1024MB
   - Managed Memory: 512MB
   - GC Reserved: 256MB
   - Texture Memory: 128MB
   - Mesh Memory: 32MB

3. **Critical Thresholds**
   - Total Memory: 1200MB
   - Managed Memory: 600MB
   - GC Reserved: 300MB
   - Texture Memory: 150MB
   - Mesh Memory: 40MB

## Integration with Other Systems

### Object Pooling System

These performance guidelines are closely integrated with the [Object Pooling](../Systems/ObjectPooling.md) system, which implements the pooling strategy outlined in this document.

### Physics System

The [Physics System](../Systems/Physics.md) follows these performance guidelines, particularly regarding collision detection optimization and physics layer management.

## References

- [Unity Memory Management](https://docs.unity3d.com/Manual/performance-managed-memory.html)
- [Performance Guidelines](https://docs.unity3d.com/Manual/UnderstandingPerformance.html)
- [Memory Profiler](https://docs.unity3d.com/Packages/com.unity.memoryprofiler@1.0/manual/index.html)
- [Unity 6 Performance Optimization](https://docs.unity3d.com/6000.0/Documentation/Manual/BestPracticeUnderstandingPerformanceInUnity.html)
- [ProfilerRecorder API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorder.html)
- [URP Performance Guidelines](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.0/manual/performance-guidelines.html)

## Revision History

| Date | Version | Changes |
|------|---------|---------|
| 2025-02-27 | 1.0 | Initial document creation |
| 2025-02-27 | 1.1 | Added memory monitoring implementation |

Last Updated: 2025-02-27
Unity Version: 6000.0.38f1 