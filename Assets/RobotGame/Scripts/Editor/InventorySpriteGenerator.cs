using UnityEngine;
using UnityEditor;
using System.IO;

namespace RobotGame.Editor
{
    /// <summary>
    /// Herramienta de editor para generar sprites de inventario automáticamente desde prefabs.
    /// Usa el sistema de preview de Unity para generar imágenes idénticas a las del Project.
    /// </summary>
    public class InventorySpriteGenerator : EditorWindow
    {
        private static int imageSize = 128;
        
        [MenuItem("Tools/Robot Game/Generate Inventory Sprites")]
        public static void ShowWindow()
        {
            GetWindow<InventorySpriteGenerator>("Sprite Generator");
        }
        
        [MenuItem("Assets/Generate Inventory Sprite", false, 100)]
        public static void GenerateFromSelection()
        {
            Object[] selectedObjects = Selection.objects;
            int generated = 0;
            
            foreach (Object obj in selectedObjects)
            {
                if (obj is GameObject prefab)
                {
                    string path = AssetDatabase.GetAssetPath(prefab);
                    if (!string.IsNullOrEmpty(path) && path.EndsWith(".prefab"))
                    {
                        GenerateSpriteForPrefab(prefab, path);
                        generated++;
                    }
                }
            }
            
            if (generated > 0)
            {
                AssetDatabase.Refresh();
                Debug.Log($"[SpriteGenerator] Generados {generated} sprites de inventario.");
            }
            else
            {
                Debug.LogWarning("[SpriteGenerator] No se seleccionaron prefabs válidos.");
            }
        }
        
        [MenuItem("Assets/Generate Inventory Sprite", true)]
        public static bool ValidateGenerateFromSelection()
        {
            foreach (Object obj in Selection.objects)
            {
                if (obj is GameObject)
                {
                    string path = AssetDatabase.GetAssetPath(obj);
                    if (!string.IsNullOrEmpty(path) && path.EndsWith(".prefab"))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        
        private void OnGUI()
        {
            GUILayout.Label("Generador de Sprites de Inventario", EditorStyles.boldLabel);
            
            EditorGUILayout.Space();
            
            EditorGUILayout.HelpBox(
                "Genera sprites usando la misma preview que Unity muestra en el Project.\n" +
                "Los sprites tendrán fondo transparente.",
                MessageType.Info
            );
            
            EditorGUILayout.Space();
            
            imageSize = EditorGUILayout.IntSlider("Tamaño (px)", imageSize, 64, 512);
            
            EditorGUILayout.Space();
            
            // Información de selección
            int prefabCount = 0;
            foreach (Object obj in Selection.objects)
            {
                if (obj is GameObject && AssetDatabase.GetAssetPath(obj).EndsWith(".prefab"))
                {
                    prefabCount++;
                }
            }
            
            EditorGUILayout.HelpBox(
                prefabCount > 0 
                    ? $"{prefabCount} prefab(s) seleccionado(s)" 
                    : "Selecciona prefabs en el Project para generar sprites",
                prefabCount > 0 ? MessageType.Info : MessageType.Warning
            );
            
            EditorGUILayout.Space();
            
            GUI.enabled = prefabCount > 0;
            if (GUILayout.Button("Generar Sprites", GUILayout.Height(40)))
            {
                GenerateFromSelection();
            }
            GUI.enabled = true;
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Generar para TODOS los Prefabs en Carpeta"))
            {
                GenerateForFolder();
            }
        }
        
        private void GenerateForFolder()
        {
            string folderPath = EditorUtility.OpenFolderPanel("Seleccionar Carpeta de Prefabs", "Assets", "");
            
            if (string.IsNullOrEmpty(folderPath)) return;
            
            if (folderPath.StartsWith(Application.dataPath))
            {
                folderPath = "Assets" + folderPath.Substring(Application.dataPath.Length);
            }
            
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
            int generated = 0;
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                
                if (prefab != null)
                {
                    GenerateSpriteForPrefab(prefab, path);
                    generated++;
                }
            }
            
            AssetDatabase.Refresh();
            Debug.Log($"[SpriteGenerator] Generados {generated} sprites de inventario.");
        }
        
        /// <summary>
        /// Genera un sprite para un prefab usando la preview de Unity.
        /// </summary>
        public static void GenerateSpriteForPrefab(GameObject prefab, string prefabPath)
        {
            string directory = Path.GetDirectoryName(prefabPath);
            string filename = Path.GetFileNameWithoutExtension(prefabPath) + "_Icon";
            GenerateSpriteForPrefab(prefab, directory, filename);
        }
        
        /// <summary>
        /// Genera un sprite para un prefab con carpeta y nombre personalizados.
        /// </summary>
        public static string GenerateSpriteForPrefab(GameObject prefab, string outputDirectory, string spriteName)
        {
            // Obtener la preview de Unity (la misma que se ve en el Project)
            Texture2D preview = AssetPreview.GetAssetPreview(prefab);
            
            // Si no está lista, forzar generación y esperar
            if (preview == null)
            {
                AssetPreview.SetPreviewTextureCacheSize(256);
                
                // Intentar varias veces
                for (int i = 0; i < 100; i++)
                {
                    preview = AssetPreview.GetAssetPreview(prefab);
                    if (preview != null) break;
                    System.Threading.Thread.Sleep(10);
                }
            }
            
            if (preview == null)
            {
                Debug.LogWarning($"[SpriteGenerator] No se pudo obtener preview para {prefab.name}. Usando método alternativo...");
                return GenerateSpriteManual(prefab, outputDirectory, spriteName);
            }
            
            // Copiar la textura (la preview de Unity es de solo lectura)
            Texture2D finalTexture = new Texture2D(preview.width, preview.height, TextureFormat.ARGB32, false);
            
            // Usar Graphics.CopyTexture si es posible, sino GetPixels
            try
            {
                RenderTexture rt = RenderTexture.GetTemporary(preview.width, preview.height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(preview, rt);
                
                RenderTexture.active = rt;
                finalTexture.ReadPixels(new Rect(0, 0, preview.width, preview.height), 0, 0);
                finalTexture.Apply();
                
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(rt);
            }
            catch
            {
                // Fallback
                Color[] pixels = preview.GetPixels();
                finalTexture.SetPixels(pixels);
                finalTexture.Apply();
            }
            
            // Guardar como PNG
            string outputPath = Path.Combine(outputDirectory, spriteName + ".png");
            
            byte[] pngData = finalTexture.EncodeToPNG();
            File.WriteAllBytes(outputPath, pngData);
            
            DestroyImmediate(finalTexture);
            
            Debug.Log($"[SpriteGenerator] Sprite generado: {outputPath}");
            
            // Importar como sprite
            AssetDatabase.ImportAsset(outputPath);
            ConfigureAsSprite(outputPath);
            
            return outputPath;
        }
        
        /// <summary>
        /// Método alternativo usando PreviewRenderUtility para casos donde AssetPreview falla.
        /// </summary>
        private static string GenerateSpriteManual(GameObject prefab, string outputDirectory, string spriteName)
        {
            var previewUtility = new PreviewRenderUtility();
            
            try
            {
                previewUtility.camera.backgroundColor = new Color(0, 0, 0, 0);
                previewUtility.camera.clearFlags = CameraClearFlags.SolidColor;
                previewUtility.camera.orthographic = true;
                previewUtility.camera.nearClipPlane = 0.01f;
                previewUtility.camera.farClipPlane = 1000f;
                
                GameObject instance = previewUtility.InstantiatePrefabInScene(prefab);
                
                Bounds bounds = new Bounds(instance.transform.position, Vector3.zero);
                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
                
                float maxExtent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
                previewUtility.camera.orthographicSize = maxExtent * 1.2f;
                
                Vector3 cameraDir = Quaternion.Euler(30f, -135f, 0f) * Vector3.forward;
                previewUtility.camera.transform.position = bounds.center - cameraDir * (maxExtent * 3f);
                previewUtility.camera.transform.LookAt(bounds.center);
                
                previewUtility.lights[0].intensity = 1f;
                previewUtility.lights[0].transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                
                previewUtility.BeginPreview(new Rect(0, 0, imageSize, imageSize), GUIStyle.none);
                previewUtility.camera.Render();
                Texture resultTexture = previewUtility.EndPreview();
                
                RenderTexture rt = resultTexture as RenderTexture;
                RenderTexture.active = rt;
                
                Texture2D texture = new Texture2D(imageSize, imageSize, TextureFormat.ARGB32, false);
                texture.ReadPixels(new Rect(0, 0, imageSize, imageSize), 0, 0);
                texture.Apply();
                
                RenderTexture.active = null;
                
                string outputPath = Path.Combine(outputDirectory, spriteName + ".png");
                byte[] pngData = texture.EncodeToPNG();
                File.WriteAllBytes(outputPath, pngData);
                
                DestroyImmediate(texture);
                
                Debug.Log($"[SpriteGenerator] Sprite generado (manual): {outputPath}");
                
                AssetDatabase.ImportAsset(outputPath);
                ConfigureAsSprite(outputPath);
                
                return outputPath;
            }
            finally
            {
                previewUtility.Cleanup();
            }
        }
        
        /// <summary>
        /// Configura la textura importada como sprite.
        /// </summary>
        private static void ConfigureAsSprite(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                
                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spritePixelsPerUnit = 100;
                importer.SetTextureSettings(settings);
                
                importer.SaveAndReimport();
            }
        }
    }
}
