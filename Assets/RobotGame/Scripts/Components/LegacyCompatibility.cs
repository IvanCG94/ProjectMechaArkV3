using UnityEngine;

namespace RobotGame.Utils
{
    /// <summary>
    /// GridRotation para compatibilidad con código legacy.
    /// </summary>
    public static class GridRotation
    {
        public enum Rotation
        {
            Deg0 = 0,
            Deg90 = 1,
            Deg180 = 2,
            Deg270 = 3
        }
        
        public static Rotation RotateClockwise(Rotation current)
        {
            return (Rotation)(((int)current + 1) % 4);
        }
        
        public static Rotation RotateCounterClockwise(Rotation current)
        {
            return (Rotation)(((int)current + 3) % 4);
        }
        
        public static Quaternion ToQuaternion(Rotation rotation)
        {
            switch (rotation)
            {
                case Rotation.Deg90: return Quaternion.Euler(0, 0, -90);
                case Rotation.Deg180: return Quaternion.Euler(0, 0, 180);
                case Rotation.Deg270: return Quaternion.Euler(0, 0, 90);
                default: return Quaternion.identity;
            }
        }
        
        public static Vector2Int RotateSize(int sizeX, int sizeY, Rotation rotation)
        {
            if (rotation == Rotation.Deg90 || rotation == Rotation.Deg270)
            {
                return new Vector2Int(sizeY, sizeX);
            }
            return new Vector2Int(sizeX, sizeY);
        }
        
        public static object RotateGridInfo(object gridInfo, Rotation rotation)
        {
            return null;
        }
        
        public static Rotation[] GetValidRotations(object tailInfo, object headInfo)
        {
            return new Rotation[] { Rotation.Deg0, Rotation.Deg90, Rotation.Deg180, Rotation.Deg270 };
        }
    }
    
    /// <summary>
    /// GridAutoDetector stub para código legacy.
    /// </summary>
    public static class GridAutoDetector
    {
        public static void AutoConfigureStructuralPart(RobotGame.Data.StructuralPartData partData)
        {
            // Debug.Log($"GridAutoDetector: Las grillas ahora se detectan automáticamente para {partData?.displayName}");
        }
        
        public static void AutoConfigureArmorPart(RobotGame.Data.ArmorPartData armorData)
        {
            // Debug.Log($"GridAutoDetector: Los studs ahora se detectan automáticamente para {armorData?.displayName}");
        }
    }
}

namespace RobotGame.Components
{
    /// <summary>
    /// Alias de StudGridHead para compatibilidad con código legacy.
    /// Todos los métodos y propiedades están en StudGridHead.
    /// </summary>
    public class GridHead : StudGridHead
    {
        // GridHead ahora hereda de StudGridHead que tiene todas las funciones necesarias
    }
}
