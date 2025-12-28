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
        
        [Header("Estructura")]
        [SerializeField] private Transform hipsAnchor;
        [SerializeField] private StructuralSocket hipsSocket;
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
        /// Socket donde se conectan las Hips.
        /// </summary>
        public StructuralSocket HipsSocket => hipsSocket;
        
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
        /// Inicializa el robot con su estructura base (crea el HipsAnchor y HipsSocket).
        /// </summary>
        public void Initialize(string id, string name, RobotTier tier)
        {
            robotId = id ?? System.Guid.NewGuid().ToString();
            robotName = name ?? "Robot";
            currentTier = tier;
            isOperational = false;
            
            // Crear el HipsAnchor y HipsSocket si no existen
            CreateHipsAnchor();
        }
        
        /// <summary>
        /// Inicializa el robot con sus componentes base (versión legacy con hips ya creadas).
        /// </summary>
        public void Initialize(string id, string name, StructuralPart hipsComponent, RobotTier tier)
        {
            robotId = id ?? System.Guid.NewGuid().ToString();
            robotName = name ?? "Robot";
            currentTier = tier;
            isOperational = false;
            
            // Crear el HipsAnchor y HipsSocket
            CreateHipsAnchor();
            
            // Conectar las hips al socket
            if (hipsComponent != null)
            {
                AttachHips(hipsComponent);
            }
        }
        
        /// <summary>
        /// Crea el HipsAnchor y el HipsSocket.
        /// </summary>
        private void CreateHipsAnchor()
        {
            if (hipsAnchor == null)
            {
                GameObject anchorGO = new GameObject("HipsAnchor");
                hipsAnchor = anchorGO.transform;
                hipsAnchor.SetParent(transform);
                hipsAnchor.localPosition = Vector3.zero;
                hipsAnchor.localRotation = Quaternion.identity;
            }
            
            if (hipsSocket == null)
            {
                hipsSocket = hipsAnchor.gameObject.AddComponent<StructuralSocket>();
                hipsSocket.Initialize(new StructuralSocketDefinition
                {
                    socketType = StructuralSocketType.Hips,
                    isRequired = true
                });
            }
        }
        
        /// <summary>
        /// Conecta unas Hips al robot.
        /// </summary>
        public bool AttachHips(StructuralPart hipsComponent)
        {
            if (hipsComponent == null) return false;
            
            if (hipsSocket == null)
            {
                CreateHipsAnchor();
            }
            
            if (hipsSocket.TryAttach(hipsComponent))
            {
                hips = hipsComponent;
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Alias de AttachHips para claridad.
        /// </summary>
        public bool SetHips(StructuralPart hipsComponent)
        {
            return AttachHips(hipsComponent);
        }
        
        /// <summary>
        /// Desconecta las Hips actuales del robot.
        /// </summary>
        public StructuralPart DetachHips()
        {
            if (hipsSocket == null || !hipsSocket.IsOccupied) return null;
            
            StructuralPart detachedHips = hipsSocket.Detach();
            hips = null;
            
            return detachedHips;
        }
        
        /// <summary>
        /// Busca y retorna el CoreSocket actual (puede cambiar si se modifica la estructura).
        /// </summary>
        public Transform FindCoreSocket()
        {
            // Buscar el socket de core en el torso
            StructuralSocket torsoSocket = hips?.GetSocket(StructuralSocketType.Torso);
            
            if (torsoSocket != null && torsoSocket.IsOccupied)
            {
                StructuralPart torso = torsoSocket.AttachedPart;
                StructuralSocket coreSocketComponent = torso?.GetSocket(StructuralSocketType.Core);
                
                if (coreSocketComponent != null)
                {
                    return coreSocketComponent.transform;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Llamado cuando se inserta un core en el robot.
        /// </summary>
        public void OnCoreInserted(RobotCore insertedCore)
        {
            core = insertedCore;
            isOperational = true;
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
