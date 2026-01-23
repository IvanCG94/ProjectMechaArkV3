using UnityEngine;
using UnityEditor;

namespace RobotGame.Editor
{
    /// <summary>
    /// Configura los layers para el sistema de robots.
    /// 
    /// IMPORTANTE: Los NOMBRES de layers deben configurarse manualmente una vez en:
    /// Edit → Project Settings → Tags and Layers
    /// 
    /// Layers requeridos:
    /// - Layer 8: Player
    /// - Layer 9: RobotNavigation
    /// - Layer 10: RobotParts
    /// - Layer 11: RobotHitbox
    /// 
    /// La MATRIZ DE COLISIONES se configura automáticamente en runtime por CollisionLayerInitializer.
    /// Esta herramienta te ayuda a crear los layers y verificar la configuración.
    /// </summary>
    public class RobotLayerSetup : EditorWindow
    {
        // Layers que usaremos
        public const int LAYER_PLAYER = 8;
        public const int LAYER_ROBOT_NAVIGATION = 9;
        public const int LAYER_ROBOT_PARTS = 10;
        public const int LAYER_ROBOT_HITBOX = 11;
        
        // Nombres de los layers
        public const string LAYER_NAME_PLAYER = "Player";
        public const string LAYER_NAME_ROBOT_NAVIGATION = "RobotNavigation";
        public const string LAYER_NAME_ROBOT_PARTS = "RobotParts";
        public const string LAYER_NAME_ROBOT_HITBOX = "RobotHitbox";
        
        [MenuItem("Tools/Robot Game/Setup Robot Layers")]
        public static void ShowWindow()
        {
            GetWindow<RobotLayerSetup>("Robot Layer Setup");
        }
        
        private void OnGUI()
        {
            GUILayout.Label("Configuración de Layers para Robots", EditorStyles.boldLabel);
            
            EditorGUILayout.Space();
            
            EditorGUILayout.HelpBox(
                "Este sistema separa los colliders del robot en capas:\n\n" +
                "• RobotNavigation: Capsule invisible para física con terreno\n" +
                "• RobotParts: Colliders de las partes (colisionan con jugador)\n" +
                "• RobotHitbox: Triggers para recibir daño\n\n" +
                "Resultado: Puedes pasar ENTRE las patas del robot",
                MessageType.Info
            );
            
            EditorGUILayout.Space();
            
            // Mostrar estado actual
            GUILayout.Label("Estado de Layers:", EditorStyles.boldLabel);
            
            DrawLayerStatus(LAYER_PLAYER, LAYER_NAME_PLAYER);
            DrawLayerStatus(LAYER_ROBOT_NAVIGATION, LAYER_NAME_ROBOT_NAVIGATION);
            DrawLayerStatus(LAYER_ROBOT_PARTS, LAYER_NAME_ROBOT_PARTS);
            DrawLayerStatus(LAYER_ROBOT_HITBOX, LAYER_NAME_ROBOT_HITBOX);
            
            EditorGUILayout.Space();
            
            // Botón para crear layers
            if (GUILayout.Button("1. Crear Layers", GUILayout.Height(30)))
            {
                CreateLayers();
            }
            
            EditorGUILayout.Space();
            
            // Botón para configurar colisiones
            if (GUILayout.Button("2. Configurar Matriz de Colisiones", GUILayout.Height(30)))
            {
                ConfigureCollisionMatrix();
            }
            
            EditorGUILayout.Space();
            
            // Mostrar matriz de colisiones actual
            GUILayout.Label("Matriz de Colisiones Relevante:", EditorStyles.boldLabel);
            
            DrawCollisionStatus(LAYER_PLAYER, LAYER_ROBOT_NAVIGATION, "Player vs RobotNavigation", false);
            DrawCollisionStatus(LAYER_PLAYER, LAYER_ROBOT_PARTS, "Player vs RobotParts", true);
            DrawCollisionStatus(LAYER_PLAYER, LAYER_ROBOT_HITBOX, "Player vs RobotHitbox", false);
            
            EditorGUILayout.Space();
            
            EditorGUILayout.HelpBox(
                "Después de configurar los layers, agrega el componente 'RobotColliderSetup' " +
                "a tus robots para configurar automáticamente los colliders.",
                MessageType.Info
            );
        }
        
        private void DrawLayerStatus(int layerIndex, string expectedName)
        {
            string currentName = LayerMask.LayerToName(layerIndex);
            bool isCorrect = currentName == expectedName;
            
            EditorGUILayout.BeginHorizontal();
            
            GUILayout.Label($"Layer {layerIndex}:", GUILayout.Width(60));
            
            if (isCorrect)
            {
                EditorGUILayout.LabelField($"✓ {currentName}", EditorStyles.boldLabel);
            }
            else if (string.IsNullOrEmpty(currentName))
            {
                EditorGUILayout.LabelField($"✗ (vacío) - Necesita: {expectedName}", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField($"⚠ {currentName} - Esperado: {expectedName}", EditorStyles.miniLabel);
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawCollisionStatus(int layer1, int layer2, string description, bool shouldCollide)
        {
            bool ignoresCollision = Physics.GetIgnoreLayerCollision(layer1, layer2);
            bool currentlyCollides = !ignoresCollision;
            bool isCorrect = currentlyCollides == shouldCollide;
            
            EditorGUILayout.BeginHorizontal();
            
            string status = currentlyCollides ? "Colisiona" : "No colisiona";
            string expected = shouldCollide ? "Debe colisionar" : "No debe colisionar";
            string icon = isCorrect ? "✓" : "✗";
            
            GUILayout.Label($"{icon} {description}: {status}", 
                isCorrect ? EditorStyles.label : EditorStyles.boldLabel);
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void CreateLayers()
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]
            );
            
            SerializedProperty layers = tagManager.FindProperty("layers");
            
            SetLayer(layers, LAYER_PLAYER, LAYER_NAME_PLAYER);
            SetLayer(layers, LAYER_ROBOT_NAVIGATION, LAYER_NAME_ROBOT_NAVIGATION);
            SetLayer(layers, LAYER_ROBOT_PARTS, LAYER_NAME_ROBOT_PARTS);
            SetLayer(layers, LAYER_ROBOT_HITBOX, LAYER_NAME_ROBOT_HITBOX);
            
            tagManager.ApplyModifiedProperties();
            
            Debug.Log("[RobotLayerSetup] Layers creados correctamente.");
            
            // Refrescar la ventana
            Repaint();
        }
        
        private void SetLayer(SerializedProperty layers, int index, string name)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(index);
            
            if (string.IsNullOrEmpty(layer.stringValue))
            {
                layer.stringValue = name;
                Debug.Log($"[RobotLayerSetup] Layer {index} configurado como '{name}'");
            }
            else if (layer.stringValue != name)
            {
                Debug.LogWarning($"[RobotLayerSetup] Layer {index} ya tiene valor '{layer.stringValue}'. " +
                    $"Cámbialo manualmente a '{name}' si es necesario.");
            }
        }
        
        private void ConfigureCollisionMatrix()
        {
            // RobotNavigation NO colisiona con Player
            Physics.IgnoreLayerCollision(LAYER_PLAYER, LAYER_ROBOT_NAVIGATION, true);
            
            // RobotParts SÍ colisiona con Player
            Physics.IgnoreLayerCollision(LAYER_PLAYER, LAYER_ROBOT_PARTS, false);
            
            // RobotHitbox NO colisiona con nada físicamente (son triggers)
            Physics.IgnoreLayerCollision(LAYER_PLAYER, LAYER_ROBOT_HITBOX, true);
            Physics.IgnoreLayerCollision(LAYER_ROBOT_NAVIGATION, LAYER_ROBOT_HITBOX, true);
            Physics.IgnoreLayerCollision(LAYER_ROBOT_PARTS, LAYER_ROBOT_HITBOX, true);
            Physics.IgnoreLayerCollision(LAYER_ROBOT_HITBOX, LAYER_ROBOT_HITBOX, true);
            
            // RobotNavigation y RobotParts no colisionan entre sí (mismo robot)
            Physics.IgnoreLayerCollision(LAYER_ROBOT_NAVIGATION, LAYER_ROBOT_PARTS, true);
            Physics.IgnoreLayerCollision(LAYER_ROBOT_NAVIGATION, LAYER_ROBOT_NAVIGATION, true);
            
            Debug.Log("[RobotLayerSetup] Matriz de colisiones configurada.");
            
            // Refrescar la ventana
            Repaint();
        }
        
        /// <summary>
        /// Verifica si los layers están configurados correctamente.
        /// </summary>
        public static bool AreLayersConfigured()
        {
            return LayerMask.LayerToName(LAYER_ROBOT_NAVIGATION) == LAYER_NAME_ROBOT_NAVIGATION &&
                   LayerMask.LayerToName(LAYER_ROBOT_PARTS) == LAYER_NAME_ROBOT_PARTS &&
                   LayerMask.LayerToName(LAYER_ROBOT_HITBOX) == LAYER_NAME_ROBOT_HITBOX;
        }
    }
}
