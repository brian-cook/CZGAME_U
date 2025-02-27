using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System;

namespace CZ.Editor
{
    /// <summary>
    /// Editor utility for ensuring proper layer setup for collision detection
    /// </summary>
    [InitializeOnLoad]
    public class LayerSetupEditor
    {
        // Define layer slots for required layers
        private const int PLAYER_LAYER_INDEX = 8; // Recommended index for Player layer
        private const int ENEMY_LAYER_INDEX = 9;  // Recommended index for Enemy layer
        
        // Tags needed for the project
        private static readonly string[] requiredTags = new string[] 
        {
            "Player",
            "Enemy"
        };

        // Constructor that gets executed when Unity launches
        static LayerSetupEditor()
        {
            // Log that the tool has started
            Debug.Log("[LayerSetupEditor] Checking required physics layers...");
            
            // Check and create layers if they don't exist
            EnsureRequiredLayers();
            
            // Check and create tags if they don't exist
            EnsureRequiredTags();
        }
        
        /// <summary>
        /// Ensures all required layers exist in the project
        /// </summary>
        private static void EnsureRequiredLayers()
        {
            // Get the current layer names
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
                
            SerializedProperty layersProp = tagManager.FindProperty("layers");
            
            if (layersProp == null || !layersProp.isArray)
            {
                Debug.LogError("[LayerSetupEditor] Layers property not found in TagManager");
                return;
            }
            
            // Check for Player layer
            SerializedProperty playerLayerProp = layersProp.GetArrayElementAtIndex(PLAYER_LAYER_INDEX);
            if (string.IsNullOrEmpty(playerLayerProp.stringValue))
            {
                playerLayerProp.stringValue = "Player";
                Debug.Log("[LayerSetupEditor] Created Player layer at index " + PLAYER_LAYER_INDEX);
            }
            else if (playerLayerProp.stringValue != "Player")
            {
                Debug.LogWarning($"[LayerSetupEditor] Layer at index {PLAYER_LAYER_INDEX} is '{playerLayerProp.stringValue}' instead of 'Player'");
            }
            
            // Check for Enemy layer
            SerializedProperty enemyLayerProp = layersProp.GetArrayElementAtIndex(ENEMY_LAYER_INDEX);
            if (string.IsNullOrEmpty(enemyLayerProp.stringValue))
            {
                enemyLayerProp.stringValue = "Enemy";
                Debug.Log("[LayerSetupEditor] Created Enemy layer at index " + ENEMY_LAYER_INDEX);
            }
            else if (enemyLayerProp.stringValue != "Enemy")
            {
                Debug.LogWarning($"[LayerSetupEditor] Layer at index {ENEMY_LAYER_INDEX} is '{enemyLayerProp.stringValue}' instead of 'Enemy'");
            }
            
            // Apply changes
            tagManager.ApplyModifiedProperties();
            
            // Verify layers were created successfully
            int playerLayer = LayerMask.NameToLayer("Player");
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            
            Debug.Log($"[LayerSetupEditor] Layer verification - Player: {playerLayer}, Enemy: {enemyLayer}");
        }
        
        /// <summary>
        /// Ensures all required tags exist in the project
        /// </summary>
        private static void EnsureRequiredTags()
        {
            // Get the current tag names
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            
            SerializedProperty tagsProp = tagManager.FindProperty("tags");
            
            if (tagsProp == null || !tagsProp.isArray)
            {
                Debug.LogError("[LayerSetupEditor] Tags property not found in TagManager");
                return;
            }
            
            // Track existing tags
            List<string> existingTags = new List<string>();
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                existingTags.Add(tagsProp.GetArrayElementAtIndex(i).stringValue);
            }
            
            // Add required tags if they don't exist
            bool tagsChanged = false;
            foreach (string tag in requiredTags)
            {
                // Skip "Player" tag since it's built-in
                if (tag == "Player") continue;
                
                if (!existingTags.Contains(tag))
                {
                    tagsProp.arraySize++;
                    tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
                    Debug.Log($"[LayerSetupEditor] Added required tag: {tag}");
                    tagsChanged = true;
                }
            }
            
            // Apply changes if any tags were added
            if (tagsChanged)
            {
                tagManager.ApplyModifiedProperties();
                Debug.Log("[LayerSetupEditor] Tags updated successfully");
            }
        }
    }
} 