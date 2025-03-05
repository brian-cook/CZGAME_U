#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System;

namespace CZ.Editor
{
    public class ReferenceResolver : EditorWindow
    {
        [MenuItem("Tools/CZ Game/Fix Assembly References")]
        public static void ShowWindow()
        {
            GetWindow<ReferenceResolver>("Assembly Reference Fixer");
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Assembly Reference Fixer", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (GUILayout.Button("Check for Circular References"))
            {
                CheckCircularReferences();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Fix References"))
            {
                FixReferences();
            }
        }

        private void CheckCircularReferences()
        {
            Dictionary<string, List<string>> dependencies = new Dictionary<string, List<string>>();
            string[] asmdefFiles = Directory.GetFiles(Application.dataPath, "*.asmdef", SearchOption.AllDirectories);

            // Load all dependencies
            foreach (var asmdefPath in asmdefFiles)
            {
                string relativePath = asmdefPath.Replace(Application.dataPath, "Assets");
                string json = File.ReadAllText(asmdefPath);
                string assemblyName = Regex.Match(json, "\"name\":\\s*\"([^\"]+)\"").Groups[1].Value;
                
                List<string> refs = new List<string>();
                MatchCollection matches = Regex.Matches(json, "\"references\":\\s*\\[([^\\]]+)\\]");
                if (matches.Count > 0)
                {
                    string refsString = matches[0].Groups[1].Value;
                    MatchCollection refMatches = Regex.Matches(refsString, "\"([^\"]+)\"");
                    foreach (Match match in refMatches)
                    {
                        string reference = match.Groups[1].Value;
                        // Handle GUID references
                        if (reference.StartsWith("GUID:"))
                            continue;
                        refs.Add(reference);
                    }
                }
                
                dependencies[assemblyName] = refs;
                Debug.Log($"Assembly {assemblyName} references: {string.Join(", ", refs)}");
            }

            // Check for circular references
            foreach (var assembly in dependencies.Keys)
            {
                CheckForCircularDependency(assembly, new HashSet<string>(), dependencies);
            }
        }

        private bool CheckForCircularDependency(string assembly, HashSet<string> visitedAssemblies, Dictionary<string, List<string>> dependencies)
        {
            if (visitedAssemblies.Contains(assembly))
            {
                Debug.LogError($"Circular dependency detected: {string.Join(" -> ", visitedAssemblies)} -> {assembly}");
                return true;
            }

            visitedAssemblies.Add(assembly);

            if (dependencies.TryGetValue(assembly, out List<string> references))
            {
                foreach (var reference in references)
                {
                    HashSet<string> newVisited = new HashSet<string>(visitedAssemblies);
                    if (dependencies.ContainsKey(reference) && CheckForCircularDependency(reference, newVisited, dependencies))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void FixReferences()
        {
            // Implement reference fixing logic
            // Sample implementation: ensure CZ.Core.Debug implements ICollisionDebugger
            string debugAsmPath = Path.Combine(Application.dataPath, "_Project/Scripts/Core/Debug/CZ.Core.Debug.asmdef");
            string interfacesAsmPath = Path.Combine(Application.dataPath, "_Project/Scripts/Core/Interfaces/CZ.Core.Interfaces.asmdef");
            
            if (File.Exists(debugAsmPath) && File.Exists(interfacesAsmPath))
            {
                string debugAsm = File.ReadAllText(debugAsmPath);
                
                // Check if already references interfaces
                if (!debugAsm.Contains("\"CZ.Core.Interfaces\""))
                {
                    debugAsm = debugAsm.Replace("\"references\": [", "\"references\": [\n        \"CZ.Core.Interfaces\",");
                    File.WriteAllText(debugAsmPath, debugAsm);
                    Debug.Log("Added CZ.Core.Interfaces reference to CZ.Core.Debug.asmdef");
                }
            }
            
            AssetDatabase.Refresh();
        }
    }
}
#endif 