# Core Game Loop

## Overview

This document provides a comprehensive checklist and overview of the Core Game Loop implementation in CZGAME. The game loop is the central system responsible for updating all game elements in the correct order and ensuring consistent gameplay across different hardware configurations.

## Core Game Loop Architecture

### Primary Components

The Core Game Loop is composed of the following components:

1. **Time Management**
   - Frame timing and delta time calculation
   - Time scale adjustments
   - Fixed timestep handling

2. **Input Processing**
   - Input polling and event processing
   - Input buffering for responsive controls
   - Input mapping to game actions

3. **Game State Update**
   - Player character update
   - Enemy AI update
   - Physics simulation
   - Collision resolution

4. **Rendering**
   - Camera positioning
   - Scene rendering
   - UI rendering
   - Post-processing effects

5. **Audio Processing**
   - Sound effect triggering
   - Music playback and transitions
   - Audio mixing

## Implementation Checklist

### Time Management ✓

- [x] **Implement TimeManager singleton**
  - [x] Frame delta time calculation
  - [x] Fixed update time step (defaulted to 0.02s - 50 updates/sec)
  - [x] Time scale control (for slow-motion effects)
  - [x] Time statistics tracking (FPS, frame time)

- [x] **Configure Unity Time settings**
  - [x] Fixed timestep: 0.02 seconds
  - [x] Maximum allowed timestep: 0.1 seconds
  - [x] Time scale: 1.0 (default)
  - [x] Maximum particle timestep: 0.03 seconds

- [x] **Implement frame limiting options**
  - [x] VSync options (0, 1, 2)
  - [x] Target framerate settings
  - [x] Frame pacing options

### Input Processing ✓

- [x] **Configure Input System**
  - [x] Define input actions
  - [x] Create input action asset
  - [x] Set up input action references

- [x] **Implement InputManager**
  - [x] Initialize input system
  - [x] Register action callbacks
  - [x] Provide centralized input access

- [x] **Input Buffering System**
  - [x] Configure buffer window (100ms default)
  - [x] Store recent inputs for responsive controls
  - [x] Clear buffer on specified conditions

### Game State Update ✓

- [x] **Implement GameManager**
  - [x] Game state machine (Menu, Playing, Paused, GameOver)
  - [x] Scene loading and transitions
  - [x] Game session data management

- [x] **Create UpdateManager**
  - [x] Register updateable objects
  - [x] Manage update priority
  - [x] Batch similar updates

- [x] **Physics Integration**
  - [x] Configure Physics2DSetup
  - [x] Implement collision listeners
  - [x] Set up collision layers and masks

### Entity Update System ✓

- [x] **Player Character Updates**
  - [x] Movement and controls
  - [x] Animation state machine
  - [x] Ability system integration

- [x] **Enemy AI Updates**
  - [x] Behavior tree execution
  - [x] Pathfinding calculations
  - [x] Decision making logic

- [x] **Environment Updates**
  - [x] Hazard timing
  - [x] Moving platforms
  - [x] Interactive elements

### Rendering Pipeline ✓

- [x] **Camera System**
  - [x] Camera following logic
  - [x] Screen shake effects
  - [x] Camera zones and transitions

- [x] **Rendering Manager**
  - [x] Layer sorting
  - [x] Render queue management
  - [x] Culling optimization

- [x] **Post-Processing**
  - [x] Configure URP volume profiles
  - [x] Effect transitions during gameplay
  - [x] Performance presets

### Performance Monitoring ✓

- [x] **Implement PerformanceMonitor**
  - [x] Track frame time
  - [x] Memory usage tracking
  - [x] System bottleneck identification

- [x] **Configure Profiling Tools**
  - [x] Unity Profiler integration
  - [x] Custom profiling markers
  - [x] Performance logging

- [x] **Add Debug Visualization**
  - [x] FPS counter
  - [x] Frame time graph
  - [x] Memory usage display

## Game Loop Timing

### Update Sequence

1. **Input Processing** (Beginning of frame)
   - Poll for new inputs
   - Process input events
   - Update input buffer

2. **Fixed Update** (Physics time step - 50Hz)
   - Physics simulation
   - Character controllers
   - Collision detection and resolution
   - Deterministic gameplay systems

3. **Variable Update** (Every frame)
   - Camera updates
   - Visual effects
   - Animation blending
   - Non-physics dependent systems

4. **Late Update** (End of frame)
   - Camera final positioning
   - UI updates
   - Post-rendering effects
   - Data logging

### Critical Timing Considerations

- **Frame Budget**: 16.67ms (60 FPS)
  - Physics: 3ms
  - Game Logic: 5ms
  - Rendering: 6ms
  - Post-processing: 2ms
  - Reserve: 0.67ms

- **Heavy Operations**:
  - Pathfinding: Schedule across multiple frames
  - Physics raycasts: Batch and minimize
  - Garbage collection: Monitor and minimize allocations

## Advanced Features

### Pause System ✓

- [x] **Game Pause Implementation**
  - [x] Time scale adjustment
  - [x] Input processing during pause
  - [x] Pause menu UI

- [x] **Selective Pausing**
  - [x] Critical systems that continue during pause
  - [x] Visual effects during pause (blur, color grading)

### Game State Transitions ⚠️

- [ ] **Scene Loading**
  - [x] Asynchronous loading
  - [x] Loading screen progress display
  - [ ] Scene transition effects (fade, etc.)

- [ ] **Game State Changes**
  - [x] State machine implementation
  - [ ] State change events and callbacks
  - [ ] Persistent state across scenes

### Time Manipulation ✓

- [x] **Slow Motion Effects**
  - [x] Time scale adjustment
  - [x] Affected/unaffected system separation
  - [x] Visual enhancements during slow motion

- [x] **Time Rewinding**
  - [x] State recording system
  - [x] Playback mechanism
  - [x] Visual indicators

## Integration with Other Systems

### Object Pooling System ✓

- [x] **Pool Initialization**
  - [x] Pre-warm pools during loading
  - [x] Configure initial pool sizes
  - [x] Set expansion policies

- [x] **Runtime Management**
  - [x] Request and return objects to pools
  - [x] Pool cleanup during scene transitions
  - [x] Memory management during gameplay

### Event System ✓

- [x] **Event Registration**
  - [x] Register for gameplay events
  - [x] Event priority handling
  - [x] Event queue management

- [x] **Event Processing**
  - [x] Process events during update phases
  - [x] Batch similar events
  - [x] Ensure deterministic event handling

### Save System ⚠️

- [ ] **State Serialization**
  - [x] Serialize game state
  - [ ] Compress save data
  - [ ] Encrypt sensitive data

- [ ] **Loading Process**
  - [x] Deserialize game state
  - [ ] Validate save data
  - [ ] Handle corrupted saves

## Performance Optimizations

### CPU Optimizations

- **Multithreading**:
  - Pathfinding calculations
  - Asset loading
  - Physics processing (where possible)

- **Update Frequency**:
  - Throttle distant object updates
  - Utilize coroutines for distributed processing
  - Implement LOD for update frequency

### Memory Optimizations

- **Object Pooling**:
  - Pre-allocate commonly used objects
  - Recycle objects instead of destroying/creating
  - Monitor pool sizes and adjust as needed

- **Asset Management**:
  - Unload unused assets during scene transitions
  - Use addressable assets for dynamic loading
  - Implement streaming for large levels

### Unity-specific Optimizations

- **Batching**:
  - Enable static batching for immobile objects
  - Configure dynamic batching thresholds
  - Use GPU instancing for repeated objects

- **Culling**:
  - Configure occlusion culling
  - Implement custom frustum culling for complex systems
  - Use layer-based culling masks

## Related Documentation

- [Object Pooling System](../Systems/ObjectPooling.md)
- [Physics System](../Systems/Physics.md)
- [Performance Guidelines](../Performance/PerformanceGuidelines.md)
- [Infrastructure Plan](Infrastructure.md)
- [Assembly Structure](AssemblyStructure.md)

## Revision History

| Date | Version | Changes |
|------|---------|---------|
| 2025-02-14 | 1.0 | Initial checklist creation |
| 2025-02-27 | 1.1 | Converted to Markdown format with cross-references |

Last Updated: 2025-02-27
Unity Version: 6000.0.38f1 