using System;
using UnityEngine;
using RobotGame.Enums;

namespace RobotGame.Data
{
    /// <summary>
    /// ScriptableObject para piezas de armadura, armas y decorativas.
    /// Estas piezas se insertan en los studs Head de las piezas estructurales.
    /// 
    /// NOMENCLATURA EN BLENDER:
    /// - Tail_T{tier}-{subtier}_{nombre} para definir studs de conexión
    /// - Box_{nombre} para definir áreas de colisión
    /// 
    /// EJEMPLO:
    /// ChestPlate (prefab)
    /// ├── ChestPlate_Visual (Mesh)
    /// ├── Box_Main (Empty, escala define tamaño de colisión)
    /// ├── Tail_T1-2_P1 (Empty @ posición 0, 0, 0)
    /// └── Tail_T1-2_P2 (Empty @ posición 0, 0.125, 0)
    /// </summary>
    [CreateAssetMenu(fileName = "NewArmorPart", menuName = "RobotGame/Parts/Armor Part")]
    public class ArmorPartData : PartDataBase
    {
        [Header("Configuración de Tier")]
        [Tooltip("Tier de esta pieza (debe coincidir con la grilla Head donde se inserta)")]
        public TierInfo tierInfo = TierInfo.Default;
        
        [Header("Estadísticas de Combate")]
        [Tooltip("Armadura/Defensa que provee")]
        public float armor = 0f;
        
        [Tooltip("Resistencia a ciertos tipos de daño (0-1)")]
        [Range(0f, 1f)]
        public float physicalResistance = 0f;
        
        [Range(0f, 1f)]
        public float energyResistance = 0f;
        
        [Range(0f, 1f)]
        public float explosiveResistance = 0f;
        
        [Header("Efectos Visuales")]
        [Tooltip("Material alternativo cuando está dañado")]
        public Material damagedMaterial;
        
        [Tooltip("Prefab de partículas al recibir daño")]
        public GameObject hitEffectPrefab;
        
        /// <summary>
        /// Tier principal de la pieza (1-6).
        /// </summary>
        public int MainTier => tierInfo.MainTier;
        
        /// <summary>
        /// Sub-tier de la pieza (1-6).
        /// </summary>
        public int SubTier => tierInfo.SubTier;
        
        /// <summary>
        /// Tier de armadura (para compatibilidad con código legacy).
        /// </summary>
        public int ArmorTier => tierInfo.MainTier;
        
        /// <summary>
        /// TierInfo de la armadura.
        /// </summary>
        public TierInfo ArmorTierInfo => tierInfo;
        
        /// <summary>
        /// Objeto de compatibilidad para código legacy que usa tailGrid.
        /// </summary>
        public TailGridCompat tailGrid => new TailGridCompat(tierInfo);
        
        /// <summary>
        /// Verifica si esta pieza puede colocarse en una grilla con el tier especificado.
        /// En el nuevo sistema, solo verifica compatibilidad de tier.
        /// </summary>
        public bool CanFitIn(TierInfo gridTier)
        {
            return gridTier.IsCompatibleWith(tierInfo);
        }
        
        private void OnValidate()
        {
            // Asegurar que la categoría no sea Structural
            if (category == PartCategory.Structural)
            {
                category = PartCategory.Armor;
            }
        }
    }
    
    /// <summary>
    /// Clase de compatibilidad para código legacy que usa tailGrid.gridInfo
    /// </summary>
    [System.Serializable]
    public class TailGridCompat
    {
        public GridInfoCompat gridInfo;
        
        public TailGridCompat(TierInfo tier)
        {
            gridInfo = new GridInfoCompat(tier);
        }
    }
    
    /// <summary>
    /// Clase de compatibilidad para código legacy que usa gridInfo.sizeX, gridInfo.sizeY
    /// </summary>
    [System.Serializable]
    public class GridInfoCompat
    {
        public int sizeX = 1;
        public int sizeY = 1;
        public int tier = 1;
        public TierInfo tierInfo;
        
        public GridInfoCompat(TierInfo tier)
        {
            this.tierInfo = tier;
            this.tier = tier.MainTier;
            // En el nuevo sistema no hay tamaño de grilla fijo
            this.sizeX = 1;
            this.sizeY = 1;
        }
        
        public int Tier => tierInfo.MainTier;
    }
}
