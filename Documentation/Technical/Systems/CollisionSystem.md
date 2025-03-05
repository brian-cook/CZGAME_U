# Collision System Documentation

## Overview

The collision system in our game is built on Unity's 2D physics engine with custom modifications to ensure reliable detection between game objects. This document outlines the key components, common issues, and solutions implemented to address collision problems.

## Key Components

### 1. Physics2DSetup

The `Physics2DSetup` class is responsible for configuring global physics settings and ensuring proper layer-based collisions. It handles:

- Setting up the collision matrix between layers
- Configuring global Physics2D settings (contact offset, iteration counts)
- Adjusting enemy collider sizes and properties
- Enforcing collision detection between specific layers (Enemy-Projectile, Player-Enemy)
- Periodically checking for and fixing collision issues

### 2. CollisionDebugger

The `CollisionDebugger` class provides runtime debugging and fixes for collision-related issues:

- Monitors and logs collision events
- Attempts to fix projectile-enemy collisions when they fail
- Verifies and corrects collider settings on game objects
- Provides detailed logging for collision troubleshooting
- Contains specific checks for SwiftEnemy objects

### 3. Layer-Based Collision

The game uses Unity's layer-based collision system with the following key layers:

- **Player**: For the player character
- **Enemy**: For all enemy types
- **Projectile**: For player projectiles
- **Default**: For general environment objects
- **Obstacle**: For solid obstacles in the game world

## Common Issues and Solutions

### 1. SwiftEnemy Collision Issues

**Problem**: SwiftEnemy objects were not being hit by projectiles due to incorrect layer settings and collider configurations.

**Solution**:
- Added collision detection methods (`OnCollisionEnter2D` and `OnTriggerEnter2D`) to both `BaseEnemy` and `SwiftEnemyController`
- Ensured SwiftEnemy objects are set to the "Enemy" layer
- Configured colliders to be non-trigger for physics collisions
- Added specific checks in `Physics2DSetup` and `CollisionDebugger` for SwiftEnemy objects

### 2. Projectile-Enemy Collision Matrix

**Problem**: The collision matrix sometimes gets reset, causing projectiles to pass through enemies.

**Solution**:
- Force-enable Projectile-Enemy layer collisions in `Physics2DSetup`
- Added periodic verification of the collision matrix
- Implemented logging to track when collisions are disabled
- Added a specific method to check and fix the collision matrix

### 3. Collider Configuration Issues

**Problem**: Colliders sometimes get misconfigured (disabled or set as triggers) preventing proper collision detection.

**Solution**:
- Added checks to ensure colliders are properly configured
- Implemented automatic correction of collider settings
- Added logging to track collider state changes
- Created specific methods to verify and fix collider settings

## Best Practices

1. **Layer Management**:
   - Always use the correct layer for game objects (Enemy layer for enemies, Projectile layer for projectiles)
   - Don't modify the layer collision matrix at runtime unless necessary

2. **Collider Setup**:
   - For physics collisions, use non-trigger colliders
   - For damage detection only (no physics response), use trigger colliders
   - Ensure colliders are appropriately sized for the visual representation

3. **Rigidbody Configuration**:
   - Use Continuous collision detection for fast-moving objects
   - Set appropriate constraints to prevent unwanted rotation or movement
   - Ensure simulation is enabled for objects that need physics interactions

4. **Debugging Collisions**:
   - Use the CollisionDebugger to log and fix collision issues
   - Check the console for warnings about disabled colliders or incorrect layer settings
   - Verify the collision matrix is correctly configured

## Recent Fixes

1. **SwiftEnemy Collision Detection**:
   - Added `OnCollisionEnter2D` and `OnTriggerEnter2D` methods to handle projectile hits
   - Ensured proper layer assignment and collider configuration
   - Added specific checks in the Physics2DSetup and CollisionDebugger

2. **BaseEnemy Collision Handling**:
   - Added collision detection methods to the base class
   - Implemented proper damage handling for projectile hits
   - Ensured consistent behavior across all enemy types

3. **Physics2DSetup Improvements**:
   - Added specific checks for SwiftEnemy objects
   - Implemented more robust layer collision enforcement
   - Added detailed logging for troubleshooting

4. **CollisionDebugger Enhancements**:
   - Added SwiftEnemy-specific checks and fixes
   - Improved logging for collision issues
   - Implemented automatic correction of common problems

## Troubleshooting

If you encounter collision issues:

1. Check the console for warnings or errors from `Physics2DSetup` or `CollisionDebugger`
2. Verify that objects are on the correct layers
3. Ensure colliders are enabled and properly configured
4. Check if the collision matrix is correctly set up
5. Use the Scene view to visualize colliders and ensure they're properly sized

For persistent issues, enable detailed logging in the `CollisionDebugger` to get more information about the collision state. 