# Core Game Loop

## Overview

This document provides a comprehensive checklist and overview of the Core Game Loop implementation in CZGAME. The game loop is the central system responsible for updating all game elements in the correct order and ensuring consistent gameplay across different hardware configurations. This implementation plan aligns with our Project Plan goals for a survival action game with Comfort Zone mechanics.

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
   - Comfort Zone mechanics
   - Physics simulation
   - Collision resolution
   - Progression systems

4. **Rendering**
   - Camera positioning
   - Scene rendering
   - UI rendering
   - Post-processing effects
   - Visual feedback for game events

5. **Audio Processing**
   - Sound effect triggering
   - Music playback and transitions
   - Audio mixing
   - Spatial audio for gameplay feedback

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

### Player Systems ⚠️

- [x] **Basic Movement System**
  - [x] WASD/Arrow key movement
  - [x] Physics-based movement with proper feel
  - [x] Character controller integration
  - [x] Animation system hookup
  
- [x] **Health System**
  - [x] Health management
  - [x] Damage processing
  - [x] Death handling
  - [x] Visual feedback for damage
  
- [ ] **Auto-Attack System**
  - [x] Basic attack implementation
  - [ ] Attack timing and cooldowns
  - [ ] Weapon type integration
  - [ ] Attack hitbox and damage calculation
  
- [ ] **Resource Management**
  - [x] Basic resource tracking
  - [ ] Resource generation/consumption
  - [ ] Resource UI feedback
  - [ ] Resource effects on gameplay
  
- [ ] **Inventory System**
  - [ ] Item representation
  - [ ] Inventory UI
  - [ ] Item pickup and equipping
  - [ ] Item effects implementation

### Comfort Zone Mechanics ⚠️

- [x] **Zone Implementation**
  - [x] Safe zone collider setup
  - [x] Player detection within zones
  - [x] Visual representation of zones
  
- [ ] **Zone Effects**
  - [x] Player visibility to AI while in zones
  - [ ] Resource denial in safe zones
  - [ ] Transition effects using shader graphs
  - [ ] Performance optimization for zone rendering
  
- [ ] **Strategic Zone Placement**
  - [ ] Zone placement system
  - [ ] Zone upgrade system
  - [ ] Zone variation implementation
  - [ ] Zone interaction with environment

- [ ] **AI Interaction with Zones**
  - [x] Basic AI avoidance of zones
  - [ ] NavMesh integration with zones
  - [ ] AI behavior changes near zones
  - [ ] Group AI behaviors around zones

### Enemy Systems ⚠️

- [x] **Enemy AI Updates**
  - [x] Behavior tree execution
  - [x] Pathfinding calculations
  - [x] Decision making logic
  
- [ ] **Enemy Type Implementation**
  - [x] Basic enemies (fast/weak)
  - [ ] Tank enemies (slow/strong)
  - [ ] Ranged enemies
  - [ ] Special/Elite enemies
  - [ ] Boss encounters
  
- [ ] **Spawn System**
  - [x] Basic enemy spawning
  - [ ] Wave management
  - [ ] Difficulty scaling
  - [ ] Zone-based spawning rules
  - [ ] Enemy density control

### Progression Systems ⚠️

- [ ] **Experience System**
  - [ ] XP gain from combat
  - [ ] Level-up mechanics
  - [ ] Player stat scaling
  - [ ] Level-up rewards

- [ ] **Skill/Ability System**
  - [x] Basic ability framework
  - [ ] Skill tree implementation
  - [ ] Ability unlocking
  - [ ] Ability upgrading
  
- [ ] **Meta-Progression**
  - [ ] Permanent upgrades between runs
  - [ ] Currency/resource persistence
  - [ ] Unlock tracking
  - [ ] Save/load for progression data

### Combat Systems ⚠️

- [x] **Damage System**
  - [x] Damage calculation
  - [x] Hit detection
  - [x] Invulnerability frames
  
- [ ] **Weapon System**
  - [x] Basic weapon functionality
  - [ ] Weapon types and variations
  - [ ] Weapon stats and scaling
  - [ ] Weapon visual effects
  
- [ ] **Combat Feedback**
  - [x] Basic hit effects
  - [ ] Damage numbers using TextMeshPro
  - [ ] Screen shake effects
  - [ ] Audio feedback for combat
  - [ ] Particle effects for impacts

### Environment Updates ✓

- [x] **Hazard timing**
  - [x] Hazard activation cycles
  - [x] Hazard damage application
  - [x] Hazard visual feedback

- [x] **Moving platforms**
  - [x] Platform movement patterns
  - [x] Player attachment to platforms
  - [x] Platform collision handling

- [x] **Interactive elements**
  - [x] Trigger zones
  - [x] Activatable objects
  - [x] Destructible elements

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
   - Comfort Zone collision detection
   - AI pathfinding updates

3. **Variable Update** (Every frame)
   - Camera updates
   - Visual effects
   - Animation blending
   - Non-physics dependent systems
   - UI updates
   - Comfort Zone visual effects

4. **Late Update** (End of frame)
   - Camera final positioning
   - UI updates
   - Post-rendering effects
   - Data logging
   - Performance monitoring

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
  - Zone calculations: Optimize for minimal overhead
  - Enemy AI: Use LOD for distant entities

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

### Comfort Zone Manager ⚠️

- [x] **Zone Management**
  - [x] Zone creation and destruction
  - [x] Zone activation/deactivation
  - [x] Zone effect application
  
- [ ] **Strategic System**
  - [ ] Zone placement rules
  - [ ] Zone upgrade system
  - [ ] Zone type variations
  - [ ] Resource integration with zones

## Performance Optimizations

### CPU Optimizations

- **Multithreading**:
  - Pathfinding calculations
  - Asset loading
  - Physics processing (where possible)
  - AI decision making using Jobs System

- **Update Frequency**:
  - Throttle distant object updates
  - Utilize coroutines for distributed processing
  - Implement LOD for update frequency
  - Optimize zone calculations based on distance

### Memory Optimizations

- **Object Pooling**:
  - Pre-allocate commonly used objects
  - Recycle objects instead of destroying/creating
  - Monitor pool sizes and adjust as needed
  - Specific pools for:
    - Projectiles and effects (100 initial, 200 max)
    - Enemies (50 initial, 100 max)
    - VFX/Particles (25 initial, 50 max)
    - UI Elements (50 initial, 100 max)

- **Asset Management**:
  - Unload unused assets during scene transitions
  - Use addressable assets for dynamic loading
  - Implement streaming for large levels

### Unity-specific Optimizations

- **Batching**:
  - Enable static batching for immobile objects
  - Configure dynamic batching thresholds
  - Use GPU instancing for repeated objects
  - Optimize comfort zone rendering

- **Culling**:
  - Configure occlusion culling
  - Implement custom frustum culling for complex systems
  - Use layer-based culling masks
  - Zone-based culling optimization

## Phase Implementation Plan

### Phase 1: Core Mechanics (CURRENT PHASE)

- **Implementation Status**: 80% Complete
  - ✅ Basic player movement
  - ✅ Simple comfort zone implementation
  - ✅ Initial enemy types with pooling
  - ⚠️ Basic combat system (in progress)
  - ⚠️ Core game loop timing (in progress)

### Phase 2: Progression Systems

- **Implementation Status**: 30% Complete
  - ⚠️ Experience/leveling system (in progress)
  - ⚠️ Basic inventory with Unity UI (in progress)
  - ❌ Resource collection (not started)
  - ❌ Initial unlockables (not started)
  - ❌ Meta-progression system (not started)

### Phase 3: Enemy Variety

- **Implementation Status**: 20% Complete
  - ⚠️ Additional enemy types (in progress)
  - ❌ Spawn system refinement (not started)
  - ⚠️ Wave management with pooling (in progress)
  - ❌ Difficulty scaling (not started)
  - ❌ Advanced AI zone awareness (not started)

### Phase 4: Advanced Features

- **Implementation Status**: 10% Complete
  - ⚠️ Complex comfort zone mechanics (in progress)
  - ❌ Advanced progression systems (not started)
  - ❌ Special abilities (not started)
  - ❌ Build variety (not started)
  - ❌ Challenge modes (not started)

### Phase 5: Polish

- **Implementation Status**: 5% Complete
  - ⚠️ UI/UX improvements (in progress)
  - ❌ Visual effects optimization (not started)
  - ❌ Sound design (not started)
  - ⚠️ Performance optimization (ongoing)

## Related Documentation

- [Object Pooling System](../Systems/ObjectPooling.md)
- [Physics System](../Systems/Physics.md)
- [Player Health System](../Systems/PlayerHealth.md)
- [Performance Guidelines](../Performance/PerformanceGuidelines.md)
- [Infrastructure Plan](Infrastructure.md)
- [Assembly Structure](AssemblyStructure.md)
- [Project Plan](../../Project/ProjectPlan.md)

## Revision History

| Date | Version | Changes |
|------|---------|---------|
| 2025-02-14 | 1.0 | Initial checklist creation |
| 2025-02-27 | 1.1 | Converted to Markdown format with cross-references |
| 2025-02-28 | 1.2 | Updated to align with Project Plan, added phase implementation tracking and Comfort Zone details |

Last Updated: 2025-02-28
Unity Version: 6000.0.38f1 