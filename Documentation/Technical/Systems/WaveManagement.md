# Wave Management System

This document provides information about the Wave Management System and its implementation details.

## Overview

The Wave Management System is responsible for spawning enemies in waves with increasing difficulty. It provides a configurable system for defining waves, enemy types, spawn behavior, and difficulty progression.

## Key Features

- **Wave Configuration**: Define waves with specific enemy counts, spawn intervals, and difficulty multipliers
- **Enemy Type Management**: Configure different enemy types with custom spawn weights and difficulty scaling
- **Special Enemy Spawning**: Schedule special/elite enemies to appear at specific points during a wave
- **Difficulty Progression**: Automatically scale difficulty across waves with configurable parameters
- **Event System**: Comprehensive events for wave start/end and enemy spawn/defeat
- **Object Pooling**: Efficient object pooling for enemy instances
- **Custom Editor**: Intuitive editor interface for configuring the Wave Manager

## Architecture

The Wave Management System consists of the following components:

1. **WaveManager**: The core component that manages waves, spawning, and game flow
2. **Wave Configuration**: Data structures defining wave properties and enemy distributions
3. **Editor Extensions**: Custom editor UI for easy configuration
4. **Object Pooling**: Efficient object reuse for enemy instances

## Wave Manager

The `WaveManager` class is the central component of the system, responsible for:

- Managing wave progression
- Spawning enemies according to wave configurations
- Tracking active enemies and wave completion
- Triggering wave-related events
- Managing difficulty scaling

### Inspector Settings

The Wave Manager exposes several configurable properties in the Inspector:

#### Wave Settings
- **Auto Generate Waves**: When enabled, waves are generated automatically based on scaling parameters
- **Wave Configurations**: Manual configuration of waves (when auto generate is disabled)
- **Difficulty Scaling Factor**: How quickly difficulty increases across waves (default: 1.2)
- **Max Wave Count**: Maximum number of waves to generate (default: 20)
- **Loop Final Wave**: Whether to loop the final wave indefinitely (default: true)

#### Enemy Types
- **Enemy Type List**: Configurable list of enemy types with prefabs and spawn parameters:
  - **Enemy Type**: String identifier for the enemy type
  - **Enemy Prefab**: Reference to the enemy GameObject prefab
  - **Initial Pool Size**: Initial object pool size for this enemy type
  - **Max Pool Size**: Maximum object pool size for this enemy type
  - **Spawn Weight**: Base weight for spawn probability
  - **Difficulty Scaling**: How this enemy's weight scales with difficulty

#### Spawn Settings
- **Spawn Points**: Array of Transform points where enemies can spawn
- **Min Distance From Player**: Minimum distance from player for valid spawn points
- **Player Transform**: Reference to the player's transform

#### Debug
- **Show Debug Info**: Display debug information during runtime

#### Events
- **On Wave Start**: Event triggered when a wave starts
- **On Wave Completed**: Event triggered when a wave is completed (includes wave number)
- **On All Waves Completed**: Event triggered when all waves are completed
- **On Enemy Spawned**: Event triggered when an enemy is spawned (includes current/total count)
- **On Enemy Defeated**: Event triggered when an enemy is defeated (includes remaining/total count)

## Wave Configuration

Each wave is defined by a `WaveConfig` structure with the following properties:

- **Wave Number**: Sequential number of the wave
- **Base Enemy Count**: Base number of enemies to spawn in this wave
- **Spawn Interval**: Time between enemy spawns (in seconds)
- **Time Between Waves**: Wait time before the next wave starts (in seconds)
- **Difficulty Multiplier**: Difficulty multiplier affecting enemy count, spawn speed, etc.
- **Special Enemies**: List of special enemies to spawn at specific points during the wave

### Special Enemy Spawns

Special enemies can be scheduled to appear at specific points during a wave:

- **Enemy Type**: Type identifier for the special enemy
- **Spawn Time Percentage**: When to spawn during the wave (0.0 to 1.0, where 0.5 is halfway through)
- **Spawn Position Offset**: Optional offset from the standard spawn position

## Implementation Details

### Wave Generation

When auto-generating waves, the system creates waves with progressively:
- Increasing enemy counts
- Decreasing spawn intervals
- Decreasing time between waves
- Increasing difficulty multipliers

Special enemies are automatically added:
- Tank enemies every 3 waves
- Elite enemies every 5 waves

### Enemy Selection

During spawning, enemies are selected based on weighted random selection:
- Base weights are defined in the enemy type configuration
- Weights are scaled by difficulty using the formula: `weight * pow(difficultyMultiplier, difficultyScaling)`
- Elite and boss enemies are excluded from the random selection (spawned only as special enemies)

### Spawn Position Selection

Spawn positions are selected randomly from the configured spawn points:
- The system attempts to find positions that are at least `minDistanceFromPlayer` away from the player
- It tries up to 10 times to find a valid position
- Falls back to any spawn point if no valid position is found

### Enemy Pooling

The system uses object pooling for efficient enemy management:
- Each enemy type has its own object pool
- Pools are initialized with the specified capacity
- Enemies are returned to the pool when defeated

### Wave Completion

A wave is considered complete when all enemies in the wave have been defeated:
- Base enemies spawned throughout the wave
- Special enemies spawned at their specified times

## Custom Editor

The Wave Manager includes a custom editor for easier configuration:
- Organized sections for different settings
- Validation for required settings (spawn points, enemy prefabs)
- Preview of auto-generated waves
- Runtime controls for starting/skipping waves

## Usage Example

To use the Wave Management System:

1. Create a GameObject in your scene and add the `WaveManager` component
2. Configure at least one enemy type with a valid prefab
3. Add spawn points to the scene and assign them to the Wave Manager
4. Set the player reference
5. Configure wave settings (auto-generate or manual configuration)
6. Connect UI elements to the Wave Manager events if needed
7. Call `StartWaves()` to begin spawning

## Best Practices

- **Enemy Prefabs**: Ensure all enemy prefabs have the appropriate components (BaseEnemy, rigidbody, collider)
- **Spawn Points**: Place spawn points at strategic locations, ideally off-screen or in areas not visible to the player
- **Difficulty Scaling**: Test your game with different difficulty scaling factors to find the right balance
- **Object Pooling**: Set appropriate initial and maximum pool sizes based on your expected enemy counts
- **UI Integration**: Use the provided events to update UI elements with wave information

## Extensibility

The Wave Management System can be extended in several ways:

- **New Enemy Types**: Add new enemy types by creating new prefabs with custom behavior
- **Custom Spawn Patterns**: Modify the `SpawnRoutine` method for different spawn patterns
- **Advanced Wave Configuration**: Extend the `WaveConfig` class with additional parameters
- **Environmental Effects**: Add environment changes triggered by wave events 