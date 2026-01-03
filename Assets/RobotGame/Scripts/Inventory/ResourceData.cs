using UnityEngine;

namespace RobotGame.Inventory
{
    /// <summary>
    /// ScriptableObject para recursos (metales, cristales, componentes, etc.).
    /// Usado para crafting, construcción y reparación.
    /// </summary>
    [CreateAssetMenu(fileName = "NewResource", menuName = "RobotGame/Inventory/Resource")]
    public class ResourceData : ScriptableObject, IInventoryItem
    {
        [Header("Información Básica")]
        [Tooltip("ID único del recurso")]
        public string resourceId;
        
        [Tooltip("Nombre para mostrar")]
        public string displayName;
        
        [Tooltip("Descripción del recurso")]
        [TextArea(2, 4)]
        public string description;
        
        [Tooltip("Ícono para UI")]
        public Sprite icon;
        
        [Header("Clasificación")]
        [Tooltip("Subcategoría del recurso")]
        public InventorySubCategory subCategory = InventorySubCategory.Metal;
        
        [Tooltip("Rareza del recurso")]
        public ItemRarity rarity = ItemRarity.Common;
        
        [Header("Apilado")]
        [Tooltip("Cantidad máxima por stack")]
        public int maxStackSize = 99;
        
        [Header("Valor")]
        [Tooltip("Valor base para comercio")]
        public int baseValue = 10;
        
        #region IInventoryItem Implementation
        
        public string ItemId => resourceId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public InventoryCategory Category => InventoryCategory.Resources;
        public InventorySubCategory SubCategory => subCategory;
        public int MaxStackSize => maxStackSize;
        public int Tier => 0; // Recursos no tienen tier
        public ItemRarity Rarity => rarity;
        
        #endregion
    }
}
