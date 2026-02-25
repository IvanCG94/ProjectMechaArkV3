using System.Collections.Generic;
using UnityEngine;
using RobotGame.Data;
using RobotGame.Enums;
using RobotGame.Utils;

namespace RobotGame.Components
{
    /// <summary>
    /// Componente runtime que representa una pieza de armadura instanciada.
    /// 
    /// Detecta automáticamente:
    /// - Tail_T{tier}-{subtier}_{nombre} → StudGridTail para conexión
    /// - Box_{nombre} → BoxCollider para validación de colisión
    /// </summary>
    public class ArmorPart : MonoBehaviour
    {
        [Header("Datos")]
        [SerializeField] private ArmorPartData armorData;
        [SerializeField] private string instanceId;
        
        [Header("Ubicación")]
        [SerializeField] private StudGridHead parentGrid;
        [SerializeField] private StudGridTail tailGrid;
        [SerializeField] private int gridPositionX = -1;
        [SerializeField] private int gridPositionY = -1;
        
        [Header("Grillas Adicionales (para apilar)")]
        [SerializeField] private List<StudGridHead> additionalGrids = new List<StudGridHead>();
        
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
        /// Grilla Head donde está colocada esta pieza.
        /// </summary>
        public StudGridHead ParentGrid => parentGrid;
        
        /// <summary>
        /// Grilla Tail de esta pieza (studs de conexión).
        /// </summary>
        public StudGridTail TailGrid => tailGrid;
        
        /// <summary>
        /// Posición X en la grilla (para compatibilidad).
        /// </summary>
        public int GridPositionX => gridPositionX;
        
        /// <summary>
        /// Posición Y en la grilla (para compatibilidad).
        /// </summary>
        public int GridPositionY => gridPositionY;
        
        /// <summary>
        /// Rotación actual (para compatibilidad, siempre 0 en nuevo sistema).
        /// </summary>
        public int CurrentRotation => 0;
        
        /// <summary>
        /// Grillas Head adicionales para apilar más piezas.
        /// </summary>
        public IReadOnlyList<StudGridHead> AdditionalGrids => additionalGrids;
        
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
            
            // Detectar y crear StudGridTail
            DetectTailGrid();
        }
        
        /// <summary>
        /// Detecta automáticamente los studs Tail desde los Empties del prefab.
        /// </summary>
        private void DetectTailGrid()
        {
            // Buscar si ya existe un StudGridTail
            tailGrid = GetComponentInChildren<StudGridTail>();
            
            if (tailGrid == null)
            {
                // Crear uno nuevo
                tailGrid = gameObject.AddComponent<StudGridTail>();
            }
            
            // Detectar studs
            tailGrid.DetectStuds();
            
            if (tailGrid.StudCount > 0)
            {
                Debug.Log($"ArmorPart: Detectados {tailGrid.StudCount} studs Tail en {armorData?.displayName ?? gameObject.name}");
            }
            else
            {
                Debug.LogWarning($"ArmorPart: No se encontraron studs Tail_ en {armorData?.displayName ?? gameObject.name}");
            }
        }
        
        /// <summary>
        /// Llamado cuando la pieza se coloca en una grilla.
        /// </summary>
        public void OnPlaced(StudGridHead grid)
        {
            parentGrid = grid;
        }
        
        /// <summary>
        /// Llamado cuando la pieza se remueve de una grilla.
        /// </summary>
        public void OnRemoved()
        {
            parentGrid = null;
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
    }
}
