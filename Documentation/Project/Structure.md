# Project Structure Documentation

## Overview

This document describes the actual structure of the CZGAME project as currently implemented. It is intended to be a living document that is updated as the project evolves.

## Assets Organization

```
Assets/
├── _Project/
│   ├── Scripts/
│   │   ├── Core/
│   │   │   ├── Configuration/
│   │   │   ├── Enemy/
│   │   │   ├── Extensions/
│   │   │   ├── Input/
│   │   │   ├── Interfaces/
│   │   │   ├── Logging/
│   │   │   ├── Player/
│   │   │   ├── Pooling/
│   │   │   ├── Resource/
│   │   │   ├── UI/
│   │   │   └── VFX/
│   │   ├── Editor/
│   │   ├── UI/
│   │   ├── Utils/
│   │   ├── ComfortZone/
│   │   └── Combat/
│   ├── Tests/
│   │   ├── EditMode/
│   │   └── PlayMode/
│   ├── Prefabs/
│   ├── Scenes/
│   ├── Resources/
│   └── Input/
└── Plugins/
```

## Assembly Structure

The project is organized into modular assemblies that follow a well-defined dependency hierarchy. See [Assembly Structure Documentation](../Technical/Architecture/AssemblyStructure.md) for details.

## Key Directories

### Core Scripts

The `Assets/_Project/Scripts/Core` directory contains the core functionality of the game, organized into logical submodules:

#### Configuration
- **Purpose**: Centralized configuration for game systems
- **Key Components**: 
  - `Physics2DSetup.cs` - Configures physics settings for 2D collisions
  - `MemoryConfiguration.cs` - Memory usage optimization

#### Enemy
- **Purpose**: Enemy behavior and management
- **Key Components**:
  - `BaseEnemy.cs` - Base class for all enemies
  - `EnemySpawner.cs` - Handles enemy spawning

#### Pooling
- **Purpose**: Object pooling for performance optimization
- **Key Components**:
  - `ObjectPool.cs` - Generic object pool implementation
  - `PoolManager.cs` - Global pool management

#### Player
- **Purpose**: Player character functionality
- **Key Components**:
  - `PlayerController.cs` - Player input and movement
  - `PlayerHealth.cs` - Health system for player

#### Resource
- **Purpose**: Game resource management
- **Key Components**:
  - `ResourceManager.cs` - Global resource management
  - `ResourceType.cs` - Resource type definitions

### Other Directories

#### Editor
- **Purpose**: Custom editor tools and extensions
- **Key Components**:
  - `LayerSetupEditor.cs` - Editor tool for configuring physics layers

#### UI
- **Purpose**: User interface components and systems
- **Key Components**: UI controllers and visual elements

#### Combat
- **Purpose**: Combat system
- **Key Components**: Damage systems, weapons, and combat mechanics

## Scripts Organization

### Managers

The project uses a manager-based architecture for global systems:

- `GameManager.cs` - Core game loop and state management
- `ResourceManager.cs` - Resource spawning and collection
- `PoolManager.cs` - Object pool management

### Component Design

Components follow these design principles:

1. **Single Responsibility**: Each component has a focused purpose
2. **Interface-Based Communication**: Components interact through interfaces
3. **Poolable Objects**: Designed for efficient memory reuse

## Resource Management

The project uses several approaches to resource management:

1. **Object Pooling**: Reuses game objects to minimize garbage collection
2. **Addressable Assets**: Used for dynamic loading (in development)
3. **Scriptable Objects**: For configuration data

## Project Configuration

Project settings are maintained in:

```
ProjectSettings/
├── Physics2DSettings.asset - 2D physics configuration
├── InputManager.asset - Legacy input configuration
└── TagManager.asset - Tags and Layers configuration
```

## Documentation

Project documentation is organized in:

```
Documentation/
├── Project/ - Project overview and structure
├── Technical/ - Technical details and architecture
├── Workflows/ - Development processes
├── Reference/ - Reference materials and guides
└── AI/ - AI assistance configuration
```

## Best Practices

When adding new components to the project:

1. Place them in the appropriate directory based on functionality
2. Ensure proper assembly references are configured
3. Update this documentation to reflect structural changes
4. Follow the established naming conventions

## Common Issues

1. **Missing Assembly References**: Ensure your assembly has the correct references. See [Assembly Structure Documentation](../Technical/Architecture/AssemblyStructure.md).
2. **Layer Configuration**: The project uses specific layers for physics interactions. See the `LayerSetupEditor.cs` for details.

Last Updated: 2025-02-27
Unity Version: 6000.0.38f1 