using UnityEngine;
using UnityEditor;
using CZ.Core.Enemy;
using System.Collections.Generic;

namespace CZ.Editor
{
    /// <summary>
    /// Custom editor for the WaveManager component
    /// </summary>
    [CustomEditor(typeof(WaveManager))]
    public class WaveManagerEditor : UnityEditor.Editor
    {
        private bool showWaveSettings = true;
        private bool showEnemyTypes = true;
        private bool showSpawnSettings = true;
        private bool showDebugOptions = true;
        private bool showEvents = false;

        /// <summary>
        /// Custom inspector for WaveManager
        /// </summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            WaveManager waveManager = (WaveManager)target;

            // Title and description
            EditorGUILayout.Space();
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.fontSize = 14;
            titleStyle.alignment = TextAnchor.MiddleCenter;
            
            EditorGUILayout.LabelField("Wave Manager", titleStyle);
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("This component manages enemy wave spawning, difficulty progression, and wave completion.", MessageType.Info);
            EditorGUILayout.Space();

            // Wave Settings
            showWaveSettings = EditorGUILayout.Foldout(showWaveSettings, "Wave Settings", true, EditorStyles.foldoutHeader);
            if (showWaveSettings)
            {
                EditorGUI.indentLevel++;
                
                // Auto generate waves
                EditorGUILayout.PropertyField(serializedObject.FindProperty("autoGenerateWaves"));
                
                // Wave configurations
                SerializedProperty waveConfigs = serializedObject.FindProperty("waveConfigurations");
                
                bool autoGenerate = serializedObject.FindProperty("autoGenerateWaves").boolValue;
                EditorGUI.BeginDisabledGroup(autoGenerate);
                
                EditorGUILayout.PropertyField(waveConfigs, new GUIContent("Wave Configurations"), true);
                
                if (autoGenerate)
                {
                    EditorGUILayout.HelpBox("Wave configurations are auto-generated. Disable auto-generate to edit manually.", MessageType.Info);
                }
                
                EditorGUI.EndDisabledGroup();
                
                // Auto generation settings
                if (autoGenerate)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Auto Generation Settings", EditorStyles.boldLabel);
                    
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("difficultyScalingFactor"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("maxWaveCount"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("loopFinalWave"));
                    
                    if (GUILayout.Button("Preview Generated Waves"))
                    {
                        // Call method to generate and display wave preview
                        PreviewGeneratedWaves(waveManager);
                    }
                }
                
                EditorGUI.indentLevel--;
            }

            // Enemy Types
            showEnemyTypes = EditorGUILayout.Foldout(showEnemyTypes, "Enemy Types", true, EditorStyles.foldoutHeader);
            if (showEnemyTypes)
            {
                EditorGUI.indentLevel++;
                SerializedProperty enemyTypes = serializedObject.FindProperty("enemyTypes");
                
                EditorGUILayout.PropertyField(enemyTypes, true);
                
                if (enemyTypes.arraySize == 0)
                {
                    EditorGUILayout.HelpBox("No enemy types defined. Add at least one enemy type.", MessageType.Warning);
                    
                    if (GUILayout.Button("Add Basic Enemy Type"))
                    {
                        AddBasicEnemyType(enemyTypes);
                    }
                }
                else
                {
                    ValidateEnemyTypes(enemyTypes);
                }
                
                EditorGUI.indentLevel--;
            }

            // Spawn Settings
            showSpawnSettings = EditorGUILayout.Foldout(showSpawnSettings, "Spawn Settings", true, EditorStyles.foldoutHeader);
            if (showSpawnSettings)
            {
                EditorGUI.indentLevel++;
                
                SerializedProperty spawnPoints = serializedObject.FindProperty("spawnPoints");
                EditorGUILayout.PropertyField(spawnPoints, true);
                
                if (spawnPoints.arraySize == 0)
                {
                    EditorGUILayout.HelpBox("No spawn points defined. The wave manager needs at least one spawn point.", MessageType.Error);
                }
                
                EditorGUILayout.PropertyField(serializedObject.FindProperty("minDistanceFromPlayer"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("playerTransform"));
                
                if (serializedObject.FindProperty("playerTransform").objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox("No player transform assigned. Will try to find by tag at runtime.", MessageType.Warning);
                }
                
                EditorGUI.indentLevel--;
            }

            // Debug Options
            showDebugOptions = EditorGUILayout.Foldout(showDebugOptions, "Debug Options", true, EditorStyles.foldoutHeader);
            if (showDebugOptions)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.PropertyField(serializedObject.FindProperty("showDebugInfo"));
                
                EditorGUI.indentLevel--;
            }

            // Events
            showEvents = EditorGUILayout.Foldout(showEvents, "Events", true, EditorStyles.foldoutHeader);
            if (showEvents)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onWaveStart"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onWaveCompleted"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onAllWavesCompleted"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onEnemySpawned"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onEnemyDefeated"));
                
                EditorGUI.indentLevel--;
            }

            // Control buttons at the bottom
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            
            if (Application.isPlaying)
            {
                if (GUILayout.Button("Start Waves", GUILayout.Height(30)))
                {
                    waveManager.StartWaves();
                }
                
                if (GUILayout.Button("Skip Wave", GUILayout.Height(30)))
                {
                    waveManager.SkipToNextWave();
                }
                
                if (GUILayout.Button("End Current Wave", GUILayout.Height(30)))
                {
                    waveManager.EndCurrentWave();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Play the game to access runtime controls", MessageType.Info);
            }
            
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Add a basic enemy type to the list
        /// </summary>
        private void AddBasicEnemyType(SerializedProperty enemyTypes)
        {
            enemyTypes.arraySize++;
            SerializedProperty newType = enemyTypes.GetArrayElementAtIndex(enemyTypes.arraySize - 1);
            
            newType.FindPropertyRelative("enemyType").stringValue = "BasicEnemy";
            newType.FindPropertyRelative("initialPoolSize").intValue = 20;
            newType.FindPropertyRelative("maxPoolSize").intValue = 50;
            newType.FindPropertyRelative("spawnWeight").floatValue = 1.0f;
            newType.FindPropertyRelative("difficultyScaling").floatValue = 1.0f;
        }

        /// <summary>
        /// Validate enemy types
        /// </summary>
        private void ValidateEnemyTypes(SerializedProperty enemyTypes)
        {
            bool hasBasicEnemy = false;
            
            for (int i = 0; i < enemyTypes.arraySize; i++)
            {
                SerializedProperty enemyType = enemyTypes.GetArrayElementAtIndex(i);
                string typeName = enemyType.FindPropertyRelative("enemyType").stringValue;
                
                if (typeName == "BasicEnemy")
                {
                    hasBasicEnemy = true;
                }
                
                // Check if prefab is assigned
                SerializedProperty prefab = enemyType.FindPropertyRelative("enemyPrefab");
                if (prefab.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox($"Enemy prefab for '{typeName}' is not assigned!", MessageType.Error);
                }
                else
                {
                    // Check if prefab has BaseEnemy component
                    GameObject prefabObj = prefab.objectReferenceValue as GameObject;
                    if (prefabObj != null && prefabObj.GetComponent<BaseEnemy>() == null)
                    {
                        EditorGUILayout.HelpBox($"Prefab for '{typeName}' doesn't have a BaseEnemy component!", MessageType.Error);
                    }
                }
            }
            
            if (!hasBasicEnemy)
            {
                EditorGUILayout.HelpBox("No 'BasicEnemy' type found. This is the fallback type and should be included.", MessageType.Warning);
                
                if (GUILayout.Button("Add Basic Enemy Type"))
                {
                    AddBasicEnemyType(enemyTypes);
                }
            }
        }

        /// <summary>
        /// Preview generated waves
        /// </summary>
        private void PreviewGeneratedWaves(WaveManager waveManager)
        {
            // Create a temporary list for preview
            List<WaveManager.WaveConfig> previewWaves = new List<WaveManager.WaveConfig>();
            
            float difficultyScalingFactor = serializedObject.FindProperty("difficultyScalingFactor").floatValue;
            int maxWaveCount = serializedObject.FindProperty("maxWaveCount").intValue;
            
            // Generate preview waves
            for (int i = 1; i <= maxWaveCount; i++)
            {
                WaveManager.WaveConfig waveConfig = new WaveManager.WaveConfig
                {
                    waveNumber = i,
                    baseEnemyCount = Mathf.FloorToInt(5 + (i * 2)),
                    spawnInterval = Mathf.Max(0.2f, 1.0f - (i * 0.05f)),
                    timeBetweenWaves = Mathf.Max(3.0f, 5.0f - (i * 0.1f)),
                    difficultyMultiplier = Mathf.Pow(difficultyScalingFactor, i - 1)
                };
                
                if (i % 3 == 0)
                {
                    waveConfig.specialEnemies.Add(new WaveManager.SpecialEnemySpawn
                    {
                        enemyType = "TankEnemy",
                        spawnTimePercentage = 0.5f,
                        spawnPositionOffset = Vector2.zero
                    });
                }
                
                if (i % 5 == 0)
                {
                    waveConfig.specialEnemies.Add(new WaveManager.SpecialEnemySpawn
                    {
                        enemyType = "EliteEnemy",
                        spawnTimePercentage = 0.75f,
                        spawnPositionOffset = Vector2.zero
                    });
                }
                
                previewWaves.Add(waveConfig);
            }
            
            // Show preview window
            WavePreviewWindow.ShowWindow(previewWaves);
        }
    }

    /// <summary>
    /// Preview window for generated waves
    /// </summary>
    public class WavePreviewWindow : EditorWindow
    {
        private List<WaveManager.WaveConfig> waves;
        private Vector2 scrollPosition;

        /// <summary>
        /// Show the wave preview window
        /// </summary>
        public static void ShowWindow(List<WaveManager.WaveConfig> waves)
        {
            WavePreviewWindow window = GetWindow<WavePreviewWindow>("Wave Preview");
            window.waves = waves;
            window.minSize = new Vector2(600, 400);
        }

        /// <summary>
        /// Draw the window GUI
        /// </summary>
        private void OnGUI()
        {
            if (waves == null || waves.Count == 0)
            {
                EditorGUILayout.HelpBox("No waves to preview.", MessageType.Info);
                return;
            }
            
            EditorGUILayout.Space();
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.fontSize = 14;
            titleStyle.alignment = TextAnchor.MiddleCenter;
            
            EditorGUILayout.LabelField("Wave Preview", titleStyle);
            EditorGUILayout.Space();
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            for (int i = 0; i < waves.Count; i++)
            {
                WaveManager.WaveConfig wave = waves[i];
                
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // Wave header
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Wave {wave.waveNumber}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Difficulty: {wave.difficultyMultiplier:F2}x", GUILayout.Width(120));
                EditorGUILayout.EndHorizontal();
                
                // Wave details
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField($"Base Enemies: {wave.baseEnemyCount}");
                EditorGUILayout.LabelField($"Spawn Interval: {wave.spawnInterval:F2}s");
                EditorGUILayout.LabelField($"Time Between Waves: {wave.timeBetweenWaves:F2}s");
                
                // Special enemies
                if (wave.specialEnemies.Count > 0)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Special Enemies:", EditorStyles.boldLabel);
                    
                    foreach (var special in wave.specialEnemies)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"• {special.enemyType}");
                        EditorGUILayout.LabelField($"Spawn: {special.spawnTimePercentage * 100:F0}% through wave", GUILayout.Width(180));
                        EditorGUILayout.EndHorizontal();
                    }
                }
                
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }
            
            EditorGUILayout.EndScrollView();
        }
    }
} 