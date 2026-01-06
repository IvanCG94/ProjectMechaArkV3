using System;
using UnityEngine;

namespace RobotGame.Enums
{
    /// <summary>
    /// Estructura serializable que representa un Tier con tier principal y subtier.
    /// Más flexible que un enum - permite cualquier combinación de tier/subtier.
    /// 
    /// Uso en Inspector:
    /// - Tier: 1-6 (tipo de robot: jugador, velociraptor, trike, trex, etc.)
    /// - SubTier: 1-6 (variante/calidad dentro del tier)
    /// 
    /// Reglas de compatibilidad:
    /// - Tier principal DEBE SER IGUAL para ser compatible
    /// - SubTier debe ser IGUAL O INFERIOR para ser compatible
    /// 
    /// Ejemplo: Estación Tier 2.3 acepta piezas 2.1, 2.2, 2.3 pero NO 2.4+, 1.x, 3.x
    /// </summary>
    [Serializable]
    public struct TierInfo : IEquatable<TierInfo>
    {
        [Tooltip("Tier principal (1=Jugador, 2=Velociraptor, 3=Trike, 4=T-Rex, etc.)")]
        [Range(1, 6)]
        public int tier;
        
        [Tooltip("Sub-tier/variante (calidad dentro del tier)")]
        [Range(1, 6)]
        public int subTier;
        
        #region Constructors
        
        /// <summary>
        /// Crea un TierInfo con los valores especificados.
        /// </summary>
        public TierInfo(int tier, int subTier)
        {
            this.tier = Mathf.Clamp(tier, 1, 6);
            this.subTier = Mathf.Clamp(subTier, 1, 6);
        }
        
        #endregion
        
        #region Static Presets
        
        // Tier 1: Robots pequeños (tamaño jugador)
        public static TierInfo Tier1_1 => new TierInfo(1, 1);
        public static TierInfo Tier1_2 => new TierInfo(1, 2);
        public static TierInfo Tier1_3 => new TierInfo(1, 3);
        public static TierInfo Tier1_4 => new TierInfo(1, 4);
        public static TierInfo Tier1_5 => new TierInfo(1, 5);
        public static TierInfo Tier1_6 => new TierInfo(1, 6);
        
        // Tier 2: Robots medianos-pequeños (meca-velociraptors)
        public static TierInfo Tier2_1 => new TierInfo(2, 1);
        public static TierInfo Tier2_2 => new TierInfo(2, 2);
        public static TierInfo Tier2_3 => new TierInfo(2, 3);
        public static TierInfo Tier2_4 => new TierInfo(2, 4);
        public static TierInfo Tier2_5 => new TierInfo(2, 5);
        public static TierInfo Tier2_6 => new TierInfo(2, 6);
        
        // Tier 3: Robots medianos (meca-trikes)
        public static TierInfo Tier3_1 => new TierInfo(3, 1);
        public static TierInfo Tier3_2 => new TierInfo(3, 2);
        public static TierInfo Tier3_3 => new TierInfo(3, 3);
        public static TierInfo Tier3_4 => new TierInfo(3, 4);
        public static TierInfo Tier3_5 => new TierInfo(3, 5);
        public static TierInfo Tier3_6 => new TierInfo(3, 6);
        
        // Tier 4: Robots grandes (meca-trex)
        public static TierInfo Tier4_1 => new TierInfo(4, 1);
        public static TierInfo Tier4_2 => new TierInfo(4, 2);
        public static TierInfo Tier4_3 => new TierInfo(4, 3);
        public static TierInfo Tier4_4 => new TierInfo(4, 4);
        public static TierInfo Tier4_5 => new TierInfo(4, 5);
        public static TierInfo Tier4_6 => new TierInfo(4, 6);
        
        // Tier 5: Robots muy grandes
        public static TierInfo Tier5_1 => new TierInfo(5, 1);
        public static TierInfo Tier5_2 => new TierInfo(5, 2);
        public static TierInfo Tier5_3 => new TierInfo(5, 3);
        public static TierInfo Tier5_4 => new TierInfo(5, 4);
        public static TierInfo Tier5_5 => new TierInfo(5, 5);
        public static TierInfo Tier5_6 => new TierInfo(5, 6);
        
        // Tier 6: Robots colosales
        public static TierInfo Tier6_1 => new TierInfo(6, 1);
        public static TierInfo Tier6_2 => new TierInfo(6, 2);
        public static TierInfo Tier6_3 => new TierInfo(6, 3);
        public static TierInfo Tier6_4 => new TierInfo(6, 4);
        public static TierInfo Tier6_5 => new TierInfo(6, 5);
        public static TierInfo Tier6_6 => new TierInfo(6, 6);
        
        /// <summary>
        /// Tier por defecto (1.1).
        /// </summary>
        public static TierInfo Default => Tier1_1;
        
        #endregion
        
        #region Properties
        
        /// <summary>
        /// Tier principal efectivo. Retorna 1 si tier es 0 (datos legacy).
        /// </summary>
        public int MainTier => tier <= 0 ? 1 : tier;
        
        /// <summary>
        /// SubTier efectivo. Retorna 1 si subTier es 0 (datos legacy).
        /// </summary>
        public int SubTier => subTier <= 0 ? 1 : subTier;
        
        /// <summary>
        /// Retorna true si el TierInfo tiene valores válidos (0 se trata como 1).
        /// </summary>
        public bool IsValid => MainTier >= 1 && MainTier <= 6 && SubTier >= 1 && SubTier <= 6;
        
        #endregion
        
        #region Compatibility Methods
        
        /// <summary>
        /// Verifica si este tier es compatible con una pieza del tier especificado.
        /// Una pieza es compatible si:
        /// - Tiene el MISMO tier principal
        /// - Tiene subtier IGUAL O INFERIOR
        /// Nota: tier/subTier 0 se tratan como 1 (datos legacy).
        /// </summary>
        /// <param name="partTier">Tier de la pieza a verificar</param>
        /// <returns>True si la pieza es compatible</returns>
        public bool IsCompatibleWith(TierInfo partTier)
        {
            // Usar MainTier/SubTier que manejan 0 como 1
            if (partTier.MainTier != this.MainTier)
                return false;
            
            if (partTier.SubTier > this.SubTier)
                return false;
            
            return true;
        }
        
        /// <summary>
        /// Verifica si este tier puede usar piezas del tier especificado.
        /// Alias de IsCompatibleWith para claridad semántica.
        /// </summary>
        public bool CanUsePart(TierInfo partTier) => IsCompatibleWith(partTier);
        
        /// <summary>
        /// Verifica si esta pieza puede ser usada por un robot/estación del tier especificado.
        /// </summary>
        public bool CanBeUsedBy(TierInfo stationTier) => stationTier.IsCompatibleWith(this);
        
        #endregion
        
        #region Comparison
        
        /// <summary>
        /// Compara dos TierInfo. 
        /// Primero compara tier principal, luego subtier.
        /// </summary>
        public int CompareTo(TierInfo other)
        {
            int tierComparison = tier.CompareTo(other.tier);
            if (tierComparison != 0)
                return tierComparison;
            
            return subTier.CompareTo(other.subTier);
        }
        
        public static bool operator >(TierInfo a, TierInfo b) => a.CompareTo(b) > 0;
        public static bool operator <(TierInfo a, TierInfo b) => a.CompareTo(b) < 0;
        public static bool operator >=(TierInfo a, TierInfo b) => a.CompareTo(b) >= 0;
        public static bool operator <=(TierInfo a, TierInfo b) => a.CompareTo(b) <= 0;
        
        #endregion
        
        #region Equality
        
        public bool Equals(TierInfo other)
        {
            return tier == other.tier && subTier == other.subTier;
        }
        
        public override bool Equals(object obj)
        {
            return obj is TierInfo other && Equals(other);
        }
        
        public override int GetHashCode()
        {
            return HashCode.Combine(tier, subTier);
        }
        
        public static bool operator ==(TierInfo a, TierInfo b) => a.Equals(b);
        public static bool operator !=(TierInfo a, TierInfo b) => !a.Equals(b);
        
        #endregion
        
        #region String Conversion
        
        /// <summary>
        /// Retorna representación string como "X.Y" (ej: "2.3").
        /// </summary>
        public override string ToString()
        {
            return $"{tier}.{subTier}";
        }
        
        /// <summary>
        /// Retorna representación larga como "Tier X.Y".
        /// </summary>
        public string ToLongString()
        {
            return $"Tier {tier}.{subTier}";
        }
        
        /// <summary>
        /// Intenta parsear un string "X.Y" a TierInfo.
        /// </summary>
        public static bool TryParse(string str, out TierInfo result)
        {
            result = Default;
            
            if (string.IsNullOrEmpty(str))
                return false;
            
            string[] parts = str.Split('.');
            if (parts.Length != 2)
                return false;
            
            if (!int.TryParse(parts[0], out int t) || !int.TryParse(parts[1], out int st))
                return false;
            
            result = new TierInfo(t, st);
            return result.IsValid;
        }
        
        #endregion
    }
}
