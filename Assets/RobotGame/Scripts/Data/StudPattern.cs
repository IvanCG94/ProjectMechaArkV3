using System;
using System.Collections.Generic;
using UnityEngine;
using RobotGame.Utils;

namespace RobotGame.Data
{
    /// <summary>
    /// Representa un stud individual en una grilla o pieza.
    /// La posición es relativa al origen de la grilla (0,0 = esquina inferior izquierda).
    /// </summary>
    [Serializable]
    public struct StudPosition : IEquatable<StudPosition>
    {
        public int x;
        public int y;
        
        public StudPosition(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
        
        /// <summary>
        /// Aplica un offset a la posición.
        /// </summary>
        public StudPosition Offset(int offsetX, int offsetY)
        {
            return new StudPosition(x + offsetX, y + offsetY);
        }
        
        public bool Equals(StudPosition other)
        {
            return x == other.x && y == other.y;
        }
        
        public override bool Equals(object obj)
        {
            return obj is StudPosition other && Equals(other);
        }
        
        public override int GetHashCode()
        {
            return HashCode.Combine(x, y);
        }
        
        public static bool operator ==(StudPosition a, StudPosition b) => a.Equals(b);
        public static bool operator !=(StudPosition a, StudPosition b) => !a.Equals(b);
        
        public override string ToString() => $"({x},{y})";
    }
    
    /// <summary>
    /// Representa un patrón de studs para una grilla Head o pieza Tail.
    /// 
    /// Para HEAD (grilla receptora): Define qué studs están disponibles para conexión.
    /// Para TAIL (pieza de armadura): Define qué studs necesita la pieza para colocarse.
    /// 
    /// Una pieza Tail puede colocarse si TODOS sus studs coinciden con studs
    /// LIBRES en el Head.
    /// </summary>
    [Serializable]
    public class StudPattern
    {
        [SerializeField]
        private List<StudPosition> studs = new List<StudPosition>();
        
        // Cache del bounding box
        private int? _cachedWidth;
        private int? _cachedHeight;
        private int? _cachedMinX;
        private int? _cachedMinY;
        
        /// <summary>
        /// Lista de posiciones de studs.
        /// </summary>
        public IReadOnlyList<StudPosition> Studs => studs;
        
        /// <summary>
        /// Cantidad de studs en el patrón.
        /// </summary>
        public int Count => studs.Count;
        
        /// <summary>
        /// Ancho del bounding box (max X - min X + 1).
        /// </summary>
        public int BoundingWidth
        {
            get
            {
                if (!_cachedWidth.HasValue) CalculateBounds();
                return _cachedWidth.Value;
            }
        }
        
        /// <summary>
        /// Alto del bounding box (max Y - min Y + 1).
        /// </summary>
        public int BoundingHeight
        {
            get
            {
                if (!_cachedHeight.HasValue) CalculateBounds();
                return _cachedHeight.Value;
            }
        }
        
        /// <summary>
        /// Mínimo X en el patrón.
        /// </summary>
        public int MinX
        {
            get
            {
                if (!_cachedMinX.HasValue) CalculateBounds();
                return _cachedMinX.Value;
            }
        }
        
        /// <summary>
        /// Mínimo Y en el patrón.
        /// </summary>
        public int MinY
        {
            get
            {
                if (!_cachedMinY.HasValue) CalculateBounds();
                return _cachedMinY.Value;
            }
        }
        
        #region Constructors
        
        public StudPattern()
        {
            studs = new List<StudPosition>();
        }
        
        public StudPattern(IEnumerable<StudPosition> positions)
        {
            studs = new List<StudPosition>(positions);
            InvalidateCache();
        }
        
        /// <summary>
        /// Crea un patrón rectangular completo (todos los studs).
        /// </summary>
        public static StudPattern CreateRectangle(int width, int height)
        {
            var pattern = new StudPattern();
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    pattern.AddStud(x, y);
                }
            }
            return pattern;
        }
        
        #endregion
        
        #region Modification
        
        /// <summary>
        /// Agrega un stud al patrón.
        /// </summary>
        public void AddStud(int x, int y)
        {
            var pos = new StudPosition(x, y);
            if (!studs.Contains(pos))
            {
                studs.Add(pos);
                InvalidateCache();
            }
        }
        
        /// <summary>
        /// Remueve un stud del patrón.
        /// </summary>
        public bool RemoveStud(int x, int y)
        {
            var pos = new StudPosition(x, y);
            bool removed = studs.Remove(pos);
            if (removed) InvalidateCache();
            return removed;
        }
        
        /// <summary>
        /// Limpia todos los studs.
        /// </summary>
        public void Clear()
        {
            studs.Clear();
            InvalidateCache();
        }
        
        #endregion
        
        #region Queries
        
        /// <summary>
        /// Verifica si hay un stud en la posición especificada.
        /// </summary>
        public bool HasStud(int x, int y)
        {
            return studs.Contains(new StudPosition(x, y));
        }
        
        /// <summary>
        /// Verifica si hay un stud en la posición especificada.
        /// </summary>
        public bool HasStud(StudPosition pos)
        {
            return studs.Contains(pos);
        }
        
        #endregion
        
        #region Rotation
        
        /// <summary>
        /// Crea un nuevo patrón rotado 90° en sentido horario.
        /// </summary>
        public StudPattern Rotate90CW()
        {
            var rotated = new StudPattern();
            int width = BoundingWidth;
            int height = BoundingHeight;
            
            foreach (var stud in studs)
            {
                // Normalizar al origen primero
                int normalizedX = stud.x - MinX;
                int normalizedY = stud.y - MinY;
                
                // Rotar: (x, y) -> (height - 1 - y, x)
                int newX = normalizedY;
                int newY = width - 1 - normalizedX;
                
                rotated.AddStud(newX, newY);
            }
            
            return rotated;
        }
        
        /// <summary>
        /// Crea un nuevo patrón rotado 180°.
        /// </summary>
        public StudPattern Rotate180()
        {
            return Rotate90CW().Rotate90CW();
        }
        
        /// <summary>
        /// Crea un nuevo patrón rotado 270° (90° CCW).
        /// </summary>
        public StudPattern Rotate270()
        {
            return Rotate90CW().Rotate90CW().Rotate90CW();
        }
        
        /// <summary>
        /// Crea un nuevo patrón con la rotación especificada.
        /// Usa GridRotation.Rotation del sistema existente.
        /// </summary>
        public StudPattern GetRotated(GridRotation.Rotation rotation)
        {
            switch (rotation)
            {
                case GridRotation.Rotation.Deg0:
                    return this;
                case GridRotation.Rotation.Deg90:
                    return Rotate90CW();
                case GridRotation.Rotation.Deg180:
                    return Rotate180();
                case GridRotation.Rotation.Deg270:
                    return Rotate270();
                default:
                    return this;
            }
        }
        
        #endregion
        
        #region Validation
        
        /// <summary>
        /// Verifica si este patrón (Tail) puede colocarse en un Head en la posición dada.
        /// </summary>
        /// <param name="headPattern">Patrón del Head (studs disponibles)</param>
        /// <param name="headOccupied">Set de studs ya ocupados en el Head</param>
        /// <param name="offsetX">Offset X donde colocar</param>
        /// <param name="offsetY">Offset Y donde colocar</param>
        /// <returns>True si todos los studs del Tail encajan en studs libres del Head</returns>
        public bool CanPlaceOn(StudPattern headPattern, HashSet<StudPosition> headOccupied, int offsetX, int offsetY)
        {
            foreach (var tailStud in studs)
            {
                // Calcular posición en el Head
                var headPos = new StudPosition(tailStud.x + offsetX, tailStud.y + offsetY);
                
                // Verificar que el Head tiene un stud en esa posición
                if (!headPattern.HasStud(headPos))
                {
                    return false;
                }
                
                // Verificar que el stud no está ocupado
                if (headOccupied != null && headOccupied.Contains(headPos))
                {
                    return false;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Obtiene todas las posiciones válidas donde este patrón puede colocarse en un Head.
        /// </summary>
        public List<Vector2Int> GetValidPlacements(StudPattern headPattern, HashSet<StudPosition> headOccupied)
        {
            var validPositions = new List<Vector2Int>();
            
            if (headPattern == null || headPattern.Count == 0 || this.Count == 0)
            {
                return validPositions;
            }
            
            // Calcular rango de búsqueda basado en bounding boxes
            int searchMinX = headPattern.MinX - this.MinX;
            int searchMaxX = headPattern.MinX + headPattern.BoundingWidth - this.BoundingWidth - this.MinX;
            int searchMinY = headPattern.MinY - this.MinY;
            int searchMaxY = headPattern.MinY + headPattern.BoundingHeight - this.BoundingHeight - this.MinY;
            
            for (int x = searchMinX; x <= searchMaxX; x++)
            {
                for (int y = searchMinY; y <= searchMaxY; y++)
                {
                    if (CanPlaceOn(headPattern, headOccupied, x, y))
                    {
                        validPositions.Add(new Vector2Int(x, y));
                    }
                }
            }
            
            return validPositions;
        }
        
        #endregion
        
        #region Private Methods
        
        private void CalculateBounds()
        {
            if (studs.Count == 0)
            {
                _cachedMinX = 0;
                _cachedMinY = 0;
                _cachedWidth = 0;
                _cachedHeight = 0;
                return;
            }
            
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            
            foreach (var stud in studs)
            {
                if (stud.x < minX) minX = stud.x;
                if (stud.x > maxX) maxX = stud.x;
                if (stud.y < minY) minY = stud.y;
                if (stud.y > maxY) maxY = stud.y;
            }
            
            _cachedMinX = minX;
            _cachedMinY = minY;
            _cachedWidth = maxX - minX + 1;
            _cachedHeight = maxY - minY + 1;
        }
        
        private void InvalidateCache()
        {
            _cachedWidth = null;
            _cachedHeight = null;
            _cachedMinX = null;
            _cachedMinY = null;
        }
        
        #endregion
        
        #region Debug
        
        /// <summary>
        /// Genera una representación visual del patrón para debug.
        /// </summary>
        public string ToVisualString()
        {
            if (studs.Count == 0) return "(vacío)";
            
            var sb = new System.Text.StringBuilder();
            
            int minX = MinX, minY = MinY;
            int width = BoundingWidth, height = BoundingHeight;
            
            // Dibujar de arriba a abajo (Y invertido para visualización)
            for (int y = minY + height - 1; y >= minY; y--)
            {
                for (int x = minX; x < minX + width; x++)
                {
                    sb.Append(HasStud(x, y) ? "●" : "·");
                    sb.Append(" ");
                }
                sb.AppendLine();
            }
            
            return sb.ToString();
        }
        
        public override string ToString()
        {
            return $"StudPattern({Count} studs, {BoundingWidth}x{BoundingHeight})";
        }
        
        #endregion
    }
}
