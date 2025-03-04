# Version Troubleshooting Guide

## Common Issues and Solutions

### Unity 6 Specific Issues

#### NaughtyAttributes Integration
**Issue**: Attributes not working on properties
```csharp
// Won't work
[ShowNonSerializedField] public float MyProperty { get; private set; }

// Will work
[ShowNonSerializedField] private float myField;
```
**Solution**: Use attributes only on fields, not properties. See [PlayerController.cs](Assets/_Project/Scripts/Core/Player/PlayerController.cs) for examples.

#### Input System (1.13.0)
**Issue**: Input actions not being recognized
**Solution**: 
1. Ensure PlayerInput component references correct action asset
2. Check action names match exactly
3. Verify action bindings in the Input Actions asset
Reference: [GameControls.inputactions](Assets/_Project/Input/GameControls.inputactions)

### Package Version Conflicts

#### Universal Render Pipeline (17.0.3)
**Known Issues**:
- Camera component requires UniversalAdditionalCameraData
- Sprite materials need updating for URP

**Solution**:
1. Add URP Camera component to cameras
2. Use Graphics Settings to assign URP Asset
3. Update materials using "Edit > Rendering > Materials > Convert Selected Built-in Materials to URP"

#### Test Framework (1.4.6)
**Issue**: PlayMode tests failing to find scene objects
**Solution**: 
1. Ensure test scene is added to build settings
2. Use `[UnitySetUp]` for scene loading
3. Follow pattern in [PlayerControllerTests.cs](Assets/_Project/Tests/PlayMode/Core/PlayerControllerTests.cs)

### Project Setup Issues

#### Assembly Definition References
**Issue**: Missing package references in assembly definitions
**Solution**: 
Check assembly definition files against these templates:
```json
// CZ.Core.asmdef
{
    "references": [
        "GUID:75469ad4d38634e559750d17036d5f7c",  // Input System
        "GUID:776d03a35f1b52c4a9aed9f56d7b4229"   // NaughtyAttributes
    ]
}

// CZ.Tests.PlayMode.asmdef
{
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "CZ.Core",
        "Unity.InputSystem",
        "Unity.InputSystem.TestFramework"
    ]
}
```

### Performance Monitoring

#### Profiler Integration
**Issue**: Performance metrics not showing in GameManager
**Solution**:
1. Verify ProfilerRecorder initialization
2. Check Unity 6 counter names:
   - Render: "Batches Count" (not "Draw Calls Count")
   - Memory: "Total Used Memory" (primary), "System Memory" (fallback)
   - GC: "GC Used Memory" (primary), "GC Reserved Memory" (fallback)
3. Follow pattern in [GameManager.cs](Assets/_Project/Scripts/Core/GameManager.cs)

```csharp
// Unity 6.0 Profiler counter initialization
drawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
memoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
gcMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory");
```

#### Memory Management Issues
**Issue**: High initial memory state triggering emergency cleanup
**Solution**:
1. Implement dynamic threshold scaling:
```csharp
float thresholdScale = Math.Max(1f, initialMemory / BASELINE_THRESHOLD_MB);
float adjustedWarning = WARNING_THRESHOLD_MB * thresholdScale;
float adjustedCritical = CRITICAL_THRESHOLD_MB * thresholdScale;
float adjustedEmergency = EMERGENCY_THRESHOLD_MB * thresholdScale;
```

2. Track relative memory changes:
```csharp
float memoryDelta = currentMemory - memoryBaseline;
float relativeDelta = memoryDelta / memoryBaseline;
```

3. Implement graduated cleanup responses:
   - Preemptive: Light cleanup at warning threshold
   - Aggressive: Full cleanup at critical threshold
   - Emergency: Scene restart if cleanup fails

**Issue**: Memory counter initialization failures
**Solution**:
1. Implement counter name fallbacks:
```csharp
string[] memoryCounters = new string[] 
{
    "Total Used Memory",    // Primary
    "System Memory",        // Fallback 1
    "Total System Memory",  // Fallback 2
    "Total Reserved Memory" // Fallback 3
};
```

2. Validate counter readings:
```csharp
float testValue = ConvertToMB(memoryRecorder.CurrentValue);
if (testValue > 0)
{
    Debug.Log($"Counter initialized: {counterName} ({testValue:F2}MB)");
    return true;
}
```

### Version Update Checklist

1. Before Updating
   - [ ] Backup project
   - [ ] Document current versions
   - [ ] Review release notes
   - [ ] Check package dependencies

2. During Update
   - [ ] Update Unity version first
   - [ ] Update packages one at a time
   - [ ] Test after each major package update
   - [ ] Keep track of any required code changes

3. After Update
   - [ ] Run all tests
   - [ ] Check performance metrics
   - [ ] Update documentation
   - [ ] Commit working state

### Quick Reference

#### Unity 6 Console Error Codes
| Error Code | Description | Solution Reference |
|------------|-------------|-------------------|
| CS0592 | Attribute usage invalid | [NaughtyAttributes Wiki](https://github.com/dbrizov/NaughtyAttributes/wiki/Attributes) |
| CS0246 | Assembly reference missing | [Assembly Definition Setup](#assembly-definition-references) |

#### Performance Baselines
| Metric | Target | Monitoring Tool |
|--------|---------|----------------|
| FPS | 60 | GameManager.MonitorPerformance() |
| Draw Calls | <100 | ProfilerRecorder (Render) |
| Memory | <1024MB | ProfilerRecorder (Memory) |

### Additional Resources

1. Unity 6 Specific
   - [Performance Guidelines](https://docs.unity3d.com/6000.0/Documentation/Manual/BestPracticeUnderstandingPerformanceInUnity.html)
   - [2D Best Practices](https://docs.unity3d.com/6000.0/Documentation/Manual/2DFeature.html)
   - [ProfilerRecorder API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorder.html)

2. Package Documentation
   - [URP Setup Guide](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.0/api/index.html)
   - [Input System Guide](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.13/manual/index.html)
   - [Input Action Assets](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.13/manual/ActionAssets.html)
   - [Performance Testing](https://docs.unity3d.com/Packages/com.unity.test-framework.performance@3.0/manual/index.html)

3. Testing Resources
   - [Test Framework Guide](https://docs.unity3d.com/Packages/com.unity.test-framework@1.4/manual/index.html)
   - [PlayMode Testing](https://docs.unity3d.com/6000.0/Documentation/Manual/PlaymodeTestFramework.html)
   - [NaughtyAttributes Documentation](https://dbrizov.github.io/na-docs/)

Last Updated: 2024-02-15
Reference: Unity 6.0 

# Unity 6 Troubleshooting Guide

## NaughtyAttributes Integration
### Common Issues and Solutions

1. Button Callbacks Not Working in Editor
```
Problem: [Button] attribute methods don't work properly when called from Unity editor
Solution: Implement proper editor vs play mode handling
```

Example Fix:
```csharp
[Button("Action")]
public void EditorAction()
{
    #if UNITY_EDITOR
    if (!UnityEditor.EditorApplication.isPlaying)
    {
        UnityEditor.EditorApplication.isPlaying = true;
        return;
    }
    #endif

    // Regular play mode logic
    ExecuteAction();
}
```

2. Coroutine Issues with Button Methods
```
Problem: Coroutines don't execute properly when called from [Button] methods
Solution: Use state management pattern instead of coroutines
```

Example Fix:
```csharp
// Instead of:
[Button] void Action() { StartCoroutine(ActionRoutine()); }

// Use:
[Button] void Action() { SetState(NewState); }
```

3. State Transition Issues
```
Problem: State changes don't propagate properly from editor buttons
Solution: Implement explicit state management
```

Example Fix:
```csharp
private void SetState(GameState newState)
{
    // Pre-transition setup
    PrepareStateChange(newState);
    
    // Update state
    currentState = newState;
    
    // Post-transition setup
    FinalizeStateChange(newState);
}
```

4. System Validation Failures
```
Problem: Systems not properly initialized when using editor buttons
Solution: Implement comprehensive validation
```

Example Fix:
```csharp
private bool ValidateSystems()
{
    // Check editor state
    #if UNITY_EDITOR
    if (!UnityEditor.EditorApplication.isPlaying)
    {
        return false;
    }
    #endif

    // Validate required systems
    return ValidateRequiredSystems();
}
```

### Best Practices for Unity 6 Integration

1. Editor Mode Considerations
- Always check EditorApplication.isPlaying
- Handle play mode transitions explicitly
- Use proper cleanup in editor context
- Maintain state consistency

2. State Management
- Use explicit state machines
- Avoid coroutine dependencies
- Implement proper validation
- Handle transitions atomically

3. System Initialization
- Validate before state changes
- Use explicit initialization
- Handle editor vs play mode differently
- Maintain proper cleanup

4. Error Handling
- Provide clear error messages
- Handle editor-specific errors
- Implement proper fallbacks
- Maintain state consistency
