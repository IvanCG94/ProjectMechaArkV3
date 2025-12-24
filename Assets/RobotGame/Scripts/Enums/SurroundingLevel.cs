using System;
using System.Collections.Generic;

namespace RobotGame.Enums
{
    /// <summary>
    /// Flags para indicar qué bordes están abiertos o qué bordes envuelve una pieza.
    /// </summary>
    [Flags]
    public enum EdgeFlags
    {
        None = 0,
        L = 1,      // Left (izquierda)
        R = 2,      // Right (derecha)
        T = 4,      // Top (arriba)
        B = 8,      // Bottom (abajo)
        LR = L | R,
        TB = T | B,
        LT = L | T,
        LB = L | B,
        RT = R | T,
        RB = R | B,
        LRT = L | R | T,
        LRB = L | R | B,
        LTB = L | T | B,
        RTB = R | T | B,
        LRTB = L | R | T | B
    }
    
    /// <summary>
    /// Tipo de Full para grillas que permiten envolvimiento completo.
    /// </summary>
    public enum FullType
    {
        None,   // No es Full
        FH,     // Full Horizontal (rotación 0° y 180°)
        FV      // Full Vertical (rotación 90° y 270°)
    }
    
    /// <summary>
    /// Nivel de Surrounding para las grillas.
    /// SN = No Surrounding (nivel base)
    /// S1, S2, S3... = Niveles crecientes
    /// Bordes (L, R, T, B) indican qué lados están abiertos/envuelven
    /// Full (FH, FV) indica envolvimiento completo
    /// </summary>
    [Serializable]
    public struct SurroundingLevel
    {
        public int level;           // 0 = SN, 1 = S1, 2 = S2, etc.
        public EdgeFlags edges;     // Qué bordes están abiertos (Head) o envuelven (Tail)
        public FullType fullType;   // Tipo de Full (None, FH, FV)
        
        /// <summary>
        /// Indica si tiene algún borde definido.
        /// </summary>
        public bool HasEdges => edges != EdgeFlags.None;
        
        /// <summary>
        /// Indica si es tipo Full (FH o FV).
        /// </summary>
        public bool IsFull => fullType != FullType.None;
        
        /// <summary>
        /// Indica si tiene los 4 bordes (LRTB).
        /// </summary>
        public bool HasAllEdges => edges == EdgeFlags.LRTB;
        
        // Constructores estáticos comunes
        public static SurroundingLevel SN => new SurroundingLevel { level = 0, edges = EdgeFlags.None, fullType = FullType.None };
        public static SurroundingLevel S1 => new SurroundingLevel { level = 1, edges = EdgeFlags.None, fullType = FullType.None };
        public static SurroundingLevel S2 => new SurroundingLevel { level = 2, edges = EdgeFlags.None, fullType = FullType.None };
        
        /// <summary>
        /// Crea un SurroundingLevel con nivel y bordes específicos.
        /// </summary>
        public static SurroundingLevel Create(int level, EdgeFlags edges = EdgeFlags.None, FullType fullType = FullType.None)
        {
            return new SurroundingLevel { level = level, edges = edges, fullType = fullType };
        }
        
        /// <summary>
        /// Verifica si una pieza Tail puede insertarse en esta grilla Head.
        /// Solo valida nivel de Surrounding, no bordes ni posición.
        /// </summary>
        public bool CanAcceptLevel(SurroundingLevel tailLevel)
        {
            // Head debe tener nivel >= tail
            return this.level >= tailLevel.level;
        }
        
        /// <summary>
        /// Verifica si los bordes del Head permiten los bordes del Tail.
        /// </summary>
        public bool CanAcceptEdges(SurroundingLevel tailLevel)
        {
            // Si el Tail no tiene bordes ni es Full (SN plano), siempre es compatible
            if (!tailLevel.HasEdges && !tailLevel.IsFull)
            {
                return true;
            }
            
            // Si el Head es Full (FH o FV)
            if (this.IsFull)
            {
                // Si el Tail es Full, debe ser del mismo tipo
                if (tailLevel.IsFull)
                {
                    return this.fullType == tailLevel.fullType;
                }
                
                // Si el Tail tiene bordes, deben ser compatibles con la orientación del Head
                if (tailLevel.HasEdges)
                {
                    // FH acepta: L, R, LR (bordes horizontales)
                    // FV acepta: T, B, TB (bordes verticales)
                    if (this.fullType == FullType.FH)
                    {
                        // Solo bordes horizontales permitidos
                        EdgeFlags horizontalEdges = EdgeFlags.L | EdgeFlags.R;
                        return (tailLevel.edges & ~horizontalEdges) == EdgeFlags.None;
                    }
                    else if (this.fullType == FullType.FV)
                    {
                        // Solo bordes verticales permitidos
                        EdgeFlags verticalEdges = EdgeFlags.T | EdgeFlags.B;
                        return (tailLevel.edges & ~verticalEdges) == EdgeFlags.None;
                    }
                }
                
                return false;
            }
            
            // Si el Tail es Full pero el Head no es Full, no es compatible
            if (tailLevel.IsFull)
            {
                return false;
            }
            
            // Caso especial LRTB: debe coincidir exactamente
            if (tailLevel.HasAllEdges)
            {
                return this.HasAllEdges;
            }
            
            // El Head debe tener al menos los bordes que el Tail necesita
            return (this.edges & tailLevel.edges) == tailLevel.edges;
        }
        
        /// <summary>
        /// Verifica compatibilidad completa (nivel + bordes).
        /// </summary>
        public bool CanAccept(SurroundingLevel tailLevel)
        {
            return CanAcceptLevel(tailLevel) && CanAcceptEdges(tailLevel);
        }
        
        /// <summary>
        /// Convierte a string en formato de nomenclatura.
        /// </summary>
        public override string ToString()
        {
            if (level == 0) return "SN";
            
            string result = $"S{level}";
            
            if (fullType != FullType.None)
            {
                result += fullType.ToString();
            }
            else if (edges != EdgeFlags.None)
            {
                result += $"_{EdgesToString(edges)}";
            }
            
            return result;
        }
        
        /// <summary>
        /// Convierte EdgeFlags a string (L, R, LR, LRTB, etc.)
        /// </summary>
        public static string EdgesToString(EdgeFlags flags)
        {
            if (flags == EdgeFlags.None) return "";
            
            string result = "";
            if ((flags & EdgeFlags.L) != 0) result += "L";
            if ((flags & EdgeFlags.R) != 0) result += "R";
            if ((flags & EdgeFlags.T) != 0) result += "T";
            if ((flags & EdgeFlags.B) != 0) result += "B";
            return result;
        }
        
        /// <summary>
        /// Parsea un string de bordes a EdgeFlags.
        /// </summary>
        public static EdgeFlags ParseEdges(string edgeString)
        {
            if (string.IsNullOrEmpty(edgeString)) return EdgeFlags.None;
            
            EdgeFlags result = EdgeFlags.None;
            string upper = edgeString.ToUpper();
            
            if (upper.Contains("L")) result |= EdgeFlags.L;
            if (upper.Contains("R")) result |= EdgeFlags.R;
            if (upper.Contains("T")) result |= EdgeFlags.T;
            if (upper.Contains("B")) result |= EdgeFlags.B;
            
            return result;
        }
        
        /// <summary>
        /// Parsea un string de nomenclatura a SurroundingLevel.
        /// Formatos: SN, S1, S2, S1_L, S1_LR, S1_LRTB, S1FH, S1FV
        /// </summary>
        public static SurroundingLevel Parse(string nomenclature)
        {
            if (string.IsNullOrEmpty(nomenclature) || nomenclature.ToUpper() == "SN")
            {
                return SN;
            }
            
            string upper = nomenclature.ToUpper();
            int level = 0;
            EdgeFlags edges = EdgeFlags.None;
            FullType fullType = FullType.None;
            
            // Verificar si tiene Full type (FH o FV)
            if (upper.Contains("FH"))
            {
                fullType = FullType.FH;
                upper = upper.Replace("FH", "");
            }
            else if (upper.Contains("FV"))
            {
                fullType = FullType.FV;
                upper = upper.Replace("FV", "");
            }
            
            // Separar por underscore para obtener bordes
            string[] parts = upper.Split('_');
            string levelPart = parts[0];
            
            // Parsear nivel (S1, S2, etc.)
            if (levelPart.StartsWith("S") && int.TryParse(levelPart.Substring(1), out int parsedLevel))
            {
                level = parsedLevel;
            }
            
            // Parsear bordes si existen (después del underscore)
            if (parts.Length > 1)
            {
                edges = ParseEdges(parts[1]);
            }
            
            return new SurroundingLevel { level = level, edges = edges, fullType = fullType };
        }
    }
}
