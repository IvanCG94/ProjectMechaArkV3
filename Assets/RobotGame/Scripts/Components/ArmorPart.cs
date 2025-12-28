using System.Collections.Generic;
using UnityEngine;
using RobotGame.Data;
using RobotGame.Utils;

namespace RobotGame.Components
{
    /// <summary>
    /// Componente runtime que representa una pieza de armadura instanciada.
    /// </summary>
    public class ArmorPart : MonoBehaviour
    {
        [Header("Datos")]
        [SerializeField] private ArmorPartData armorData;
        [SerializeField] private string instanceId;
        
        [Header("Ubicación")]
        [SerializeField] private GridHead parentGrid;
        [SerializeField] private int gridPositionX;
        [SerializeField] private int gridPositionY;
        [SerializeField] private GridRotation.Rotation currentRotation;
        
        [Header("Grillas Adicionales")]
        [SerializeField] private List<GridHead> additionalGrids = new List<GridHead>();
        
        [Header("Estado")]
        [SerializeField] private float currentDurability;
        
        /// <summary>
        /// Datos de la pieza de armadura (ScriptableObject).
        /// </summary>
        public ArmorPartData ArmorData => armorData;
        
        /// <summary>
        /// ID único de esta instancia.
        /// </summary>
        public string InstanceId => instanceId;
        
        /// <summary>
        /// Grilla donde está colocada esta pieza.
        /// </summary>
        public GridHead ParentGrid => parentGrid;
        
        /// <summary>
        /// Posición X en la grilla (celda inicial).
        /// </summary>
        public int GridPositionX => gridPositionX;
        
        /// <summary>
        /// Posición Y en la grilla (celda inicial).
        /// </summary>
        public int GridPositionY => gridPositionY;
        
        /// <summary>
        /// Rotación actual de la pieza.
        /// </summary>
        public GridRotation.Rotation CurrentRotation => currentRotation;
        
        /// <summary>
        /// Grillas Head adicionales para apilar más piezas.
        /// </summary>
        public IReadOnlyList<GridHead> AdditionalGrids => additionalGrids;
        
        /// <summary>
        /// Durabilidad actual de la pieza.
        /// </summary>
        public float CurrentDurability => currentDurability;
        
        /// <summary>
        /// Durabilidad máxima de la pieza.
        /// </summary>
        public float MaxDurability => armorData != null ? armorData.durability : 0f;
        
        /// <summary>
        /// Porcentaje de durabilidad (0-1).
        /// </summary>
        public float DurabilityPercent => MaxDurability > 0 ? currentDurability / MaxDurability : 0f;
        
        /// <summary>
        /// Inicializa la pieza de armadura con sus datos.
        /// </summary>
        public void Initialize(ArmorPartData data, string id = null)
        {
            armorData = data;
            instanceId = id ?? System.Guid.NewGuid().ToString();
            currentDurability = data.durability;
            
            // Crear grillas adicionales si las tiene
            CreateAdditionalGrids();
        }
        
        private void CreateAdditionalGrids()
        {
            additionalGrids.Clear();
            
            if (armorData.additionalHeadGrids == null) return;
            
            foreach (var gridDef in armorData.additionalHeadGrids)
            {
                // Buscar el transform hijo por nombre
                Transform gridTransform = FindChildByName(transform, gridDef.transformName);
                
                if (gridTransform == null)
                {
                    // Si no existe, crear un nuevo GameObject
                    GameObject gridGO = new GameObject($"Grid_{gridDef.gridInfo.gridName}");
                    gridTransform = gridGO.transform;
                    gridTransform.SetParent(transform);
                    gridTransform.localPosition = Vector3.zero;
                    gridTransform.localRotation = Quaternion.identity;
                }
                
                // Agregar el componente GridHead
                GridHead grid = gridTransform.GetComponent<GridHead>();
                if (grid == null)
                {
                    grid = gridTransform.gameObject.AddComponent<GridHead>();
                }
                
                grid.Initialize(gridDef.gridInfo);
                additionalGrids.Add(grid);
            }
        }
        
        /// <summary>
        /// Busca una grilla adicional por nombre.
        /// </summary>
        public GridHead GetAdditionalGrid(string gridName)
        {
            return additionalGrids.Find(g => g.GridInfo.gridName == gridName);
        }
        
        /// <summary>
        /// Llamado cuando la pieza se coloca en una grilla.
        /// </summary>
        public void OnPlaced(GridHead grid, int posX, int posY)
        {
            OnPlaced(grid, posX, posY, GridRotation.Rotation.Deg0);
        }
        
        /// <summary>
        /// Llamado cuando la pieza se coloca en una grilla con rotación.
        /// </summary>
        public void OnPlaced(GridHead grid, int posX, int posY, GridRotation.Rotation rotation)
        {
            parentGrid = grid;
            gridPositionX = posX;
            gridPositionY = posY;
            currentRotation = rotation;
        }
        
        /// <summary>
        /// Llamado cuando la pieza se remueve de una grilla.
        /// </summary>
        public void OnRemoved(GridHead grid)
        {
            if (parentGrid == grid)
            {
                parentGrid = null;
                gridPositionX = -1;
                gridPositionY = -1;
            }
        }
        
        /// <summary>
        /// Aplica daño a la pieza.
        /// </summary>
        /// <param name="damage">Cantidad de daño base</param>
        /// <param name="damageType">Tipo de daño (0=physical, 1=energy, 2=explosive)</param>
        /// <returns>Daño efectivo después de resistencias</returns>
        public float TakeDamage(float damage, int damageType = 0)
        {
            float resistance = 0f;
            
            switch (damageType)
            {
                case 0: resistance = armorData.physicalResistance; break;
                case 1: resistance = armorData.energyResistance; break;
                case 2: resistance = armorData.explosiveResistance; break;
            }
            
            float effectiveDamage = damage * (1f - resistance);
            currentDurability = Mathf.Max(0f, currentDurability - effectiveDamage);
            
            // Actualizar visual si está muy dañado
            if (DurabilityPercent < 0.3f && armorData.damagedMaterial != null)
            {
                ApplyDamagedVisual();
            }
            
            return effectiveDamage;
        }
        
        /// <summary>
        /// Repara la pieza.
        /// </summary>
        public void Repair(float amount)
        {
            currentDurability = Mathf.Min(MaxDurability, currentDurability + amount);
        }
        
        /// <summary>
        /// Repara completamente la pieza.
        /// </summary>
        public void FullRepair()
        {
            currentDurability = MaxDurability;
        }
        
        private void ApplyDamagedVisual()
        {
            if (armorData.damagedMaterial == null) return;
            
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                renderer.material = armorData.damagedMaterial;
            }
        }
        
        private Transform FindChildByName(Transform parent, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            
            foreach (Transform child in parent)
            {
                if (child.name == name)
                {
                    return child;
                }
                
                Transform found = FindChildByName(child, name);
                if (found != null)
                {
                    return found;
                }
            }
            
            return null;
        }
    }
}
