using UnityEngine;

namespace RobotGame
{
    /// <summary>
    /// Configura los layers y la matriz de colisiones automáticamente al inicio del juego.
    /// Este script debe ejecutarse ANTES de que se creen robots.
    /// 
    /// Agregar a un GameObject en la escena inicial o usar [RuntimeInitializeOnLoadMethod].
    /// </summary>
    public class CollisionLayerInitializer : MonoBehaviour
    {
        private static bool isInitialized = false;
        
        /// <summary>
        /// Se ejecuta automáticamente al cargar el juego, antes de cualquier Awake.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeOnLoad()
        {
            Initialize();
        }
        
        private void Awake()
        {
            Initialize();
        }
        
        /// <summary>
        /// Configura la matriz de colisiones.
        /// </summary>
        public static void Initialize()
        {
            if (isInitialized) return;
            
            // Nota: Los NOMBRES de layers se configuran en el Editor (ProjectSettings/TagManager)
            // pero la MATRIZ DE COLISIONES sí se puede configurar por código.
            
            // Verificar que los layers existan
            if (!ValidateLayers())
            {
                Debug.LogError("[CollisionLayerInitializer] Los layers no están configurados. " +
                    "Ve a Edit → Project Settings → Tags and Layers y crea:\n" +
                    "Layer 8: Player\n" +
                    "Layer 9: RobotNavigation\n" +
                    "Layer 10: RobotParts\n" +
                    "Layer 11: RobotHitbox\n\n" +
                    "O usa Tools → Robot Game → Setup Robot Layers en el Editor.");
                return;
            }
            
            ConfigureCollisionMatrix();
            
            isInitialized = true;
            Debug.Log("[CollisionLayerInitializer] Matriz de colisiones configurada correctamente.");
        }
        
        /// <summary>
        /// Verifica que los layers necesarios existan.
        /// </summary>
        private static bool ValidateLayers()
        {
            // Verificar que los layers tengan nombres asignados
            string playerLayer = LayerMask.LayerToName(RobotLayers.PLAYER);
            string navLayer = LayerMask.LayerToName(RobotLayers.ROBOT_NAVIGATION);
            string partsLayer = LayerMask.LayerToName(RobotLayers.ROBOT_PARTS);
            string hitboxLayer = LayerMask.LayerToName(RobotLayers.ROBOT_HITBOX);
            
            bool valid = true;
            
            if (string.IsNullOrEmpty(playerLayer))
            {
                Debug.LogWarning($"[CollisionLayerInitializer] Layer {RobotLayers.PLAYER} no tiene nombre. Debería ser 'Player'");
                valid = false;
            }
            
            if (string.IsNullOrEmpty(navLayer))
            {
                Debug.LogWarning($"[CollisionLayerInitializer] Layer {RobotLayers.ROBOT_NAVIGATION} no tiene nombre. Debería ser 'RobotNavigation'");
                valid = false;
            }
            
            if (string.IsNullOrEmpty(partsLayer))
            {
                Debug.LogWarning($"[CollisionLayerInitializer] Layer {RobotLayers.ROBOT_PARTS} no tiene nombre. Debería ser 'RobotParts'");
                valid = false;
            }
            
            if (string.IsNullOrEmpty(hitboxLayer))
            {
                Debug.LogWarning($"[CollisionLayerInitializer] Layer {RobotLayers.ROBOT_HITBOX} no tiene nombre. Debería ser 'RobotHitbox'");
                valid = false;
            }
            
            return valid;
        }
        
        /// <summary>
        /// Configura qué layers colisionan entre sí.
        /// </summary>
        private static void ConfigureCollisionMatrix()
        {
            // RobotNavigation NO colisiona con Player (puedes pasar entre las patas)
            Physics.IgnoreLayerCollision(RobotLayers.PLAYER, RobotLayers.ROBOT_NAVIGATION, true);
            
            // RobotParts SÍ colisiona con Player (no atraviesas el cuerpo)
            Physics.IgnoreLayerCollision(RobotLayers.PLAYER, RobotLayers.ROBOT_PARTS, false);
            
            // RobotHitbox NO colisiona físicamente con nada (son triggers para daño)
            Physics.IgnoreLayerCollision(RobotLayers.PLAYER, RobotLayers.ROBOT_HITBOX, true);
            Physics.IgnoreLayerCollision(RobotLayers.ROBOT_NAVIGATION, RobotLayers.ROBOT_HITBOX, true);
            Physics.IgnoreLayerCollision(RobotLayers.ROBOT_PARTS, RobotLayers.ROBOT_HITBOX, true);
            Physics.IgnoreLayerCollision(RobotLayers.ROBOT_HITBOX, RobotLayers.ROBOT_HITBOX, true);
            
            // Partes del mismo robot no colisionan entre sí
            Physics.IgnoreLayerCollision(RobotLayers.ROBOT_NAVIGATION, RobotLayers.ROBOT_PARTS, true);
            Physics.IgnoreLayerCollision(RobotLayers.ROBOT_NAVIGATION, RobotLayers.ROBOT_NAVIGATION, true);
            Physics.IgnoreLayerCollision(RobotLayers.ROBOT_PARTS, RobotLayers.ROBOT_PARTS, true);
        }
        
        /// <summary>
        /// Fuerza reinicialización (útil para testing).
        /// </summary>
        public static void ForceReinitialize()
        {
            isInitialized = false;
            Initialize();
        }
    }
}
