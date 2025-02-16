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
2. Check Unity 6 Profiler category strings
3. Follow pattern in [GameManager.cs](Assets/_Project/Scripts/Core/GameManager.cs)

```csharp
drawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
memoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
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