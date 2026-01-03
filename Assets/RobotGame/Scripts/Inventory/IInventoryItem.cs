using UnityEngine;

namespace RobotGame.Inventory
{
    /// <summary>
    /// Interfaz que deben implementar todos los ScriptableObjects que pueden estar en el inventario.
    /// Permite que PartDataBase, ResourceData, BuildingPartData, etc. sean items de inventario.
    /// </summary>
    public interface IInventoryItem
    {
        /// <summary>
        /// ID único del item.
        /// </summary>
        string ItemId { get; }
        
        /// <summary>
        /// Nombre para mostrar en UI.
        /// </summary>
        string DisplayName { get; }
        
        /// <summary>
        /// Descripción del item.
        /// </summary>
        string Description { get; }
        
        /// <summary>
        /// Ícono para mostrar en UI.
        /// </summary>
        Sprite Icon { get; }
        
        /// <summary>
        /// Categoría principal del item.
        /// </summary>
        InventoryCategory Category { get; }
        
        /// <summary>
        /// Subcategoría para filtrado.
        /// </summary>
        InventorySubCategory SubCategory { get; }
        
        /// <summary>
        /// Cantidad máxima que se puede apilar (1 = no apilable).
        /// </summary>
        int MaxStackSize { get; }
        
        /// <summary>
        /// Tier del item (0 si no aplica).
        /// </summary>
        int Tier { get; }
        
        /// <summary>
        /// Rareza del item para colorear bordes en UI.
        /// </summary>
        ItemRarity Rarity { get; }
    }
    
    /// <summary>
    /// Rareza de items (afecta color de borde en UI).
    /// </summary>
    public enum ItemRarity
    {
        Common,     // Gris/Blanco
        Uncommon,   // Verde
        Rare,       // Azul
        Epic,       // Morado
        Legendary   // Dorado/Naranja
    }
}
