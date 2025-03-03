using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Pool;

namespace CZ.Core.Enemy
{
    /// <summary>
    /// Manages enemy wave spawning, difficulty progression, and wave completion
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        [System.Serializable]
        public class EnemyTypeInfo
        {
            public string enemyType;
            public GameObject enemyPrefab;
            public int initialPoolSize = 20;
            public int maxPoolSize = 50;
            [Tooltip("Base weight for spawn probability")]
            public float spawnWeight = 1f;
            [Tooltip("How this enemy's weight scales with difficulty (higher = more common at higher difficulties)")]
            public float difficultyScaling = 1f;
        }

        [System.Serializable]
        public class WaveConfig
        {
            public int waveNumber;
            public int baseEnemyCount;
            public float spawnInterval = 1.0f;
            public float timeBetweenWaves = 5.0f;
            [Tooltip("Difficulty multiplier affecting enemy count, spawn speed, etc.")]
            public float difficultyMultiplier = 1.0f;
            [Tooltip("Special enemies to spawn at fixed points during the wave (0.5 = halfway through)")]
            public List<SpecialEnemySpawn> specialEnemies = new List<SpecialEnemySpawn>();
        }

        [System.Serializable]
        public class SpecialEnemySpawn
        {
            public string enemyType;
            [Range(0, 1)]
            public float spawnTimePercentage;
            public Vector2 spawnPositionOffset;
        }

        [Header("Wave Settings")]
        [SerializeField] private List<WaveConfig> waveConfigurations = new List<WaveConfig>();
        [SerializeField] private bool autoGenerateWaves = true;
        [SerializeField] private float difficultyScalingFactor = 1.2f;
        [SerializeField] private int maxWaveCount = 20;
        [SerializeField] private bool loopFinalWave = true;

        [Header("Enemy Types")]
        [SerializeField] private List<EnemyTypeInfo> enemyTypes = new List<EnemyTypeInfo>();
        
        [Header("Spawn Settings")]
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private float minDistanceFromPlayer = 5f;
        [SerializeField] private Transform playerTransform;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;
        
        [Header("Events")]
        public UnityEvent onWaveStart;
        public UnityEvent<int> onWaveCompleted;
        public UnityEvent onAllWavesCompleted;
        public UnityEvent<int, int> onEnemySpawned; // current, total
        public UnityEvent<int, int> onEnemyDefeated; // remaining, total

        // Private properties
        private int currentWave = 0;
        private int remainingEnemiesInWave = 0;
        private int totalEnemiesInWave = 0;
        private bool isWaveActive = false;
        private bool isSpawning = false;
        private Dictionary<string, IObjectPool<BaseEnemy>> enemyPools = new Dictionary<string, IObjectPool<BaseEnemy>>();
        private List<BaseEnemy> activeEnemies = new List<BaseEnemy>();
        private WaveConfig currentWaveConfig;
        private float waveStartTime;
        private Coroutine spawnCoroutine;
        private Coroutine waveControlCoroutine;

        /// <summary>
        /// Initialize component
        /// </summary>
        private void Awake()
        {
            if (waveConfigurations.Count == 0 && autoGenerateWaves)
            {
                GenerateWaveConfigurations();
            }

            // Initialize object pools for each enemy type
            InitializeEnemyPools();

            // Validate spawn points
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                Debug.LogError("No spawn points assigned to WaveManager!");
            }

            // Validate player transform
            if (playerTransform == null)
            {
                Debug.LogWarning("No player transform assigned to WaveManager. Trying to find Player tag.");
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    playerTransform = player.transform;
                }
                else
                {
                    Debug.LogError("No player transform found!");
                }
            }
        }

        /// <summary>
        /// Generate wave configurations based on difficulty scaling
        /// </summary>
        private void GenerateWaveConfigurations()
        {
            waveConfigurations.Clear();

            for (int i = 1; i <= maxWaveCount; i++)
            {
                WaveConfig waveConfig = new WaveConfig
                {
                    waveNumber = i,
                    baseEnemyCount = Mathf.FloorToInt(5 + (i * 2)),
                    spawnInterval = Mathf.Max(0.2f, 1.0f - (i * 0.05f)),
                    timeBetweenWaves = Mathf.Max(3.0f, 5.0f - (i * 0.1f)),
                    difficultyMultiplier = Mathf.Pow(difficultyScalingFactor, i - 1)
                };

                // Add special enemies periodically
                if (i % 3 == 0)
                {
                    // Add tank enemy
                    waveConfig.specialEnemies.Add(new SpecialEnemySpawn
                    {
                        enemyType = "TankEnemy",
                        spawnTimePercentage = 0.5f,
                        spawnPositionOffset = Vector2.zero
                    });
                }

                if (i % 5 == 0)
                {
                    // Add elite enemy
                    waveConfig.specialEnemies.Add(new SpecialEnemySpawn
                    {
                        enemyType = "EliteEnemy",
                        spawnTimePercentage = 0.75f,
                        spawnPositionOffset = Vector2.zero
                    });
                }

                waveConfigurations.Add(waveConfig);
            }
        }

        /// <summary>
        /// Initialize object pools for each enemy type
        /// </summary>
        private void InitializeEnemyPools()
        {
            foreach (var enemyType in enemyTypes)
            {
                if (enemyType.enemyPrefab == null)
                {
                    Debug.LogError($"Enemy prefab for {enemyType.enemyType} is null!");
                    continue;
                }

                BaseEnemy baseEnemyComponent = enemyType.enemyPrefab.GetComponent<BaseEnemy>();
                if (baseEnemyComponent == null)
                {
                    Debug.LogError($"Prefab {enemyType.enemyPrefab.name} doesn't have BaseEnemy component!");
                    continue;
                }

                // Create a new object pool for this enemy type
                IObjectPool<BaseEnemy> pool = new ObjectPool<BaseEnemy>(
                    createFunc: () => 
                    {
                        GameObject obj = Instantiate(enemyType.enemyPrefab);
                        BaseEnemy enemy = obj.GetComponent<BaseEnemy>();
                        return enemy;
                    },
                    actionOnGet: (enemy) => 
                    {
                        enemy.gameObject.SetActive(true);
                    },
                    actionOnRelease: (enemy) => 
                    {
                        enemy.gameObject.SetActive(false);
                    },
                    actionOnDestroy: (enemy) => 
                    {
                        Destroy(enemy.gameObject);
                    },
                    defaultCapacity: enemyType.initialPoolSize,
                    maxSize: enemyType.maxPoolSize
                );

                enemyPools.Add(enemyType.enemyType, pool);
            }
        }

        /// <summary>
        /// Start the first wave
        /// </summary>
        public void StartWaves()
        {
            if (waveControlCoroutine != null)
            {
                StopCoroutine(waveControlCoroutine);
            }

            currentWave = 0;
            waveControlCoroutine = StartCoroutine(WaveControlRoutine());
        }

        /// <summary>
        /// Skip to the next wave
        /// </summary>
        public void SkipToNextWave()
        {
            if (isWaveActive)
            {
                EndCurrentWave();
            }
        }

        /// <summary>
        /// Main wave control coroutine
        /// </summary>
        private IEnumerator WaveControlRoutine()
        {
            while (currentWave < waveConfigurations.Count || (loopFinalWave && waveConfigurations.Count > 0))
            {
                // Get the next wave index, looping if needed
                int nextWaveIndex = loopFinalWave && currentWave >= waveConfigurations.Count 
                    ? waveConfigurations.Count - 1 
                    : currentWave;
                
                // Start the wave
                yield return StartCoroutine(RunWave(waveConfigurations[nextWaveIndex]));
                
                // Wave completed
                currentWave++;
                
                // Wait between waves
                float waitTime = currentWaveConfig?.timeBetweenWaves ?? 5f;
                Debug.Log($"Wave {currentWave} completed. Next wave in {waitTime} seconds.");
                
                yield return new WaitForSeconds(waitTime);
            }
            
            // All waves completed
            Debug.Log("All waves completed!");
            onAllWavesCompleted.Invoke();
        }

        /// <summary>
        /// Run a single wave
        /// </summary>
        private IEnumerator RunWave(WaveConfig waveConfig)
        {
            currentWaveConfig = waveConfig;
            isWaveActive = true;
            waveStartTime = Time.time;
            
            // Calculate total enemies in wave
            int totalEnemies = CalculateTotalEnemiesInWave(waveConfig);
            remainingEnemiesInWave = totalEnemies;
            totalEnemiesInWave = totalEnemies;
            
            Debug.Log($"Starting Wave {waveConfig.waveNumber} with {totalEnemies} enemies.");
            onWaveStart.Invoke();
            
            // Start spawning
            spawnCoroutine = StartCoroutine(SpawnRoutine(waveConfig));
            
            // Wait until all enemies are defeated
            while (remainingEnemiesInWave > 0)
            {
                yield return null;
            }
            
            // Wave completed
            isWaveActive = false;
            onWaveCompleted.Invoke(waveConfig.waveNumber);
        }

        /// <summary>
        /// Calculate total enemies in a wave
        /// </summary>
        private int CalculateTotalEnemiesInWave(WaveConfig waveConfig)
        {
            int baseCount = Mathf.RoundToInt(waveConfig.baseEnemyCount * waveConfig.difficultyMultiplier);
            int specialCount = waveConfig.specialEnemies.Count;
            return baseCount + specialCount;
        }

        /// <summary>
        /// Spawning routine for a wave
        /// </summary>
        private IEnumerator SpawnRoutine(WaveConfig waveConfig)
        {
            isSpawning = true;
            
            // Calculate how many base enemies to spawn
            int baseEnemiesToSpawn = Mathf.RoundToInt(waveConfig.baseEnemyCount * waveConfig.difficultyMultiplier);
            int enemiesSpawned = 0;
            
            // Calculate wave duration (estimated) for special enemy timing
            float waveDuration = waveConfig.spawnInterval * baseEnemiesToSpawn;
            
            // Track special enemies to spawn
            List<SpecialEnemySpawn> pendingSpecialSpawns = new List<SpecialEnemySpawn>(waveConfig.specialEnemies);
            
            // Start spawning base enemies
            while (enemiesSpawned < baseEnemiesToSpawn)
            {
                float waveProgress = (Time.time - waveStartTime) / waveDuration;
                
                // Check for special enemies to spawn based on wave progress
                for (int i = pendingSpecialSpawns.Count - 1; i >= 0; i--)
                {
                    if (waveProgress >= pendingSpecialSpawns[i].spawnTimePercentage)
                    {
                        SpawnEnemy(pendingSpecialSpawns[i].enemyType, GetSpawnPosition(pendingSpecialSpawns[i].spawnPositionOffset));
                        pendingSpecialSpawns.RemoveAt(i);
                    }
                }
                
                // Spawn a base enemy
                SpawnEnemy(SelectRandomEnemyType(waveConfig.difficultyMultiplier), GetSpawnPosition());
                enemiesSpawned++;
                
                // Invoke spawn event
                onEnemySpawned.Invoke(enemiesSpawned, totalEnemiesInWave);
                
                // Wait for next spawn
                yield return new WaitForSeconds(waveConfig.spawnInterval);
            }
            
            // Spawn any remaining special enemies
            foreach (var specialSpawn in pendingSpecialSpawns)
            {
                SpawnEnemy(specialSpawn.enemyType, GetSpawnPosition(specialSpawn.spawnPositionOffset));
            }
            
            isSpawning = false;
        }

        /// <summary>
        /// Select a random enemy type based on weights and difficulty
        /// </summary>
        private string SelectRandomEnemyType(float difficultyMultiplier)
        {
            // Calculate weighted probabilities
            float totalWeight = 0;
            Dictionary<string, float> scaledWeights = new Dictionary<string, float>();
            
            foreach (var enemyType in enemyTypes)
            {
                // Skip elite types for random selection
                if (enemyType.enemyType.Contains("Elite") || enemyType.enemyType.Contains("Boss"))
                    continue;
                    
                // Scale weight based on difficulty
                float scaledWeight = enemyType.spawnWeight * Mathf.Pow(difficultyMultiplier, enemyType.difficultyScaling);
                scaledWeights[enemyType.enemyType] = scaledWeight;
                totalWeight += scaledWeight;
            }
            
            // No valid enemy types
            if (totalWeight <= 0)
                return "BasicEnemy";
                
            // Select based on weighted random
            float randomValue = UnityEngine.Random.Range(0, totalWeight);
            float cumulativeWeight = 0;
            
            foreach (var pair in scaledWeights)
            {
                cumulativeWeight += pair.Value;
                if (randomValue <= cumulativeWeight)
                    return pair.Key;
            }
            
            // Fallback
            return "BasicEnemy";
        }

        /// <summary>
        /// Get a valid spawn position
        /// </summary>
        private Vector3 GetSpawnPosition(Vector2 offset = default)
        {
            if (spawnPoints.Length == 0)
            {
                Debug.LogError("No spawn points available!");
                return Vector3.zero;
            }
            
            // Try to find a valid spawn point away from player
            for (int i = 0; i < 10; i++) // Try 10 times to find valid point
            {
                Transform spawnPoint = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
                Vector3 position = spawnPoint.position + new Vector3(offset.x, offset.y, 0);
                
                // Check if it's far enough from player
                if (playerTransform != null)
                {
                    float distanceToPlayer = Vector3.Distance(position, playerTransform.position);
                    if (distanceToPlayer >= minDistanceFromPlayer)
                    {
                        return position;
                    }
                }
                else
                {
                    return position;
                }
            }
            
            // If we can't find a good spawn point after 10 tries, just use a random one
            Transform fallbackSpawn = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
            return fallbackSpawn.position + new Vector3(offset.x, offset.y, 0);
        }

        /// <summary>
        /// Spawn an enemy of the specified type at the given position
        /// </summary>
        private BaseEnemy SpawnEnemy(string enemyType, Vector3 position)
        {
            // Fallback to basic enemy if type doesn't exist
            if (!enemyPools.ContainsKey(enemyType))
            {
                Debug.LogWarning($"Enemy type {enemyType} not found in pools, using BasicEnemy instead.");
                enemyType = "BasicEnemy";
                
                // If still no pool, can't spawn
                if (!enemyPools.ContainsKey(enemyType))
                {
                    Debug.LogError("Cannot spawn enemy, no valid pools!");
                    return null;
                }
            }
            
            // Get enemy from pool
            BaseEnemy enemy = enemyPools[enemyType].Get();
            
            // Setup enemy
            enemy.transform.position = position;
            enemy.gameObject.SetActive(true);
            
            // Set target to player if available
            if (playerTransform != null)
            {
                enemy.SetTarget(playerTransform.position);
            }
            
            // Setup death callback
            enemy.OnEnemyDefeated += HandleEnemyDefeated;
            
            // Add to active enemies list
            activeEnemies.Add(enemy);
            
            return enemy;
        }

        /// <summary>
        /// Handle enemy defeat
        /// </summary>
        private void HandleEnemyDefeated(BaseEnemy enemy)
        {
            if (activeEnemies.Contains(enemy))
            {
                activeEnemies.Remove(enemy);
                
                // Remove callback
                enemy.OnEnemyDefeated -= HandleEnemyDefeated;
                
                // Decrement counter and raise event
                remainingEnemiesInWave--;
                onEnemyDefeated.Invoke(remainingEnemiesInWave, totalEnemiesInWave);
                
                // Return to pool
                string enemyType = DetermineEnemyType(enemy);
                if (enemyPools.ContainsKey(enemyType))
                {
                    enemyPools[enemyType].Release(enemy);
                }
                else
                {
                    Debug.LogWarning($"Could not find pool for enemy type {enemyType}");
                    enemy.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Determine the enemy type from the enemy component
        /// </summary>
        private string DetermineEnemyType(BaseEnemy enemy)
        {
            if (enemy is SwiftEnemyController)
                return "SwiftEnemy";
            // Add more types as implemented
            
            return "BasicEnemy";
        }

        /// <summary>
        /// End current wave and clean up
        /// </summary>
        public void EndCurrentWave()
        {
            // Stop spawning
            if (spawnCoroutine != null)
            {
                StopCoroutine(spawnCoroutine);
                isSpawning = false;
            }
            
            // Remove all active enemies
            for (int i = activeEnemies.Count - 1; i >= 0; i--)
            {
                BaseEnemy enemy = activeEnemies[i];
                enemy.OnEnemyDefeated -= HandleEnemyDefeated;
                
                string enemyType = DetermineEnemyType(enemy);
                if (enemyPools.ContainsKey(enemyType))
                {
                    enemyPools[enemyType].Release(enemy);
                }
                else
                {
                    Destroy(enemy.gameObject);
                }
            }
            
            activeEnemies.Clear();
            remainingEnemiesInWave = 0;
        }

        /// <summary>
        /// Debug information
        /// </summary>
        private void OnGUI()
        {
            if (!showDebugInfo) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 300, 150));
            GUILayout.Label($"Wave: {currentWave + 1}/{waveConfigurations.Count}");
            GUILayout.Label($"Enemies: {remainingEnemiesInWave}/{totalEnemiesInWave}");
            GUILayout.Label($"Active Enemies: {activeEnemies.Count}");
            GUILayout.Label($"Is Spawning: {isSpawning}");
            
            if (isWaveActive && currentWaveConfig != null)
            {
                float difficultyMultiplier = currentWaveConfig.difficultyMultiplier;
                GUILayout.Label($"Difficulty: {difficultyMultiplier:F2}x");
            }
            
            GUILayout.EndArea();
        }

        /// <summary>
        /// Draw debug gizmos
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (spawnPoints != null)
            {
                Gizmos.color = Color.red;
                foreach (var spawnPoint in spawnPoints)
                {
                    if (spawnPoint != null)
                    {
                        Gizmos.DrawWireSphere(spawnPoint.position, 0.5f);
                    }
                }
            }
            
            if (playerTransform != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(playerTransform.position, minDistanceFromPlayer);
            }
        }
    }
} 