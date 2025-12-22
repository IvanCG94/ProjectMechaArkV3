using System;
using UnityEngine;
using RobotGame.Enums;

namespace RobotGame.Data
{
    /// <summary>
    /// ScriptableObject para piezas de armadura, armas y decorativas.
    /// Estas piezas se insertan en las grillas Head de las piezas estructurales.
    /// </summary>
    [CreateAssetMenu(fileName = "NewArmorPart", menuName = "RobotGame/Parts/Armor Part")]
    public class ArmorPartData : PartDataBase
    {
        [Header("Configuración de Grilla")]
        [Tooltip("Grilla Tail que define el espacio que ocupa esta pieza")]
        public TailGridDefinition tailGrid;
        
        [Header("Grillas Head Adicionales (Opcional)")]
        [Tooltip("Algunas piezas de armadura pueden tener sus propias grillas para apilar más piezas")]
        public HeadGridDefinition[] additionalHeadGrids;
        
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
        /// Tamaño de la pieza en celdas.
        /// </summary>
        public Vector2Int Size => new Vector2Int(tailGrid.gridInfo.sizeX, tailGrid.gridInfo.sizeY);
        
        /// <summary>
        /// Nivel de Surrounding de la pieza.
        /// </summary>
        public SurroundingLevel Surrounding => tailGrid.gridInfo.surrounding;
        
        /// <summary>
        /// Verifica si esta pieza puede insertarse en una grilla Head específica.
        /// </summary>
        public bool CanFitIn(GridInfo headGrid)
        {
            // Verificar compatibilidad de Surrounding
            if (!headGrid.surrounding.CanAccept(tailGrid.gridInfo.surrounding))
            {
                return false;
            }
            
            // Verificar que el tamaño de la pieza no exceda el de la grilla
            if (tailGrid.gridInfo.sizeX > headGrid.sizeX || tailGrid.gridInfo.sizeY > headGrid.sizeY)
            {
                return false;
            }
            
            return true;
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
}
