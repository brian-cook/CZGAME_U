# Environment Setup Guide

## Overview

This document outlines the complete environment setup process for the CZGAME project. Follow these steps to ensure you have the correct Unity version, development tools, and project configuration.

## 1. Version Control Setup ✓

- [x] Initialize Git repository
- [x] Configure Git LFS
- [x] Set up .gitignore for Unity
- [x] Create development and main branches
- [x] Set up remote repository
- [x] Configure .gitattributes

## 2. Unity Project Setup

- **Unity Version**: 6000.0.38f1 (Unity 6)
- [x] Configure for 2D
- [x] URP Setup (17.0.3 for Unity 6)
- [x] Set API compatibility to .NET Standard 2.1 ✓
- [x] Switch to IL2CPP scripting backend ✓
- [ ] Set target platforms (Windows, MacOS, Linux) (Need to verify location)

### Project Settings Reference

#### Player Settings Location
Edit > Project Settings > Player
- API Compatibility: .NET Standard 2.1 ✓
- Scripting Backend: IL2CPP ✓
- Active Input Handling: Both ✓

#### Platform Support
Note: Need to verify correct location and process for enabling target platforms

#### Platform Settings Location
File > Build Settings
1. Click "Switch Platform" to change active platform
2. Supported Platforms:
   - Windows (Standalone)
   - MacOS (Standalone)
   - Linux (Standalone)
3. Add scenes to build
4. Configure platform-specific settings in Player Settings

### Common Issues

1. **IL2CPP Build Issues**
   - Ensure build tools are installed
   - Visual Studio Build Tools required

2. **Platform Switching**
   - Initial switch may take time
   - Requires platform module installation
   - May require Unity Hub module installation

### Performance Impact

- IL2CPP provides better runtime performance
- Longer build times but better optimization
- Platform-specific considerations documented in [Performance Guidelines](../Technical/Performance/PerformanceGuidelines.md)

## 3. Package Installation ✓

- [x] Universal RP (17.0.3)
- [x] Input System (1.13.0)
- [x] TextMeshPro (3.0.8)
- [x] Cinemachine (2.9.7)
- [x] 2D Feature Pack (2.0.1)
- [x] Shader Graph (17.0.3)
- [x] Addressables (1.21.19)
- [x] Navigation (1.1.5)
- [x] NaughtyAttributes (2.1.4)

## 4. Project Structure ✓

- [x] Create _Project folder structure
- [x] Set up folder hierarchy
- [x] Configure meta files
- [x] Move documentation to root level

For complete project structure details, see [Project Structure Documentation](../Project/Structure.md).

## 5. Development Tools

### IDE Configuration (Cursor)

#### Currently Using ✓

- [x] Cursor Editor (0.45.11)
  - Built on VSCode 1.96.2
  - Build Date: 2025-02-07
  - Electron: 32.2.6
  - Node.js: 20.18.1
  - OS: Windows 10 (26100)
  - Full VSCode compatibility
  - Includes Unity debugging support
  - Native IntelliSense support

#### Technical Details
- Commit: f5f18731406b73244e0558ee7716d77c8096d150
- Chromium: 128.0.6613.186
- V8: 12.8.374.38-electron.0

#### External Tools Configuration ✓

- [x] External Script Editor: Cursor (internal)
- [x] Args: $(File)
- [x] Editor integration enabled

### Unity Debug Settings ✓

- [x] Enable Development Build
- [x] Enable Allow Debugging
- [x] Configure Debug symbols
- [x] Set up logging levels

### Integration Verification ✓

- [x] Test script editing (double-click TestScript.cs)
- [x] Verify debugging capabilities
- [x] Check IntelliSense functionality

## 6. Documentation ✓

- [x] Create README.md
- [x] Set up Documentation folder structure
- [x] Add setup guides
- [x] Add architecture documentation

See [Documentation Structure](../Project/Structure.md#documentation) for details on documentation organization.

## 7. Testing Setup

### Test Runner Setup

- [x] Open Test Runner (Window > General > Test Runner)
- [x] Verify EditMode and PlayMode test assemblies
- [x] Configure test settings
- [x] Set up initial test structure
- [x] Verify tests pass

### Test Categories

1. **EditMode Tests** ✓
   - Basic component tests
   - System tests
   - Integration tests

2. **PlayMode Tests** ✓
   - Gameplay tests
   - Performance tests
   - Integration tests

For detailed testing guidelines, refer to the [Testing Guidelines](../Guidelines/testing_guidelines.md).

## Platform Configuration

### Windows Setup

1. **Primary Platform**
   - [x] Windows Build Support
   - [x] Install IL2CPP backend (added via Unity Hub)
   - [x] Development Build enabled
   - [x] Script Debugging enabled
   - [x] Wait for Managed Debugger

Note: IL2CPP installation requires:
- Unity Hub module installation
- Editor restart after installation
- ~1-2GB additional disk space

### Additional Platforms (Optional)

1. **macOS Support**
   - [ ] Install macOS Build Support module
   - [ ] Restart Unity after installation

2. **Linux Support**
   - [ ] Install Linux Build Support module
   - [ ] Restart Unity after installation

Note: Additional platform modules can be installed via Unity Hub

## IL2CPP Installation Steps

1. **Unity Hub Method**:
   - Click "Install Editor"
   - Select matching Unity version (6000.0.38f1)
   - Check "Windows Build Support (IL2CPP)"
   - Complete installation
   - Restart Unity Editor

2. **Alternative Method**:
   - Install Visual Studio Build Tools 2022
   - Select C++ build tools
   - Select Windows SDK

### Visual Studio Configuration

#### Required Workloads

1. **.NET desktop development**
   - .NET Framework 4.7.2 development tools
   - .NET Framework 4.8 development tools
   
2. **Desktop development with C++**
   - Windows 10 SDK
   - MSVC v143 - VS 2022 C++ x64/x86 build tools
   - C++ core features
   
Note: C++ workload is required for Unity's IL2CPP scripting backend

### IL2CPP Verification

- [ ] Visual Studio C++ workload installed
  - Check Visual Studio Installer progress
  - Verify C++ components installed
- [ ] IL2CPP available in Player Settings
  - Open Edit > Project Settings > Player
  - Check Scripting Backend dropdown
- [ ] Test build compiles successfully
  - Create test build
  - Check for IL2CPP compilation errors

## Next Steps

1. Complete Unity Project Settings configuration
2. Set up development tools
3. Configure testing framework

## Unity 6 Documentation Resources

### Unity Documentation Hub

- Unity Docs Home: [https://docs.unity.com/](https://docs.unity.com/)
- Editor Manual: [https://docs.unity3d.com/6000.0/Documentation/Manual/](https://docs.unity3d.com/6000.0/Documentation/Manual/)
- Scripting API: [https://docs.unity3d.com/6000.0/Documentation/ScriptReference/](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/)
- Services Documentation: [https://docs.unity.com/gaming-services/](https://docs.unity.com/gaming-services/)

### Key Features

- Boost rendering performance
- Enhanced multiplayer capabilities
- Expanded multiplatform support
- Runtime AI with Sentis
- Improved visual capabilities
- Enhanced productivity tools

Built from: 6000.0.40f1

### Learning Resources

#### Getting Started

- Unity 6 Overview: [https://docs.unity3d.com/6000.0/Documentation/Manual/WhatsNewUnity6.html](https://docs.unity3d.com/6000.0/Documentation/Manual/WhatsNewUnity6.html)
- 2D Game Development: [https://docs.unity3d.com/6000.0/Documentation/Manual/2DFeature.html](https://docs.unity3d.com/6000.0/Documentation/Manual/2DFeature.html)
- URP Setup: [https://docs.unity3d.com/6000.0/Documentation/Manual/UniversalRP.html](https://docs.unity3d.com/6000.0/Documentation/Manual/UniversalRP.html)
- IL2CPP: [https://docs.unity3d.com/6000.0/Documentation/Manual/IL2CPP.html](https://docs.unity3d.com/6000.0/Documentation/Manual/IL2CPP.html)

#### Best Practices

- Project Organization: [https://docs.unity3d.com/6000.0/Documentation/Manual/BestPracticeGuides.html](https://docs.unity3d.com/6000.0/Documentation/Manual/BestPracticeGuides.html)
- Performance Optimization: [https://docs.unity3d.com/6000.0/Documentation/Manual/BestPracticeUnderstandingPerformanceInUnity.html](https://docs.unity3d.com/6000.0/Documentation/Manual/BestPracticeUnderstandingPerformanceInUnity.html)
- Memory Management: [https://docs.unity3d.com/6000.0/Documentation/Manual/performance-memory-management.html](https://docs.unity3d.com/6000.0/Documentation/Manual/performance-memory-management.html)

#### Development Guides

- Input System: [https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.inputsystem.html](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.inputsystem.html)
- 2D Animation: [https://docs.unity3d.com/6000.0/Documentation/Manual/2DAnimation.html](https://docs.unity3d.com/6000.0/Documentation/Manual/2DAnimation.html)
- Physics 2D: [https://docs.unity3d.com/6000.0/Documentation/Manual/Physics2DReference.html](https://docs.unity3d.com/6000.0/Documentation/Manual/Physics2DReference.html)
- UI Toolkit: [https://docs.unity3d.com/6000.0/Documentation/Manual/UIToolkit.html](https://docs.unity3d.com/6000.0/Documentation/Manual/UIToolkit.html)

#### Tools and Services

- Package Manager: [https://docs.unity3d.com/6000.0/Documentation/Manual/Packages.html](https://docs.unity3d.com/6000.0/Documentation/Manual/Packages.html)
- Asset Store: [https://docs.unity3d.com/6000.0/Documentation/Manual/AssetStore.html](https://docs.unity3d.com/6000.0/Documentation/Manual/AssetStore.html)
- Unity Services: [https://docs.unity.com/gaming-services/](https://docs.unity.com/gaming-services/)

## Related Documentation

- [Infrastructure Plan](../Technical/Architecture/Infrastructure.md)
- [Performance Guidelines](../Technical/Performance/PerformanceGuidelines.md)
- [Object Pooling System](../Technical/Systems/ObjectPooling.md)
- [Physics System](../Technical/Systems/Physics.md)
- [Project Structure](../Project/Structure.md)

## Revision History

| Date | Version | Changes |
|------|---------|---------|
| 2025-02-14 | 1.0 | Initial document creation |
| 2025-02-27 | 1.1 | Converted to Markdown format with cross-references |

Last Updated: 2025-02-27
Unity Version: 6000.0.38f1 