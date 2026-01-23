using System.Collections.Generic;
using UnityEngine;
using RobotGame.Data;
using RobotGame.Enums;
using RobotGame.Combat;

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
        [SerializeField] private TierInfo currentTier = TierInfo.Tier1_1;
        
        [Header("Estado")]
        [SerializeField] private bool isOperational = false;
        [SerializeField] private bool isDead = false;
        
        [Header("Partes con Salud")]
        [SerializeField] private List<PartHealth> registeredParts = new List<PartHealth>();
        
        [Header("Combat Tracking")]
        [Tooltip("ID del último ataque que golpeó a este robot (para evitar daño múltiple por ataque)")]
        [SerializeField] private int lastReceivedAttackId = -1;
        
        [Tooltip("Si es true, solo la primera parte golpeada por ataque recibe daño")]
        [SerializeField] private bool useAttackIdFiltering = true;
        
        [Header("Colliders")]
        [Tooltip("Si es true, configura automáticamente los colliders al hacer operacional")]
        [SerializeField] private bool autoSetupColliders = true;
        private RobotColliderSetup colliderSetup;
        
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
        public TierInfo CurrentTier => currentTier;
        
        /// <summary>
        /// Si el robot está operacional (tiene core y está activo).
        /// </summary>
        public bool IsOperational => isOperational;
        
        /// <summary>
        /// Si el robot está muerto (parte crítica destruida).
        /// </summary>
        public bool IsDead => isDead;
        
        /// <summary>
        /// Si el robot está vivo (no muerto).
        /// </summary>
        public bool IsAlive => !isDead;
        
        /// <summary>
        /// Lista de partes con salud registradas.
        /// </summary>
        public IReadOnlyList<PartHealth> RegisteredParts => registeredParts;
        
        /// <summary>
        /// Sistema de colliders del robot.
        /// </summary>
        public RobotColliderSetup ColliderSetup => colliderSetup;
        
        #region Events
        
        /// <summary>
        /// Se dispara cuando el robot muere. Params: robot, parte crítica que causó la muerte
        /// </summary>
        public event System.Action<Robot, PartHealth> OnRobotDeath;
        
        /// <summary>
        /// Se dispara cuando una parte del robot es destruida. Params: robot, parte destruida
        /// </summary>
        public event System.Action<Robot, PartHealth> OnPartDestroyed;
        
        /// <summary>
        /// Evento estático para cuando cualquier robot muere.
        /// </summary>
        public static event System.Action<Robot, List<StructuralPart>, List<ArmorPart>> OnRobotDeathWithLoot;
        
        #endregion
        
        /// <summary>
        /// Inicializa el robot con su estructura base (crea el HipsAnchor y HipsSocket).
        /// </summary>
        public void Initialize(string id, string name, TierInfo tier)
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
        public void Initialize(string id, string name, StructuralPart hipsComponent, TierInfo tier)
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
        /// Limpia la referencia a Hips sin desconectar.
        /// Usado cuando las piezas ya fueron destruidas externamente.
        /// </summary>
        public void ClearHips()
        {
            hips = null;
            
            // También limpiar el socket si existe
            if (hipsSocket != null)
            {
                hipsSocket.ForceDetach();
            }
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
            
            // Configurar colliders automáticamente si está habilitado
            if (autoSetupColliders)
            {
                SetupColliders();
            }
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
        /// Configura los layers del robot para física y combate.
        /// Llamar después de ensamblar el robot completamente.
        /// Los colliders deben estar en los prefabs de las partes.
        /// </summary>
        public void SetupColliders()
        {
            if (colliderSetup == null)
            {
                colliderSetup = GetComponent<RobotColliderSetup>();
                if (colliderSetup == null)
                {
                    colliderSetup = gameObject.AddComponent<RobotColliderSetup>();
                }
            }
            
            colliderSetup.Initialize(this);
        }
        
        /// <summary>
        /// Reasigna los layers después de agregar/quitar partes.
        /// </summary>
        public void RefreshColliders()
        {
            if (colliderSetup != null)
            {
                colliderSetup.AssignLayers();
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
        
        #region Part Health Management
        
        /// <summary>
        /// Verifica si este robot puede recibir daño del ataque especificado.
        /// Si el attackId es el mismo que el último recibido, retorna false (ya fue golpeado).
        /// Si es diferente o es ataque de área (attackId = -1), retorna true.
        /// </summary>
        /// <param name="attackId">ID único del ataque. -1 para ataques de área que siempre pasan.</param>
        /// <returns>True si puede recibir daño, false si ya fue golpeado por este ataque</returns>
        public bool CanReceiveDamageFromAttack(int attackId)
        {
            Debug.Log($"[Robot DEBUG] {robotName}.CanReceiveDamageFromAttack({attackId}) - lastReceived: {lastReceivedAttackId}, useFiltering: {useAttackIdFiltering}");
            
            // Ataques de área (attackId = -1) siempre pasan
            if (attackId < 0)
            {
                Debug.Log($"[Robot DEBUG] AttackId < 0, permitiendo (área)");
                return true;
            }
            
            // Si no usamos filtrado, siempre permitir
            if (!useAttackIdFiltering)
            {
                Debug.Log($"[Robot DEBUG] Filtrado desactivado, permitiendo");
                return true;
            }
            
            // Si es el mismo ataque, rechazar
            if (attackId == lastReceivedAttackId)
            {
                Debug.Log($"[Robot DEBUG] AttackId {attackId} == lastReceived {lastReceivedAttackId}, RECHAZANDO");
                return false;
            }
            
            Debug.Log($"[Robot DEBUG] AttackId {attackId} != lastReceived {lastReceivedAttackId}, permitiendo");
            return true;
        }
        
        /// <summary>
        /// Registra que este robot fue golpeado por un ataque.
        /// Llamar después de aplicar daño exitosamente.
        /// </summary>
        public void RegisterAttackHit(int attackId)
        {
            if (attackId >= 0)
            {
                lastReceivedAttackId = attackId;
                Debug.Log($"[Robot] {robotName} registró golpe del ataque #{attackId}");
            }
        }
        
        /// <summary>
        /// Resetea el tracking de ataques. 
        /// Útil si quieres que el mismo ataque pueda golpear de nuevo.
        /// </summary>
        public void ResetAttackTracking()
        {
            lastReceivedAttackId = -1;
        }
        
        /// <summary>
        /// Registra una parte con salud en este robot.
        /// Llamado automáticamente por PartHealth en Start.
        /// </summary>
        public void RegisterPartHealth(PartHealth part)
        {
            if (part != null && !registeredParts.Contains(part))
            {
                registeredParts.Add(part);
                part.OnPartDestroyed += HandlePartDestroyed;
            }
        }
        
        /// <summary>
        /// Desregistra una parte con salud de este robot.
        /// </summary>
        public void UnregisterPartHealth(PartHealth part)
        {
            if (part != null && registeredParts.Contains(part))
            {
                part.OnPartDestroyed -= HandlePartDestroyed;
                registeredParts.Remove(part);
            }
        }
        
        /// <summary>
        /// Maneja cuando una parte es destruida.
        /// </summary>
        private void HandlePartDestroyed(PartHealth part)
        {
            OnPartDestroyed?.Invoke(this, part);
            
            // Remover de la lista
            registeredParts.Remove(part);
        }
        
        /// <summary>
        /// Llamado cuando una parte crítica es destruida.
        /// </summary>
        public void OnCriticalPartDestroyed(PartHealth criticalPart)
        {
            if (isDead) return; // Ya está muerto
            
            isDead = true;
            isOperational = false;
            
            Debug.Log($"[Robot] {robotName} ha MUERTO! Parte crítica destruida: {criticalPart.gameObject.name}");
            
            // Recolectar partes sobrevivientes para loot
            List<StructuralPart> survivingStructural = new List<StructuralPart>();
            List<ArmorPart> survivingArmor = new List<ArmorPart>();
            
            CollectSurvivingParts(survivingStructural, survivingArmor);
            
            Debug.Log($"[Robot] Loot disponible: {survivingStructural.Count} partes estructurales, {survivingArmor.Count} piezas de armadura");
            
            // Disparar eventos
            OnRobotDeath?.Invoke(this, criticalPart);
            OnRobotDeathWithLoot?.Invoke(this, survivingStructural, survivingArmor);
            
            // Destruir el robot después de un pequeño delay
            Invoke(nameof(DestroyRobot), 0.5f);
        }
        
        /// <summary>
        /// Recolecta las partes que sobrevivieron para el loot.
        /// </summary>
        private void CollectSurvivingParts(List<StructuralPart> structuralParts, List<ArmorPart> armorParts)
        {
            // Buscar partes estructurales que aún existen y no están destruidas
            foreach (var partHealth in registeredParts)
            {
                if (partHealth != null && partHealth.IsAlive && partHealth.StructuralPart != null)
                {
                    structuralParts.Add(partHealth.StructuralPart);
                }
            }
            
            // Buscar piezas de armadura
            foreach (var structPart in structuralParts)
            {
                foreach (var grid in structPart.ArmorGrids)
                {
                    foreach (var armorPart in grid.PlacedParts)
                    {
                        if (armorPart != null)
                        {
                            // Verificar si la armadura tiene salud y está viva
                            PartHealth armorHealth = armorPart.GetComponent<PartHealth>();
                            if (armorHealth == null || armorHealth.IsAlive)
                            {
                                armorParts.Add(armorPart);
                            }
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Destruye el robot y todos sus hijos.
        /// </summary>
        private void DestroyRobot()
        {
            // TODO: Podrías spawnear items de loot aquí antes de destruir
            // Por ahora solo destruimos
            Destroy(gameObject);
        }
        
        /// <summary>
        /// Mata al robot instantáneamente (para testing o efectos especiales).
        /// </summary>
        public void Kill()
        {
            if (isDead) return;
            
            // Buscar cualquier parte crítica para simular su destrucción
            foreach (var part in registeredParts)
            {
                if (part.IsCriticalPart)
                {
                    OnCriticalPartDestroyed(part);
                    return;
                }
            }
            
            // Si no hay partes críticas registradas, matar directamente
            isDead = true;
            isOperational = false;
            OnRobotDeath?.Invoke(this, null);
            Invoke(nameof(DestroyRobot), 0.5f);
        }
        
        #endregion
    }
}
