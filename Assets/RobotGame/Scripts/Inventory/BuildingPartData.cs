using UnityEngine;

namespace RobotGame.Inventory
{
    /// <summary>
    /// Tipo de pieza de construcción.
    /// </summary>
    public enum BuildingPartType
    {
        Foundation,
        Wall,
        Floor,
        Ceiling,
        Door,
        Window,
        Stairs,
        Ramp,
        Fence,
        Furniture,
        Storage,
        Workstation,
        Defense,
        Decoration
    }
    
    /// <summary>
    /// ScriptableObject para piezas de construcción de bases.
    /// </summary>
    [CreateAssetMenu(fileName = "NewBuildingPart", menuName = "RobotGame/Inventory/Building Part")]
    public class BuildingPartData : ScriptableObject, IInventoryItem
    {
        [Header("Información Básica")]
        [Tooltip("ID único de la pieza")]
        public string partId;
        
        [Tooltip("Nombre para mostrar")]
        public string displayName;
        
        [Tooltip("Descripción")]
        [TextArea(2, 4)]
        public string description;
        
        [Tooltip("Ícono para UI")]
        public Sprite icon;
        
        [Tooltip("Prefab de la pieza")]
        public GameObject prefab;
        
        [Header("Clasificación")]
        [Tooltip("Tipo de pieza de construcción")]
        public BuildingPartType partType = BuildingPartType.Wall;
        
        [Tooltip("Rareza")]
        public ItemRarity rarity = ItemRarity.Common;
        
        [Header("Apilado")]
        [Tooltip("Cantidad máxima por stack")]
        public int maxStackSize = 20;
        
        [Header("Estadísticas")]
        [Tooltip("Puntos de estructura/HP")]
        public float structurePoints = 100f;
        
        [Tooltip("Resistencia al daño (0-1)")]
        [Range(0f, 1f)]
        public float damageResistance = 0f;
        
        [Header("Construcción")]
        [Tooltip("Tamaño de la pieza en unidades de grilla")]
        public Vector3Int gridSize = Vector3Int.one;
        
        [Tooltip("Si puede rotar")]
        public bool canRotate = true;
        
        [Tooltip("Si necesita fundación debajo")]
        public bool requiresFoundation = true;
        
        #region IInventoryItem Implementation
        
        public string ItemId => partId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public InventoryCategory Category => InventoryCategory.BuildingParts;
        public int MaxStackSize => maxStackSize;
        public int Tier => 0;
        public ItemRarity Rarity => rarity;
        
        public InventorySubCategory SubCategory
        {
            get
            {
                switch (partType)
                {
                    case BuildingPartType.Foundation:
                        return InventorySubCategory.Foundation;
                    case BuildingPartType.Wall:
                    case BuildingPartType.Floor:
                    case BuildingPartType.Ceiling:
                        return InventorySubCategory.Wall;
                    case BuildingPartType.Door:
                    case BuildingPartType.Window:
                        return InventorySubCategory.Door;
                    case BuildingPartType.Furniture:
                    case BuildingPartType.Storage:
                    case BuildingPartType.Workstation:
                    case BuildingPartType.Decoration:
                        return InventorySubCategory.Furniture;
                    case BuildingPartType.Defense:
                        return InventorySubCategory.Defense;
                    default:
                        return InventorySubCategory.None;
                }
            }
        }
        
        #endregion
    }
}
