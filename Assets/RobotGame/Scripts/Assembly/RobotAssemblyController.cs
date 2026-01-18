using System.Collections.Generic;
using UnityEngine;
using RobotGame.Components;
using RobotGame.Control;
using RobotGame.Data;
using RobotGame.Systems;
using RobotGame.Utils;
using RobotGame.Enums;
using RobotGame.Inventory;
using RobotGame.UI;

namespace RobotGame.Assembly
{
    /// <summary>
    /// Controlador de ensamblaje unificado con integración de inventario.
    /// 
    /// FUNCIONALIDAD:
    /// - Colocar/remover piezas de armadura en grillas
    /// - Colocar/remover piezas estructurales en sockets
    /// - Preview holográfico de piezas
    /// - Integración con sistema de inventario y UI
    /// 
    /// FLUJO:
    /// 1. Estación activa el modo edición
    /// 2. Se abre el panel de inventario
    /// 3. Usuario selecciona item del inventario o usa scroll
    /// 4. Click para colocar (resta del inventario)
    /// 5. Click derecho para remover (suma al inventario)
    /// </summary>
    public class RobotAssemblyController : MonoBehaviour
    {
        #region Serialized Fields
        
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
        [SerializeField] private InventoryPanelUI inventoryPanel;
        
        [Header("Fallback - Piezas sin inventario (para testing)")]
        [SerializeField] private List<ArmorPartData> fallbackArmorParts = new List<ArmorPartData>();
        [SerializeField] private List<StructuralPartData> fallbackStructuralParts = new List<StructuralPartData>();
        
        #endregion
        
        #region Private Fields
        
        // Estado
        private bool isActive = false;
        private Robot targetRobot;
        private AssemblyMode currentMode = AssemblyMode.Armor;
        private AssemblyEditMode currentEditMode = AssemblyEditMode.None;
        private bool useInventory = true;
        
        // Cámara y control
        private Camera mainCamera;
        private PlayerCamera playerCamera;
        private PlayerController playerController;
        private bool playerControllerWasInEditMode = false;
        private Rigidbody manuallySetKinematicRb = null;
        private bool wasKinematicBeforeEdit = false;
        
        // Pieza seleccionada actual
        private ArmorPartData selectedArmorData;
        private StructuralPartData selectedStructuralData;
        
        // Armor
        private List<GridHead> availableGrids = new List<GridHead>();
        private GridHead hoveredGrid = null;
        private int currentPositionX = 0;
        private int currentPositionY = 0;
        private GridRotation.Rotation currentRotation = GridRotation.Rotation.Deg0;
        
        // Structural
        private List<StructuralSocket> availableSockets = new List<StructuralSocket>();
        private StructuralSocket hoveredSocket = null;
        
        // Índices para fallback (sin inventario)
        private int fallbackArmorIndex = 0;
        private int fallbackStructuralIndex = 0;
        
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
        public ArmorPartData SelectedArmor => selectedArmorData;
        public StructuralPartData SelectedStructural => selectedStructuralData;
        
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
            
            // Buscar panel de inventario si no está asignado
            if (inventoryPanel == null)
            {
                inventoryPanel = FindObjectOfType<InventoryPanelUI>();
            }
            
            // Suscribirse a eventos del inventario
            if (inventoryPanel != null)
            {
                inventoryPanel.OnItemSelected += OnInventoryItemSelected;
                inventoryPanel.OnCategoryChanged += OnInventoryCategoryChanged;
            }
            
            // Verificar si hay inventario disponible
            useInventory = PlayerInventory.Instance != null && inventoryPanel != null;
            
            mainCamera = Camera.main;
            playerCamera = FindObjectOfType<PlayerCamera>();
            playerController = FindObjectOfType<PlayerController>();
        }
        
        private void OnDestroy()
        {
            if (station != null)
            {
                station.OnEditModeStarted -= OnEditModeStarted;
                station.OnEditModeEnded -= OnEditModeEnded;
            }
            
            if (inventoryPanel != null)
            {
                inventoryPanel.OnItemSelected -= OnInventoryItemSelected;
                inventoryPanel.OnCategoryChanged -= OnInventoryCategoryChanged;
            }
            
            CleanupPreviewObjects();
        }
        
        private void Update()
        {
            if (!isActive) return;
            
            // Toggle modo con Tab
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
            
            // Solo mostrar UI de debug si no hay panel de inventario
            if (inventoryPanel == null || !inventoryPanel.IsOpen)
            {
                DrawEditModeUI();
            }
        }
        
        #endregion
        
        #region Station Events
        
        private void OnEditModeStarted(UnifiedAssemblyStation s, AssemblyEditMode mode, Robot robot)
        {
            if (s != station) return;
            
            targetRobot = robot;
            currentEditMode = mode;
            
            // Determinar si usar snapshot
            useSnapshot = (mode == AssemblyEditMode.EditOwnRobot || mode == AssemblyEditMode.EditMecha);
            
            if (useSnapshot)
            {
                editSnapshot = RobotSnapshot.Capture(targetRobot);
                Debug.Log($"RobotAssemblyController: Snapshot capturado para '{targetRobot.name}'");
            }
            else
            {
                editSnapshot = null;
                Debug.Log($"RobotAssemblyController: Sin snapshot (modo cascarón)");
            }
            
            ActivateController();
        }
        
        private void OnEditModeEnded(UnifiedAssemblyStation s)
        {
            if (s != station) return;
            
            // Validar antes de salir si hay snapshot
            if (useSnapshot && editSnapshot != null)
            {
                if (!ValidateAndExit())
                {
                    // No se puede salir, la configuración es inválida
                    return;
                }
            }
            
            DeactivateController();
            
            // Notificar a la estación que complete la salida
            station.CompleteExitEditMode();
        }
        
        /// <summary>
        /// Valida la configuración actual y restaura si es inválida.
        /// </summary>
        /// <returns>True si se puede salir, False si se restauró snapshot.</returns>
        private bool ValidateAndExit()
        {
            if (editSnapshot == null)
            {
                return true; // Sin snapshot, siempre se puede salir
            }
            
            if (targetRobot == null)
            {
                Debug.LogWarning("RobotAssemblyController: No hay robot. Restaurando desde snapshot...");
                RestoreFromSnapshot();
                return true;
            }
            
            // Capturar estado actual
            RobotSnapshot currentState = RobotSnapshot.Capture(targetRobot);
            
            // Validar que la configuración sea válida
            List<string> errors;
            if (!currentState.IsValid(out errors))
            {
                Debug.LogWarning($"RobotAssemblyController: Configuración inválida ({string.Join(", ", errors)}). Restaurando desde snapshot...");
                RestoreFromSnapshot();
                return true;
            }
            
            // Configuración válida, limpiar snapshot
            Debug.Log("RobotAssemblyController: Configuración válida. Guardando cambios.");
            editSnapshot = null;
            return true;
        }
        
        /// <summary>
        /// Restaura el robot desde el snapshot guardado.
        /// También restaura el inventario (devuelve piezas colocadas, quita piezas removidas).
        /// </summary>
        private void RestoreFromSnapshot()
        {
            if (editSnapshot == null)
            {
                Debug.LogWarning("RobotAssemblyController: No hay snapshot para restaurar.");
                return;
            }
            
            // Capturar estado actual antes de restaurar (para calcular diferencias de inventario)
            RobotSnapshot currentState = null;
            if (targetRobot != null)
            {
                currentState = RobotSnapshot.Capture(targetRobot);
            }
            
            RobotCore playerCore = FindPlayerCore();
            
            // Determinar si es un mecha (tiene WildRobot)
            bool isMecha = targetRobot != null && targetRobot.GetComponent<RobotGame.AI.WildRobot>() != null;
            
            if (isMecha)
            {
                // Para mechas: restaurar in-place para preservar WildRobot y otros componentes
                Debug.Log("RobotAssemblyController: Restaurando mecha in-place...");
                
                if (editSnapshot.RestoreInPlace(targetRobot, playerCore))
                {
                    Debug.Log("RobotAssemblyController: Mecha restaurado in-place exitosamente.");
                    
                    // Restaurar inventario si está activo
                    if (useInventory && currentState != null)
                    {
                        RestoreInventoryFromSnapshot(editSnapshot, currentState);
                    }
                }
                else
                {
                    Debug.LogError("RobotAssemblyController: Error al restaurar mecha in-place.");
                }
            }
            else
            {
                // Para robots normales: destruir y recrear
                Robot restoredRobot = editSnapshot.Restore(targetRobot, playerCore);
                
                if (restoredRobot != null)
                {
                    targetRobot = restoredRobot;
                    Debug.Log("RobotAssemblyController: Configuración restaurada desde snapshot.");
                    
                    // Restaurar inventario si está activo
                    if (useInventory && currentState != null)
                    {
                        RestoreInventoryFromSnapshot(editSnapshot, currentState);
                    }
                }
                else
                {
                    Debug.LogError("RobotAssemblyController: Error al restaurar desde snapshot.");
                }
            }
            
            editSnapshot = null;
        }
        
        /// <summary>
        /// Restaura el inventario calculando la diferencia entre snapshot y estado actual.
        /// </summary>
        private void RestoreInventoryFromSnapshot(RobotSnapshot originalSnapshot, RobotSnapshot currentSnapshot)
        {
            if (!useInventory || PlayerInventory.Instance == null)
            {
                Debug.Log("RobotAssemblyController: No hay inventario activo, no se restauran piezas");
                return;
            }
            
            if (currentSnapshot == null)
            {
                Debug.LogWarning("RobotAssemblyController: No hay snapshot actual para comparar");
                return;
            }
            
            // Capturar piezas del estado ACTUAL (antes de restaurar)
            List<ArmorPartData> currentArmors = new List<ArmorPartData>();
            List<StructuralPartData> currentStructurals = new List<StructuralPartData>();
            CaptureSnapshotPieces(currentSnapshot, currentArmors, currentStructurals);
            
            // Capturar piezas del estado ORIGINAL (snapshot guardado)
            List<ArmorPartData> originalArmors = new List<ArmorPartData>();
            List<StructuralPartData> originalStructurals = new List<StructuralPartData>();
            CaptureSnapshotPieces(originalSnapshot, originalArmors, originalStructurals);
            
            int armorCount = 0;
            int structuralCount = 0;
            
            // Devolver armaduras que están en CURRENT pero NO en ORIGINAL
            // (son las que el jugador agregó durante la edición)
            foreach (var armorData in currentArmors)
            {
                if (armorData != null && !originalArmors.Contains(armorData))
                {
                    PlayerInventory.Instance.AddItem(armorData, 1);
                    armorCount++;
                    // Remover de la lista original para manejar duplicados correctamente
                }
                else if (armorData != null)
                {
                    // Remover una instancia de originalArmors para manejar duplicados
                    originalArmors.Remove(armorData);
                }
            }
            
            // Devolver estructurales que están en CURRENT pero NO en ORIGINAL
            foreach (var structuralData in currentStructurals)
            {
                if (structuralData != null && !originalStructurals.Contains(structuralData))
                {
                    PlayerInventory.Instance.AddItem(structuralData, 1);
                    structuralCount++;
                }
                else if (structuralData != null)
                {
                    originalStructurals.Remove(structuralData);
                }
            }
            
            Debug.Log($"RobotAssemblyController: Inventario restaurado - {armorCount} armaduras, {structuralCount} estructurales devueltas al inventario");
        }
        
        /// <summary>
        /// Extrae todas las piezas de un snapshot a listas.
        /// </summary>
        private void CaptureSnapshotPieces(RobotSnapshot snapshot, List<ArmorPartData> armors, List<StructuralPartData> structurals)
        {
            if (snapshot == null) return;
            
            // Armaduras de las Hips
            foreach (var armor in snapshot.hipsArmors)
            {
                if (armor.armorData != null)
                {
                    armors.Add(armor.armorData);
                }
            }
            
            // Piezas estructurales y sus armaduras recursivamente
            CaptureSnapshotPartsRecursive(snapshot.attachedParts, armors, structurals);
        }
        
        private void CaptureSnapshotPartsRecursive(List<StructuralSnapshot> parts, List<ArmorPartData> armors, List<StructuralPartData> structurals)
        {
            foreach (var part in parts)
            {
                // La pieza estructural
                if (part.partData != null)
                {
                    structurals.Add(part.partData);
                }
                
                // Sus armaduras
                foreach (var armor in part.armors)
                {
                    if (armor.armorData != null)
                    {
                        armors.Add(armor.armorData);
                    }
                }
                
                // Recursivo para hijos
                CaptureSnapshotPartsRecursive(part.children, armors, structurals);
            }
        }
        
        #endregion
        
        #region Inventory Events
        
        private void OnInventoryItemSelected(IInventoryItem item)
        {
            if (item == null) return;
            
            // Determinar tipo de item y seleccionarlo
            if (item is ArmorPartData armorData)
            {
                selectedArmorData = armorData;
                
                // Cambiar a modo armor si no estamos en él
                if (currentMode != AssemblyMode.Armor)
                {
                    SetMode(AssemblyMode.Armor);
                }
                
                Debug.Log($"RobotAssemblyController: Armadura seleccionada - {armorData.displayName}");
            }
            else if (item is StructuralPartData structuralData)
            {
                selectedStructuralData = structuralData;
                
                // Cambiar a modo structural si no estamos en él
                if (currentMode != AssemblyMode.Structural)
                {
                    SetMode(AssemblyMode.Structural);
                }
                
                Debug.Log($"RobotAssemblyController: Estructural seleccionada - {structuralData.displayName}");
            }
        }
        
        private void OnInventoryCategoryChanged(InventoryCategory category)
        {
            // Sincronizar modo con categoría del inventario
            if (category == InventoryCategory.StructuralParts)
            {
                SetMode(AssemblyMode.Structural);
            }
            else if (category == InventoryCategory.ArmorParts)
            {
                SetMode(AssemblyMode.Armor);
            }
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
            
            // Poner PlayerController en modo edición si está controlando este robot
            // Comparamos GameObjects para ser más robustos
            playerControllerWasInEditMode = false;
            if (playerController != null)
            {
                bool isControllingTarget = playerController.Target != null && 
                                           playerController.Target.gameObject == targetRobot.gameObject;
                
                Debug.Log($"RobotAssemblyController: PlayerController.Target={playerController.Target?.name}, " +
                          $"targetRobot={targetRobot.name}, isControllingTarget={isControllingTarget}");
                
                if (isControllingTarget)
                {
                    playerController.EnterEditModeState();
                    playerControllerWasInEditMode = true;
                    Debug.Log("RobotAssemblyController: PlayerController puesto en modo edición");
                }
            }
            
            // SIEMPRE asegurar que el Rigidbody del robot esté en kinematic durante edición
            // Esto evita que caiga si PlayerController no lo está controlando
            manuallySetKinematicRb = null;
            Rigidbody targetRb = targetRobot.GetComponent<Rigidbody>();
            if (targetRb != null && !playerControllerWasInEditMode)
            {
                wasKinematicBeforeEdit = targetRb.isKinematic;
                targetRb.linearVelocity = Vector3.zero;
                targetRb.isKinematic = true;
                manuallySetKinematicRb = targetRb;
                Debug.Log($"RobotAssemblyController: Rigidbody puesto en kinematic manualmente (era kinematic={wasKinematicBeforeEdit})");
            }
            
            // Desactivar colliders principales del robot
            DisableRobotMainColliders();
            
            // Resetear estado
            currentMode = AssemblyMode.Armor;
            currentRotation = GridRotation.Rotation.Deg0;
            currentPreviewData = null;
            currentStructuralPreviewData = null;
            selectedArmorData = null;
            selectedStructuralData = null;
            
            // Recolectar grillas y sockets
            CollectAvailableGrids();
            CollectAvailableSockets();
            
            // Abrir panel de inventario con el tier de la estación
            if (inventoryPanel != null)
            {
                TierInfo stationTier = station != null ? station.StationTier : TierInfo.Tier1_1;
                inventoryPanel.EnterEditMode(currentMode, stationTier);
            }
            
            Debug.Log($"RobotAssemblyController: Activado para '{targetRobot.name}'");
        }
        
        private void DeactivateController()
        {
            isActive = false;
            currentEditMode = AssemblyEditMode.None;
            
            // Restaurar colliders
            RestoreRobotMainColliders();
            
            // Restaurar Rigidbody si lo modificamos manualmente
            if (manuallySetKinematicRb != null)
            {
                manuallySetKinematicRb.isKinematic = wasKinematicBeforeEdit;
                Debug.Log($"RobotAssemblyController: Rigidbody restaurado a kinematic={wasKinematicBeforeEdit}");
                manuallySetKinematicRb = null;
            }
            
            // Sacar PlayerController del modo edición si lo habíamos puesto
            if (playerControllerWasInEditMode && playerController != null)
            {
                playerController.ExitEditModeState();
                playerController.RecalculateCollider();
                Debug.Log("RobotAssemblyController: PlayerController restaurado de modo edición");
            }
            playerControllerWasInEditMode = false;
            
            // Ocultar previews
            if (armorPreviewObject != null) armorPreviewObject.SetActive(false);
            if (structuralPreviewObject != null) structuralPreviewObject.SetActive(false);
            
            // Cerrar panel de inventario
            if (inventoryPanel != null)
            {
                inventoryPanel.ExitEditMode();
            }
            
            // Restaurar cámara
            if (playerCamera != null)
            {
                playerCamera.ExitEditMode();
                
                // Si el PlayerController tiene un target, usar ese para la cámara
                if (playerController != null && playerController.Target != null)
                {
                    playerCamera.SetTarget(playerController.Target, true);
                }
                else
                {
                    // Fallback: buscar el robot del jugador
                    RobotCore playerCore = FindPlayerCore();
                    if (playerCore != null && playerCore.CurrentRobot != null)
                    {
                        playerCamera.SetTarget(playerCore.CurrentRobot.transform, true);
                    }
                }
            }
            
            // Limpiar referencias
            targetRobot = null;
            editSnapshot = null;
            availableGrids.Clear();
            availableSockets.Clear();
            selectedArmorData = null;
            selectedStructuralData = null;
            
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
        
        #region Mode Management
        
        private void ToggleMode()
        {
            AssemblyMode newMode = currentMode == AssemblyMode.Armor 
                ? AssemblyMode.Structural 
                : AssemblyMode.Armor;
            
            SetMode(newMode);
        }
        
        private void SetMode(AssemblyMode mode)
        {
            if (currentMode == mode) return;
            
            // Ocultar preview actual
            if (currentMode == AssemblyMode.Armor)
            {
                if (armorPreviewObject != null) armorPreviewObject.SetActive(false);
            }
            else
            {
                if (structuralPreviewObject != null) structuralPreviewObject.SetActive(false);
            }
            
            currentMode = mode;
            
            // Sincronizar con panel de inventario
            if (inventoryPanel != null && inventoryPanel.IsOpen)
            {
                InventoryCategory targetCategory = mode == AssemblyMode.Structural 
                    ? InventoryCategory.StructuralParts 
                    : InventoryCategory.ArmorParts;
                
                inventoryPanel.SelectCategory(targetCategory);
            }
            
            Debug.Log($"RobotAssemblyController: Modo cambiado a {mode}");
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
            
            disabledColliders.Clear();
        }
        
        #endregion
        
        #region Armor Mode
        
        private void UpdateArmorMode()
        {
            DetectGridUnderMouse();
            
            // Cambiar pieza con scroll (solo si no hay inventario o no hay selección)
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0 && !useInventory)
            {
                CycleFallbackArmor(scroll > 0 ? 1 : -1);
            }
            
            // Rotar con R
            if (Input.GetKeyDown(rotateKey))
            {
                RotateArmorWithPositionAdjust();
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
                    Debug.Log($"=== RAYCAST HIT: {hit.collider.gameObject.name} ===");
                }
                
                GridHead grid = hit.collider.GetComponent<GridHead>();
                
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
            
            // Usar el cellSize de la grilla en lugar de hardcoded
            float cellSize = grid.CellSize;
            
            int cellX = Mathf.FloorToInt(localPoint.x / cellSize);
            int cellY = Mathf.FloorToInt(localPoint.y / cellSize);
            
            cellX = Mathf.Clamp(cellX, 0, grid.GridInfo.sizeX - 1);
            cellY = Mathf.Clamp(cellY, 0, grid.GridInfo.sizeY - 1);
            
            currentPositionX = cellX;
            currentPositionY = cellY;
        }
        
        /// <summary>
        /// Rota la pieza de armadura manteniendo el centro visual en la misma posición.
        /// </summary>
        private void RotateArmorWithPositionAdjust()
        {
            ArmorPartData armorData = GetCurrentArmorData();
            if (armorData == null) return;
            
            int originalSizeX = armorData.tailGrid.gridInfo.sizeX;
            int originalSizeY = armorData.tailGrid.gridInfo.sizeY;
            
            // Tamaño antes de rotar
            var oldSize = GridRotation.RotateSize(originalSizeX, originalSizeY, currentRotation);
            
            // Calcular el centro de la pieza actual (en coordenadas de celda)
            float centerX = currentPositionX + oldSize.x * 0.5f;
            float centerY = currentPositionY + oldSize.y * 0.5f;
            
            // Rotar
            currentRotation = GridRotation.RotateClockwise(currentRotation);
            
            // Tamaño después de rotar
            var newSize = GridRotation.RotateSize(originalSizeX, originalSizeY, currentRotation);
            
            // Calcular nueva posición para mantener el mismo centro
            currentPositionX = Mathf.RoundToInt(centerX - newSize.x * 0.5f);
            currentPositionY = Mathf.RoundToInt(centerY - newSize.y * 0.5f);
            
            // Clampar para no salirse de la grilla
            if (hoveredGrid != null)
            {
                int maxX = Mathf.Max(0, hoveredGrid.GridInfo.sizeX - newSize.x);
                int maxY = Mathf.Max(0, hoveredGrid.GridInfo.sizeY - newSize.y);
                currentPositionX = Mathf.Clamp(currentPositionX, 0, maxX);
                currentPositionY = Mathf.Clamp(currentPositionY, 0, maxY);
            }
        }
        
        private ArmorPartData GetCurrentArmorData()
        {
            // Prioridad: selección del inventario
            if (selectedArmorData != null)
            {
                return selectedArmorData;
            }
            
            // Fallback: lista local
            if (fallbackArmorParts.Count > 0 && fallbackArmorIndex >= 0 && fallbackArmorIndex < fallbackArmorParts.Count)
            {
                return fallbackArmorParts[fallbackArmorIndex];
            }
            
            return null;
        }
        
        private void CycleFallbackArmor(int direction)
        {
            if (fallbackArmorParts.Count == 0) return;
            
            fallbackArmorIndex += direction;
            
            if (fallbackArmorIndex < 0)
                fallbackArmorIndex = fallbackArmorParts.Count - 1;
            else if (fallbackArmorIndex >= fallbackArmorParts.Count)
                fallbackArmorIndex = 0;
            
            selectedArmorData = fallbackArmorParts[fallbackArmorIndex];
        }
        
        private void TryPlaceArmor()
        {
            if (hoveredGrid == null) return;
            
            ArmorPartData armorData = GetCurrentArmorData();
            if (armorData == null) 
            {
                Debug.LogWarning("RobotAssemblyController: No hay armadura seleccionada");
                return;
            }
            
            // Verificar inventario
            if (useInventory && !PlayerInventory.Instance.HasItem(armorData, 1))
            {
                Debug.LogWarning($"RobotAssemblyController: No tienes {armorData.displayName} en el inventario");
                return;
            }
            
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
                // Restar del inventario
                if (useInventory)
                {
                    PlayerInventory.Instance.RemoveItem(armorData, 1);
                }
                
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
                ArmorPartData partData = part.ArmorData;
                bool hadAdditionalGrids = part.AdditionalGrids != null && part.AdditionalGrids.Count > 0;
                
                if (hoveredGrid.Remove(part))
                {
                    // Devolver al inventario
                    if (useInventory && partData != null)
                    {
                        PlayerInventory.Instance.AddItem(partData, 1);
                    }
                    
                    Destroy(part.gameObject);
                    Debug.Log($"Armadura removida{(useInventory ? " y devuelta al inventario" : "")}");
                    
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
            
            // Cambiar pieza con scroll (solo sin inventario)
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0 && !useInventory)
            {
                CycleFallbackStructural(scroll > 0 ? 1 : -1);
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
        
        private StructuralPartData GetCurrentStructuralData()
        {
            // Prioridad: selección del inventario
            if (selectedStructuralData != null)
            {
                return selectedStructuralData;
            }
            
            // Fallback: lista local
            if (fallbackStructuralParts.Count > 0 && fallbackStructuralIndex >= 0 && fallbackStructuralIndex < fallbackStructuralParts.Count)
            {
                return fallbackStructuralParts[fallbackStructuralIndex];
            }
            
            return null;
        }
        
        private void CycleFallbackStructural(int direction)
        {
            if (fallbackStructuralParts.Count == 0) return;
            
            fallbackStructuralIndex += direction;
            
            if (fallbackStructuralIndex < 0)
                fallbackStructuralIndex = fallbackStructuralParts.Count - 1;
            else if (fallbackStructuralIndex >= fallbackStructuralParts.Count)
                fallbackStructuralIndex = 0;
            
            selectedStructuralData = fallbackStructuralParts[fallbackStructuralIndex];
        }
        
        private void TryPlaceStructural()
        {
            if (hoveredSocket == null) return;
            if (hoveredSocket.IsOccupied) return;
            
            StructuralPartData partData = GetCurrentStructuralData();
            if (partData == null)
            {
                Debug.LogWarning("RobotAssemblyController: No hay pieza estructural seleccionada");
                return;
            }
            
            // Verificar inventario
            if (useInventory && !PlayerInventory.Instance.HasItem(partData, 1))
            {
                Debug.LogWarning($"RobotAssemblyController: No tienes {partData.displayName} en el inventario");
                return;
            }
            
            // Validar tipo de socket
            if (partData.partType != hoveredSocket.SocketType)
            {
                Debug.LogWarning("Tipo de pieza no coincide con socket");
                return;
            }
            
            // Validar tier
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
            
            bool success = false;
            
            // Caso especial: HipsSocket
            if (hoveredSocket == targetRobot.HipsSocket)
            {
                success = targetRobot.AttachHips(part);
            }
            else
            {
                success = hoveredSocket.TryAttach(part);
            }
            
            if (success)
            {
                // Restar del inventario
                if (useInventory)
                {
                    PlayerInventory.Instance.RemoveItem(partData, 1);
                }
                
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
            
            StructuralPartData partData = part.PartData;
            
            // IMPORTANTE: Extraer cualquier Core antes de destruir la pieza
            ExtractCoreFromPart(part);
            
            hoveredSocket.Detach();
            
            // Devolver al inventario
            if (useInventory && partData != null)
            {
                PlayerInventory.Instance.AddItem(partData, 1);
            }
            
            Destroy(part.gameObject);
            Debug.Log($"Pieza estructural removida{(useInventory ? " y devuelta al inventario" : "")}");
            
            CollectAvailableSockets();
            CollectAvailableGrids();
        }
        
        /// <summary>
        /// Extrae cualquier Core que esté dentro de una pieza estructural antes de destruirla.
        /// Esto protege tanto el Core del jugador como los Cores de mechas.
        /// </summary>
        private void ExtractCoreFromPart(StructuralPart part)
        {
            if (part == null) return;
            
            // Buscar cualquier RobotCore en la pieza y sus hijos
            var cores = part.GetComponentsInChildren<RobotCore>(true);
            
            foreach (var core in cores)
            {
                if (core != null && core.IsActive)
                {
                    Debug.Log($"RobotAssemblyController: Extrayendo Core '{core.name}' antes de destruir la pieza");
                    core.Extract();
                }
            }
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
            
            if (targetRobot.HipsSocket != null)
            {
                availableSockets.Add(targetRobot.HipsSocket);
                targetRobot.HipsSocket.EnsureCollider();
            }
            
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
            
            // Material de preview - Compatible con URP
            Shader previewShader = Shader.Find("Universal Render Pipeline/Lit");
            if (previewShader == null)
            {
                // Fallback para Built-in RP
                previewShader = Shader.Find("Standard");
            }
            if (previewShader == null)
            {
                // Último fallback
                previewShader = Shader.Find("Sprites/Default");
            }
            
            if (previewShader != null)
            {
                previewMaterial = new Material(previewShader);
                
                // Configurar transparencia para URP
                if (previewShader.name.Contains("Universal"))
                {
                    // URP Lit shader transparency settings
                    previewMaterial.SetFloat("_Surface", 1); // 0 = Opaque, 1 = Transparent
                    previewMaterial.SetFloat("_Blend", 0);   // 0 = Alpha, 1 = Premultiply, 2 = Additive, 3 = Multiply
                    previewMaterial.SetFloat("_AlphaClip", 0);
                    previewMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    previewMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    previewMaterial.SetFloat("_ZWrite", 0);
                    previewMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    previewMaterial.renderQueue = 3000;
                }
                else
                {
                    // Built-in RP Standard shader transparency settings
                    previewMaterial.SetFloat("_Mode", 3);
                    previewMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    previewMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    previewMaterial.SetInt("_ZWrite", 0);
                    previewMaterial.DisableKeyword("_ALPHATEST_ON");
                    previewMaterial.EnableKeyword("_ALPHABLEND_ON");
                    previewMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    previewMaterial.renderQueue = 3000;
                }
            }
            else
            {
                Debug.LogError("RobotAssemblyController: No se encontró ningún shader válido para preview");
            }
            
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
            
            UpdateArmorPreviewModel(armorData);
            
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
            
            // Usar el cellSize de la grilla si está disponible, sino usar default
            float cellSize = hoveredGrid != null ? hoveredGrid.CellSize : 0.1f;
            float halfCell = cellSize * 0.5f;
            
            // El pivotContainer se posiciona en el centro del área rotada
            float centerX = rotatedSize.x * halfCell;
            float centerY = rotatedSize.y * halfCell;
            pivotContainer.transform.localPosition = new Vector3(centerX, centerY, 0f);
            
            // El modelContainer se posiciona para centrar el modelo original
            float originalCenterX = originalSizeX * halfCell;
            float originalCenterY = originalSizeY * halfCell;
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
                
                // Verificar inventario también
                if (canPlace && useInventory && !PlayerInventory.Instance.HasItem(armorData, 1))
                {
                    canPlace = false;
                }
                
                previewColor = canPlace ? previewColorValid : previewColorInvalid;
                
                Vector3 cellPos = hoveredGrid.CellToWorldPosition(currentPositionX, currentPositionY);
                armorPreviewObject.transform.position = cellPos;
                armorPreviewObject.transform.rotation = hoveredGrid.transform.rotation;
            }
            
            if (previewMaterial != null)
            {
                previewMaterial.color = previewColor;
            }
        }
        
        private void UpdateArmorPreviewModel(ArmorPartData armorData)
        {
            if (armorData != currentPreviewData)
            {
                currentPreviewData = armorData;
                
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
            
            UpdateStructuralPreviewModel(structuralData);
            
            if (structuralData == null || structuralData.prefab == null || hoveredSocket == null)
            {
                structuralPreviewObject.SetActive(false);
                return;
            }
            
            structuralPreviewObject.SetActive(true);
            
            structuralPreviewObject.transform.position = hoveredSocket.transform.position;
            structuralPreviewObject.transform.rotation = hoveredSocket.transform.rotation;
            
            // Validaciones
            bool tierCompatible = structuralData.IsCompatibleWith(targetRobot.CurrentTier);
            bool typeMatches = structuralData.partType == hoveredSocket.SocketType;
            bool socketFree = !hoveredSocket.IsOccupied;
            bool hasInInventory = !useInventory || PlayerInventory.Instance.HasItem(structuralData, 1);
            
            Color previewColor = (tierCompatible && typeMatches && socketFree && hasInInventory) 
                ? previewColorValid 
                : previewColorInvalid;
            
            foreach (var renderer in structuralPreviewObject.GetComponentsInChildren<MeshRenderer>())
            {
                foreach (var mat in renderer.materials)
                {
                    mat.color = previewColor;
                }
            }
        }
        
        private void UpdateStructuralPreviewModel(StructuralPartData structuralData)
        {
            if (structuralData != currentStructuralPreviewData)
            {
                currentStructuralPreviewData = structuralData;
                
                foreach (Transform child in structuralPreviewObject.transform)
                {
                    Destroy(child.gameObject);
                }
                
                if (structuralData != null && structuralData.prefab != null)
                {
                    GameObject modelInstance = Instantiate(structuralData.prefab, structuralPreviewObject.transform);
                    modelInstance.transform.localPosition = Vector3.zero;
                    modelInstance.transform.localRotation = Quaternion.identity;
                    
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
        
        #region Debug UI
        
        private void DrawEditModeUI()
        {
            GUILayout.BeginArea(new Rect(10, 60, 250, 250));
            GUILayout.BeginVertical("box");
            
            GUILayout.Label($"=== EDITANDO ROBOT ===");
            GUILayout.Label($"Robot: {targetRobot?.name}");
            GUILayout.Label($"Tier: {targetRobot?.CurrentTier}");
            GUILayout.Label($"Inventario: {(useInventory ? "Activo" : "Desactivado")}");
            GUILayout.Label("");
            GUILayout.Label($"Modo: {currentMode}");
            GUILayout.Label($"[Tab] Cambiar modo");
            GUILayout.Label("");
            
            if (currentMode == AssemblyMode.Armor)
            {
                ArmorPartData armor = GetCurrentArmorData();
                GUILayout.Label($"Pieza: {armor?.displayName ?? "Ninguna"}");
                if (armor != null)
                {
                    GUILayout.Label($"Tamaño: {armor.tailGrid.gridInfo.sizeX}x{armor.tailGrid.gridInfo.sizeY}");
                    if (useInventory)
                    {
                        int count = PlayerInventory.Instance.GetItemCount(armor);
                        GUILayout.Label($"En inventario: {count}");
                    }
                }
                GUILayout.Label("[R] Rotar");
            }
            else
            {
                StructuralPartData structural = GetCurrentStructuralData();
                GUILayout.Label($"Pieza: {structural?.displayName ?? "Ninguna"}");
                if (structural != null)
                {
                    GUILayout.Label($"Tipo: {structural.partType}");
                    if (useInventory)
                    {
                        int count = PlayerInventory.Instance.GetItemCount(structural);
                        GUILayout.Label($"En inventario: {count}");
                    }
                }
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
