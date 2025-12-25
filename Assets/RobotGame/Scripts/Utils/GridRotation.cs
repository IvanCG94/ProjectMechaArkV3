using UnityEngine;
using RobotGame.Enums;
using RobotGame.Data;

namespace RobotGame.Utils
{
    /// <summary>
    /// Utilidad para calcular transformaciones de grillas al rotar.
    /// Maneja la conversión de bordes y tamaños según la rotación aplicada.
    /// </summary>
    public static class GridRotation
    {
        /// <summary>
        /// Rotaciones posibles (en grados).
        /// </summary>
        public enum Rotation
        {
            Deg0 = 0,
            Deg90 = 90,
            Deg180 = 180,
            Deg270 = 270
        }
        
        /// <summary>
        /// Obtiene la siguiente rotación en sentido horario.
        /// </summary>
        public static Rotation RotateClockwise(Rotation current)
        {
            switch (current)
            {
                case Rotation.Deg0: return Rotation.Deg90;
                case Rotation.Deg90: return Rotation.Deg180;
                case Rotation.Deg180: return Rotation.Deg270;
                case Rotation.Deg270: return Rotation.Deg0;
                default: return Rotation.Deg0;
            }
        }
        
        /// <summary>
        /// Obtiene la siguiente rotación en sentido anti-horario.
        /// </summary>
        public static Rotation RotateCounterClockwise(Rotation current)
        {
            switch (current)
            {
                case Rotation.Deg0: return Rotation.Deg270;
                case Rotation.Deg90: return Rotation.Deg0;
                case Rotation.Deg180: return Rotation.Deg90;
                case Rotation.Deg270: return Rotation.Deg180;
                default: return Rotation.Deg0;
            }
        }
        
        /// <summary>
        /// Transforma un borde individual al rotar 90 grados en sentido horario.
        /// L -> T -> R -> B -> L
        /// </summary>
        private static EdgeFlags RotateEdge90(EdgeFlags edge)
        {
            EdgeFlags result = EdgeFlags.None;
            
            if ((edge & EdgeFlags.L) != 0) result |= EdgeFlags.T;
            if ((edge & EdgeFlags.T) != 0) result |= EdgeFlags.R;
            if ((edge & EdgeFlags.R) != 0) result |= EdgeFlags.B;
            if ((edge & EdgeFlags.B) != 0) result |= EdgeFlags.L;
            
            return result;
        }
        
        /// <summary>
        /// Transforma los bordes según la rotación aplicada.
        /// </summary>
        public static EdgeFlags RotateEdges(EdgeFlags edges, Rotation rotation)
        {
            // LRTB no cambia con la rotación
            if (edges == EdgeFlags.LRTB)
            {
                return EdgeFlags.LRTB;
            }
            
            EdgeFlags result = edges;
            
            int steps = (int)rotation / 90;
            for (int i = 0; i < steps; i++)
            {
                result = RotateEdge90(result);
            }
            
            return result;
        }
        
        /// <summary>
        /// Calcula el nuevo tamaño de la grilla al rotar.
        /// 90° y 270° intercambian X e Y.
        /// </summary>
        public static Vector2Int RotateSize(int sizeX, int sizeY, Rotation rotation)
        {
            switch (rotation)
            {
                case Rotation.Deg0:
                case Rotation.Deg180:
                    return new Vector2Int(sizeX, sizeY);
                    
                case Rotation.Deg90:
                case Rotation.Deg270:
                    return new Vector2Int(sizeY, sizeX);
                    
                default:
                    return new Vector2Int(sizeX, sizeY);
            }
        }
        
        /// <summary>
        /// Transforma el FullType según la rotación.
        /// FH rotado 90° o 270° se convierte en FV y viceversa.
        /// </summary>
        public static FullType RotateFullType(FullType fullType, Rotation rotation)
        {
            if (fullType == FullType.None)
            {
                return FullType.None;
            }
            
            switch (rotation)
            {
                case Rotation.Deg0:
                case Rotation.Deg180:
                    return fullType; // No cambia
                    
                case Rotation.Deg90:
                case Rotation.Deg270:
                    // FH <-> FV
                    return fullType == FullType.FH ? FullType.FV : FullType.FH;
                    
                default:
                    return fullType;
            }
        }
        
        /// <summary>
        /// Crea un SurroundingLevel transformado según la rotación.
        /// </summary>
        public static SurroundingLevel RotateSurrounding(SurroundingLevel original, Rotation rotation)
        {
            return SurroundingLevel.Create(
                original.level,
                RotateEdges(original.edges, rotation),
                RotateFullType(original.fullType, rotation)
            );
        }
        
        /// <summary>
        /// Crea un GridInfo transformado según la rotación.
        /// </summary>
        public static GridInfo RotateGridInfo(GridInfo original, Rotation rotation)
        {
            Vector2Int newSize = RotateSize(original.sizeX, original.sizeY, rotation);
            SurroundingLevel newSurrounding = RotateSurrounding(original.surrounding, rotation);
            
            return new GridInfo
            {
                isHead = original.isHead,
                sizeX = newSize.x,
                sizeY = newSize.y,
                surrounding = newSurrounding,
                gridName = original.gridName
            };
        }
        
        /// <summary>
        /// Convierte una rotación a Quaternion para aplicar al transform.
        /// </summary>
        public static Quaternion ToQuaternion(Rotation rotation)
        {
            return Quaternion.Euler(0, 0, -(int)rotation); // Negativo para sentido horario
        }
        
        /// <summary>
        /// Obtiene las rotaciones válidas para una pieza SN (sin envolvimiento).
        /// Depende de si el tamaño encaja en la grilla.
        /// </summary>
        public static Rotation[] GetValidRotationsForSN(int tailSizeX, int tailSizeY, int headSizeX, int headSizeY)
        {
            bool fits0or180 = tailSizeX <= headSizeX && tailSizeY <= headSizeY;
            bool fits90or270 = tailSizeY <= headSizeX && tailSizeX <= headSizeY;
            
            if (fits0or180 && fits90or270)
            {
                return new[] { Rotation.Deg0, Rotation.Deg90, Rotation.Deg180, Rotation.Deg270 };
            }
            else if (fits0or180)
            {
                return new[] { Rotation.Deg0, Rotation.Deg180 };
            }
            else if (fits90or270)
            {
                return new[] { Rotation.Deg90, Rotation.Deg270 };
            }
            
            return new Rotation[0]; // No encaja en ninguna rotación
        }
        
        /// <summary>
        /// Verifica si una rotación específica es válida para un Tail en un Head.
        /// </summary>
        public static bool IsRotationValid(GridInfo tailInfo, GridInfo headInfo, Rotation rotation)
        {
            // Obtener el GridInfo rotado
            GridInfo rotatedTail = RotateGridInfo(tailInfo, rotation);
            
            // Verificar que el tamaño encaje
            if (rotatedTail.sizeX > headInfo.sizeX || rotatedTail.sizeY > headInfo.sizeY)
            {
                return false;
            }
            
            // Verificar compatibilidad de Surrounding
            if (!headInfo.surrounding.CanAccept(rotatedTail.surrounding))
            {
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Obtiene todas las rotaciones válidas para un Tail en un Head.
        /// </summary>
        public static Rotation[] GetValidRotations(GridInfo tailInfo, GridInfo headInfo)
        {
            var validRotations = new System.Collections.Generic.List<Rotation>();
            
            foreach (Rotation rot in new[] { Rotation.Deg0, Rotation.Deg90, Rotation.Deg180, Rotation.Deg270 })
            {
                if (IsRotationValid(tailInfo, headInfo, rot))
                {
                    validRotations.Add(rot);
                }
            }
            
            return validRotations.ToArray();
        }
    }
}
