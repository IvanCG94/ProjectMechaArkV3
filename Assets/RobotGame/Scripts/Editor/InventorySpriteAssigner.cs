using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace RobotGame.Editor
{
    /// <summary>
    /// Herramienta para generar y asignar sprites de inventario desde el prefab del ScriptableObject.
    /// 
    /// Campos esperados en el ScriptableObject (PartDataBase):
    /// - icon: Sprite donde se asignará el icono generado
    /// - prefab: GameObject usado para generar el sprite
    /// 
    /// Uso:
    /// 1. Selecciona ScriptableObjects en el Project
    /// 2. Click derecho → "Generate Sprite from Prefab"
    /// 
    /// O desde: Tools → Robot Game → Generate Sprites from ScriptableObjects
    /// </summary>
    public class InventorySpriteAssigner : EditorWindow
    {
        private string scriptableObjectFolder = "Assets/RobotGame/Data";
        private string spriteFieldName = "icon";
        private string prefabFieldName = "prefab";
        private string spriteOutputFolder = ""; // Vacío = junto al SO
        
        private Vector2 scrollPosition;
        private List<GenerationPreview> previews = new List<GenerationPreview>();
        
        private class GenerationPreview
        {
            public ScriptableObject scriptableObject;
            public string soPath;
            public GameObject prefab;
            public Sprite currentSprite;
            public bool selected;
        }
        
        [MenuItem("Tools/Robot Game/Generate Sprites from ScriptableObjects")]
        public static void ShowWindow()
        {
            GetWindow<InventorySpriteAssigner>("Sprite Generator");
        }
        
        /// <summary>
        /// Click derecho en ScriptableObjects seleccionados.
        /// </summary>
        [MenuItem("Assets/Generate Sprite from Prefab", false, 101)]
        public static void GenerateFromSelection()
        {
            // Usar configuración por defecto
            string spriteField = "icon";
            string prefabField = "prefab";
            
            int generated = 0;
            
            foreach (Object obj in Selection.objects)
            {
                if (obj is ScriptableObject so)
                {
                    if (GenerateSpriteForSO(so, spriteField, prefabField, null))
                    {
                        generated++;
                    }
                }
            }
            
            if (generated > 0)
            {
                AssetDatabase.Refresh();
                AssetDatabase.SaveAssets();
                Debug.Log($"[SpriteGenerator] Generados y asignados {generated} sprites.");
            }
            else
            {
                Debug.LogWarning("[SpriteGenerator] No se encontraron ScriptableObjects válidos con prefab asignado.");
            }
        }
        
        [MenuItem("Assets/Generate Sprite from Prefab", true)]
        public static bool ValidateGenerateFromSelection()
        {
            foreach (Object obj in Selection.objects)
            {
                if (obj is ScriptableObject) return true;
            }
            return false;
        }
        
        private void OnGUI()
        {
            GUILayout.Label("Generar Sprites desde ScriptableObjects", EditorStyles.boldLabel);
            
            EditorGUILayout.Space();
            
            EditorGUILayout.HelpBox(
                "Esta herramienta genera sprites automáticamente usando el prefab asignado " +
                "en cada ScriptableObject y lo asigna al campo 'icon'.",
                MessageType.Info
            );
            
            EditorGUILayout.Space();
            
            GUILayout.Label("Configuración de Campos", EditorStyles.boldLabel);
            
            spriteFieldName = EditorGUILayout.TextField("Campo de Icono", spriteFieldName);
            prefabFieldName = EditorGUILayout.TextField("Campo de Prefab", prefabFieldName);
            
            EditorGUILayout.Space();
            
            GUILayout.Label("Carpetas", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            scriptableObjectFolder = EditorGUILayout.TextField("Carpeta de SOs", scriptableObjectFolder);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string folder = EditorUtility.OpenFolderPanel("Seleccionar Carpeta", "Assets", "");
                if (!string.IsNullOrEmpty(folder) && folder.StartsWith(Application.dataPath))
                {
                    scriptableObjectFolder = "Assets" + folder.Substring(Application.dataPath.Length);
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            spriteOutputFolder = EditorGUILayout.TextField("Carpeta de Sprites", spriteOutputFolder);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string folder = EditorUtility.OpenFolderPanel("Carpeta de Salida", "Assets", "");
                if (!string.IsNullOrEmpty(folder) && folder.StartsWith(Application.dataPath))
                {
                    spriteOutputFolder = "Assets" + folder.Substring(Application.dataPath.Length);
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.HelpBox(
                "Si 'Carpeta de Sprites' está vacía, los sprites se guardan junto al ScriptableObject.",
                MessageType.None
            );
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Escanear ScriptableObjects"))
            {
                ScanScriptableObjects();
            }
            
            EditorGUILayout.Space();
            
            // Mostrar preview
            if (previews.Count > 0)
            {
                GUILayout.Label($"Encontrados: {previews.Count} ScriptableObjects con prefab", EditorStyles.boldLabel);
                
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(250));
                
                foreach (var preview in previews)
                {
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    
                    preview.selected = EditorGUILayout.Toggle(preview.selected, GUILayout.Width(20));
                    
                    EditorGUILayout.ObjectField(preview.scriptableObject, typeof(ScriptableObject), false, GUILayout.Width(150));
                    
                    GUILayout.Label("→", GUILayout.Width(20));
                    
                    EditorGUILayout.ObjectField(preview.prefab, typeof(GameObject), false, GUILayout.Width(120));
                    
                    if (preview.currentSprite != null)
                    {
                        GUILayout.Label("(tiene sprite)", EditorStyles.miniLabel);
                    }
                    else
                    {
                        GUILayout.Label("(sin sprite)", EditorStyles.miniLabel);
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
                
                EditorGUILayout.EndScrollView();
                
                EditorGUILayout.Space();
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Seleccionar Todos"))
                {
                    foreach (var p in previews) p.selected = true;
                }
                if (GUILayout.Button("Deseleccionar"))
                {
                    foreach (var p in previews) p.selected = false;
                }
                if (GUILayout.Button("Solo Sin Sprite"))
                {
                    foreach (var p in previews) p.selected = p.currentSprite == null;
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space();
                
                int toGenerate = 0;
                foreach (var p in previews) if (p.selected) toGenerate++;
                
                GUI.enabled = toGenerate > 0;
                if (GUILayout.Button($"Generar y Asignar Sprites ({toGenerate})", GUILayout.Height(35)))
                {
                    GenerateSprites();
                }
                GUI.enabled = true;
            }
        }
        
        private void ScanScriptableObjects()
        {
            previews.Clear();
            
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { scriptableObjectFolder });
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ScriptableObject so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                
                if (so == null) continue;
                
                SerializedObject serializedObject = new SerializedObject(so);
                SerializedProperty prefabProperty = serializedObject.FindProperty(prefabFieldName);
                SerializedProperty spriteProperty = serializedObject.FindProperty(spriteFieldName);
                
                if (prefabProperty == null) continue;
                
                GameObject prefab = prefabProperty.objectReferenceValue as GameObject;
                if (prefab == null) continue;
                
                Sprite currentSprite = spriteProperty?.objectReferenceValue as Sprite;
                
                previews.Add(new GenerationPreview
                {
                    scriptableObject = so,
                    soPath = path,
                    prefab = prefab,
                    currentSprite = currentSprite,
                    selected = currentSprite == null
                });
            }
            
            Debug.Log($"[SpriteGenerator] Encontrados {previews.Count} ScriptableObjects con prefab.");
        }
        
        private void GenerateSprites()
        {
            int generated = 0;
            
            foreach (var preview in previews)
            {
                if (!preview.selected) continue;
                
                string outputFolder = string.IsNullOrEmpty(spriteOutputFolder) 
                    ? Path.GetDirectoryName(preview.soPath) 
                    : spriteOutputFolder;
                
                if (GenerateSpriteForSO(preview.scriptableObject, spriteFieldName, prefabFieldName, outputFolder))
                {
                    generated++;
                }
            }
            
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
            
            Debug.Log($"[SpriteGenerator] Generados y asignados {generated} sprites.");
            
            ScanScriptableObjects();
        }
        
        /// <summary>
        /// Genera sprite para un ScriptableObject específico.
        /// </summary>
        public static bool GenerateSpriteForSO(ScriptableObject so, string spriteField, string prefabField, string outputFolder)
        {
            SerializedObject serializedObject = new SerializedObject(so);
            SerializedProperty prefabProperty = serializedObject.FindProperty(prefabField);
            SerializedProperty spriteProperty = serializedObject.FindProperty(spriteField);
            
            if (prefabProperty == null || spriteProperty == null)
            {
                Debug.LogWarning($"[SpriteGenerator] {so.name}: No tiene campos '{prefabField}' o '{spriteField}'");
                return false;
            }
            
            GameObject prefab = prefabProperty.objectReferenceValue as GameObject;
            if (prefab == null)
            {
                Debug.LogWarning($"[SpriteGenerator] {so.name}: No tiene prefab asignado");
                return false;
            }
            
            // Determinar carpeta de salida
            string soPath = AssetDatabase.GetAssetPath(so);
            if (string.IsNullOrEmpty(outputFolder))
            {
                outputFolder = Path.GetDirectoryName(soPath);
            }
            
            // Generar sprite
            string spritePath = InventorySpriteGenerator.GenerateSpriteForPrefab(
                prefab, 
                outputFolder, 
                so.name + "_Icon"
            );
            
            if (string.IsNullOrEmpty(spritePath))
            {
                return false;
            }
            
            // Cargar y asignar sprite
            AssetDatabase.Refresh();
            Sprite generatedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            
            if (generatedSprite != null)
            {
                spriteProperty.objectReferenceValue = generatedSprite;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(so);
                
                Debug.Log($"[SpriteGenerator] {so.name}: Sprite generado y asignado");
                return true;
            }
            
            return false;
        }
    }
}
