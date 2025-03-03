# Enemy Types Documentation

This document provides information about the different enemy types in the game and their implementation details.

## Core Enemy Architecture

All enemy types in the game inherit from the `BaseEnemy` class, which provides common functionality such as:
- Health management
- Damage handling
- Death sequences
- Movement towards targets
- Object pooling integration
- Physics configuration

Each specific enemy type extends this base functionality with unique behaviors and characteristics.

## Swift Enemy

![Swift Enemy Visual Reference](../../Media/Placeholder_SwiftEnemy.png)

The Swift Enemy is a fast, agile enemy with low health that uses erratic movement patterns and can dodge incoming attacks.

### Swift Enemy Characteristics

- **Health**: Lower than standard enemies (75 vs 100)
- **Speed**: 1.5x faster than standard enemies
- **Behavior**: Erratic movement with dodge capabilities
- **Special Ability**: Can dodge player attacks and projectiles

### Technical Implementation

The Swift Enemy is implemented in the `SwiftEnemyController` class, which extends `BaseEnemy` and adds the following key features:

#### Erratic Movement

Swift enemies use a randomized movement pattern:
```csharp
private void UpdateErraticMovement()
{
    // Periodically change direction
    if (Time.time > lastDirectionChangeTime + directionChangeInterval)
    {
        // Calculate base direction toward target
        Vector2 toTarget = ((Vector2)transform.position - rb.position).normalized;
        
        // Apply randomness to direction
        Vector2 randomDirection = Random.insideUnitCircle.normalized;
        Vector2 newDirection = Vector2.Lerp(toTarget, randomDirection, movementRandomness);
        
        // Update direction and timestamp
        currentMoveDirection = newDirection.normalized;
        lastDirectionChangeTime = Time.time;
    }
    
    // Calculate final movement velocity
    Vector2 targetVelocity = currentMoveDirection * (moveSpeed * speedMultiplier);
    
    // Apply movement with appropriate agility
    rb.velocity = Vector2.Lerp(rb.velocity, targetVelocity, Time.fixedDeltaTime * agility);
}
```

#### Dodge Behavior

Swift enemies can detect and dodge incoming threats:
```csharp
private void CheckForDodgeOpportunity()
{
    // Find potential threats (projectiles or player) within detection range
    Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, dodgeDetectionRange, 
        LayerMask.GetMask("Player", "Projectile"));
    
    foreach (var collider in colliders)
    {
        // Skip if it's another enemy
        if (collider.gameObject.layer == LayerMask.NameToLayer("Enemy"))
            continue;
        
        // Check if it's a projectile or player
        bool isProjectile = collider.GetComponent<Player.Projectile>() != null;
        bool isPlayer = collider.GetComponent<Player.PlayerController>() != null;
        
        if (isProjectile || isPlayer)
        {
            // Calculate threat direction
            Vector2 threatDirection = (collider.transform.position - transform.position).normalized;
            
            // Check if threat is coming toward us (dot product)
            Rigidbody2D threatRb = collider.GetComponent<Rigidbody2D>();
            
            if (threatRb != null)
            {
                Vector2 threatVelocity = threatRb.velocity.normalized;
                float dotProduct = Vector2.Dot(threatDirection, threatVelocity);
                
                // If threat is moving toward us and random check passes
                if (dotProduct < -0.5f && Random.value < dodgeProbability)
                {
                    // Execute dodge
                    PerformDodge(threatDirection);
                    break;
                }
            }
        }
    }
}
```

#### Animation Integration

The Swift Enemy integrates with the Unity animation system using an `Animator` component:
- Speed parameter - Controls movement animation speed
- Dodge trigger - Activates dodge animation
- Damage trigger - Activates damage reaction animation

### Prefab Configuration

When creating a Swift Enemy prefab in Unity, use the following configuration:

#### Components
- SwiftEnemyController (Script)
- SpriteRenderer
- CircleCollider2D (smaller radius than standard enemies)
- Rigidbody2D (lower mass for higher agility)
- Animator

#### Physics Settings
- **Rigidbody2D**:
  - Mass: 1.0 (lower than standard enemy)
  - Drag: 0.2 (low for responsive movement)
  - Gravity Scale: 0
  - Collision Detection: Continuous
  - Interpolation: Interpolate
  - Constraints: Freeze Rotation

#### Animation Parameters
- "Speed" (float) - For movement animation
- "Dodge" (trigger) - For dodge animation
- "TakeDamage" (trigger) - For damage reaction

### SwiftEnemyController Inspector Settings

The SwiftEnemyController script exposes several configurable properties:

#### Movement Settings
- **Speed Multiplier**: 1.5 (default) - How much faster than base enemies
- **Agility**: 1.2 (default) - How quickly it can change direction
- **Movement Randomness**: 0.3 (default) - Randomness of movement (0=direct, 1=erratic)
- **Direction Change Interval**: 0.5s (default) - How frequently direction changes occur

#### Dodge Behavior
- **Can Dodge**: true (default) - Whether this enemy can dodge
- **Dodge Force**: 8 (default) - Force applied during dodge
- **Dodge Cooldown**: 1.5s (default) - Cooldown between dodge attempts
- **Dodge Probability**: 0.7 (default) - Probability of dodge when conditions are met
- **Dodge Detection Range**: 5 (default) - Maximum distance to detect threats

#### Health Settings
- **Swift Enemy Health**: 75 (default) - Health for this enemy type

## Tank Enemy

*Coming soon*

## Ranged Enemy 

*Coming soon*

## Special/Elite Enemies

*Coming soon*

## Boss Enemies

*Coming soon* 