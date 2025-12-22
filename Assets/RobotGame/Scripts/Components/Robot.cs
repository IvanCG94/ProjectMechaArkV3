using System.Collections.Generic;
using UnityEngine;
using RobotGame.Data;
using RobotGame.Enums;

namespace RobotGame.Components
{
    /// <summary>
    /// Componente principal que representa un robot ensamblado.
    /// El GameObject raíz del robot tiene este componente.
    /// </summary>
    public class Robot : MonoBehaviour
    {
        [Header("Identificación")]
        [SerializeField] private string robotId;
        [SerializeField] private string robotName;
        
        [Header("Core")]
        [SerializeField] private RobotCore core;
        [SerializeField] private Transform coreSocket;
        
        [Header("Estructura")]
        [SerializeField] private StructuralPart hips;
        [SerializeField] private RobotTier currentTier;
        
        [Header("Estado")]
        [SerializeField] private bool isOperational = false;
        
        /// <summary>
        /// ID único del robot.
        /// </summary>
        public string RobotId => robotId;
        
        /// <summary>
        /// Nombre del robot.
        /// </summary>
        public string RobotName => robotName;
        
        /// <summary>
        /// Core insertado en el robot (null si no tiene).
        /// </summary>
        public RobotCore Core => core;
        
        /// <summary>
        /// Transform donde se inserta el core.
        /// </summary>
        public Transform CoreSocket => coreSocket;
        
        /// <summary>
        /// Pieza estructural raíz (Hips).
        /// </summary>
        public StructuralPart Hips => hips;
        
        /// <summary>
        /// Tier actual del robot (definido por el core).
        /// </summary>
        public RobotTier CurrentTier => currentTier;
        
        /// <summary>
        /// Si el robot está operacional (tiene core y está activo).
        /// </summary>
        public bool IsOperational => isOperational;
        
        /// <summary>
        /// Inicializa el robot con sus componentes base.
        /// </summary>
        public void Initialize(string id, string name, StructuralPart hipsComponent, RobotTier tier)
        {
            robotId = id ?? System.Guid.NewGuid().ToString();
            robotName = name ?? "Robot";
            hips = hipsComponent;
            currentTier = tier;
            isOperational = false;
            
            // Buscar o crear el socket del core
            FindOrCreateCoreSocket();
        }
        
        private void FindOrCreateCoreSocket()
        {
            // Buscar el socket de core en el torso (si existe)
            StructuralSocket torsoSocket = hips?.GetSocket(StructuralSocketType.Torso);
            
            if (torsoSocket != null && torsoSocket.IsOccupied)
            {
                StructuralPart torso = torsoSocket.AttachedPart;
                StructuralSocket coreSocketComponent = torso?.GetSocket(StructuralSocketType.Core);
                
                if (coreSocketComponent != null)
                {
                    coreSocket = coreSocketComponent.transform;
                    return;
                }
            }
            
            // Si no se encontró, crear uno temporal en el robot
            if (coreSocket == null)
            {
                GameObject coreSocketGO = new GameObject("CoreSocket");
                coreSocket = coreSocketGO.transform;
                coreSocket.SetParent(transform);
                coreSocket.localPosition = Vector3.up; // Posición por defecto arriba del robot
            }
        }
        
        /// <summary>
        /// Llamado cuando se inserta un core en el robot.
        /// </summary>
        public void OnCoreInserted(RobotCore insertedCore)
        {
            core = insertedCore;
            isOperational = true;
            
            Debug.Log($"Core insertado en robot {robotName}. Robot operacional.");
        }
        
        /// <summary>
        /// Llamado cuando se extrae el core del robot.
        /// </summary>
        public void OnCoreExtracted(RobotCore extractedCore)
        {
            if (core == extractedCore)
            {
                core = null;
                isOperational = false;
                
                Debug.Log($"Core extraído de robot {robotName}. Robot inactivo.");
            }
        }
        
        /// <summary>
        /// Obtiene una pieza estructural por tipo.
        /// </summary>
        public StructuralPart GetStructuralPart(StructuralSocketType type)
        {
            if (type == StructuralSocketType.Hips)
            {
                return hips;
            }
            
            // Buscar en los sockets del hips
            StructuralSocket socket = hips?.GetSocket(type);
            if (socket != null && socket.IsOccupied)
            {
                return socket.AttachedPart;
            }
            
            // Buscar recursivamente en las piezas conectadas
            return FindStructuralPartRecursive(hips, type);
        }
        
        private StructuralPart FindStructuralPartRecursive(StructuralPart part, StructuralSocketType type)
        {
            if (part == null) return null;
            
            foreach (var socket in part.ChildSockets)
            {
                if (socket.IsOccupied && socket.AttachedPart != null)
                {
                    if (socket.SocketType == type)
                    {
                        return socket.AttachedPart;
                    }
                    
                    // Buscar en los hijos de esta pieza
                    StructuralPart found = FindStructuralPartRecursive(socket.AttachedPart, type);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Obtiene todas las piezas estructurales del robot.
        /// </summary>
        public List<StructuralPart> GetAllStructuralParts()
        {
            List<StructuralPart> parts = new List<StructuralPart>();
            
            if (hips != null)
            {
                parts.Add(hips);
                parts.AddRange(hips.GetAllAttachedParts());
            }
            
            return parts;
        }
        
        /// <summary>
        /// Obtiene todas las piezas de armadura del robot.
        /// </summary>
        public List<ArmorPart> GetAllArmorParts()
        {
            List<ArmorPart> armorParts = new List<ArmorPart>();
            
            foreach (var structuralPart in GetAllStructuralParts())
            {
                foreach (var grid in structuralPart.ArmorGrids)
                {
                    armorParts.AddRange(grid.PlacedParts);
                }
            }
            
            return armorParts;
        }
        
        /// <summary>
        /// Calcula el peso total del robot.
        /// </summary>
        public float CalculateTotalWeight()
        {
            float weight = 0f;
            
            foreach (var part in GetAllStructuralParts())
            {
                weight += part.PartData.weight;
            }
            
            foreach (var armor in GetAllArmorParts())
            {
                weight += armor.ArmorData.weight;
            }
            
            if (core != null && core.CoreData != null)
            {
                weight += core.CoreData.weight;
            }
            
            return weight;
        }
        
        /// <summary>
        /// Calcula la armadura total del robot.
        /// </summary>
        public float CalculateTotalArmor()
        {
            float armor = 0f;
            
            foreach (var armorPart in GetAllArmorParts())
            {
                armor += armorPart.ArmorData.armor;
            }
            
            return armor;
        }
    }
}
