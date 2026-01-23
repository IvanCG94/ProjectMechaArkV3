using UnityEngine;

namespace RobotGame
{
    /// <summary>
    /// Constantes de layers para el sistema de robots.
    /// Usar con: gameObject.layer = RobotLayers.ROBOT_PARTS;
    /// </summary>
    public static class RobotLayers
    {
        // Índices de layers
        public const int PLAYER = 8;
        public const int ROBOT_NAVIGATION = 9;
        public const int ROBOT_PARTS = 10;
        public const int ROBOT_HITBOX = 11;
        
        // LayerMasks para Physics queries
        public static readonly LayerMask PlayerMask = 1 << PLAYER;
        public static readonly LayerMask RobotNavigationMask = 1 << ROBOT_NAVIGATION;
        public static readonly LayerMask RobotPartsMask = 1 << ROBOT_PARTS;
        public static readonly LayerMask RobotHitboxMask = 1 << ROBOT_HITBOX;
        
        // Combinaciones útiles
        public static readonly LayerMask AllRobotMask = RobotNavigationMask | RobotPartsMask | RobotHitboxMask;
        public static readonly LayerMask DamageableMask = RobotHitboxMask; // Layers que pueden recibir daño
        
        /// <summary>
        /// Verifica si un layer es de robot.
        /// </summary>
        public static bool IsRobotLayer(int layer)
        {
            return layer == ROBOT_NAVIGATION || layer == ROBOT_PARTS || layer == ROBOT_HITBOX;
        }
        
        /// <summary>
        /// Obtiene el nombre del layer.
        /// </summary>
        public static string GetLayerName(int layer)
        {
            switch (layer)
            {
                case PLAYER: return "Player";
                case ROBOT_NAVIGATION: return "RobotNavigation";
                case ROBOT_PARTS: return "RobotParts";
                case ROBOT_HITBOX: return "RobotHitbox";
                default: return LayerMask.LayerToName(layer);
            }
        }
    }
}
