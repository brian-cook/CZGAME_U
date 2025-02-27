# CZGAME Project Overview

## Introduction

CZGAME is a 2D game built with Unity 6 that focuses on fast-paced action and enemy management. The project uses a modular architecture with well-defined components and systems.

## Development Environment

- **Unity Version**: 6000.0.38f1 (Unity 6)
- **Primary Platform**: Windows Standalone
- **Development Tool**: Cursor IDE
- **Version Control**: Git with LFS support

## Key Systems

### Core Gameplay

- **Player Control**: Input-driven character movement and interactions
- **Enemy Management**: AI-controlled enemies with pooling for performance
- **Resource Management**: Collection and management of in-game resources
- **Physics**: Custom physics configuration for optimal gameplay

### Technical Foundation

- **Assembly Structure**: Modular code organization with clear dependencies
- **Object Pooling**: Efficient reuse of game objects for performance
- **Configuration**: Centralized settings and runtime configuration

## Project Status

The project is currently in active development with the following key milestones:

- ✅ Core gameplay mechanics
- ✅ Player movement and control
- ✅ Basic enemy behavior
- ✅ Physics system configuration
- ✅ Object pooling implementation
- 🔄 Resource management (in progress)
- 🔄 Visual effects system (in progress)
- ⏳ Advanced enemy behaviors (planned)
- ⏳ Level design tools (planned)

## Architecture

The project follows a component-based architecture with clear separation of concerns:

```
Player <-- GameManager --> Enemy
   |           |            |
   v           v            v
Input      Resource     Physics
System     Management   Configuration
```

Key architectural principles:

1. **Modularity**: Systems are designed to be modular and replaceable
2. **Interface-Driven**: Components communicate through interfaces
3. **Data-Oriented**: Key gameplay elements are data-driven
4. **Performance-Focused**: Careful attention to performance implications

## Documentation Structure

Project documentation is organized as follows:

- **Project**: High-level overview and project structure
- **Technical**: Detailed technical documentation
- **Workflows**: Development and deployment workflows
- **Reference**: External references and resources
- **AI**: AI assistance configuration

## Key Technologies

- **Unity Universal Render Pipeline (17.0.3)**: Modern rendering for 2D graphics
- **Input System (1.13.0)**: Flexible input handling
- **Physics2D**: Custom-configured 2D physics
- **NaughtyAttributes (2.1.4)**: Enhanced Inspector functionality

## Getting Started

To begin working with the project:

1. Clone the repository
2. Open in Unity 6.0 (version 6000.0.38f1)
3. Review the [Project Structure](Structure.md) document
4. Check the [Assembly Structure](../Technical/Architecture/AssemblyStructure.md) documentation

## Best Practices

This project follows these best practices:

1. **Consistent Naming**: Clear naming conventions for all assets
2. **Documentation**: Comprehensive documentation for all systems
3. **Testing**: Unit and integration tests for core functionality
4. **Performance**: Regular performance profiling and optimization

## Contact

- **Lead Developer**: Brian
- **Project Repository**: [URL to the repository]

Last Updated: 2025-02-27
Unity Version: 6000.0.38f1 