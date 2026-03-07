using UnityEngine;
using UnityEditor;
using RobotGame.Components;
using RobotGame.Data;
using RobotGame.Utils;

namespace RobotGame.Editor
{
    /// <summary>
    /// Editor personalizado para StudGridHead.
    /// </summary>
    [CustomEditor(typeof(StudGridHead))]
    public class StudGridHeadEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            StudGridHead grid = (StudGridHead)target;
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Información", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Studs detectados: {grid.StudCount}");
            EditorGUILayout.LabelField($"Studs disponibles: {grid.AvailableStudCount}");
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Detectar Studs"))
            {
                grid.DetectStuds();
                EditorUtility.SetDirty(grid);
            }
            
            if (GUILayout.Button("Limpiar Ocupación"))
            {
                grid.ClearAllOccupation();
                EditorUtility.SetDirty(grid);
            }
        }
    }
    
    /// <summary>
    /// Editor personalizado para StudGridTail.
    /// </summary>
    [CustomEditor(typeof(StudGridTail))]
    public class StudGridTailEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            StudGridTail grid = (StudGridTail)target;
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Información", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Studs detectados: {grid.StudCount}");
            
            if (grid.StudCount > 0 && grid.Studs[0].tierInfo.IsValid)
            {
                EditorGUILayout.LabelField($"Tier: {grid.Studs[0].tierInfo}");
            }
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Detectar Studs"))
            {
                grid.DetectStuds();
                EditorUtility.SetDirty(grid);
            }
        }
    }
    
    /// <summary>
    /// Editor personalizado para CollisionValidator.
    /// </summary>
    [CustomEditor(typeof(CollisionValidator))]
    public class CollisionValidatorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            CollisionValidator validator = (CollisionValidator)target;
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Activar Modo Edición (Test)"))
            {
                // Buscar el primer robot en la escena
                var robot = FindObjectOfType<StructuralPart>();
                if (robot != null)
                {
                    validator.EnableEditMode(robot.transform);
                }
                else
                {
                    // Debug.LogWarning("No se encontró ningún StructuralPart en la escena");
                }
            }
            
            if (GUILayout.Button("Desactivar Modo Edición"))
            {
                validator.DisableEditMode();
            }
        }
    }
    
    /// <summary>
    /// Editor personalizado para ArmorPartData.
    /// </summary>
    [CustomEditor(typeof(ArmorPartData))]
    public class ArmorPartDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            ArmorPartData data = (ArmorPartData)target;
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Auto-Detección", EditorStyles.boldLabel);
            
            if (data.prefab != null)
            {
                if (GUILayout.Button("Detectar Studs y Boxes del Prefab"))
                {
                    DetectFromPrefab(data);
                }
                
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Contenido del Prefab:", EditorStyles.miniLabel);
                
                // Mostrar studs detectados
                var tailStuds = StudDetector.DetectTailStuds(data.prefab.transform);
                EditorGUILayout.LabelField($"  Studs Tail: {tailStuds.Count}");
                
                foreach (var stud in tailStuds)
                {
                    EditorGUILayout.LabelField($"    - {stud.transformName} (T{stud.tierInfo})");
                }
                
                // Mostrar boxes detectados
                int boxCount = CountBoxes(data.prefab.transform);
                EditorGUILayout.LabelField($"  BoxColliders (Box_): {boxCount}");
            }
            else
            {
                EditorGUILayout.HelpBox("Asigna un prefab para ver la auto-detección", MessageType.Info);
            }
        }
        
        private void DetectFromPrefab(ArmorPartData data)
        {
            var tailStuds = StudDetector.DetectTailStuds(data.prefab.transform);
            
            if (tailStuds.Count > 0)
            {
                // Usar el tier del primer stud encontrado
                data.tierInfo = tailStuds[0].tierInfo;
                EditorUtility.SetDirty(data);
                // Debug.Log($"ArmorPartData: Detectado Tier {data.tierInfo} con {tailStuds.Count} studs");
            }
            else
            {
                // Debug.LogWarning($"ArmorPartData: No se encontraron studs Tail_ en {data.prefab.name}");
            }
        }
        
        private int CountBoxes(Transform root)
        {
            int count = 0;
            foreach (Transform child in root)
            {
                if (child.name.StartsWith("Box_"))
                {
                    count++;
                }
                count += CountBoxes(child);
            }
            return count;
        }
    }
    
    /// <summary>
    /// Editor personalizado para StructuralPartData.
    /// </summary>
    [CustomEditor(typeof(StructuralPartData))]
    public class StructuralPartDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            StructuralPartData data = (StructuralPartData)target;
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Auto-Detección", EditorStyles.boldLabel);
            
            if (data.prefab != null)
            {
                // Botón para detectar todo automáticamente
                if (GUILayout.Button("Detectar Todo del Prefab (Sockets + Studs)"))
                {
                    DetectAllFromPrefab(data);
                }
                
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Contenido del Prefab:", EditorStyles.miniLabel);
                
                // Mostrar sockets detectados
                var detectedSockets = SocketAutoDetector.DetectSockets(data.prefab);
                EditorGUILayout.LabelField($"  Sockets (Socket_): {detectedSockets.Count}");
                
                foreach (var socket in detectedSockets)
                {
                    EditorGUILayout.LabelField($"    - {socket.socketType} ({socket.transformName})");
                }
                
                // Mostrar studs Head detectados
                var headStuds = StudDetector.DetectHeadStuds(data.prefab.transform);
                EditorGUILayout.LabelField($"  Studs Head: {headStuds.Count}");
                
                // Agrupar por tier
                var tierCounts = new System.Collections.Generic.Dictionary<string, int>();
                foreach (var stud in headStuds)
                {
                    string tierKey = stud.tierInfo.ToString();
                    if (!tierCounts.ContainsKey(tierKey))
                        tierCounts[tierKey] = 0;
                    tierCounts[tierKey]++;
                }
                
                foreach (var kvp in tierCounts)
                {
                    EditorGUILayout.LabelField($"    - T{kvp.Key}: {kvp.Value} studs");
                }
                
                // Mostrar boxes detectados
                int boxCount = CountBoxes(data.prefab.transform);
                EditorGUILayout.LabelField($"  BoxColliders (Box_): {boxCount}");
                
                // Mostrar diferencias entre detectado y configurado
                EditorGUILayout.Space();
                if (data.structuralSockets.Count != detectedSockets.Count)
                {
                    EditorGUILayout.HelpBox(
                        $"Sockets configurados ({data.structuralSockets.Count}) != detectados ({detectedSockets.Count}). " +
                        "Presiona 'Detectar Todo' para sincronizar.", 
                        MessageType.Warning);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Asigna un prefab para ver la auto-detección", MessageType.Info);
            }
        }
        
        private void DetectAllFromPrefab(StructuralPartData data)
        {
            // Detectar sockets
            var detectedSockets = SocketAutoDetector.DetectSockets(data.prefab);
            
            if (detectedSockets.Count > 0)
            {
                data.structuralSockets.Clear();
                data.structuralSockets.AddRange(detectedSockets);
                // Debug.Log($"StructuralPartData '{data.displayName}': Detectados {detectedSockets.Count} sockets");
            }
            else
            {
                // Debug.Log($"StructuralPartData '{data.displayName}': No se encontraron sockets Socket_*");
            }
            
            // Detectar studs Head (solo para logging, se detectan en runtime)
            var headStuds = StudDetector.DetectHeadStuds(data.prefab.transform);
            if (headStuds.Count > 0)
            {
                // Debug.Log($"StructuralPartData '{data.displayName}': {headStuds.Count} studs Head_ detectados (se usarán en runtime)");
            }
            
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }
        
        private int CountBoxes(Transform root)
        {
            int count = 0;
            foreach (Transform child in root)
            {
                if (child.name.StartsWith("Box_"))
                {
                    count++;
                }
                count += CountBoxes(child);
            }
            return count;
        }
    }
}
