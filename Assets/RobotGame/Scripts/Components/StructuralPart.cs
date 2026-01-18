using System.Collections.Generic;
using UnityEngine;
using RobotGame.Data;
using RobotGame.Enums;

namespace RobotGame.Components
{
    /// <summary>
    /// Componente runtime que representa una pieza estructural instanciada en el robot.
    /// </summary>
    public class StructuralPart : MonoBehaviour
    {
        [Header("Datos")]
        [SerializeField] private StructuralPartData partData;
        [SerializeField] private string instanceId;
        
        [Header("Conexiones")]
        [SerializeField] private StructuralSocket parentSocket;
        [SerializeField] private List<StructuralSocket> childSockets = new List<StructuralSocket>();
        [SerializeField] private List<GridHead> armorGrids = new List<GridHead>();
        
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
        public IReadOnlyList<GridHead> ArmorGrids => armorGrids;
        
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
            
            // Crear grillas de armadura
            CreateArmorGrids();
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
        
        private void CreateArmorGrids()
        {
            armorGrids.Clear();
            
            Debug.Log($"StructuralPart.CreateArmorGrids: Creando {partData.armorGrids.Count} grillas para {partData.displayName}");
            
            foreach (var gridDef in partData.armorGrids)
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
                    Debug.Log($"  - Creado nuevo GameObject para grid: {gridDef.gridInfo.gridName}");
                }
                else
                {
                    Debug.Log($"  - Encontrado transform existente para grid: {gridDef.transformName}");
                }
                
                // Agregar el componente GridHead
                GridHead grid = gridTransform.GetComponent<GridHead>();
                if (grid == null)
                {
                    grid = gridTransform.gameObject.AddComponent<GridHead>();
                    Debug.Log($"  - Agregado componente GridHead a: {gridTransform.name}");
                }
                else
                {
                    Debug.Log($"  - GridHead ya existía en: {gridTransform.name}");
                }
                
                grid.Initialize(gridDef.gridInfo);
                armorGrids.Add(grid);
                
                // Verificar que el collider se creó
                BoxCollider col = grid.GetComponent<BoxCollider>();
                Debug.Log($"  - Grid {grid.name}: Collider={col != null}, IsTrigger={col?.isTrigger}, Size={col?.size}");
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
        /// Busca una grilla de armadura por nombre.
        /// </summary>
        public GridHead GetArmorGrid(string gridName)
        {
            return armorGrids.Find(g => g.GridInfo.gridName == gridName);
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
    }
}
