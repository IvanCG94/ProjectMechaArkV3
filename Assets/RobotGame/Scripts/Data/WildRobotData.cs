using System;
using System.Collections.Generic;
using UnityEngine;
using RobotGame.Enums;

namespace RobotGame.Data
{
    /// <summary>
    /// Comportamiento del robot salvaje.
    /// </summary>
    public enum WildRobotBehavior
    {
        Passive,        // No ataca, huye si es atacado
        Neutral,        // No ataca a menos que sea provocado
        Aggressive,     // Ataca al jugador si lo detecta
        Territorial     // Ataca si el jugador entra en su territorio
    }
    
    /// <summary>
    /// ScriptableObject que define un tipo de robot salvaje.
    /// Similar a RobotConfiguration pero sin Core y con datos de IA/comportamiento.
    /// 
    /// Ejemplo: "Mecha Raptor" - un robot Tier 2 agresivo con piezas específicas.
    /// </summary>
    [CreateAssetMenu(fileName = "NewWildRobot", menuName = "RobotGame/Wild Robot Data")]
    public class WildRobotData : ScriptableObject
    {
        [Header("Identificación")]
        [Tooltip("Nombre de la especie/tipo de robot")]
        public string speciesName = "Wild Robot";
        
        [Tooltip("Descripción del robot")]
        [TextArea(2, 4)]
        public string description;
        
        [Tooltip("Icono para UI/Bestiario")]
        public Sprite icon;
        
        [Header("Tier")]
        [Tooltip("Tier del robot salvaje (define qué piezas puede usar)")]
        public TierInfo tier = TierInfo.Tier2_1;
        
        [Header("Estructura Base")]
        [Tooltip("Las Hips (piernas) - raíz del robot")]
        public StructuralPartData hips;
        
        [Tooltip("Piezas de armadura en las Hips")]
        public List<PlacedArmorPiece> hipsArmorPieces = new List<PlacedArmorPiece>();
        
        [Header("Piezas Estructurales Conectadas")]
        [Tooltip("Piezas conectadas a los sockets de las Hips")]
        public List<AttachedStructuralPiece> attachedParts = new List<AttachedStructuralPiece>();
        
        [Header("Comportamiento")]
        [Tooltip("Comportamiento base del robot")]
        public WildRobotBehavior behavior = WildRobotBehavior.Neutral;
        
        [Tooltip("Radio de detección del jugador")]
        [Range(1f, 50f)]
        public float detectionRadius = 10f;
        
        [Tooltip("Radio del territorio (para comportamiento Territorial)")]
        [Range(1f, 100f)]
        public float territoryRadius = 20f;
        
        [Tooltip("Velocidad de movimiento")]
        [Range(1f, 20f)]
        public float moveSpeed = 5f;
        
        [Tooltip("Velocidad de rotación")]
        [Range(30f, 360f)]
        public float rotationSpeed = 120f;
        
        [Header("Combate")]
        [Tooltip("Daño base de ataque")]
        public float attackDamage = 10f;
        
        [Tooltip("Velocidad de ataque (ataques por segundo)")]
        [Range(0.1f, 5f)]
        public float attackSpeed = 1f;
        
        [Tooltip("Rango de ataque")]
        [Range(0.5f, 10f)]
        public float attackRange = 2f;
        
        [Header("Loot")]
        [Tooltip("Probabilidad de soltar cada pieza al ser derrotado (0-1)")]
        [Range(0f, 1f)]
        public float partDropChance = 0.5f;
        
        [Tooltip("Items adicionales que puede soltar")]
        public List<LootDrop> additionalLoot = new List<LootDrop>();
        
        [Header("Spawn")]
        [Tooltip("Peso de spawn (mayor = más común)")]
        [Range(1, 100)]
        public int spawnWeight = 10;
        
        [Tooltip("Nivel mínimo de zona para aparecer")]
        public int minimumZoneLevel = 1;
        
        /// <summary>
        /// Valida que todas las piezas sean compatibles con el tier del robot.
        /// </summary>
        public bool ValidateConfiguration(out List<string> errors)
        {
            errors = new List<string>();
            
            if (hips == null)
            {
                errors.Add("No hay Hips asignadas");
                return false;
            }
            
            // Validar Hips
            if (!hips.IsCompatibleWith(tier))
            {
                errors.Add($"Hips '{hips.displayName}' (Tier {hips.tier}) no es compatible con el robot (Tier {tier})");
            }
            
            // Validar piezas estructurales recursivamente
            ValidateStructuralPieces(attachedParts, errors);
            
            // Validar piezas de armadura en Hips
            ValidateArmorPieces(hipsArmorPieces, hips, errors);
            
            return errors.Count == 0;
        }
        
        private void ValidateStructuralPieces(List<AttachedStructuralPiece> pieces, List<string> errors)
        {
            foreach (var piece in pieces)
            {
                if (piece.partData == null) continue;
                
                if (!piece.partData.IsCompatibleWith(tier))
                {
                    errors.Add($"Pieza '{piece.partData.displayName}' (Tier {piece.partData.tier}) no es compatible con el robot (Tier {tier})");
                }
                
                // Validar armadura en esta pieza
                ValidateArmorPieces(piece.armorPieces, piece.partData, errors);
                
                // Recursión para piezas hijas
                ValidateStructuralPieces(piece.childParts, errors);
            }
        }
        
        private void ValidateArmorPieces(List<PlacedArmorPiece> armorPieces, StructuralPartData parentPart, List<string> errors)
        {
            foreach (var armor in armorPieces)
            {
                if (armor.armorData == null) continue;
                
                if (!armor.armorData.IsCompatibleWith(tier))
                {
                    errors.Add($"Armadura '{armor.armorData.displayName}' (Tier {armor.armorData.tier}) no es compatible con el robot (Tier {tier})");
                }
                
                // Verificar que la grilla existe en el padre
                var grid = parentPart.armorGrids.Find(g => g.gridInfo.gridName == armor.targetGridName);
                if (grid == null || grid.transformName == null)
                {
                    errors.Add($"Grilla '{armor.targetGridName}' no existe en '{parentPart.displayName}'");
                }
            }
        }
        
        /// <summary>
        /// Obtiene todas las piezas estructurales de esta configuración.
        /// </summary>
        public List<StructuralPartData> GetAllStructuralParts()
        {
            var parts = new List<StructuralPartData>();
            
            if (hips != null)
            {
                parts.Add(hips);
            }
            
            CollectStructuralParts(attachedParts, parts);
            
            return parts;
        }
        
        private void CollectStructuralParts(List<AttachedStructuralPiece> pieces, List<StructuralPartData> result)
        {
            foreach (var piece in pieces)
            {
                if (piece.partData != null)
                {
                    result.Add(piece.partData);
                    CollectStructuralParts(piece.childParts, result);
                }
            }
        }
        
        /// <summary>
        /// Obtiene todas las piezas de armadura de esta configuración.
        /// </summary>
        public List<ArmorPartData> GetAllArmorParts()
        {
            var parts = new List<ArmorPartData>();
            
            foreach (var armor in hipsArmorPieces)
            {
                if (armor.armorData != null)
                {
                    parts.Add(armor.armorData);
                }
            }
            
            CollectArmorParts(attachedParts, parts);
            
            return parts;
        }
        
        private void CollectArmorParts(List<AttachedStructuralPiece> pieces, List<ArmorPartData> result)
        {
            foreach (var piece in pieces)
            {
                foreach (var armor in piece.armorPieces)
                {
                    if (armor.armorData != null)
                    {
                        result.Add(armor.armorData);
                    }
                }
                
                CollectArmorParts(piece.childParts, result);
            }
        }
        
        /// <summary>
        /// Calcula el peso total del robot.
        /// </summary>
        public float CalculateTotalWeight()
        {
            float weight = 0f;
            
            foreach (var part in GetAllStructuralParts())
            {
                weight += part.weight;
            }
            
            foreach (var armor in GetAllArmorParts())
            {
                weight += armor.weight;
            }
            
            return weight;
        }
        
        /// <summary>
        /// Calcula la durabilidad total del robot.
        /// </summary>
        public float CalculateTotalDurability()
        {
            float durability = 0f;
            
            foreach (var part in GetAllStructuralParts())
            {
                durability += part.durability;
            }
            
            foreach (var armor in GetAllArmorParts())
            {
                durability += armor.durability;
            }
            
            return durability;
        }
        
        private void OnValidate()
        {
            // Validar en el editor
            if (Application.isPlaying) return;
            
            List<string> errors;
            if (!ValidateConfiguration(out errors) && errors.Count > 0)
            {
                Debug.LogWarning($"WildRobotData '{speciesName}' tiene problemas de configuración:\n{string.Join("\n", errors)}");
            }
        }
    }
    
    /// <summary>
    /// Define un item de loot con su probabilidad.
    /// </summary>
    [Serializable]
    public class LootDrop
    {
        [Tooltip("Item a soltar (puede ser cualquier ScriptableObject)")]
        public ScriptableObject item;
        
        [Tooltip("Probabilidad de soltar (0-1)")]
        [Range(0f, 1f)]
        public float dropChance = 0.1f;
        
        [Tooltip("Cantidad mínima")]
        public int minQuantity = 1;
        
        [Tooltip("Cantidad máxima")]
        public int maxQuantity = 1;
    }
}
