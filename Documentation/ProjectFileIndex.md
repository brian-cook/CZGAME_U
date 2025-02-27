# CZGAME Project File Index

This file serves as a comprehensive index of the project's file structure, including both code files and assets. Use this index to quickly locate files and understand where new components should be placed according to the implementation plan in `CoreGameLoop.md`.

## Last Updated
- Date: 2025-02-28
- Unity Version: 6000.0.38f1

## Purpose
This index provides a complete view of the project structure to facilitate:
1. Quick location of key files and systems
2. Understanding of the project organization
3. Guidance for placing new components
4. Reference for implementation of `CoreGameLoop.md` components

## Table of Contents
1. [Scripts](#scripts)
2. [Assets](#assets)
3. [Documentation](#documentation)
4. [Configuration](#configuration)
5. [Implementation Guidelines](#implementation-guidelines)

## Scripts

### Core Systems
Core game functionality is located in `Assets/_Project/Scripts/Core/`:

| System | Location | Purpose | Status |
|--------|----------|---------|--------|
| Game Manager | `Assets/_Project/Scripts/Core/GameManager.cs` | Core game loop and state management | Implemented |
| Game State | `Assets/_Project/Scripts/Core/GameState.cs` | Game state definitions | Implemented |
| Configuration | `Assets/_Project/Scripts/Core/Configuration/` | System configuration | Implemented |
| - Physics Setup | `Assets/_Project/Scripts/Core/Configuration/Physics2DSetup.cs` | 2D physics configuration | Implemented |
| - Memory Config | `Assets/_Project/Scripts/Core/Configuration/MemoryConfiguration.cs` | Memory optimization | Implemented |
| Enemy | `Assets/_Project/Scripts/Core/Enemy/` | Enemy behavior and management | Implemented |
| - Base Enemy | `Assets/_Project/Scripts/Core/Enemy/BaseEnemy.cs` | Base enemy functionality | Implemented |
| - Enemy Spawner | `Assets/_Project/Scripts/Core/Enemy/EnemySpawner.cs` | Enemy spawning system | Implemented |
| Interfaces | `Assets/_Project/Scripts/Core/Interfaces/` | Core interfaces | Implemented |
| - IDamageable | `Assets/_Project/Scripts/Core/Interfaces/IDamageable.cs` | Interface for damageable entities | Implemented |
| - IPoolable | `Assets/_Project/Scripts/Core/Interfaces/IPoolable.cs` | Interface for poolable objects | Implemented |
| Logging | `Assets/_Project/Scripts/Core/Logging/` | Centralized logging | Implemented |
| Player | `Assets/_Project/Scripts/Core/Player/` | Player functionality | Implemented |
| - Player Controller | `Assets/_Project/Scripts/Core/Player/PlayerController.cs` | Player input and movement | Implemented |
| - Player Health | `Assets/_Project/Scripts/Core/Player/PlayerHealth.cs` | Player health management | Implemented |
| Pooling | `Assets/_Project/Scripts/Core/Pooling/` | Object pooling | Implemented |
| - Object Pool | `Assets/_Project/Scripts/Core/Pooling/ObjectPool.cs` | Generic pool implementation | Implemented |
| - Pool Manager | `Assets/_Project/Scripts/Core/Pooling/PoolManager.cs` | Global pool management | Implemented |
| Resource | `Assets/_Project/Scripts/Core/Resource/` | Resource management | Implemented |
| - Resource Manager | `Assets/_Project/Scripts/Core/Resource/ResourceManager.cs` | Global resource management | Implemented |
| Input | `Assets/_Project/Scripts/Core/Input/` | Input management | Implemented |
| Extensions | `Assets/_Project/Scripts/Core/Extensions/` | Extension methods | Implemented |
| UI (Core) | `Assets/_Project/Scripts/Core/UI/` | Core UI functionality | Implemented |
| VFX | `Assets/_Project/Scripts/Core/VFX/` | Visual effects | Implemented |

### Gameplay Systems
Gameplay systems are located in specific directories:

| System | Location | Purpose | Status |
|--------|----------|---------|--------|
| Comfort Zone System | `Assets/_Project/Scripts/ComfortZone/` | Main game mechanic | In Progress |
| - Zone Manager | `Assets/_Project/Scripts/ComfortZone/ZoneManager.cs` | Zone management | In Progress |
| - Zone Effects | `Assets/_Project/Scripts/ComfortZone/ZoneEffects.cs` | Zone visual/gameplay effects | In Progress |
| Combat System | `Assets/_Project/Scripts/Combat/` | Combat mechanics | In Progress |
| - Damage System | `Assets/_Project/Scripts/Combat/DamageSystem.cs` | Damage calculation and application | In Progress |
| - Weapon Manager | `Assets/_Project/Scripts/Combat/WeaponManager.cs` | Weapon management | In Progress |
| - Projectile System | `Assets/_Project/Scripts/Combat/ProjectileSystem.cs` | Projectile management | Planned |
| UI System | `Assets/_Project/Scripts/UI/` | Game UI | In Progress |
| Utilities | `Assets/_Project/Scripts/Utils/` | Utility functions | Implemented |

### Editor Scripts
Editor extensions are located in `Assets/_Project/Scripts/Editor/`:

| System | Location | Purpose | Status |
|--------|----------|---------|--------|
| Layer Setup | `Assets/_Project/Scripts/Editor/LayerSetupEditor.cs` | Physics layer configuration | Implemented |

## Assets

### Prefabs
Prefabs are located in `Assets/_Project/Prefabs/`:

| Category | Location | Purpose | Status |
|----------|----------|---------|--------|
| Core | `Assets/_Project/Prefabs/Core/` | Core gameplay prefabs | Implemented |
| Combat | `Assets/_Project/Prefabs/Combat/` | Combat-related prefabs | In Progress |
| UI | `Assets/_Project/Prefabs/UI/` | UI prefabs | In Progress |
| Zones | `Assets/_Project/Prefabs/Zones/` | Comfort zone prefabs | In Progress |

### ScriptableObjects
ScriptableObjects are located in `Assets/_Project/ScriptableObjects/`:

| Category | Location | Purpose | Status |
|----------|----------|---------|--------|
| Enemies | `Assets/_Project/ScriptableObjects/Enemies/` | Enemy configurations | In Progress |
| Weapons | `Assets/_Project/ScriptableObjects/Weapons/` | Weapon configurations | In Progress |
| Zones | `Assets/_Project/ScriptableObjects/Zones/` | Zone configurations | In Progress |

### Scenes
Scenes are located in `Assets/_Project/Scenes/`.

### Art
Art assets are located in `Assets/_Project/Art/`:

| Category | Location | Purpose |
|----------|----------|---------|
| Materials | `Assets/_Project/Art/Materials/` | Material assets |
| Models | `Assets/_Project/Art/Models/` | 3D model assets |
| Sprites | `Assets/_Project/Art/Sprites/` | Sprite assets |
| VFX | `Assets/_Project/Art/VFX/` | Visual effects |

### Audio
Audio assets are located in `Assets/_Project/Audio/`.

### Input
Input configuration is located in `Assets/_Project/Input/`:

| File | Location | Purpose |
|------|----------|---------|
| Input Actions | `Assets/_Project/Input/InputSystem_Actions.inputactions` | Input system configuration |

### Resources
Runtime-loaded resources are located in `Assets/_Project/Resources/`.

### Settings
Project settings are located in `Assets/_Project/Settings/`:

| File | Location | Purpose |
|------|----------|---------|
| URP Profile | `Assets/_Project/Settings/DefaultVolumeProfile.asset` | URP volume profile |
| URP Global Settings | `Assets/_Project/Settings/UniversalRenderPipelineGlobalSettings.asset` | URP global settings |
| URP Settings | `Assets/_Project/Settings/URPSettings/` | Additional URP settings |

## Documentation
Documentation is organized in the `Documentation/` directory:

| Section | Location | Purpose |
|---------|----------|---------|
| Project | `Documentation/Project/` | Project overview and structure |
| Technical | `Documentation/Technical/` | Technical documentation |
| - Architecture | `Documentation/Technical/Architecture/` | Architecture documentation |
| - Systems | `Documentation/Technical/Systems/` | System documentation |
| - Performance | `Documentation/Technical/Performance/` | Performance guidelines |
| Setup | `Documentation/Setup/` | Setup documentation |
| Workflows | `Documentation/Workflows/` | Development workflows |
| Reference | `Documentation/Reference/` | Reference documentation |
| Guidelines | `Documentation/Guidelines/` | Development guidelines |
| Resources | `Documentation/Resources/` | Supporting resources |

## Configuration
Project configuration files:

| File | Location | Purpose |
|------|----------|---------|
| Assembly Definitions | Various `.asmdef` files | Define code assembly structure |
| Package Manifest | `Packages/manifest.json` | Define project dependencies |
| Project Settings | `ProjectSettings/` | Unity project settings |

## Implementation Guidelines

When implementing new components from the `CoreGameLoop.md` plan:

1. **Scripts**: 
   - Place new scripts in the appropriate category folder
   - Create new folders if needed for new systems
   - Follow the naming conventions in existing files
   - Add assembly references as needed (see `AssemblyStructure.md`)

2. **Prefabs**:
   - Place in the appropriate prefab category
   - Create new categories if needed

3. **ScriptableObjects**:
   - Create new ScriptableObject types in the appropriate script folders
   - Place instances in the appropriate ScriptableObjects category

4. **Documentation**:
   - Add system documentation to `Documentation/Technical/Systems/`
   - Update this index when adding major components
   - Reference existing documentation for formatting guidance

5. **Status Tracking**:
   - Update the status in this index as components move from Planned to In Progress to Implemented
   - Update implementation checklists in `CoreGameLoop.md`

## Using This Index

1. **Finding Files**: Use this index to quickly locate files by system or category
2. **Implementation Guidance**: Reference the Implementation Guidelines when adding new components
3. **Status Tracking**: Track the status of system implementation
4. **Integration Planning**: Understand dependencies between systems for integration planning

This index should be updated regularly as new components are added to the project. 