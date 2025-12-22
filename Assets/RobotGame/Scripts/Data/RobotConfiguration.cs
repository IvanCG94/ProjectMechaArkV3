using System;
using System.Collections.Generic;
using UnityEngine;
using RobotGame.Enums;

namespace RobotGame.Data
{
    /// <summary>
    /// Define una pieza de armadura colocada en una posición específica de una grilla.
    /// </summary>
    [Serializable]
    public class PlacedArmorPiece
    {
        [Tooltip("Datos de la pieza de armadura")]
        public ArmorPartData armorData;
        
        [Tooltip("Nombre de la grilla Head donde está colocada")]
        public string targetGridName;
        
        [Tooltip("Posición X en la grilla (celda inicial)")]
        public int gridPositionX;
        
        [Tooltip("Posición Y en la grilla (celda inicial)")]
        public int gridPositionY;
    }
    
    /// <summary>
    /// Define una pieza estructural conectada a un socket.
    /// </summary>
    [Serializable]
    public class AttachedStructuralPiece
    {
        [Tooltip("Datos de la pieza estructural")]
        public StructuralPartData partData;
        
        [Tooltip("Tipo de socket al que está conectada")]
        public StructuralSocketType attachedToSocket;
        
        [Tooltip("Piezas estructurales hijas conectadas a esta pieza")]
        public List<AttachedStructuralPiece> childParts = new List<AttachedStructuralPiece>();
        
        [Tooltip("Piezas de armadura colocadas en las grillas de esta pieza")]
        public List<PlacedArmorPiece> armorPieces = new List<PlacedArmorPiece>();
    }
    
    /// <summary>
    /// ScriptableObject que define la configuración completa de un robot.
    /// Esto se usa para el "inventario inicial" y para guardar configuraciones de robots.
    /// </summary>
    [CreateAssetMenu(fileName = "NewRobotConfig", menuName = "RobotGame/Robot Configuration")]
    public class RobotConfiguration : ScriptableObject
    {
        [Header("Información Básica")]
        [Tooltip("Nombre del robot/configuración")]
        public string robotName = "New Robot";
        
        [Tooltip("Descripción de la configuración")]
        [TextArea(2, 4)]
        public string description;
        
        [Tooltip("Icono para UI")]
        public Sprite icon;
        
        [Header("Core")]
        [Tooltip("El Core que define el tier de este robot")]
        public CoreData core;
        
        [Header("Estructura Base")]
        [Tooltip("Las Hips (piernas) - raíz del robot")]
        public StructuralPartData hips;
        
        [Tooltip("Piezas de armadura en las Hips")]
        public List<PlacedArmorPiece> hipsArmorPieces = new List<PlacedArmorPiece>();
        
        [Header("Piezas Estructurales Conectadas")]
        [Tooltip("Piezas conectadas a los sockets de las Hips")]
        public List<AttachedStructuralPiece> attachedParts = new List<AttachedStructuralPiece>();
        
        /// <summary>
        /// Valida que todas las piezas sean compatibles con el tier del core.
        /// </summary>
        public bool ValidateConfiguration(out List<string> errors)
        {
            errors = new List<string>();
            
            if (core == null)
            {
                errors.Add("No hay Core asignado");
                return false;
            }
            
            if (hips == null)
            {
                errors.Add("No hay Hips asignadas");
                return false;
            }
            
            // Validar Hips
            if (!core.CanUsePart(hips))
            {
                errors.Add($"Hips '{hips.displayName}' no es compatible con el Core (Tier {core.tier})");
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
                
                if (!core.CanUsePart(piece.partData))
                {
                    errors.Add($"Pieza '{piece.partData.displayName}' no es compatible con el Core (Tier {core.tier})");
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
                
                if (!core.CanUsePart(armor.armorData))
                {
                    errors.Add($"Armadura '{armor.armorData.displayName}' no es compatible con el Core (Tier {core.tier})");
                }
                
                // Verificar que la grilla existe en el padre
                var grid = parentPart.armorGrids.Find(g => g.gridInfo.gridName == armor.targetGridName);
                if (grid == null || grid.transformName == null)
                {
                    errors.Add($"Grilla '{armor.targetGridName}' no existe en '{parentPart.displayName}'");
                }
                else
                {
                    // Verificar compatibilidad de Surrounding
                    if (!armor.armorData.CanFitIn(grid.gridInfo))
                    {
                        errors.Add($"Armadura '{armor.armorData.displayName}' no es compatible con la grilla '{armor.targetGridName}'");
                    }
                }
            }
        }
        
        /// <summary>
        /// Calcula el peso total del robot.
        /// </summary>
        public float CalculateTotalWeight()
        {
            float weight = 0f;
            
            if (hips != null) weight += hips.weight;
            if (core != null) weight += core.weight;
            
            weight += CalculateStructuralWeight(attachedParts);
            weight += CalculateArmorWeight(hipsArmorPieces);
            
            return weight;
        }
        
        private float CalculateStructuralWeight(List<AttachedStructuralPiece> pieces)
        {
            float weight = 0f;
            
            foreach (var piece in pieces)
            {
                if (piece.partData != null)
                {
                    weight += piece.partData.weight;
                    weight += CalculateArmorWeight(piece.armorPieces);
                    weight += CalculateStructuralWeight(piece.childParts);
                }
            }
            
            return weight;
        }
        
        private float CalculateArmorWeight(List<PlacedArmorPiece> armorPieces)
        {
            float weight = 0f;
            
            foreach (var armor in armorPieces)
            {
                if (armor.armorData != null)
                {
                    weight += armor.armorData.weight;
                }
            }
            
            return weight;
        }
    }
}
