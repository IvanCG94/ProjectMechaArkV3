using System.Collections.Generic;
using UnityEngine;
using RobotGame.Components;
using RobotGame.Control;
using RobotGame.Data;
using RobotGame.Systems;
using RobotGame.Utils;
using RobotGame.Enums;

namespace RobotGame.Assembly
{
    /// <summary>
    /// Controlador de ensamblaje unificado.
    /// Maneja la lógica de edición de robots independiente del tier.
    /// 
    /// FUNCIONALIDAD:
    /// - Colocar/remover piezas de armadura en grillas
    /// - Colocar/remover piezas estructurales en sockets
    /// - Preview holográfico de piezas
    /// - Detección de grillas y sockets bajo el mouse
    /// 
    /// INTEGRACIÓN:
    /// - Se activa cuando UnifiedAssemblyStation entra en modo edición
    /// - Escucha eventos OnEditModeStarted/OnEditModeEnded
    /// - Funciona igual para Tier 1 (jugador) y Tier 2+ (mechas)
    /// </summary>
    public class RobotAssemblyController : MonoBehaviour
    {
        #region Serialized Fields
        
        [Header("Piezas Disponibles - Armadura")]
        [SerializeField] private List<ArmorPartData> armorParts = new List<ArmorPartData>();
        
        [Header("Piezas Disponibles - Estructurales")]
        [SerializeField] private List<StructuralPartData> structuralParts = new List<StructuralPartData>();
        
        [Header("Controles")]
        [SerializeField] private KeyCode toggleModeKey = KeyCode.Tab;
        [SerializeField] private KeyCode rotateKey = KeyCode.R;
        [SerializeField] private KeyCode debugKey = KeyCode.G;
        
        [Header("Preview")]
        [SerializeField] private Color previewColorValid = new Color(0f, 1f, 0f, 0.5f);
        [SerializeField] private Color previewColorInvalid = new Color(1f, 0f, 0f, 0.5f);
        [SerializeField] private Color previewColorNeutral = new Color(0f, 0.5f, 1f, 0.5f);
        
        [Header("Referencias")]
        [SerializeField] private UnifiedAssemblyStation station;
        
        #endregion
        
        #region Private Fields
        
        // Estado
        private bool isActive = false;
        private Robot targetRobot;
        private AssemblyMode currentMode = AssemblyMode.Armor;
        
        // Cámara
        private Camera mainCamera;
        private PlayerCamera playerCamera;
        
        // Armor
        private List<GridHead> availableGrids = new List<GridHead>();
        private GridHead hoveredGrid = null;
        private int currentArmorIndex = 0;
        private int currentPositionX = 0;
        private int currentPositionY = 0;
        private GridRotation.Rotation currentRotation = GridRotation.Rotation.Deg0;
        
        // Structural
        private List<StructuralSocket> availableSockets = new List<StructuralSocket>();
        private StructuralSocket hoveredSocket = null;
        private int currentStructuralIndex = 0;
        
        // Preview Armor
        private GameObject armorPreviewObject;
        private GameObject pivotContainer;
        private GameObject modelContainer;
        private List<MeshRenderer> previewRenderers = new List<MeshRenderer>();
        private Material previewMaterial;
        private ArmorPartData currentPreviewData;
        
        // Preview Structural
        private GameObject structuralPreviewObject;
        private StructuralPartData currentStructuralPreviewData;
        
        // Snapshot para validación
        private RobotSnapshot editSnapshot;
        private bool useSnapshot;
        
        // Colliders desactivados del robot
        private List<Collider> disabledColliders = new List<Collider>();
        
        #endregion
        
        #region Properties
        
        public bool IsActive => isActive;
        public Robot TargetRobot => targetRobot;
        public AssemblyMode CurrentMode => currentMode;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            // Buscar estación si no está asignada
            if (station == null)
            {
                station = GetComponent<UnifiedAssemblyStation>();
                if (station == null)
                {
                    station = GetComponentInParent<UnifiedAssemblyStation>();
                }
            }
            
            CreatePreviewObjects();
        }
        
        private void Start()
        {
            // Suscribirse a eventos de la estación
            if (station != null)
            {
                station.OnEditModeStarted += OnEditModeStarted;
                station.OnEditModeEnded += OnEditModeEnded;
            }
            
            mainCamera = Camera.main;
            playerCamera = FindObjectOfType<PlayerCamera>();
        }
        
        private void OnDestroy()
        {
            if (station != null)
            {
                station.OnEditModeStarted -= OnEditModeStarted;
                station.OnEditModeEnded -= OnEditModeEnded;
            }
            
            CleanupPreviewObjects();
        }
        
        private void Update()
        {
            if (!isActive) return;
            
            // Toggle modo
            if (Input.GetKeyDown(toggleModeKey))
            {
                ToggleMode();
            }
            
            // Lógica según modo
            if (currentMode == AssemblyMode.Armor)
            {
                UpdateArmorMode();
            }
            else
            {
                UpdateStructuralMode();
            }
        }
        
        private void OnGUI()
        {
            if (!isActive) return;
            
            DrawEditModeUI();
        }
        
        #endregion
        
        #region Station Events
        
        private void OnEditModeStarted(UnifiedAssemblyStation s, AssemblyEditMode mode, Robot robot)
        {
            if (s != station) return;
            
            targetRobot = robot;
            
            // Determinar si usar snapshot
            useSnapshot = (mode == AssemblyEditMode.EditOwnRobot || mode == AssemblyEditMode.EditMecha);
            
            if (useSnapshot)
            {
                editSnapshot = RobotSnapshot.Capture(targetRobot);
            }
            
            ActivateController();
        }
        
        private void OnEditModeEnded(UnifiedAssemblyStation s)
        {
            if (s != station) return;
            
            DeactivateController();
        }
        
        #endregion
        
        #region Activation
        
        private void ActivateController()
        {
            if (targetRobot == null)
            {
                Debug.LogError("RobotAssemblyController: No hay robot objetivo");
                return;
            }
            
            isActive = true;
            
            // Configurar cámara
            if (playerCamera != null)
            {
                playerCamera.EnterEditMode();
                playerCamera.SetTarget(targetRobot.transform, false);
            }
            
            // Desactivar colliders principales del robot
            DisableRobotMainColliders();
            
            // Resetear estado
            currentMode = AssemblyMode.Armor;
            currentRotation = GridRotation.Rotation.Deg0;
            currentPreviewData = null;
            currentStructuralPreviewData = null;
            
            // Recolectar grillas y sockets
            CollectAvailableGrids();
            CollectAvailableSockets();
            
            Debug.Log($"RobotAssemblyController: Activado para '{targetRobot.name}'");
        }
        
        private void DeactivateController()
        {
            isActive = false;
            
            // Restaurar colliders
            RestoreRobotMainColliders();
            
            // Ocultar previews
            if (armorPreviewObject != null) armorPreviewObject.SetActive(false);
            if (structuralPreviewObject != null) structuralPreviewObject.SetActive(false);
            
            // Restaurar cámara
            if (playerCamera != null)
            {
                playerCamera.ExitEditMode();
                
                // Buscar el robot del jugador
                RobotCore playerCore = FindPlayerCore();
                if (playerCore != null && playerCore.CurrentRobot != null)
                {
                    playerCamera.SetTarget(playerCore.CurrentRobot.transform, true);
                }
            }
            
            // Limpiar referencias
            targetRobot = null;
            editSnapshot = null;
            availableGrids.Clear();
            availableSockets.Clear();
            
            Debug.Log("RobotAssemblyController: Desactivado");
        }
        
        private RobotCore FindPlayerCore()
        {
            var cores = FindObjectsOfType<RobotCore>();
            foreach (var core in cores)
            {
                if (core.IsPlayerCore)
                    return core;
            }
            return null;
        }
        
        #endregion
        
        #region Collider Management
        
        private void DisableRobotMainColliders()
        {
            disabledColliders.Clear();
            
            if (targetRobot == null) return;
            
            var allColliders = targetRobot.GetComponentsInChildren<Collider>();
            
            foreach (var col in allColliders)
            {
                // Solo desactivar colliders que NO son triggers
                if (!col.isTrigger && 
                    col.GetComponent<GridHead>() == null && 
                    col.GetComponent<StructuralSocket>() == null)
                {
                    col.enabled = false;
                    disabledColliders.Add(col);
                }
            }
            
            Debug.Log($"RobotAssemblyController: {disabledColliders.Count} colliders desactivados");
        }
        
        private void RestoreRobotMainColliders()
        {
            foreach (var col in disabledColliders)
            {
                if (col != null)
                {
                    col.enabled = true;
                }
            }
            
            Debug.Log($"RobotAssemblyController: {disabledColliders.Count} colliders restaurados");
            disabledColliders.Clear();
        }
        
        #endregion
        
        #region Mode Toggle
        
        private void ToggleMode()
        {
            // Ocultar preview actual
            if (currentMode == AssemblyMode.Armor)
            {
                if (armorPreviewObject != null) armorPreviewObject.SetActive(false);
            }
            else
            {
                if (structuralPreviewObject != null) structuralPreviewObject.SetActive(false);
            }
            
            // Cambiar modo
            currentMode = currentMode == AssemblyMode.Armor ? AssemblyMode.Structural : AssemblyMode.Armor;
            
            Debug.Log($"RobotAssemblyController: Modo cambiado a {currentMode}");
        }
        
        #endregion
        
        #region Armor Mode
        
        private void UpdateArmorMode()
        {
            DetectGridUnderMouse();
            
            // Cambiar pieza con scroll
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0)
            {
                ChangeArmorPiece(scroll > 0 ? 1 : -1);
            }
            
            // Rotar con R
            if (Input.GetKeyDown(rotateKey))
            {
                currentRotation = GridRotation.RotateClockwise(currentRotation);
            }
            
            // Colocar con click izquierdo
            if (Input.GetMouseButtonDown(0) && hoveredGrid != null)
            {
                TryPlaceArmor();
            }
            
            // Remover con click derecho
            if (Input.GetMouseButtonDown(1) && hoveredGrid != null)
            {
                TryRemoveArmor();
            }
            
            UpdateArmorPreview();
        }
        
        private void DetectGridUnderMouse()
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            GridHead newHoveredGrid = null;
            
            bool debugMode = Input.GetKey(debugKey);
            
            if (Physics.Raycast(ray, out hit, 100f, ~0, QueryTriggerInteraction.Collide))
            {
                if (debugMode)
                {
                    Debug.Log($"=== RAYCAST HIT ===");
                    Debug.Log($"  Object: {hit.collider.gameObject.name}");
                    Debug.Log($"  IsTrigger: {hit.collider.isTrigger}");
                }
                
                GridHead grid = hit.collider.GetComponent<GridHead>();
                
                if (debugMode && grid != null)
                {
                    Debug.Log($"  GridHead: {grid.name}");
                    Debug.Log($"  In availableGrids: {availableGrids.Contains(grid)}");
                }
                
                if (grid != null && availableGrids.Contains(grid))
                {
                    newHoveredGrid = grid;
                    CalculateGridPosition(grid, hit.point);
                }
            }
            
            if (newHoveredGrid != hoveredGrid)
            {
                hoveredGrid = newHoveredGrid;
                currentPositionX = 0;
                currentPositionY = 0;
            }
        }
        
        private void CalculateGridPosition(GridHead grid, Vector3 worldPoint)
        {
            Vector3 localPoint = grid.transform.InverseTransformPoint(worldPoint);
            
            int cellX = Mathf.FloorToInt(localPoint.x / 0.1f);
            int cellY = Mathf.FloorToInt(localPoint.y / 0.1f);
            
            cellX = Mathf.Clamp(cellX, 0, grid.GridInfo.sizeX - 1);
            cellY = Mathf.Clamp(cellY, 0, grid.GridInfo.sizeY - 1);
            
            currentPositionX = cellX;
            currentPositionY = cellY;
        }
        
        private void ChangeArmorPiece(int direction)
        {
            if (armorParts.Count == 0) return;
            
            currentArmorIndex += direction;
            
            if (currentArmorIndex < 0)
                currentArmorIndex = armorParts.Count - 1;
            else if (currentArmorIndex >= armorParts.Count)
                currentArmorIndex = 0;
        }
        
        private ArmorPartData GetCurrentArmorData()
        {
            if (armorParts.Count == 0 || currentArmorIndex < 0 || currentArmorIndex >= armorParts.Count)
                return null;
            return armorParts[currentArmorIndex];
        }
        
        private void TryPlaceArmor()
        {
            if (hoveredGrid == null) return;
            
            ArmorPartData armorData = GetCurrentArmorData();
            if (armorData == null) return;
            
            // Las armaduras no tienen restricción de tier
            
            if (!hoveredGrid.CanPlace(armorData, currentPositionX, currentPositionY, currentRotation))
            {
                return;
            }
            
            ArmorPart armorPart = RobotFactory.Instance.CreateArmorPart(armorData);
            if (armorPart == null)
            {
                Debug.LogError("Error al crear la pieza de armadura");
                return;
            }
            
            if (hoveredGrid.TryPlace(armorPart, currentPositionX, currentPositionY, currentRotation))
            {
                Debug.Log($"Armadura '{armorData.displayName}' colocada");
                
                if (armorPart.AdditionalGrids != null && armorPart.AdditionalGrids.Count > 0)
                {
                    CollectAvailableGrids();
                }
            }
            else
            {
                Debug.LogError("Error al colocar la pieza en la grilla");
                Destroy(armorPart.gameObject);
            }
        }
        
        private void TryRemoveArmor()
        {
            if (hoveredGrid == null) return;
            
            ArmorPart part = hoveredGrid.GetPartAtCell(currentPositionX, currentPositionY);
            if (part != null)
            {
                bool hadAdditionalGrids = part.AdditionalGrids != null && part.AdditionalGrids.Count > 0;
                
                if (hoveredGrid.Remove(part))
                {
                    Destroy(part.gameObject);
                    Debug.Log("Armadura removida");
                    
                    if (hadAdditionalGrids)
                    {
                        CollectAvailableGrids();
                    }
                }
            }
        }
        
        #endregion
        
        #region Structural Mode
        
        private void UpdateStructuralMode()
        {
            DetectSocketUnderMouse();
            
            // Cambiar pieza con scroll
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0)
            {
                ChangeStructuralPiece(scroll > 0 ? 1 : -1);
            }
            
            // Colocar con click izquierdo
            if (Input.GetMouseButtonDown(0) && hoveredSocket != null)
            {
                TryPlaceStructural();
            }
            
            // Remover con click derecho
            if (Input.GetMouseButtonDown(1) && hoveredSocket != null)
            {
                TryRemoveStructural();
            }
            
            UpdateStructuralPreview();
        }
        
        private void DetectSocketUnderMouse()
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            StructuralSocket newHoveredSocket = null;
            
            if (Physics.Raycast(ray, out hit, 100f, ~0, QueryTriggerInteraction.Collide))
            {
                StructuralSocket socket = hit.collider.GetComponent<StructuralSocket>();
                if (socket != null && availableSockets.Contains(socket))
                {
                    newHoveredSocket = socket;
                }
            }
            
            hoveredSocket = newHoveredSocket;
        }
        
        private void ChangeStructuralPiece(int direction)
        {
            if (structuralParts.Count == 0) return;
            
            currentStructuralIndex += direction;
            
            if (currentStructuralIndex < 0)
                currentStructuralIndex = structuralParts.Count - 1;
            else if (currentStructuralIndex >= structuralParts.Count)
                currentStructuralIndex = 0;
        }
        
        private StructuralPartData GetCurrentStructuralData()
        {
            if (structuralParts.Count == 0 || currentStructuralIndex < 0 || currentStructuralIndex >= structuralParts.Count)
                return null;
            return structuralParts[currentStructuralIndex];
        }
        
        private void TryPlaceStructural()
        {
            if (hoveredSocket == null || structuralParts.Count == 0) return;
            if (hoveredSocket.IsOccupied) return;
            
            StructuralPartData partData = GetCurrentStructuralData();
            if (partData == null) return;
            
            // Validar tipo de socket
            if (partData.partType != hoveredSocket.SocketType)
            {
                Debug.LogWarning("Tipo de pieza no coincide con socket");
                return;
            }
            
            // Validar tier para piezas estructurales
            if (!partData.IsCompatibleWith(targetRobot.CurrentTier))
            {
                Debug.LogWarning($"Pieza no compatible con robot Tier {targetRobot.CurrentTier}");
                return;
            }
            
            // Crear la pieza
            StructuralPart part = RobotFactory.Instance.CreateStructuralPart(partData, hoveredSocket.transform);
            if (part == null)
            {
                Debug.LogError("Error al crear pieza estructural");
                return;
            }
            
            // Caso especial: si es el HipsSocket, usar AttachHips del Robot
            if (hoveredSocket == targetRobot.HipsSocket)
            {
                if (targetRobot.AttachHips(part))
                {
                    Debug.Log($"Hips '{partData.displayName}' conectadas");
                    Physics.SyncTransforms();
                    CollectAvailableSockets();
                    CollectAvailableGrids();
                }
                else
                {
                    Debug.LogError("Error al conectar las Hips");
                    Destroy(part.gameObject);
                }
            }
            else
            {
                if (hoveredSocket.TryAttach(part))
                {
                    Debug.Log($"Pieza '{partData.displayName}' colocada");
                    Physics.SyncTransforms();
                    CollectAvailableSockets();
                    CollectAvailableGrids();
                }
                else
                {
                    Debug.LogError("Error al conectar la pieza");
                    Destroy(part.gameObject);
                }
            }
        }
        
        private void TryRemoveStructural()
        {
            if (hoveredSocket == null || !hoveredSocket.IsOccupied) return;
            
            StructuralPart part = hoveredSocket.AttachedPart;
            if (part == null) return;
            
            // Verificar que no tenga hijos conectados
            foreach (var childSocket in part.ChildSockets)
            {
                if (childSocket.IsOccupied)
                {
                    Debug.LogWarning("No se puede remover: tiene piezas hijas conectadas");
                    return;
                }
            }
            
            hoveredSocket.Detach();
            Destroy(part.gameObject);
            Debug.Log("Pieza estructural removida");
            
            CollectAvailableSockets();
            CollectAvailableGrids();
        }
        
        #endregion
        
        #region Collection
        
        private void CollectAvailableGrids()
        {
            availableGrids.Clear();
            
            if (targetRobot == null) return;
            
            var parts = targetRobot.GetAllStructuralParts();
            
            foreach (var part in parts)
            {
                foreach (var grid in part.ArmorGrids)
                {
                    availableGrids.Add(grid);
                    grid.EnsureCollider();
                }
                
                // También grillas de armaduras existentes
                CollectArmorPartGrids(part.ArmorGrids);
            }
            
            Debug.Log($"RobotAssemblyController: {availableGrids.Count} grillas disponibles");
        }
        
        private void CollectArmorPartGrids(IReadOnlyList<GridHead> grids)
        {
            foreach (var grid in grids)
            {
                foreach (var armorPart in grid.PlacedParts)
                {
                    if (armorPart.AdditionalGrids != null)
                    {
                        foreach (var additionalGrid in armorPart.AdditionalGrids)
                        {
                            if (!availableGrids.Contains(additionalGrid))
                            {
                                availableGrids.Add(additionalGrid);
                                additionalGrid.EnsureCollider();
                                CollectArmorPartGrids(armorPart.AdditionalGrids);
                            }
                        }
                    }
                }
            }
        }
        
        private void CollectAvailableSockets()
        {
            availableSockets.Clear();
            
            if (targetRobot == null) return;
            
            // HipsSocket siempre disponible
            if (targetRobot.HipsSocket != null)
            {
                availableSockets.Add(targetRobot.HipsSocket);
                targetRobot.HipsSocket.EnsureCollider();
            }
            
            // Sockets de todas las partes estructurales
            var parts = targetRobot.GetAllStructuralParts();
            foreach (var part in parts)
            {
                foreach (var socket in part.ChildSockets)
                {
                    availableSockets.Add(socket);
                    socket.EnsureCollider();
                }
            }
            
            Debug.Log($"RobotAssemblyController: {availableSockets.Count} sockets disponibles");
        }
        
        #endregion
        
        #region Preview Objects
        
        private void CreatePreviewObjects()
        {
            // Preview de armadura
            armorPreviewObject = new GameObject("ArmorPreview");
            armorPreviewObject.transform.SetParent(transform);
            armorPreviewObject.SetActive(false);
            
            pivotContainer = new GameObject("PivotContainer");
            pivotContainer.transform.SetParent(armorPreviewObject.transform);
            
            modelContainer = new GameObject("ModelContainer");
            modelContainer.transform.SetParent(pivotContainer.transform);
            
            // Material de preview
            previewMaterial = new Material(Shader.Find("Standard"));
            previewMaterial.SetFloat("_Mode", 3); // Transparent
            previewMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            previewMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            previewMaterial.SetInt("_ZWrite", 0);
            previewMaterial.DisableKeyword("_ALPHATEST_ON");
            previewMaterial.EnableKeyword("_ALPHABLEND_ON");
            previewMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            previewMaterial.renderQueue = 3000;
            
            // Preview de estructural
            structuralPreviewObject = new GameObject("StructuralPreview");
            structuralPreviewObject.transform.SetParent(transform);
            structuralPreviewObject.SetActive(false);
        }
        
        private void CleanupPreviewObjects()
        {
            if (armorPreviewObject != null) Destroy(armorPreviewObject);
            if (structuralPreviewObject != null) Destroy(structuralPreviewObject);
            if (previewMaterial != null) Destroy(previewMaterial);
        }
        
        private void UpdateArmorPreview()
        {
            ArmorPartData armorData = GetCurrentArmorData();
            
            UpdateArmorPreviewModel();
            
            if (armorData == null || armorData.prefab == null)
            {
                armorPreviewObject.SetActive(false);
                return;
            }
            
            armorPreviewObject.SetActive(true);
            
            // Calcular tamaño rotado
            int originalSizeX = armorData.tailGrid.gridInfo.sizeX;
            int originalSizeY = armorData.tailGrid.gridInfo.sizeY;
            var rotatedSize = GridRotation.RotateSize(originalSizeX, originalSizeY, currentRotation);
            
            float centerX = rotatedSize.x * 0.05f;
            float centerY = rotatedSize.y * 0.05f;
            pivotContainer.transform.localPosition = new Vector3(centerX, centerY, 0f);
            
            float originalCenterX = originalSizeX * 0.05f;
            float originalCenterY = originalSizeY * 0.05f;
            modelContainer.transform.localPosition = new Vector3(-originalCenterX, -originalCenterY, 0f);
            
            pivotContainer.transform.localRotation = GridRotation.ToQuaternion(currentRotation);
            
            // Color según estado
            Color previewColor;
            
            if (hoveredGrid == null)
            {
                previewColor = previewColorNeutral;
                
                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                Vector3 worldPos = ray.origin + ray.direction * 2f;
                armorPreviewObject.transform.position = worldPos;
                armorPreviewObject.transform.rotation = Quaternion.identity;
            }
            else
            {
                bool canPlace = hoveredGrid.CanPlace(armorData, currentPositionX, currentPositionY, currentRotation);
                previewColor = canPlace ? previewColorValid : previewColorInvalid;
                
                Vector3 cellPos = hoveredGrid.CellToWorldPosition(currentPositionX, currentPositionY);
                armorPreviewObject.transform.position = cellPos;
                armorPreviewObject.transform.rotation = hoveredGrid.transform.rotation;
            }
            
            previewMaterial.color = previewColor;
        }
        
        private void UpdateArmorPreviewModel()
        {
            ArmorPartData armorData = GetCurrentArmorData();
            
            if (armorData != currentPreviewData)
            {
                currentPreviewData = armorData;
                
                // Limpiar modelo anterior
                foreach (Transform child in modelContainer.transform)
                {
                    Destroy(child.gameObject);
                }
                previewRenderers.Clear();
                
                if (armorData != null && armorData.prefab != null)
                {
                    GameObject modelInstance = Instantiate(armorData.prefab, modelContainer.transform);
                    modelInstance.transform.localPosition = Vector3.zero;
                    modelInstance.transform.localRotation = Quaternion.identity;
                    
                    // Desactivar colliders y aplicar material
                    foreach (var col in modelInstance.GetComponentsInChildren<Collider>())
                    {
                        col.enabled = false;
                    }
                    
                    foreach (var renderer in modelInstance.GetComponentsInChildren<MeshRenderer>())
                    {
                        Material[] mats = new Material[renderer.materials.Length];
                        for (int i = 0; i < mats.Length; i++)
                        {
                            mats[i] = previewMaterial;
                        }
                        renderer.materials = mats;
                        previewRenderers.Add(renderer);
                    }
                }
            }
        }
        
        private void UpdateStructuralPreview()
        {
            StructuralPartData structuralData = GetCurrentStructuralData();
            
            UpdateStructuralPreviewModel();
            
            if (structuralData == null || structuralData.prefab == null || hoveredSocket == null)
            {
                structuralPreviewObject.SetActive(false);
                return;
            }
            
            structuralPreviewObject.SetActive(true);
            
            // Posicionar en el socket
            structuralPreviewObject.transform.position = hoveredSocket.transform.position;
            structuralPreviewObject.transform.rotation = hoveredSocket.transform.rotation;
            
            // Color según validez
            bool tierCompatible = structuralData.IsCompatibleWith(targetRobot.CurrentTier);
            bool typeMatches = structuralData.partType == hoveredSocket.SocketType;
            bool socketFree = !hoveredSocket.IsOccupied;
            
            Color previewColor = (tierCompatible && typeMatches && socketFree) ? previewColorValid : previewColorInvalid;
            
            foreach (var renderer in structuralPreviewObject.GetComponentsInChildren<MeshRenderer>())
            {
                foreach (var mat in renderer.materials)
                {
                    mat.color = previewColor;
                }
            }
        }
        
        private void UpdateStructuralPreviewModel()
        {
            StructuralPartData structuralData = GetCurrentStructuralData();
            
            if (structuralData != currentStructuralPreviewData)
            {
                currentStructuralPreviewData = structuralData;
                
                // Limpiar modelo anterior
                foreach (Transform child in structuralPreviewObject.transform)
                {
                    Destroy(child.gameObject);
                }
                
                if (structuralData != null && structuralData.prefab != null)
                {
                    GameObject modelInstance = Instantiate(structuralData.prefab, structuralPreviewObject.transform);
                    modelInstance.transform.localPosition = Vector3.zero;
                    modelInstance.transform.localRotation = Quaternion.identity;
                    
                    // Desactivar colliders y aplicar material transparente
                    foreach (var col in modelInstance.GetComponentsInChildren<Collider>())
                    {
                        col.enabled = false;
                    }
                    
                    foreach (var renderer in modelInstance.GetComponentsInChildren<MeshRenderer>())
                    {
                        Material[] mats = new Material[renderer.materials.Length];
                        for (int i = 0; i < mats.Length; i++)
                        {
                            mats[i] = new Material(previewMaterial);
                        }
                        renderer.materials = mats;
                    }
                }
            }
        }
        
        #endregion
        
        #region UI
        
        private void DrawEditModeUI()
        {
            GUILayout.BeginArea(new Rect(10, 60, 250, 200));
            GUILayout.BeginVertical("box");
            
            GUILayout.Label($"=== EDITANDO ROBOT ===");
            GUILayout.Label($"Robot: {targetRobot?.name}");
            GUILayout.Label($"Tier: {targetRobot?.CurrentTier}");
            GUILayout.Label("");
            GUILayout.Label($"Modo: {currentMode}");
            GUILayout.Label($"[Tab] Cambiar modo");
            GUILayout.Label("");
            
            if (currentMode == AssemblyMode.Armor)
            {
                ArmorPartData armor = GetCurrentArmorData();
                GUILayout.Label($"Pieza: {armor?.displayName ?? "Ninguna"}");
                GUILayout.Label($"Tamaño: {armor?.tailGrid.gridInfo.sizeX}x{armor?.tailGrid.gridInfo.sizeY}");
                GUILayout.Label("[Scroll] Cambiar pieza");
                GUILayout.Label("[R] Rotar");
            }
            else
            {
                StructuralPartData structural = GetCurrentStructuralData();
                GUILayout.Label($"Pieza: {structural?.displayName ?? "Ninguna"}");
                GUILayout.Label($"Tipo: {structural?.partType}");
                GUILayout.Label("[Scroll] Cambiar pieza");
            }
            
            GUILayout.Label("");
            GUILayout.Label("[Click Izq] Colocar");
            GUILayout.Label("[Click Der] Remover");
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        
        #endregion
    }
}
