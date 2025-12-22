using System;

namespace RobotGame.Enums
{
    /// <summary>
    /// Nivel de Surrounding para las grillas.
    /// SN = No Surrounding (nivel base)
    /// S1, S2, S3... = Niveles crecientes
    /// </summary>
    [Serializable]
    public struct SurroundingLevel
    {
        public int level;       // 0 = SN, 1 = S1, 2 = S2, etc.
        public bool isFull;     // True si tiene el flag F
        
        public static SurroundingLevel SN => new SurroundingLevel { level = 0, isFull = false };
        public static SurroundingLevel S1 => new SurroundingLevel { level = 1, isFull = false };
        public static SurroundingLevel S1F => new SurroundingLevel { level = 1, isFull = true };
        public static SurroundingLevel S2 => new SurroundingLevel { level = 2, isFull = false };
        public static SurroundingLevel S2F => new SurroundingLevel { level = 2, isFull = true };
        
        /// <summary>
        /// Crea un SurroundingLevel a partir de un nivel específico.
        /// </summary>
        public static SurroundingLevel Create(int level, bool isFull = false)
        {
            return new SurroundingLevel { level = level, isFull = isFull };
        }
        
        /// <summary>
        /// Verifica si una pieza Tail puede insertarse en esta grilla Head.
        /// </summary>
        /// <param name="tailLevel">El SurroundingLevel de la pieza Tail</param>
        /// <returns>True si es compatible</returns>
        public bool CanAccept(SurroundingLevel tailLevel)
        {
            // Si el tail tiene F, el head debe tener el mismo nivel Y también F
            if (tailLevel.isFull)
            {
                return this.isFull && this.level == tailLevel.level;
            }
            
            // Sin F, el head debe tener nivel >= tail
            return this.level >= tailLevel.level;
        }
        
        /// <summary>
        /// Convierte a string en formato de nomenclatura (SN, S1, S2F, etc.)
        /// </summary>
        public override string ToString()
        {
            if (level == 0) return "SN";
            return $"S{level}{(isFull ? "F" : "")}";
        }
        
        /// <summary>
        /// Parsea un string de nomenclatura a SurroundingLevel.
        /// </summary>
        public static SurroundingLevel Parse(string nomenclature)
        {
            if (string.IsNullOrEmpty(nomenclature) || nomenclature.ToUpper() == "SN")
            {
                return SN;
            }
            
            string upper = nomenclature.ToUpper();
            bool isFull = upper.EndsWith("F");
            
            if (isFull)
            {
                upper = upper.Substring(0, upper.Length - 1);
            }
            
            if (upper.StartsWith("S") && int.TryParse(upper.Substring(1), out int level))
            {
                return new SurroundingLevel { level = level, isFull = isFull };
            }
            
            // Default to SN if parsing fails
            return SN;
        }
    }
}
