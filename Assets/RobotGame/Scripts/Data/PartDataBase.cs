using UnityEngine;
using RobotGame.Enums;
using RobotGame.Inventory;

namespace RobotGame.Data
{
    /// <summary>
    /// ScriptableObject base para todas las piezas del robot.
    /// Implementa IInventoryItem para ser compatible con el sistema de inventario.
    /// </summary>
    public abstract class PartDataBase : ScriptableObject, IInventoryItem
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
        
        [Tooltip("Tier de la pieza (tier.subtier)")]
        public TierInfo tier = TierInfo.Tier1_1;
        
        [Header("Inventario")]
        [Tooltip("Rareza del item")]
        public ItemRarity rarity = ItemRarity.Common;
        
        [Tooltip("Cantidad máxima apilable (1 = no apilable, 99 para piezas)")]
        public int maxStackSize = 99;
        
        [Header("Estadísticas Base")]
        [Tooltip("Peso de la pieza")]
        public float weight = 1f;
        
        [Tooltip("Durabilidad/HP de la pieza")]
        public float durability = 100f;
        
        #region IInventoryItem Implementation
        
        public string ItemId => partId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public int MaxStackSize => maxStackSize;
        public ItemRarity Rarity => rarity;
        
        /// <summary>
        /// Categoría de inventario (derivada de PartCategory).
        /// </summary>
        public virtual InventoryCategory Category
        {
            get
            {
                switch (category)
                {
                    case PartCategory.Structural:
                        return InventoryCategory.StructuralParts;
                    case PartCategory.Armor:
                    case PartCategory.Decorative:
                        return InventoryCategory.ArmorParts;
                    default:
                        return InventoryCategory.StructuralParts;
                }
            }
        }
        
        /// <summary>
        /// Subcategoría de inventario (por defecto None, override en clases derivadas).
        /// </summary>
        public virtual InventorySubCategory SubCategory => InventorySubCategory.None;
        
        /// <summary>
        /// Tier del item (1-4).
        /// </summary>
        public int Tier => MainTier;
        
        #endregion
        
        #region Tier Properties
        
        /// <summary>
        /// Obtiene el tier principal (1, 2, 3 o 4) del tier de esta pieza.
        /// </summary>
        public int MainTier
        {
            get
            {
                return tier.MainTier;
            }
        }
        
        /// <summary>
        /// Obtiene el subtier (1-6) del tier de esta pieza.
        /// </summary>
        public int TierVariant
        {
            get
            {
                return tier.SubTier;
            }
        }
        
        /// <summary>
        /// Verifica si esta pieza es compatible con un robot/estación del tier especificado.
        /// </summary>
        public bool IsCompatibleWith(TierInfo targetTier)
        {
            return targetTier.IsCompatibleWith(tier);
        }
        
        #endregion
    }
}
