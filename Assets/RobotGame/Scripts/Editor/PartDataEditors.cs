#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using RobotGame.Data;
using RobotGame.Utils;

namespace RobotGame.Editor
{
    /// <summary>
    /// Editor personalizado para StructuralPartData que agrega botón de auto-detección.
    /// </summary>
    [CustomEditor(typeof(StructuralPartData))]
    public class StructuralPartDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Dibujar el inspector por defecto
            DrawDefaultInspector();
            
            StructuralPartData partData = (StructuralPartData)target;
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Auto-Detección", EditorStyles.boldLabel);
            
            if (partData.prefab == null)
            {
                EditorGUILayout.HelpBox("Asigna un prefab para habilitar la auto-detección de grillas.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Busca Empties con nomenclatura Head_NxM_SX_nombre en el prefab y configura automáticamente las grillas de armadura.",
                    MessageType.Info
                );
                
                if (GUILayout.Button("Auto-Detectar Grillas Head", GUILayout.Height(30)))
                {
                    Undo.RecordObject(partData, "Auto-Detect Head Grids");
                    GridAutoDetector.AutoConfigureStructuralPart(partData);
                    EditorUtility.SetDirty(partData);
                }
                
                // Mostrar preview de lo que detectaría
                if (GUILayout.Button("Preview (sin aplicar)"))
                {
                    var grids = GridAutoDetector.DetectHeadGrids(partData.prefab);
                    if (grids.Count == 0)
                    {
                        Debug.Log("No se encontraron grillas Head_ en el prefab.");
                    }
                    else
                    {
                        Debug.Log($"Se encontrarían {grids.Count} grillas:");
                        foreach (var grid in grids)
                        {
                            Debug.Log($"  - {grid.transformName}: {grid.gridInfo.sizeX}x{grid.gridInfo.sizeY} {grid.gridInfo.surrounding}");
                        }
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Editor personalizado para ArmorPartData que agrega botón de auto-detección.
    /// </summary>
    [CustomEditor(typeof(ArmorPartData))]
    public class ArmorPartDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Dibujar el inspector por defecto
            DrawDefaultInspector();
            
            ArmorPartData armorData = (ArmorPartData)target;
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Auto-Detección", EditorStyles.boldLabel);
            
            if (armorData.prefab == null)
            {
                EditorGUILayout.HelpBox("Asigna un prefab para habilitar la auto-detección de grillas.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Busca Empties con nomenclatura Tail_NxM_SX_nombre para la grilla principal y Head_NxM_SX_nombre para grillas adicionales.",
                    MessageType.Info
                );
                
                if (GUILayout.Button("Auto-Detectar Grillas", GUILayout.Height(30)))
                {
                    Undo.RecordObject(armorData, "Auto-Detect Armor Grids");
                    GridAutoDetector.AutoConfigureArmorPart(armorData);
                    EditorUtility.SetDirty(armorData);
                }
                
                // Mostrar preview de lo que detectaría
                if (GUILayout.Button("Preview (sin aplicar)"))
                {
                    var tailGrid = GridAutoDetector.DetectSingleTailGrid(armorData.prefab);
                    var headGrids = GridAutoDetector.DetectHeadGrids(armorData.prefab);
                    
                    if (tailGrid == null)
                    {
                        Debug.Log("No se encontró grilla Tail_ en el prefab.");
                    }
                    else
                    {
                        Debug.Log($"Grilla Tail encontrada: {tailGrid.transformName} - {tailGrid.gridInfo.sizeX}x{tailGrid.gridInfo.sizeY} {tailGrid.gridInfo.surrounding}");
                    }
                    
                    if (headGrids.Count > 0)
                    {
                        Debug.Log($"Se encontrarían {headGrids.Count} grillas Head adicionales:");
                        foreach (var grid in headGrids)
                        {
                            Debug.Log($"  - {grid.transformName}: {grid.gridInfo.sizeX}x{grid.gridInfo.sizeY} {grid.gridInfo.surrounding}");
                        }
                    }
                }
            }
        }
    }
}
#endif
