using System.Collections.Generic;
using UnityEngine;
using RobotGame;
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
        private List<StudGridHead> availableGrids = new List<StudGridHead>();
        private StudGridHead hoveredGrid = null;
        private int currentPositionX = 0;
        private int currentPositionY = 0;
        private GridRotation.Rotation currentRotation = GridRotation.Rotation.Deg0;
        
        // Rotación 3D de la pieza de armadura (para el nuevo sistema de studs)
        private Quaternion armorRotation3D = Quaternion.identity;
        
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
        
        // Cache de validación del preview (para evitar recalcular cada frame)
        private int lastValidatedStudIndex = -1;
        private Quaternion lastValidatedRotation = Quaternion.identity;
        private StudGridHead lastValidatedGrid = null;
        private bool cachedCanPlace = false;
        
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
                // Debug.Log($"RobotAssemblyController: Snapshot capturado para '{targetRobot.name}'");
            }
            else
            {
                editSnapshot = null;
                // Debug.Log($"RobotAssemblyController: Sin snapshot (modo cascarón)");
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
                // Debug.LogWarning("RobotAssemblyController: No hay robot. Restaurando desde snapshot...");
                RestoreFromSnapshot();
                return true;
            }
            
            // Capturar estado actual
            RobotSnapshot currentState = RobotSnapshot.Capture(targetRobot);
            
            // Validar que la configuración sea válida
            List<string> errors;
            if (!currentState.IsValid(out errors))
            {
                // Debug.LogWarning($"RobotAssemblyController: Configuración inválida ({string.Join(", ", errors)}). Restaurando desde snapshot...");
                RestoreFromSnapshot();
                return true;
            }
            
            // Configuración válida, limpiar snapshot
            // Debug.Log("RobotAssemblyController: Configuración válida. Guardando cambios.");
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
                // Debug.LogWarning("RobotAssemblyController: No hay snapshot para restaurar.");
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
                // Debug.Log("RobotAssemblyController: Restaurando mecha in-place...");
                
                if (editSnapshot.RestoreInPlace(targetRobot, playerCore))
                {
                    // Debug.Log("RobotAssemblyController: Mecha restaurado in-place exitosamente.");
                    
                    // Restaurar inventario si está activo
                    if (useInventory && currentState != null)
                    {
                        RestoreInventoryFromSnapshot(editSnapshot, currentState);
                    }
                }
                else
                {
                    // Debug.LogError("RobotAssemblyController: Error al restaurar mecha in-place.");
                }
            }
            else
            {
                // Para robots normales: destruir y recrear
                Robot restoredRobot = editSnapshot.Restore(targetRobot, playerCore);
                
                if (restoredRobot != null)
                {
                    targetRobot = restoredRobot;
                    // Debug.Log("RobotAssemblyController: Configuración restaurada desde snapshot.");
                    
                    // Restaurar inventario si está activo
                    if (useInventory && currentState != null)
                    {
                        RestoreInventoryFromSnapshot(editSnapshot, currentState);
                    }
                }
                else
                {
                    // Debug.LogError("RobotAssemblyController: Error al restaurar desde snapshot.");
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
                // Debug.Log("RobotAssemblyController: No hay inventario activo, no se restauran piezas");
                return;
            }
            
            if (currentSnapshot == null)
            {
                // Debug.LogWarning("RobotAssemblyController: No hay snapshot actual para comparar");
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
            
            // Debug.Log($"RobotAssemblyController: Inventario restaurado - {armorCount} armaduras, {structuralCount} estructurales devueltas al inventario");
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
                
                // Invalidar cache de validación
                lastValidatedStudIndex = -1;
                lastValidatedGrid = null;
                
                // Cambiar a modo armor si no estamos en él
                if (currentMode != AssemblyMode.Armor)
                {
                    SetMode(AssemblyMode.Armor);
                }
                
                // Debug.Log($"RobotAssemblyController: Armadura seleccionada - {armorData.displayName}");
            }
            else if (item is StructuralPartData structuralData)
            {
                selectedStructuralData = structuralData;
                
                // Cambiar a modo structural si no estamos en él
                if (currentMode != AssemblyMode.Structural)
                {
                    SetMode(AssemblyMode.Structural);
                }
                
                // Debug.Log($"RobotAssemblyController: Estructural seleccionada - {structuralData.displayName}");
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
                // Debug.LogError("RobotAssemblyController: No hay robot objetivo");
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
                
                // Debug.Log($"RobotAssemblyController: PlayerController.Target={playerController.Target?.name}, " +
                          // $"targetRobot={targetRobot.name}, isControllingTarget={isControllingTarget}");
                
                if (isControllingTarget)
                {
                    playerController.EnterEditModeState();
                    playerControllerWasInEditMode = true;
                    // Debug.Log("RobotAssemblyController: PlayerController puesto en modo edición");
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
                // Debug.Log($"RobotAssemblyController: Rigidbody puesto en kinematic manualmente (era kinematic={wasKinematicBeforeEdit})");
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
            
            // Activar Box_ colliders DESPUÉS de recolectar grillas
            EnableBoxCollidersForGridDetection();
            
            // Abrir panel de inventario con el tier de la estación
            if (inventoryPanel != null)
            {
                TierInfo stationTier = station != null ? station.StationTier : TierInfo.Tier1_1;
                inventoryPanel.EnterEditMode(currentMode, stationTier);
            }
            
            // Poner el robot en T-Pose para edición
            targetRobot.SetEditPose(true);
            
            // Debug.Log($"RobotAssemblyController: Activado para '{targetRobot.name}'");
        }
        
        private void DeactivateController()
        {
            isActive = false;
            currentEditMode = AssemblyEditMode.None;
            
            // Restaurar colliders (también desactiva los Box_ de detección)
            RestoreRobotMainColliders();
            
            // Restaurar Rigidbody si lo modificamos manualmente
            if (manuallySetKinematicRb != null)
            {
                manuallySetKinematicRb.isKinematic = wasKinematicBeforeEdit;
                // Debug.Log($"RobotAssemblyController: Rigidbody restaurado a kinematic={wasKinematicBeforeEdit}");
                manuallySetKinematicRb = null;
            }
            
            // Sacar PlayerController del modo edición si lo habíamos puesto
            if (playerControllerWasInEditMode && playerController != null)
            {
                playerController.ExitEditModeState();
                playerController.RecalculateCollider();
                // Debug.Log("RobotAssemblyController: PlayerController restaurado de modo edición");
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
            
            // Restaurar pose normal del robot (salir de T-Pose)
            if (targetRobot != null)
            {
                Debug.Log("[RobotAssemblyController] Saliendo de edición - Actualizando movilidad...");
                targetRobot.SetEditPose(false);
                
                // Aplicar multiplicador de movilidad actualizado
                targetRobot.ApplyMobilityToAnimators();
                
                // Actualizar PlayerController si controla este robot
                if (playerController != null && playerController.Target == targetRobot.transform)
                {
                    Debug.Log("[RobotAssemblyController] Actualizando PlayerController mobility (mismo target)...");
                    playerController.UpdateMobilityMultiplier();
                }
            }
            else
            {
                Debug.Log("[RobotAssemblyController] targetRobot es NULL al salir!");
            }
            
            // Limpiar referencias
            targetRobot = null;
            editSnapshot = null;
            availableGrids.Clear();
            availableSockets.Clear();
            selectedArmorData = null;
            selectedStructuralData = null;
            
            // Debug.Log("RobotAssemblyController: Desactivado");
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
            
            // Resetear rotación al cambiar de modo
            armorRotation3D = Quaternion.identity;
            
            // Sincronizar con panel de inventario
            if (inventoryPanel != null && inventoryPanel.IsOpen)
            {
                InventoryCategory targetCategory = mode == AssemblyMode.Structural 
                    ? InventoryCategory.StructuralParts 
                    : InventoryCategory.ArmorParts;
                
                inventoryPanel.SelectCategory(targetCategory);
            }
            
            // Debug.Log($"RobotAssemblyController: Modo cambiado a {mode}");
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
                // No desactivar: triggers, StudGridHead, StructuralSocket, ni Box_ (usados para detección)
                if (!col.isTrigger && 
                    col.GetComponent<StudGridHead>() == null && 
                    col.GetComponent<StructuralSocket>() == null &&
                    !col.gameObject.name.StartsWith("Box_"))
                {
                    col.enabled = false;
                    disabledColliders.Add(col);
                }
            }
            
            // Debug.Log($"RobotAssemblyController: {disabledColliders.Count} colliders desactivados");
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
            
            // Desactivar los Box_ colliders que activamos para detección
            DisableBoxCollidersForGridDetection();
        }
        
        // Lista de Box_ colliders activados para detección de grillas
        private List<BoxCollider> activatedBoxColliders = new List<BoxCollider>();
        
        /// <summary>
        /// Activa los BoxColliders de los Box_ que están en partes con StudGridHead.
        /// </summary>
        private void EnableBoxCollidersForGridDetection()
        {
            activatedBoxColliders.Clear();
            
            if (targetRobot == null) return;
            
            // Buscar TODOS los Box_ en el robot y activarlos como triggers
            var allTransforms = targetRobot.GetComponentsInChildren<Transform>(true);
            
            foreach (var t in allTransforms)
            {
                if (t.name.StartsWith("Box_"))
                {
                    BoxCollider bc = t.GetComponent<BoxCollider>();
                    if (bc == null)
                    {
                        // Crear collider basado en la escala del Empty
                        bc = t.gameObject.AddComponent<BoxCollider>();
                        bc.size = Vector3.one;
                        bc.center = Vector3.zero;
                    }
                    
                    bc.isTrigger = true;
                    bc.enabled = true;
                    activatedBoxColliders.Add(bc);
                }
            }
            
            // Debug.Log($"[GRID] {activatedBoxColliders.Count} Box_ colliders activados para detección");
        }
        
        /// <summary>
        /// Desactiva los BoxColliders de Box_ que activamos.
        /// </summary>
        private void DisableBoxCollidersForGridDetection()
        {
            foreach (var bc in activatedBoxColliders)
            {
                if (bc != null)
                {
                    bc.isTrigger = false; // Restaurar a no-trigger para validación de colisión
                    bc.enabled = false;
                }
            }
            
            activatedBoxColliders.Clear();
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
            
            // Rotar con R (eje Y) o Shift+R (eje X)
            if (Input.GetKeyDown(rotateKey))
            {
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    // Shift+R: Rotar 90° en X
                    armorRotation3D = Quaternion.Euler(90, 0, 0) * armorRotation3D;
                    // Debug.Log($"Armadura rotada en X. Rotación actual: {armorRotation3D.eulerAngles}");
                }
                else
                {
                    // R: Rotar 90° en Y
                    armorRotation3D = Quaternion.Euler(0, 90, 0) * armorRotation3D;
                    // Debug.Log($"Armadura rotada en Y. Rotación actual: {armorRotation3D.eulerAngles}");
                }
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
            
            StudGridHead newHoveredGrid = null;
            
            // Máscara que excluye hitboxes de combate (Layer 11) para no interferir con detección de studs
            int editModeMask = ~RobotLayers.RobotHitboxMask;
            
            // Raycast buscando triggers (los Box_ son triggers para no interferir con validación)
            if (Physics.Raycast(ray, out hit, 100f, editModeMask, QueryTriggerInteraction.Collide))
            {
                // Verificar si golpeamos un Box_ (collider de parte estructural)
                if (hit.collider.gameObject.name.StartsWith("Box_"))
                {
                    // Subir en la jerarquía hasta encontrar un StudGridHead
                    Transform current = hit.collider.transform.parent;
                    int depth = 0;
                    while (current != null && depth < 20)
                    {
                        StudGridHead grid = current.GetComponent<StudGridHead>();
                        if (grid != null && availableGrids.Contains(grid))
                        {
                            newHoveredGrid = grid;
                            CalculateGridPosition(grid, hit.point);
                            break;
                        }
                        current = current.parent;
                        depth++;
                    }
                }
                else
                {
                    // Fallback: buscar StudGridHead directamente en el collider (BoxCollider automático)
                    StudGridHead grid = hit.collider.GetComponent<StudGridHead>();
                    
                    if (grid != null && availableGrids.Contains(grid))
                    {
                        newHoveredGrid = grid;
                        CalculateGridPosition(grid, hit.point);
                    }
                }
            }
            
            if (newHoveredGrid != hoveredGrid)
            {
                hoveredGrid = newHoveredGrid;
                currentPositionX = 0;
                currentPositionY = 0;
            }
        }
        
        private void CalculateGridPosition(StudGridHead grid, Vector3 worldPoint)
        {
            // Encontrar el stud más cercano al punto de hit
            int closestStudIndex = grid.FindClosestStud(worldPoint);
            grid.CurrentHoveredStudIndex = closestStudIndex;
            
            // Para compatibilidad, también actualizamos las coordenadas de celda
            // aunque ya no se usan realmente para el posicionamiento
            if (closestStudIndex >= 0)
            {
                currentPositionX = closestStudIndex; // Usamos el índice como "X"
                currentPositionY = 0; // No usado en el nuevo sistema
            }
            else
            {
                currentPositionX = 0;
                currentPositionY = 0;
            }
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
            
            // Resetear rotación al cambiar de pieza
            armorRotation3D = Quaternion.identity;
        }
        
        private void TryPlaceArmor()
        {
            if (hoveredGrid == null) return;
            
            ArmorPartData armorData = GetCurrentArmorData();
            if (armorData == null) return;
            
            // Info básica para logs
            int anchorIndex = hoveredGrid.CurrentHoveredStudIndex;
            Transform boneTransform = hoveredGrid.GetCurrentStudParentTransform();
            string boneName = boneTransform != null ? boneTransform.name : "null";
            
            // Verificar inventario
            if (useInventory && !PlayerInventory.Instance.HasItem(armorData, 1))
            {
                // Debug.LogWarning($"[COLOCAR] FALLO: '{armorData.displayName}' - No tienes en inventario");
                return;
            }
            
            // Crear la pieza temporalmente para obtener su TailGrid
            ArmorPart armorPart = RobotFactory.Instance.CreateArmorPart(armorData);
            if (armorPart == null)
            {
                // Debug.LogError($"[COLOCAR] FALLO: '{armorData.displayName}' - Error al crear pieza");
                return;
            }
            
            var tailGrid = armorPart.TailGrid;
            if (tailGrid == null || tailGrid.StudCount == 0)
            {
                // Debug.LogError($"[COLOCAR] FALLO: '{armorData.displayName}' - No tiene TailGrid");
                Destroy(armorPart.gameObject);
                return;
            }
            
            // Obtener rotación del hueso y combinar con rotación del usuario
            Quaternion boneRotation = hoveredGrid.GetCurrentStudRotation();
            Quaternion finalRotation = boneRotation * armorRotation3D;
            
            // Validar que TODOS los Tails puedan colocarse CON LA ROTACIÓN FINAL
            if (!hoveredGrid.CanPlaceAllTails(tailGrid, anchorIndex, finalRotation))
            {
                string occupiedInfo = GetOccupiedStudsInfo(hoveredGrid, tailGrid, anchorIndex, finalRotation);
                // Debug.LogWarning($"[COLOCAR] FALLO STUDS: '{armorData.displayName}' en '{boneName}' (stud {anchorIndex})\n  → {occupiedInfo}");
                Destroy(armorPart.gameObject);
                return;
            }
            
            // Calcular la posición correcta
            Vector3 armorPosition = hoveredGrid.CalculateArmorPosition(tailGrid, anchorIndex, finalRotation);
            armorPart.transform.position = armorPosition;
            armorPart.transform.rotation = finalRotation;
            armorPart.transform.SetParent(boneTransform, worldPositionStays: true);
            
            // VALIDACIÓN DE COLISIÓN
            string collisionWith = GetCollisionInfo(armorPart);
            if (collisionWith != null)
            {
                // Debug.LogWarning($"[COLOCAR] FALLO COLISIÓN: '{armorData.displayName}' en '{boneName}'\n  → Colisiona con: {collisionWith}");
                Destroy(armorPart.gameObject);
                return;
            }
            
            // Colocar y marcar TODOS los studs como ocupados
            if (hoveredGrid.PlaceArmorWithAllTails(armorPart, anchorIndex, finalRotation))
            {
                if (useInventory)
                {
                    PlayerInventory.Instance.RemoveItem(armorData, 1);
                }
                
                lastValidatedStudIndex = -1;
                lastValidatedGrid = null;
                
                // Debug.Log($"[COLOCAR] ÉXITO: '{armorData.displayName}' en '{boneName}' (stud {anchorIndex})");
                
                if (armorPart.AdditionalGrids != null && armorPart.AdditionalGrids.Count > 0)
                {
                    CollectAvailableGrids();
                }
            }
            else
            {
                // Debug.LogError($"[COLOCAR] FALLO: '{armorData.displayName}' - PlaceArmorWithAllTails retornó false");
                Destroy(armorPart.gameObject);
            }
        }
        
        /// <summary>
        /// Obtiene información de qué studs están ocupados o no encontrados.
        /// </summary>
        private string GetOccupiedStudsInfo(StudGridHead grid, StudGridTail tailGrid, int anchorIndex, Quaternion rotation)
        {
            if (grid == null || tailGrid == null) return "Grid o TailGrid null";
            
            var tails = tailGrid.Studs;
            if (tails == null || tails.Count == 0) return "Sin Tails";
            
            var problems = new System.Collections.Generic.List<string>();
            Vector3 anchorHeadPos = grid.GetStudWorldPosition(anchorIndex);
            Vector3 firstTailLocal = tails[0].localPosition;
            
            for (int i = 0; i < tails.Count; i++)
            {
                Vector3 tailOffset = tails[i].localPosition - firstTailLocal;
                Vector3 rotatedOffset = rotation * tailOffset;
                Vector3 targetPos = anchorHeadPos + rotatedOffset;
                
                int headIndex = grid.FindClosestStudIndex(targetPos, 0.01f);
                
                if (headIndex < 0)
                {
                    problems.Add($"Tail[{i}] '{tails[i].name}': No encontró Head cercano");
                }
                else if (grid.IsStudOccupied(headIndex))
                {
                    string occupant = grid.GetOccupantName(headIndex);
                    problems.Add($"Tail[{i}] '{tails[i].name}': Head[{headIndex}] OCUPADO por '{occupant}'");
                }
            }
            
            return problems.Count > 0 ? string.Join(", ", problems) : "Razón desconocida";
        }
        
        /// <summary>
        /// Obtiene información de con qué colisiona la pieza, o null si no colisiona.
        /// </summary>
        private string GetCollisionInfo(ArmorPart newPart)
        {
            if (newPart == null) return null;
            
            Physics.SyncTransforms();
            
            var newBoxColliders = GetBoxColliders(newPart.gameObject);
            if (newBoxColliders.Count == 0) return null;
            
            foreach (var newCol in newBoxColliders)
            {
                if (newCol == null) continue;
                
                Collider[] overlaps = Physics.OverlapBox(
                    newCol.bounds.center,
                    newCol.bounds.extents * 0.95f,
                    newCol.transform.rotation,
                    ~0,
                    QueryTriggerInteraction.Ignore
                );
                
                foreach (var overlap in overlaps)
                {
                    if (overlap == null || overlap == newCol) continue;
                    if (overlap.transform.IsChildOf(newPart.transform)) continue;
                    if (!overlap.gameObject.name.StartsWith("Box_")) continue;
                    
                    return $"'{newCol.gameObject.name}' con '{overlap.gameObject.name}' (parte: {overlap.transform.parent?.name ?? "?"})";
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Verifica si una pieza de armadura colisiona con colliders Box_ existentes.
        /// Solo verifica colliders cuyo GameObject empiece con "Box_".
        /// </summary>
        private bool CheckArmorCollision(ArmorPart newPart)
        {
            if (newPart == null) return false;
            
            Physics.SyncTransforms();
            
            // Obtener colliders Box_ de la nueva pieza
            var newBoxColliders = GetBoxColliders(newPart.gameObject);
            
            if (newBoxColliders.Count == 0)
            {
                return false;
            }
            
            // Recopilar todos los colliders Box_ existentes
            List<Collider> existingBoxColliders = new List<Collider>();
            
            // 1. Box_ de armaduras colocadas
            foreach (var grid in availableGrids)
            {
                if (grid == null) continue;
                foreach (var placed in grid.PlacedParts)
                {
                    if (placed != null && placed != newPart)
                    {
                        existingBoxColliders.AddRange(GetBoxColliders(placed.gameObject));
                    }
                }
            }
            
            // 2. Box_ de partes estructurales (excluyendo los de newPart y los de grillas con studs)
            if (targetRobot != null)
            {
                var allBoxes = GetBoxColliders(targetRobot.gameObject);
                foreach (var box in allBoxes)
                {
                    // Excluir si es hijo de la nueva pieza
                    if (box.transform.IsChildOf(newPart.transform))
                        continue;
                    
                    // Excluir si es hijo de una grilla disponible (parte estructural donde se colocan armaduras)
                    bool isPartOfGrid = false;
                    foreach (var grid in availableGrids)
                    {
                        if (grid != null && box.transform.IsChildOf(grid.transform))
                        {
                            isPartOfGrid = true;
                            break;
                        }
                    }
                    if (isPartOfGrid)
                        continue;
                    
                    existingBoxColliders.Add(box);
                }
            }
            
            if (existingBoxColliders.Count == 0)
            {
                return false;
            }
            
            // Verificar intersección usando GetBoxColliderBounds (funciona con colliders deshabilitados)
            foreach (var newCol in newBoxColliders)
            {
                Bounds newBounds = GetBoxColliderBounds(newCol);
                
                // Reducir ligeramente para evitar falsos positivos
                newBounds.extents *= 0.9f;
                
                foreach (var existingCol in existingBoxColliders)
                {
                    if (existingCol == null) continue;
                    
                    Bounds existingBounds = GetBoxColliderBounds(existingCol);
                    
                    if (newBounds.Intersects(existingBounds))
                    {
                        // Debug.Log($"¡COLISIÓN DETECTADA! '{newCol.gameObject.name}' con '{existingCol.gameObject.name}'");
                        return true;
                    }
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Obtiene todos los colliders cuyo GameObject empiece con "Box_".
        /// </summary>
        private List<Collider> GetBoxColliders(GameObject root)
        {
            var result = new List<Collider>();
            var allColliders = root.GetComponentsInChildren<Collider>(true); // incluir inactivos
            
            foreach (var col in allColliders)
            {
                if (col.gameObject.name.StartsWith("Box_"))
                {
                    result.Add(col);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Calcula los bounds de un BoxCollider incluso si está deshabilitado.
        /// Cuando un collider está disabled, Unity retorna bounds de tamaño 0.
        /// </summary>
        private Bounds GetBoxColliderBounds(Collider col)
        {
            if (col is BoxCollider box)
            {
                // Calcular bounds manualmente usando transform
                Vector3 worldCenter = box.transform.TransformPoint(box.center);
                Vector3 worldSize = Vector3.Scale(box.size, box.transform.lossyScale);
                
                // Crear bounds en world space
                // Nota: esto es aproximado para rotaciones, pero funciona bien para la mayoría de casos
                Bounds bounds = new Bounds(worldCenter, worldSize);
                
                // Para rotaciones, necesitamos calcular el AABB correcto
                Vector3 halfExtents = box.size * 0.5f;
                Vector3[] corners = new Vector3[8];
                corners[0] = box.center + new Vector3(-halfExtents.x, -halfExtents.y, -halfExtents.z);
                corners[1] = box.center + new Vector3(halfExtents.x, -halfExtents.y, -halfExtents.z);
                corners[2] = box.center + new Vector3(-halfExtents.x, halfExtents.y, -halfExtents.z);
                corners[3] = box.center + new Vector3(halfExtents.x, halfExtents.y, -halfExtents.z);
                corners[4] = box.center + new Vector3(-halfExtents.x, -halfExtents.y, halfExtents.z);
                corners[5] = box.center + new Vector3(halfExtents.x, -halfExtents.y, halfExtents.z);
                corners[6] = box.center + new Vector3(-halfExtents.x, halfExtents.y, halfExtents.z);
                corners[7] = box.center + new Vector3(halfExtents.x, halfExtents.y, halfExtents.z);
                
                // Transformar a world space y calcular AABB
                bounds = new Bounds(box.transform.TransformPoint(corners[0]), Vector3.zero);
                for (int i = 1; i < 8; i++)
                {
                    bounds.Encapsulate(box.transform.TransformPoint(corners[i]));
                }
                
                return bounds;
            }
            
            // Para otros tipos de collider, usar bounds normal (solo funciona si está enabled)
            return col.bounds;
        }
        
        private void TryRemoveArmor()
        {
            if (hoveredGrid == null) return;
            
            // Usar el nuevo método que busca por stud actual
            ArmorPart part = hoveredGrid.GetPartAtCurrentStud();
            
            if (part == null)
            {
                // Debug.Log("TryRemoveArmor: No hay pieza en el stud actual");
                return;
            }
            
            ArmorPartData partData = part.ArmorData;
            bool hadAdditionalGrids = part.AdditionalGrids != null && part.AdditionalGrids.Count > 0;
            
            // Debug.Log($"TryRemoveArmor: Intentando remover '{part.gameObject.name}'");
            
            if (hoveredGrid.RemovePart(part))
            {
                // Devolver al inventario
                if (useInventory && partData != null)
                {
                    PlayerInventory.Instance.AddItem(partData, 1);
                }
                
                Destroy(part.gameObject);
                
                // Invalidar cache de validación (cambió el estado del robot)
                lastValidatedStudIndex = -1;
                lastValidatedGrid = null;
                
                // Debug.Log($"Armadura '{part.gameObject.name}' removida{(useInventory ? " y devuelta al inventario" : "")}");
                
                if (hadAdditionalGrids)
                {
                    CollectAvailableGrids();
                }
            }
            else
            {
                // Debug.LogWarning($"TryRemoveArmor: No se pudo remover '{part.gameObject.name}'");
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
            
            // Máscara que excluye hitboxes de combate (Layer 11)
            int editModeMask = ~RobotLayers.RobotHitboxMask;
            
            if (Physics.Raycast(ray, out hit, 100f, editModeMask, QueryTriggerInteraction.Collide))
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
                // Debug.LogWarning("RobotAssemblyController: No hay pieza estructural seleccionada");
                return;
            }
            
            // Verificar inventario
            if (useInventory && !PlayerInventory.Instance.HasItem(partData, 1))
            {
                // Debug.LogWarning($"RobotAssemblyController: No tienes {partData.displayName} en el inventario");
                return;
            }
            
            // Validar tipo de socket
            if (partData.partType != hoveredSocket.SocketType)
            {
                // Debug.LogWarning("Tipo de pieza no coincide con socket");
                return;
            }
            
            // Validar tier
            if (!partData.IsCompatibleWith(targetRobot.CurrentTier))
            {
                // Debug.LogWarning($"Pieza no compatible con robot Tier {targetRobot.CurrentTier}");
                return;
            }
            
            // Crear la pieza
            StructuralPart part = RobotFactory.Instance.CreateStructuralPart(partData, hoveredSocket.transform);
            if (part == null)
            {
                // Debug.LogError("Error al crear pieza estructural");
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
                
                // Debug.Log($"Pieza '{partData.displayName}' colocada");
                Physics.SyncTransforms();
                CollectAvailableSockets();
                CollectAvailableGrids();
            }
            else
            {
                // Debug.LogError("Error al conectar la pieza");
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
                    // Debug.LogWarning("No se puede remover: tiene piezas hijas conectadas");
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
            // Debug.Log($"Pieza estructural removida{(useInventory ? " y devuelta al inventario" : "")}");
            
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
                    // Debug.Log($"RobotAssemblyController: Extrayendo Core '{core.name}' antes de destruir la pieza");
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
            
            // Debug.Log($"RobotAssemblyController: {availableGrids.Count} grillas disponibles");
        }
        
        private void CollectArmorPartGrids(IReadOnlyList<StudGridHead> grids)
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
            
            // Debug.Log($"RobotAssemblyController: {availableSockets.Count} sockets disponibles");
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
                // Debug.LogError("RobotAssemblyController: No se encontró ningún shader válido para preview");
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
            
            // Detectar Tails del prefab para calcular offset
            var tailStuds = StudDetector.DetectTailStuds(armorData.prefab.transform);
            Vector3 firstTailOffset = Vector3.zero;
            if (tailStuds.Count > 0)
            {
                firstTailOffset = tailStuds[0].localPosition;
            }
            
            // Color según estado
            Color previewColor;
            
            if (hoveredGrid == null)
            {
                previewColor = previewColorNeutral;
                
                // Sin grilla: mostrar en el aire con rotación del usuario
                pivotContainer.transform.localPosition = Vector3.zero;
                pivotContainer.transform.localRotation = armorRotation3D;
                modelContainer.transform.localPosition = -firstTailOffset;
                
                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                Vector3 worldPos = ray.origin + ray.direction * 2f;
                armorPreviewObject.transform.position = worldPos;
                armorPreviewObject.transform.rotation = Quaternion.identity;
                
                // Resetear cache cuando no hay grilla
                lastValidatedGrid = null;
                lastValidatedStudIndex = -1;
            }
            else
            {
                // Con grilla: obtener rotación del hueso y combinar con rotación del usuario
                Quaternion boneRotation = hoveredGrid.GetCurrentStudRotation();
                Quaternion finalRotation = boneRotation * armorRotation3D;
                
                // Obtener posición del stud seleccionado
                Vector3 studPos = hoveredGrid.GetCurrentHoveredStudPosition();
                int currentStudIndex = hoveredGrid.CurrentHoveredStudIndex;
                
                // Posicionar el preview en la posición del stud con rotación combinada
                armorPreviewObject.transform.position = studPos;
                armorPreviewObject.transform.rotation = finalRotation;
                
                // El modelo se offsetea por el primer Tail
                pivotContainer.transform.localPosition = Vector3.zero;
                pivotContainer.transform.localRotation = Quaternion.identity;
                modelContainer.transform.localPosition = -firstTailOffset;
                
                // Solo revalidar si cambió el stud, la grilla o la rotación
                bool needsRevalidation = (currentStudIndex != lastValidatedStudIndex) ||
                                         (hoveredGrid != lastValidatedGrid) ||
                                         (finalRotation != lastValidatedRotation);
                
                if (needsRevalidation)
                {
                    // Simular colocación completa (mismo código que TryPlaceArmor)
                    cachedCanPlace = SimulatePlacement(armorData, currentStudIndex, finalRotation);
                    
                    // Guardar estado para cache
                    lastValidatedStudIndex = currentStudIndex;
                    lastValidatedGrid = hoveredGrid;
                    lastValidatedRotation = finalRotation;
                }
                
                previewColor = cachedCanPlace ? previewColorValid : previewColorInvalid;
            }
            
            if (previewMaterial != null)
            {
                previewMaterial.color = previewColor;
            }
        }
        
        /// <summary>
        /// Simula la colocación de una armadura sin colocarla realmente.
        /// Usa exactamente la misma lógica que TryPlaceArmor.
        /// </summary>
        private bool SimulatePlacement(ArmorPartData armorData, int anchorIndex, Quaternion finalRotation)
        {
            if (hoveredGrid == null || armorData == null) return false;
            
            // Verificar inventario
            if (useInventory && !PlayerInventory.Instance.HasItem(armorData, 1))
            {
                return false;
            }
            
            // Crear la pieza temporalmente para validar
            ArmorPart tempArmor = RobotFactory.Instance.CreateArmorPart(armorData);
            if (tempArmor == null) return false;
            
            bool canPlace = false;
            
            try
            {
                var tailGrid = tempArmor.TailGrid;
                if (tailGrid == null || tailGrid.StudCount == 0)
                {
                    return false;
                }
                
                // Validar studs (misma lógica que TryPlaceArmor)
                if (!hoveredGrid.CanPlaceAllTails(tailGrid, anchorIndex, finalRotation))
                {
                    return false;
                }
                
                // Posicionar para validar colisión (misma lógica que TryPlaceArmor)
                Vector3 armorPosition = hoveredGrid.CalculateArmorPosition(tailGrid, anchorIndex, finalRotation);
                tempArmor.transform.position = armorPosition;
                tempArmor.transform.rotation = finalRotation;
                
                Transform boneTransform = hoveredGrid.GetCurrentStudParentTransform();
                tempArmor.transform.SetParent(boneTransform, worldPositionStays: true);
                
                // Validar colisión - usar GetCollisionInfo como TryPlaceArmor
                if (GetCollisionInfo(tempArmor) != null)
                {
                    return false;
                }
                
                canPlace = true;
            }
            finally
            {
                // Siempre destruir la pieza temporal
                if (tempArmor != null)
                {
                    Destroy(tempArmor.gameObject);
                }
            }
            
            return canPlace;
        }
        
        /// <summary>
        /// Verifica si el preview actual colisionaría con colliders Box_ existentes.
        /// </summary>
        private bool CheckPreviewCollision()
        {
            if (modelContainer == null) return false;
            
            // Obtener bounds del preview desde los renderers
            var renderers = modelContainer.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0) return false;
            
            Bounds previewBounds = new Bounds();
            bool initialized = false;
            
            foreach (var renderer in renderers)
            {
                if (!initialized)
                {
                    previewBounds = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    previewBounds.Encapsulate(renderer.bounds);
                }
            }
            
            if (!initialized) return false;
            
            // Reducir ligeramente
            previewBounds.extents *= 0.85f;
            
            // Recopilar todos los colliders Box_ existentes
            List<Collider> existingBoxColliders = new List<Collider>();
            
            // 1. Box_ de armaduras colocadas
            foreach (var grid in availableGrids)
            {
                if (grid == null) continue;
                foreach (var placed in grid.PlacedParts)
                {
                    if (placed != null)
                    {
                        existingBoxColliders.AddRange(GetBoxColliders(placed.gameObject));
                    }
                }
            }
            
            // 2. Box_ de partes estructurales (excluyendo los de grillas con studs)
            if (targetRobot != null)
            {
                var allBoxes = GetBoxColliders(targetRobot.gameObject);
                foreach (var box in allBoxes)
                {
                    // Excluir si es hijo de una grilla disponible
                    bool isPartOfGrid = false;
                    foreach (var grid in availableGrids)
                    {
                        if (grid != null && box.transform.IsChildOf(grid.transform))
                        {
                            isPartOfGrid = true;
                            break;
                        }
                    }
                    if (!isPartOfGrid)
                    {
                        existingBoxColliders.Add(box);
                    }
                }
            }
            
            // Verificar intersección usando GetBoxColliderBounds (funciona con colliders deshabilitados)
            foreach (var existingCol in existingBoxColliders)
            {
                if (existingCol == null) continue;
                
                Bounds existingBounds = GetBoxColliderBounds(existingCol);
                
                if (previewBounds.Intersects(existingBounds))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Verifica si todos los Tails de una lista pueden colocarse en la grilla.
        /// Usa POSICIONES MUNDIALES para la comparación, aplicando la rotación especificada.
        /// Todos los Heads deben pertenecer al mismo grupo (mismo hueso).
        /// </summary>
        private bool CanPlaceAllTailsFromData(StudGridHead grid, List<StudPoint> tailStuds, int anchorHeadIndex, Quaternion rotation)
        {
            if (grid == null || tailStuds == null || tailStuds.Count == 0)
                return false;
            
            if (anchorHeadIndex < 0 || anchorHeadIndex >= grid.Studs.Count)
                return false;
            
            var firstTail = tailStuds[0];
            Vector3 anchorHeadWorldPos = grid.GetStudWorldPosition(anchorHeadIndex);
            var anchorHead = grid.Studs[anchorHeadIndex];
            
            // GroupId requerido - todos los Heads deben pertenecer al mismo grupo
            string requiredGroupId = anchorHead.groupId;
            
            List<int> matchedHeadIndices = new List<int>();
            
            foreach (var tail in tailStuds)
            {
                // Verificar tier
                if (!tail.tierInfo.IsCompatibleWith(anchorHead.tierInfo))
                    return false;
                
                // Calcular offset del Tail respecto al primer Tail (en espacio local)
                Vector3 tailOffsetLocal = tail.localPosition - firstTail.localPosition;
                
                // APLICAR LA ROTACIÓN al offset
                Vector3 rotatedOffset = rotation * tailOffsetLocal;
                
                // Posición mundial esperada para este Tail
                Vector3 expectedWorldPos = anchorHeadWorldPos + rotatedOffset;
                
                // Buscar Head en esa posición mundial CON EL MISMO GRUPO
                int headIndex = grid.FindHeadAtWorldPosition(expectedWorldPos, tail.tierInfo, requiredGroupId);
                
                if (headIndex < 0 || grid.IsStudOccupied(headIndex))
                    return false;
                
                if (matchedHeadIndices.Contains(headIndex))
                    return false;
                
                matchedHeadIndices.Add(headIndex);
            }
            
            return true;
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
