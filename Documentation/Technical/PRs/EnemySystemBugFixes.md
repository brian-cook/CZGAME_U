# Pull Request: Enemy System Bug Fixes

## Description
This PR addresses several compiler errors and missing functionality in the Enemy system. The fixes improve the integration between the Wave Manager and Enemy classes, as well as fixing references to the Player namespace and assembly definitions.

## Changes Made

### 1. Fixed Assembly Reference for Player Namespace
- Added "CZ.Core.Player" to the references list in the CZ.Core.Enemy.asmdef file
- This resolves the error where the Player namespace couldn't be found in the CZ.Core namespace

### 2. Fixed Missing Player Namespace in SwiftEnemyController
- Added `using CZ.Core.Player;` directive to SwiftEnemyController.cs
- This resolves errors related to accessing Player.Projectile and Player.PlayerController

### 3. Added OnEnemyDefeated Event System
- Added delegate and event for enemy defeat to BaseEnemy class:
  ```csharp
  public delegate void EnemyDefeatedHandler(BaseEnemy enemy);
  public event EnemyDefeatedHandler OnEnemyDefeated;
  ```
- Triggered the event in the CompleteDeathSequence method before the enemy is returned to pool
- Added proper error handling for the death sequence

### 4. Fixed SetTarget Parameter Type in WaveManager
- Modified WaveManager.cs to pass `playerTransform.position` instead of `playerTransform` to the SetTarget method
- This resolves the type mismatch between Transform and Vector3

### 5. Resolved Random Ambiguity
- Fixed the ambiguity errors in `BaseEnemy.cs` by fully qualifying all `Random` references with `UnityEngine.Random` to avoid conflict with `System.Random`.

### 6. Fixed FixedUpdate Inheritance in BaseEnemy
- Changed the `FixedUpdate()` method in `BaseEnemy` from `private` to `protected virtual`
- This allows derived classes to properly override the movement behavior in tests and specialized enemy classes
- Resolves CS0115 errors in test classes that were attempting to override the method

### 7. Fixed Swift Enemy Tests
- Modified `SwiftEnemyTests.cs` to pass `target.position` instead of `target` to the `SetTarget` method
- Added a `CurrentTarget` property to `SwiftEnemyController` for testing purposes
- Added a `SetTargetTransformForTesting` method to store references to target game objects for proper test cleanup
- This resolves both the type mismatch error and the missing property errors in the test file

## Issues Fixed
The following compilation errors have been resolved:
```
Assets\_Project\Scripts\Core\Enemy\SwiftEnemyController.cs(5,15): error CS0234: The type or namespace name 'Player' does not exist in the namespace 'CZ.Core' (are you missing an assembly reference?)

Assets\_Project\Scripts\Core\Enemy\SwiftEnemyController.cs(216,59): error CS0246: The type or namespace name 'Player' could not be found (are you missing a using directive or an assembly reference?)

Assets\_Project\Scripts\Core\Enemy\SwiftEnemyController.cs(217,55): error CS0246: The type or namespace name 'Player' could not be found (are you missing a using directive or an assembly reference?)

Assets\_Project\Scripts\Core\Enemy\WaveManager.cs(470,33): error CS1503: Argument 1: cannot convert from 'UnityEngine.Transform' to 'UnityEngine.Vector3'

Assets\_Project\Scripts\Core\Enemy\WaveManager.cs(474,19): error CS1061: 'BaseEnemy' does not contain a definition for 'OnEnemyDefeated' and no accessible extension method 'OnEnemyDefeated' accepting a first argument of type 'BaseEnemy' could be found (are you missing a using directive or an assembly reference?)

Assets\_Project\Scripts\Core\Enemy\WaveManager.cs(492,23): error CS1061: 'BaseEnemy' does not contain a definition for 'OnEnemyDefeated' and no accessible extension method 'OnEnemyDefeated' accepting a first argument of type 'BaseEnemy' could be found (are you missing a using directive or an assembly reference?)

Assets\_Project\Scripts\Core\Enemy\WaveManager.cs(540,23): error CS1061: 'BaseEnemy' does not contain a definition for 'OnEnemyDefeated' and no accessible extension method 'OnEnemyDefeated' accepting a first argument of type 'BaseEnemy' could be found (are you missing a using directive or an assembly reference?)

Assets\_Project\Scripts\Core\Enemy\BaseEnemy.cs(10,15): error CS0029: Cannot implicitly convert type 'UnityEngine.Transform' to 'UnityEngine.Vector3'

Assets\_Project\Scripts\Core\Enemy\BaseEnemy.cs(10,15): error CS0104: 'Random' is an ambiguous reference between 'UnityEngine.Random' and 'System.Random'

Assets\_Project\Tests\EditMode\Core\Enemy\WaveManagerTests.cs(206,37): error CS0115: 'WaveManagerTests.MockBaseEnemy.FixedUpdate()': no suitable method found to override

Assets\_Project\Tests\EditMode\Core\Enemy\SwiftEnemyTests.cs(49,34): error CS1503: Argument 1: cannot convert from 'UnityEngine.Transform' to 'UnityEngine.Vector3'

Assets\_Project\Tests\EditMode\Core\Enemy\SwiftEnemyTests.cs(62,28): error CS1061: 'SwiftEnemyController' does not contain a definition for 'CurrentTarget' and no accessible extension method 'CurrentTarget' accepting a first argument of type 'SwiftEnemyController' could be found (are you missing a using directive or an assembly reference?)

Assets\_Project\Tests\EditMode\Core\Enemy\SwiftEnemyTests.cs(64,52): error CS1061: 'SwiftEnemyController' does not contain a definition for 'CurrentTarget' and no accessible extension method 'CurrentTarget' accepting a first argument of type 'SwiftEnemyController' could be found (are you missing a using directive or an assembly reference?)
```

## Technical Details
- Added proper assembly reference between Enemy and Player assemblies
- Added proper event system for enemy defeat notifications
- Improved error handling in the death sequence
- Fixed parameter type mismatches in method calls
- Added required namespace references
- Fully qualified Random calls to use UnityEngine.Random
- Modified method accessibility for proper inheritance and polymorphism
- Added testing support properties and methods to maintain test functionality

## Testing Done
- Verified that all compiler errors are resolved
- Confirmed that event handling maintains the proper lifecycle of enemy objects
- Ensured proper parameter types are used in method calls
- Validated assembly references are correctly set up
- Tested inheritance relationships in unit test mocks
- Verified that test teardown properly cleans up created objects

## Related Documentation
- This PR works in conjunction with the previous PR "SwiftEnemyMethodOverridesFix"
- Follows C# best practices for events and error handling
- Maintains Unity component lifecycle patterns
- Follows Unity assembly definition reference best practices
- Implements testing best practices for Unity test fixtures

## Dependencies
- CZ.Core.Player assembly 