using UnityEngine;
using RobotGame.Enums;

namespace RobotGame.Data
{
    /// <summary>
    /// ScriptableObject base para todas las piezas del robot.
    /// </summary>
    public abstract class PartDataBase : ScriptableObject
    {
        [Header("Información Básica")]
        [Tooltip("ID único de la pieza")]
        public string partId;
        
        [Tooltip("Nombre para mostrar")]
        public string displayName;
        
        [Tooltip("Descripción de la pieza")]
        [TextArea(2, 4)]
        public string description;
        
        [Tooltip("Icono para UI")]
        public Sprite icon;
        
        [Tooltip("Prefab de la pieza")]
        public GameObject prefab;
        
        [Header("Clasificación")]
        [Tooltip("Categoría de la pieza")]
        public PartCategory category;
        
        [Tooltip("Tier de la pieza")]
        public RobotTier tier;
        
        [Header("Estadísticas Base")]
        [Tooltip("Peso de la pieza")]
        public float weight = 1f;
        
        [Tooltip("Durabilidad/HP de la pieza")]
        public float durability = 100f;
        
        /// <summary>
        /// Obtiene el tier principal (1, 2, 3 o 4) del tier de esta pieza.
        /// </summary>
        public int MainTier
        {
            get
            {
                string tierName = tier.ToString();
                // Tier1_1 -> 1, Tier2_3 -> 2, etc.
                if (tierName.StartsWith("Tier"))
                {
                    return int.Parse(tierName[4].ToString());
                }
                return 1;
            }
        }
        
        /// <summary>
        /// Obtiene la variante (1, 2 o 3) del tier de esta pieza.
        /// </summary>
        public int TierVariant
        {
            get
            {
                string tierName = tier.ToString();
                // Tier1_1 -> 1, Tier1_2 -> 2, etc.
                int underscoreIndex = tierName.IndexOf('_');
                if (underscoreIndex >= 0 && underscoreIndex < tierName.Length - 1)
                {
                    return int.Parse(tierName[underscoreIndex + 1].ToString());
                }
                return 1;
            }
        }
        
        /// <summary>
        /// Verifica si esta pieza es compatible con un core de cierto tier.
        /// </summary>
        public bool IsCompatibleWith(RobotTier coreTier)
        {
            // Obtener tier principal y variante del core
            string coreTierName = coreTier.ToString();
            int coreMainTier = int.Parse(coreTierName[4].ToString());
            int coreVariant = int.Parse(coreTierName[coreTierName.IndexOf('_') + 1].ToString());
            
            // Debe ser el mismo tier principal
            if (MainTier != coreMainTier)
            {
                return false;
            }
            
            // La variante de la pieza debe ser <= variante del core
            return TierVariant <= coreVariant;
        }
    }
}
