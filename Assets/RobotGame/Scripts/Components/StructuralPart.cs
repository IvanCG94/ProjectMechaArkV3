using System.Collections.Generic;
using UnityEngine;
using RobotGame.Data;
using RobotGame.Enums;
using RobotGame.Utils;

namespace RobotGame.Components
{
    /// <summary>
    /// Componente runtime que representa una pieza estructural instanciada en el robot.
    /// 
    /// Detecta automáticamente:
    /// - Head_T{tier}-{subtier}_{nombre} → StudGridHead para armadura
    /// - Box_{nombre} → BoxCollider para validación de colisión
    /// </summary>
    public class StructuralPart : MonoBehaviour
    {
        [Header("Datos")]
        [SerializeField] private StructuralPartData partData;
        [SerializeField] private string instanceId;
        
        [Header("Conexiones")]
        [SerializeField] private StructuralSocket parentSocket;
        [SerializeField] private List<StructuralSocket> childSockets = new List<StructuralSocket>();
        [SerializeField] private List<StudGridHead> armorGrids = new List<StudGridHead>();
        
        [Header("Componentes")]
        [SerializeField] private Animator animator;
        
        /// <summary>
        /// Datos de la pieza (ScriptableObject).
        /// </summary>
        public StructuralPartData PartData => partData;
        
        /// <summary>
        /// ID único de esta instancia.
        /// </summary>
        public string InstanceId => instanceId;
        
        /// <summary>
        /// Socket al que está conectada esta pieza (null si es raíz).
        /// </summary>
        public StructuralSocket ParentSocket => parentSocket;
        
        /// <summary>
        /// Sockets disponibles para conectar otras piezas estructurales.
        /// </summary>
        public IReadOnlyList<StructuralSocket> ChildSockets => childSockets;
        
        /// <summary>
        /// Grillas disponibles para colocar piezas de armadura.
        /// </summary>
        public IReadOnlyList<StudGridHead> ArmorGrids => armorGrids;
        
        /// <summary>
        /// Animator de esta pieza (si tiene).
        /// </summary>
        public Animator Animator => animator;
        
        /// <summary>
        /// Si esta parte tiene un socket para el Core.
        /// Las partes con CoreSocket son críticas - si se destruyen, el robot muere.
        /// </summary>
        public bool HasCoreSocket
        {
            get
            {
                foreach (var socket in childSockets)
                {
                    if (socket.SocketType == StructuralSocketType.Core)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
        
        /// <summary>
        /// Inicializa la pieza estructural con sus datos y genera sockets/grillas.
        /// </summary>
        public void Initialize(StructuralPartData data, string id = null)
        {
            partData = data;
            instanceId = id ?? System.Guid.NewGuid().ToString();
            
            // Obtener o agregar Animator
            animator = GetComponent<Animator>();
            if (animator == null && data.animatorController != null)
            {
                animator = gameObject.AddComponent<Animator>();
            }
            
            if (animator != null && data.animatorController != null)
            {
                animator.runtimeAnimatorController = data.animatorController;
                if (data.animatorAvatar != null)
                {
                    animator.avatar = data.animatorAvatar;
                }
            }
            
            // Crear sockets estructurales
            CreateStructuralSockets();
            
            // Auto-detectar y crear grillas de armadura desde los Empties Head_T*
            DetectArmorGrids();
        }
        
        private void CreateStructuralSockets()
        {
            childSockets.Clear();
            
            foreach (var socketDef in partData.structuralSockets)
            {
                // Buscar el transform hijo por nombre
                Transform socketTransform = FindChildByName(transform, socketDef.transformName);
                
                if (socketTransform == null)
                {
                    // Si no existe, crear un nuevo GameObject
                    GameObject socketGO = new GameObject($"Socket_{socketDef.socketType}");
                    socketTransform = socketGO.transform;
                    socketTransform.SetParent(transform);
                    socketTransform.localPosition = socketDef.localPosition;
                    socketTransform.localRotation = Quaternion.Euler(socketDef.localRotation);
                }
                
                // Agregar el componente StructuralSocket
                StructuralSocket socket = socketTransform.GetComponent<StructuralSocket>();
                if (socket == null)
                {
                    socket = socketTransform.gameObject.AddComponent<StructuralSocket>();
                }
                
                socket.Initialize(socketDef);
                childSockets.Add(socket);
            }
        }
        
        /// <summary>
        /// Detecta automáticamente los studs Head desde los Empties del prefab.
        /// </summary>
        private void DetectArmorGrids()
        {
            armorGrids.Clear();
            
            // Detectar todos los studs Head en este objeto y sus hijos (recursivo)
            var allHeadStuds = StudDetector.DetectHeadStuds(transform);
            
            if (allHeadStuds.Count == 0)
            {
                // Debug.Log($"StructuralPart: No se encontraron studs Head_ en {partData?.displayName ?? gameObject.name}");
                return;
            }
            
            // Debug.Log($"StructuralPart: Detectados {allHeadStuds.Count} studs Head en {partData?.displayName ?? gameObject.name}");
            
            // Agrupar studs por TierInfo
            var studsByTier = new Dictionary<TierInfo, List<StudPoint>>();
            
            foreach (var stud in allHeadStuds)
            {
                if (!studsByTier.ContainsKey(stud.tierInfo))
                {
                    studsByTier[stud.tierInfo] = new List<StudPoint>();
                }
                studsByTier[stud.tierInfo].Add(stud);
            }
            
            // Crear un StudGridHead por cada tier EN ESTE MISMO GAMEOBJECT (no como hijo)
            foreach (var kvp in studsByTier)
            {
                TierInfo tier = kvp.Key;
                List<StudPoint> studsForTier = kvp.Value;
                
                // Agregar componente StudGridHead al mismo GameObject
                StudGridHead grid = gameObject.AddComponent<StudGridHead>();
                grid.SetStuds(studsForTier);
                
                armorGrids.Add(grid);
                
                // Debug.Log($"StructuralPart: Agregado StudGridHead T{tier} con {studsForTier.Count} studs en {partData?.displayName ?? gameObject.name}");
            }
        }
        
        /// <summary>
        /// Busca un socket hijo por tipo.
        /// </summary>
        public StructuralSocket GetSocket(StructuralSocketType type)
        {
            return childSockets.Find(s => s.SocketType == type);
        }
        
        /// <summary>
        /// Busca una grilla de armadura por TierInfo.
        /// </summary>
        public StudGridHead GetArmorGrid(TierInfo tierInfo)
        {
            // Buscar en los grids detectados
            // Como DetectStuds se llama en el grid, necesitamos comparar los studs
            foreach (var grid in armorGrids)
            {
                if (grid.StudCount > 0)
                {
                    var firstStud = grid.Studs[0];
                    if (firstStud.tierInfo == tierInfo)
                    {
                        return grid;
                    }
                }
            }
            return null;
        }
        
        /// <summary>
        /// Busca una grilla de armadura por nombre (compatibilidad legacy).
        /// </summary>
        public StudGridHead GetArmorGrid(string gridName)
        {
            // Buscar por nombre del transform
            foreach (var grid in armorGrids)
            {
                if (grid.name == gridName || grid.name.Contains(gridName))
                {
                    return grid;
                }
            }
            
            // Si no se encuentra por nombre, intentar parsear como TierInfo
            if (TierInfo.TryParse(gridName, out TierInfo parsedTier))
            {
                return GetArmorGrid(parsedTier);
            }
            
            // Retornar la primera grilla si solo hay una
            if (armorGrids.Count == 1)
            {
                return armorGrids[0];
            }
            
            return null;
        }
        
        /// <summary>
        /// Obtiene la primera grilla de armadura disponible.
        /// </summary>
        public StudGridHead GetFirstArmorGrid()
        {
            return armorGrids.Count > 0 ? armorGrids[0] : null;
        }
        
        /// <summary>
        /// Llamado cuando esta pieza se conecta a un socket.
        /// </summary>
        public void OnAttachedToSocket(StructuralSocket socket)
        {
            parentSocket = socket;
        }
        
        /// <summary>
        /// Llamado cuando esta pieza se desconecta de un socket.
        /// </summary>
        public void OnDetachedFromSocket(StructuralSocket socket)
        {
            if (parentSocket == socket)
            {
                parentSocket = null;
            }
        }
        
        /// <summary>
        /// Busca recursivamente un transform hijo por nombre.
        /// </summary>
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
        
        /// <summary>
        /// Obtiene todas las piezas estructurales conectadas recursivamente.
        /// </summary>
        public List<StructuralPart> GetAllAttachedParts()
        {
            List<StructuralPart> parts = new List<StructuralPart>();
            
            foreach (var socket in childSockets)
            {
                if (socket.IsOccupied && socket.AttachedPart != null)
                {
                    parts.Add(socket.AttachedPart);
                    parts.AddRange(socket.AttachedPart.GetAllAttachedParts());
                }
            }
            
            return parts;
        }
        
        #region Weight Calculation
        
        /// <summary>
        /// Calcula el peso total de esta parte + todas sus partes hijas + armaduras.
        /// Usado para el sistema de movilidad jerárquico.
        /// </summary>
        public float CalculateBranchWeight()
        {
            float weight = 0f;
            
            // Peso de esta parte
            if (partData != null)
            {
                weight += partData.weight;
            }
            
            // Peso de las armaduras en esta parte
            foreach (var grid in armorGrids)
            {
                foreach (var armor in grid.PlacedParts)
                {
                    if (armor != null && armor.ArmorData != null)
                    {
                        weight += armor.ArmorData.weight;
                    }
                }
            }
            
            // Peso de partes hijas (recursivo)
            foreach (var socket in childSockets)
            {
                if (socket.IsOccupied && socket.AttachedPart != null)
                {
                    weight += socket.AttachedPart.CalculateBranchWeight();
                }
            }
            
            return weight;
        }
        
        /// <summary>
        /// Calcula el multiplicador de velocidad para esta parte basado en peso/fuerza.
        /// Solo aplicable si isLocomotionPart = true.
        /// </summary>
        public float GetBranchSpeedMultiplier()
        {
            if (partData == null || !partData.isLocomotionPart)
            {
                return 1f;
            }
            
            float branchWeight = CalculateBranchWeight();
            float strength = partData.strength;
            
            // Sin fuerza no puede mover nada
            if (strength <= 0f)
            {
                return branchWeight > 0f ? 0f : 1f;
            }
            
            float ratio = branchWeight / strength;
            
            // Constantes (mismas que Robot.cs)
            const float OPTIMAL_RATIO = 1.0f;
            const float MAX_RATIO = 2.0f;
            const float MAX_SPEED = 1.3f;
            
            // Si el ratio es menor o igual a óptimo, puede ir más rápido
            if (ratio <= OPTIMAL_RATIO)
            {
                float t = 1f - (ratio / OPTIMAL_RATIO);
                return Mathf.Lerp(1f, MAX_SPEED, t);
            }
            
            // Si el ratio excede el máximo, no puede moverse
            if (ratio >= MAX_RATIO)
            {
                return 0f;
            }
            
            // Interpolar linealmente entre 1.0 y 0
            float overloadFactor = (ratio - OPTIMAL_RATIO) / (MAX_RATIO - OPTIMAL_RATIO);
            return Mathf.Lerp(1f, 0f, overloadFactor);
        }
        
        #endregion
    }
}
