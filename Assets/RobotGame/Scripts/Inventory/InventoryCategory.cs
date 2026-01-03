namespace RobotGame.Inventory
{
    /// <summary>
    /// Categorías principales del inventario.
    /// Cada categoría representa una pestaña en el UI.
    /// </summary>
    public enum InventoryCategory
    {
        /// <summary>
        /// Piezas estructurales del robot (Head, Torso, Hips, Legs, Arms).
        /// </summary>
        StructuralParts,
        
        /// <summary>
        /// Piezas de armadura decorativas.
        /// </summary>
        ArmorParts,
        
        /// <summary>
        /// Cores para robots.
        /// </summary>
        Cores,
        
        /// <summary>
        /// Recursos básicos (metales, cristales, componentes).
        /// </summary>
        Resources,
        
        /// <summary>
        /// Piezas de construcción para bases.
        /// </summary>
        BuildingParts,
        
        /// <summary>
        /// Consumibles (reparación, mejoras temporales).
        /// </summary>
        Consumables,
        
        /// <summary>
        /// Items de misión o especiales.
        /// </summary>
        QuestItems
    }
    
    /// <summary>
    /// Subcategorías para filtrado más específico.
    /// </summary>
    public enum InventorySubCategory
    {
        // Estructurales
        None,
        Head,
        Torso,
        Hips,
        Legs,
        Arms,
        Tail,
        
        // Recursos
        Metal,
        Crystal,
        Circuit,
        Energy,
        
        // Building
        Foundation,
        Wall,
        Door,
        Furniture,
        Defense,
        
        // Consumables
        RepairKit,
        Buff,
        Fuel
    }
}
