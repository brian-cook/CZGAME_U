# Unity Testing Guidelines

## Overview
These guidelines are part of the core compliance requirements for the project. They define the standards and best practices for writing and maintaining tests in Unity 6.0.

## Coroutine and Yield Return Limitations

### Yield Return in Try-Catch Blocks
```csharp
// INCORRECT - Will cause compiler error CS1626
[UnityTest]
public IEnumerator TestMethod()
{
    try
    {
        yield return null; // ERROR: Cannot yield a value in try block with catch clause
    }
    catch (Exception e)
    {
        Debug.LogError(e);
    }
}

// CORRECT - Separate try-catch from yield operations
[UnityTest]
public IEnumerator TestMethod()
{
    bool operationSuccess = false;
    try
    {
        // Setup or verification code
        operationSuccess = true;
    }
    catch (Exception e)
    {
        Debug.LogError(e);
        throw;
    }

    if (operationSuccess)
    {
        // Perform yield operations outside try-catch
        yield return null;
    }
}
```

**Important Note**: C# compiler error CS1626 occurs when using `yield return` inside a try-catch block. This is a language-level restriction, not specific to Unity. Always structure your test methods to perform yield operations outside of try-catch blocks.

## Test Structure Best Practices

### 1. Setup and Teardown
- Use `[UnitySetUp]` for scene and GameObject initialization
- Ensure proper cleanup in `[UnityTearDown]`
- Handle scene management carefully
- Clean up resources in reverse order of creation

### 2. Scene Management
- Use unique scene names for each test
- Clean up existing test scenes before creating new ones
- Properly handle scene loading/unloading operations
- Wait for scene operations to complete

### 3. Resource Management
- Clean up GameObjects and components explicitly
- Handle static resources properly
- Use proper object destruction sequence
- Implement proper input system cleanup

### 4. Error Handling
- Separate error handling from coroutine operations
- Use state flags to control flow after try-catch blocks
- Provide detailed error messages
- Log errors with proper context

### 5. Performance Considerations
- Clean up resources promptly
- Avoid memory leaks
- Handle input system cleanup properly
- Use appropriate wait times between operations

## Reference Implementation
See `Assets/_Project/Tests/PlayMode/Core/Player/PlayerMovementTests.cs` for examples of:
- Proper test structure
- Scene management
- Resource cleanup
- Error handling
- Coroutine usage

## Unity 6.0 Specific Requirements
1. Use NUnit 3.5+ test framework
2. Follow new Input System guidelines
3. Implement proper performance monitoring
4. Handle async operations correctly
5. Use proper scene management APIs

## Common Issues and Solutions
1. Scene name conflicts
   - Use timestamp + GUID for unique names
   - Clean up existing scenes before tests

2. Resource leaks
   - Implement proper cleanup sequence
   - Handle static resources
   - Clean up input system

3. Coroutine limitations
   - Structure code to avoid yield in try-catch
   - Use state flags for flow control
   - Separate error handling
   
   Example of proper async operation handling:
   ```csharp
   // INCORRECT - Yielding inside try-catch
   public IEnumerator UnloadScene()
   {
       try
       {
           yield return SceneManager.UnloadSceneAsync(scene);  // CS1626 Error
       }
       catch (Exception e)
       {
           Debug.LogError(e);
       }
   }

   // CORRECT - Separate operation start from yield
   public IEnumerator UnloadScene()
   {
       bool operationStarted = false;
       bool operationError = false;
       string errorMessage = string.Empty;
       AsyncOperation unloadOperation = null;

       try
       {
           unloadOperation = SceneManager.UnloadSceneAsync(scene);
           operationStarted = true;
       }
       catch (Exception e)
       {
           operationError = true;
           errorMessage = e.Message;
           Debug.LogError($"Failed to start scene unload: {e.Message}");
       }

       if (operationError)
       {
           throw new Exception($"Scene unload failed: {errorMessage}");
       }

       if (operationStarted && unloadOperation != null)
       {
           while (!unloadOperation.isDone)
           {
               yield return null;
           }
       }
   }
   ```

   Key points:
   - Start operations in try-catch block
   - Use state flags to track success/failure
   - Perform yield operations outside try-catch
   - Maintain proper error context
   - Handle cleanup appropriately

4. Input System Cleanup
   - Properly disable input actions before disposal
   - Follow correct cleanup order:
     1. Unsubscribe from all input events
     2. Disable specific action maps (e.g., `controls.Player.Disable()`)
     3. Disable the entire controls asset
     4. Dispose the controls asset
     5. Set reference to null

5. Test Log Message Handling
   - Use `LogAssert.Expect()` for known warning/error messages
   - Common messages to handle:
     ```csharp
     // Input System cleanup messages
     LogAssert.Expect(LogType.Assert, "This will cause a leak and performance issues, GameControls.Player.Disable() has not been called.");
     
     // Unity lifecycle messages
     LogAssert.Expect(LogType.Warning, "DontDestroyOnLoad only works for root GameObjects or components on root GameObjects.");
     ```
   - Place expectations in test setup
   - Document why each message is expected
   - Update expectations when Unity or package versions change

6. Scene Cleanup Best Practices
   - Implement thorough scene cleanup:
     ```csharp
     private IEnumerator CleanupExistingTestScenes()
     {
         int sceneCount = SceneManager.sceneCount;
         for (int i = 0; i < sceneCount; i++)
         {
             var scene = SceneManager.GetSceneAt(i);
             if (scene.name.StartsWith("TestScene"))
             {
                 yield return SceneManager.UnloadSceneAsync(scene);
             }
         }
         // Force cleanup
         System.GC.Collect();
         GC.WaitForPendingFinalizers();
         yield return null;
     }
     ```
   - Call cleanup in both setup and teardown
   - Handle cleanup failures gracefully
   - Document cleanup requirements

## Scene Management Best Practices

### Scene Name Uniqueness
```csharp
// RECOMMENDED - Comprehensive unique scene name generation
private string GetUniqueSceneName()
{
    return $"TestScene_{DateTime.Now.Ticks}_{Process.GetCurrentProcess().Id}" +
           $"_{Thread.CurrentThread.ManagedThreadId}_{Guid.NewGuid().ToString("N")}";
}
```

Key components for unique scene names:
1. Timestamp (Ticks)
2. Process ID
3. Thread ID
4. Random GUID
5. Descriptive prefix

### Scene Cleanup
```csharp
// RECOMMENDED - Robust scene cleanup with error handling
private IEnumerator CleanupTestScenes()
{
    bool unloadError = false;
    string errorMessage = string.Empty;
    
    foreach (var scene in GetTestScenes())
    {
        try
        {
            var operation = SceneManager.UnloadSceneAsync(scene);
            while (!operation.isDone) yield return null;
        }
        catch (Exception e)
        {
            unloadError = true;
            errorMessage = e.Message;
            Debug.LogError($"Scene cleanup failed: {e.Message}");
        }
    }
    
    if (unloadError) throw new Exception($"Cleanup failed: {errorMessage}");
    
    // Thorough cleanup
    Resources.UnloadUnusedAssets();
    System.GC.Collect();
    GC.WaitForPendingFinalizers();
    yield return null;
}
```

Important cleanup steps:
1. Track errors with boolean flags
2. Handle exceptions per scene
3. Unload unused assets
4. Force garbage collection
5. Wait for finalizers

### Memory Management
- Monitor memory usage patterns
- Track baseline and final states
- Clean up resources promptly
- Handle static resources properly

## Compliance Requirements
This document is part of the project's compliance requirements. All tests must:
1. Follow these guidelines
2. Pass all test runner checks
3. Maintain proper cleanup
4. Handle errors appropriately
5. Document any deviations

See [compliance.mdc](mdc:Documentation/Guidelines/compliance.mdc) for full compliance requirements.

## PlayerController Testing Best Practices

### Persistent Root Pattern
```csharp
private static GameObject persistentRoot;
private static bool isOneTimeSetupComplete;

[UnitySetUp]
public IEnumerator UnityOneTimeSetup()
{
    if (!isOneTimeSetupComplete)
    {
        // Create a persistent root object
        persistentRoot = new GameObject("[TestRoot]");
        Object.DontDestroyOnLoad(persistentRoot);
        
        // Parent test objects to persistent root
        playerObject.transform.SetParent(persistentRoot.transform);
        
        isOneTimeSetupComplete = true;
    }
    yield return null;
}
```

Key benefits:
1. Maintains consistent object hierarchy
2. Prevents scene-related GameObject issues
3. Simplifies cleanup process
4. Avoids DontDestroyOnLoad warnings

### Input System Testing
```csharp
public class PlayerControllerTests : InputTestFixture
{
    private GameControls controls;
    private Keyboard keyboard;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        
        // Initialize input devices
        keyboard = InputSystem.AddDevice<Keyboard>();
        InputSystem.Update();

        // Initialize and enable controls
        controls = new GameControls();
        controls.Enable();
    }

    private void CleanupTestObjects()
    {
        if (controls != null)
        {
            controls.Player.Disable();
            controls.Disable();
            controls.Dispose();
            controls = null;
        }

        if (keyboard != null)
        {
            InputSystem.RemoveDevice(keyboard);
            keyboard = null;
        }
    }
}
```

Important aspects:
1. Inherit from InputTestFixture
2. Proper device initialization
3. Thorough cleanup sequence
4. Null checks in cleanup

### Test Environment Setup
```csharp
private IEnumerator SetupTestEnvironment()
{
    // Create test scene if needed
    testScene = SceneManager.CreateScene("PlayerTestScene");
    SceneManager.SetActiveScene(testScene);
    
    // Verify object hierarchy
    Assert.That(playerObject.transform.parent, Is.EqualTo(persistentRoot.transform));
    
    // Initialize game state
    if (GameManager.Instance.CurrentGameState != GameManager.GameState.Playing)
    {
        GameManager.Instance.StartGame();
    }
    
    yield return new WaitForSeconds(0.1f);
    
    // Verify setup
    Assert.That(playerController.enabled, Is.True);
    Assert.That(GameManager.Instance.CurrentGameState, 
                Is.EqualTo(GameManager.GameState.Playing));
}
```

Best practices:
1. Maintain consistent scene state
2. Verify object relationships
3. Initialize game state properly
4. Add appropriate verification
5. Use minimal wait times

### Movement Testing Pattern
```csharp
[UnityTest]
public IEnumerator PlayerController_Movement_RespondsToInput()
{
    yield return SetupTestEnvironment();
    
    // Simulate input
    var input = new Vector2(1f, 0f);
    #if UNITY_EDITOR || DEVELOPMENT_BUILD
    playerController.TestInput(input);
    #endif
    
    // Wait for physics
    yield return new WaitForFixedUpdate();
    yield return new WaitForFixedUpdate();
    
    // Verify movement
    var rb = playerController.GetComponent<Rigidbody2D>();
    Assert.That(rb.linearVelocity.x, Is.GreaterThan(0f));
    
    yield return TearDownTestEnvironment();
}
```

Testing considerations:
1. Use SetupTestEnvironment for consistent initialization
2. Implement proper input simulation
3. Account for physics timing
4. Verify expected behavior
5. Clean up properly

### Common Issues and Solutions

1. **GameObject Hierarchy Issues**
   - Use persistent root object
   - Maintain parent-child relationships
   - Avoid scene transitions when possible
   - Verify hierarchy in setup and tests

2. **Input System Cleanup**
   - Disable action maps before disposal
   - Remove input devices properly
   - Clear all references
   - Force cleanup in teardown

3. **Physics Testing**
   - Use WaitForFixedUpdate for physics updates
   - Account for physics initialization time
   - Verify physics properties directly
   - Reset physics state between tests

4. **Game State Management**
   - Initialize game state explicitly
   - Verify state transitions
   - Clean up state in teardown
   - Handle dependencies properly

5. **Resource Management**
   - Use immediate destruction (DestroyImmediate)
   - Clear all references
   - Force garbage collection
   - Verify cleanup completion

## Object Pooling Test Best Practices

### Pool Stress Testing
```csharp
[UnityTest]
public IEnumerator Pool_StressTest()
{
    // 1. Setup phase
    int targetCount = 10;
    float spawnInterval = 0.1f;
    float testTimeout = 5.0f;
    
    // 2. Initialize pool with sufficient capacity
    poolManager.Initialize(targetCount, targetCount * 2);
    yield return null;
    
    // 3. Track timing and progress
    float elapsedTime = 0f;
    int spawnedCount = 0;
    
    while (elapsedTime < testTimeout && spawnedCount < targetCount)
    {
        // 4. Spawn attempt with verification
        if (poolManager.TrySpawnObject(out var spawnedObject))
        {
            spawnedCount++;
            Assert.That(spawnedObject, Is.Not.Null, 
                       $"Spawned object {spawnedCount} should not be null");
        }
        
        // 5. Controlled timing
        yield return new WaitForSeconds(spawnInterval);
        elapsedTime += spawnInterval;
    }
    
    // 6. Final verification
    Assert.That(spawnedCount, Is.EqualTo(targetCount), 
                $"Failed to spawn {targetCount} objects. Only spawned {spawnedCount}");
}
```

Key requirements:
1. **Initialization**
   - Pre-allocate sufficient pool capacity
   - Verify initial pool state
   - Document pool configuration

2. **Performance Monitoring**
   - Track spawn timing
   - Monitor memory usage
   - Log pool statistics

3. **Error Handling**
   - Handle pool expansion
   - Track failed spawns
   - Provide detailed failure context

4. **Cleanup**
   - Return objects to pool
   - Reset pool state
   - Verify final cleanup

### Common Pool Testing Issues

1. **Capacity Management**
   ```csharp
   // INCORRECT
   pool.Initialize(5, 10); // Too small for stress test
   
   // CORRECT
   pool.Initialize(targetCount, targetCount * 2);
   ```

2. **Timing Control**
   ```csharp
   // INCORRECT
   yield return null; // Inconsistent timing
   
   // CORRECT
   yield return new WaitForSeconds(spawnInterval);
   ```

3. **Progress Tracking**
   ```csharp
   // RECOMMENDED
   private void LogPoolStats(string phase)
   {
       Debug.Log($"[Pool Stats] {phase}" +
                 $"\nActive: {pool.ActiveCount}" +
                 $"\nAvailable: {pool.AvailableCount}" +
                 $"\nTotal: {pool.TotalCount}");
   }
   ```

4. **Memory Management**
   ```csharp
   // RECOMMENDED
   private IEnumerator VerifyPoolMemory()
   {
       long beforeMem = GC.GetTotalMemory(false);
       yield return null;
       
       // Perform pool operations
       
       long afterMem = GC.GetTotalMemory(true);
       Assert.That(afterMem - beforeMem, Is.LessThan(maxMemoryDelta),
           "Pool operations exceeded memory threshold");
   }
   ```

### Best Practices for Pool Testing

1. **Configuration**
   - Set realistic pool sizes
   - Configure appropriate timeouts
   - Document performance expectations

2. **Verification**
   - Check object validity
   - Verify pool statistics
   - Monitor resource usage

3. **Cleanup**
   - Return all objects
   - Reset pool state
   - Verify memory cleanup

4. **Documentation**
   - Log pool operations
   - Track performance metrics
   - Document test assumptions 

## Test Structure Verification
When implementing or reviewing tests, verify the following aspects to ensure tests are passable:

### 1. Scope and Constants Management
```csharp
// INCORRECT - Constants defined within test method
public IEnumerator StressTest()
{
    const int targetCount = 10;
    const float spawnInterval = 0.2f;
    // ... test using these constants
    for (int i = 0; i < maxCycles; i++) // ERROR: maxCycles not in scope
    {
        // ... test logic
    }
}

// CORRECT - Constants defined at class level
public class SystemTests
{
    // Test configuration constants
    private const int TARGET_COUNT = 10;
    private const float SPAWN_INTERVAL = 0.2f;
    private const float CYCLE_TIMEOUT = 10.0f;
    private const int MAX_CYCLES = 3;
    private const float MAX_MEMORY_DELTA = 50f;

    public IEnumerator StressTest()
    {
        // Constants now accessible throughout the test
        for (int i = 0; i < MAX_CYCLES; i++)
        {
            // ... test logic
        }
    }
}
```

Key considerations:
1. Define test configuration constants at class level
2. Use proper C# naming conventions for constants (ALL_CAPS)
3. Document the purpose and expected values of constants
4. Consider making constants public if they need to be referenced by other test classes

### 2. Test State Management
```csharp
// RECOMMENDED - Proper state tracking
public IEnumerator ComplexTest()
{
    bool setupSuccess = false;
    bool testInProgress = false;
    
    // Setup phase with error handling
    try
    {
        // Setup code
        setupSuccess = true;
    }
    catch (Exception e)
    {
        Debug.LogError($"Setup failed: {e.Message}");
        Assert.Fail($"Test setup failed: {e.Message}");
        yield break;
    }

    if (setupSuccess)
    {
        yield return SetupEnvironment();
        testInProgress = true;
    }

    // Main test execution
    if (testInProgress)
    {
        // Test logic
    }

    // Always cleanup
    yield return CleanupEnvironment();
}
```

Important aspects:
1. Track setup and test progress states
2. Handle errors appropriately at each phase
3. Ensure cleanup runs regardless of test outcome
4. Use clear state flags to control test flow

### 3. Performance Test Structure
For performance-sensitive tests:
```csharp
[UnityTest]
#if UNITY_INCLUDE_PERFORMANCE_TESTING
[Performance]
#endif
public IEnumerator PerformanceTest()
{
    // Initial metrics
    float startMemory = GetTotalMemoryMB();
    float startTime = Time.time;

    // Test execution with timeout
    const float timeout = 10.0f;
    bool completed = false;
    while (Time.time - startTime < timeout && !completed)
    {
        // Test logic
        yield return new WaitForSeconds(0.1f);
    }

    // Verify performance metrics
    float memoryDelta = GetTotalMemoryMB() - startMemory;
    Assert.That(memoryDelta, Is.LessThan(50f), 
        $"Memory usage exceeded threshold: {memoryDelta:F2}MB");
}
```

Performance test requirements:
1. Track and verify memory usage
2. Implement appropriate timeouts
3. Log detailed performance metrics
4. Use consistent intervals for operations
5. Include cleanup between test cycles

### 4. Test Verification Checklist
Before implementing or submitting tests, verify:

#### Structure
- [ ] Constants and configuration properly scoped
- [ ] State tracking implemented correctly
- [ ] Error handling follows guidelines
- [ ] Cleanup procedures are robust

#### Timing
- [ ] Appropriate timeouts for operations
- [ ] Sufficient intervals between actions
- [ ] Proper yield statements placement
- [ ] No yields within try-catch blocks

#### Resources
- [ ] Memory usage tracked and verified
- [ ] Objects properly cleaned up
- [ ] Scene management handled correctly
- [ ] Input system properly initialized/cleaned

#### Documentation
- [ ] Test purpose clearly stated
- [ ] Constants documented
- [ ] Performance requirements specified
- [ ] Error scenarios documented

### 5. Common Test Issues and Solutions

#### Scope Issues
```csharp
// Problem: Constants not accessible
Assets\_Project\Tests\PlayMode\Core\Enemy\EnemySystemPlayTests.cs(283,45): 
error CS0103: The name 'maxCycles' does not exist in the current context

// Solution: Move constants to class level
public class TestClass
{
    private const int MAX_CYCLES = 3;
    // ... test methods
}
```

#### Try-Catch with Yields
```csharp
// INCORRECT
try
{
    yield return SomeOperation(); // CS1626 Error
}
catch (Exception e)
{
    Debug.LogError(e);
}

// CORRECT
bool operationSuccess = false;
try
{
    operationSuccess = StartOperation();
}
catch (Exception e)
{
    Debug.LogError(e);
}

if (operationSuccess)
{
    yield return WaitForOperation();
}
```

#### Memory Management
```csharp
// RECOMMENDED
private IEnumerator PerformanceTest()
{
    float initialMemory = GetTotalMemoryMB();
    
    // Test operations
    
    float finalDelta = GetTotalMemoryMB() - initialMemory;
    Assert.That(finalDelta, Is.LessThan(MAX_MEMORY_DELTA),
        $"Memory leak detected: {finalDelta:F2}MB");
}
```

### 6. Test Lifecycle Best Practices

#### Setup Phase
```csharp
[UnitySetUp]
public IEnumerator Setup()
{
    // 1. Scene setup
    testScene = SceneManager.CreateScene(GetUniqueSceneName());
    SceneManager.SetActiveScene(testScene);

    // 2. Component initialization
    try
    {
        // Initialize components
    }
    catch (Exception e)
    {
        Debug.LogError($"Setup failed: {e.Message}");
        yield break;
    }

    // 3. Wait for initialization
    yield return null;
}
```

#### Teardown Phase
```csharp
[UnityTearDown]
public IEnumerator Teardown()
{
    // 1. Stop ongoing operations
    if (spawner != null)
    {
        spawner.StopSpawning();
        spawner.DespawnAllEnemies();
    }

    // 2. Scene cleanup
    if (testScene.isLoaded)
    {
        yield return SceneManager.UnloadSceneAsync(testScene);
    }

    // 3. Force cleanup
    System.GC.Collect();
    yield return null;
}
```

Remember: These guidelines are critical for maintaining test reliability and preventing false failures. Always verify test structure before implementation and review. 

## Cleanup Message Verification
### Input System Cleanup Messages
Unity 6.0's Input System generates both synchronous and asynchronous cleanup messages. Tests must properly handle both types:

```csharp
public class InputSystemTests : InputTestFixture
{
    private static bool cleanupMessageReceived = false;
    private static readonly string[] ExpectedMessages = new[]
    {
        // Synchronous cleanup messages
        "This will cause a leak and performance issues, GameControls.Player.Disable() has not been called.",
        "Input System disabled",
        // Asynchronous cleanup messages
        "Input System cleanup completed",
        // Scene-related messages that may appear during cleanup
        "DontDestroyOnLoad only works for root GameObjects or components on root GameObjects."
    };

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        cleanupMessageReceived = false;
        Application.logMessageReceived += OnLogMessageReceived;
        LogAssert.ignoreFailingMessages = true;
    }

    private void OnLogMessageReceived(string logString, string stackTrace, LogType type)
    {
        // Check both Assert and Log types for cleanup messages
        if ((type == LogType.Assert || type == LogType.Log) && 
            ExpectedMessages.Any(msg => logString.Contains(msg)))
        {
            cleanupMessageReceived = true;
            Debug.Log($"[Test] Cleanup message received: {logString}");
        }
    }

    [SetUp]
    public override void Setup()
    {
        // Reset cleanup flag for each test
        cleanupMessageReceived = false;
        base.Setup();
    }

    [TearDown]
    public override void TearDown()
    {
        try
        {
            if (controls != null)
            {
                controls.Player.Disable();
                Debug.Log("[Test] Input System disabled");
                controls.Disable();
                controls.Dispose();
                controls = null;
            }
            
            base.TearDown();
            
            // Allow time for async cleanup messages
            System.Threading.Thread.Sleep(100);
            
            Assert.That(cleanupMessageReceived, Is.True, 
                "Input system cleanup message should have been received during test execution");
        }
        catch (Exception e)
        {
            Debug.LogError($"TearDown failed: {e.Message}");
            throw;
        }
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Application.logMessageReceived -= OnLogMessageReceived;
        LogAssert.ignoreFailingMessages = false;
    }
}
```

Key Points for Cleanup Message Verification:
1. **Message Types**:
   - Monitor both `LogType.Assert` and `LogType.Log` messages
   - Include both immediate and async cleanup messages in expectations
   - Document all expected message patterns

2. **Timing Considerations**:
   - Reset verification flag at test start
   - Allow window for async messages in teardown
   - Use small delay after cleanup operations
   - Handle both immediate and delayed cleanup notifications

3. **State Management**:
   - Track cleanup state across test lifecycle
   - Reset state for each test
   - Verify cleanup completion in teardown
   - Handle state transitions

4. **Error Handling**:
   - Log received cleanup messages for debugging
   - Provide clear failure messages
   - Include context in error reports
   - Handle cleanup failures gracefully

### Common Cleanup Verification Issues

1. **Missing Async Messages**
   ```csharp
   // INCORRECT - Only checks Assert messages
   if (type == LogType.Assert && ExpectedMessages.Any(msg => logString.Contains(msg)))
   
   // CORRECT - Checks both Assert and Log messages
   if ((type == LogType.Assert || type == LogType.Log) && 
       ExpectedMessages.Any(msg => logString.Contains(msg)))
   ```

2. **Timing Issues**
   ```csharp
   // INCORRECT - No time for async messages
   controls.Dispose();
   Assert.That(cleanupMessageReceived, Is.True);
   
   // CORRECT - Allows time for async messages
   controls.Dispose();
   System.Threading.Thread.Sleep(100);
   Assert.That(cleanupMessageReceived, Is.True);
   ```

3. **Incomplete Message Patterns**
   ```csharp
   // INCORRECT - Missing async cleanup messages
   private static readonly string[] ExpectedMessages = new[]
   {
       "GameControls.Player.Disable() has not been called"
   };
   
   // CORRECT - Includes all cleanup message patterns
   private static readonly string[] ExpectedMessages = new[]
   {
       "GameControls.Player.Disable() has not been called",
       "Input System disabled",
       "Input System cleanup completed"
   };
   ```

4. **State Management**
   ```csharp
   // INCORRECT - State not reset between tests
   [SetUp]
   public override void Setup()
   {
       base.Setup();
   }
   
   // CORRECT - State reset for each test
   [SetUp]
   public override void Setup()
   {
       cleanupMessageReceived = false;
       base.Setup();
   }
   ```

### Best Practices for Cleanup Verification

1. **Message Monitoring**:
   - Define comprehensive message patterns
   - Include both sync and async messages
   - Document message timing expectations
   - Log received messages for debugging

2. **State Tracking**:
   - Reset state at test start
   - Track cleanup progress
   - Verify final state
   - Handle state transitions

3. **Timing Management**:
   - Account for async operations
   - Use appropriate delays
   - Handle cleanup order
   - Verify completion timing

4. **Error Handling**:
   - Provide clear error messages
   - Include cleanup context
   - Handle partial cleanup
   - Log cleanup progress

Remember: These guidelines are critical for maintaining test reliability and preventing false failures. Always verify test structure before implementation and review. 