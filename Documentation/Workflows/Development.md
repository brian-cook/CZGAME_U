# Development Workflow

This document outlines the recommended development workflow for the CZGAME project.

## Initial Setup

1. **Environment Setup**
   - Install Unity 6000.0.38f1
   - Install Git with LFS support
   - Configure Unity to use external script editor (Visual Studio or Rider recommended)

2. **Project Setup**
   - Clone the repository
   - Open project in Unity
   - Verify all packages are correctly imported
   - Run initial tests to ensure environment is working

## Development Cycle

### 1. Feature Planning

- Review tasks in the project management system
- Understand requirements and acceptance criteria
- Break down tasks into smaller, manageable units
- Discuss approach with team if needed

### 2. Branch Management

- Create a new feature branch from `develop`
  ```
  git checkout develop
  git pull
  git checkout -b feature/[feature-name]
  ```
- For bug fixes, use the prefix `bugfix/`
  ```
  git checkout -b bugfix/[bug-description]
  ```

### 3. Implementing Features

- Follow the project's code style and organization
- Ensure new scripts are placed in appropriate directories
- Update/add assembly references as needed
- Implement the feature with appropriate testing
- Document important implementation details

### 4. Testing

- Run PlayMode tests to verify functionality
- Run EditMode tests to verify logic
- Perform manual testing in the editor
- Check for performance impact using the Unity Profiler

### 5. Code Quality

- Review your own code for:
  - Proper error handling
  - Optimization opportunities
  - Readability and maintainability
  - Documentation completeness

### 6. Committing Changes

- Commit frequently with meaningful messages
  ```
  git add [changed files]
  git commit -m "Feature: Implemented [feature]"
  ```
- For bug fixes:
  ```
  git commit -m "Fix: Resolved issue with [description]"
  ```

### 7. Preparing for Review

- Push your branch to the remote repository
  ```
  git push origin feature/[feature-name]
  ```
- Create a pull request to the `develop` branch
- Provide a detailed description of your changes
- Reference any related tasks or issues

### 8. Code Review

- Address feedback from code reviews
- Make necessary changes
- Push updated changes
- Request re-review if needed

### 9. Merging

- Once approved, merge the feature branch into `develop`
- Delete the feature branch after successful merge

### 10. Deployment

- Features will be bundled into releases from the `develop` branch
- Release candidates will be created as needed
- Final releases will be merged to `main`

## Handling Dependencies

- When adding new dependencies:
  - Update the `manifest.json` file
  - Document the dependency in relevant documentation
  - Communicate with the team about the new dependency

## Working with Scene Files

- Never modify scene files that are not part of your task
- Create backup copies of scenes before making significant changes
- Use prefabs whenever possible to minimize scene changes

## Best Practices

1. **Communication**
   - Discuss complex changes before implementation
   - Document decisions made during development
   - Communicate blockers as early as possible

2. **Documentation**
   - Update relevant documentation when making changes
   - Add comments to complex code sections
   - Create/update README files for new components

3. **Performance**
   - Be mindful of performance implications
   - Use object pooling for frequently instantiated objects
   - Profile code before and after significant changes

4. **Unity-Specific**
   - Follow the project's serialization guidelines
   - Use ScriptableObjects for configuration data
   - Prefer interfaces for component communication

## Common Issues and Solutions

### Unity Console Errors

- Check for missing references in prefabs
- Verify scene dependencies
- Check script execution order if timing-related

### Git Issues

- For large binary files, ensure Git LFS is properly configured
- When resolving merge conflicts in Unity assets, use Unity's Smart Merge tool
- For complex merges, consider using Unity's Collection Manager

### Performance Problems

- Use the Profiler to identify bottlenecks
- Check for excessive Instantiate/Destroy calls
- Look for Update methods that could be optimized

## Reference Documents

- [Project Structure](../Project/Structure.md)
- [Assembly Structure](../Technical/Architecture/AssemblyStructure.md)
- [Physics System](../Technical/Systems/Physics.md)

Last Updated: 2025-02-27
Unity Version: 6000.0.38f1 