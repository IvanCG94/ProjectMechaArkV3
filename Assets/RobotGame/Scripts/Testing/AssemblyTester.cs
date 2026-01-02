using System.Collections.Generic;
using UnityEngine;
using RobotGame.Components;
using RobotGame.Control;
using RobotGame.Data;
using RobotGame.Systems;
using RobotGame.Utils;
using RobotGame.Enums;
using RobotGame.Assembly;

namespace RobotGame.Testing
{
    /// <summary>
    /// Script de prueba para ensamblar piezas interactivamente.
    /// Soporta tanto piezas de armadura (en grillas) como piezas estructurales (en sockets).
    /// 
    /// INTEGRACIÓN CON ASSEMBLY STATION:
    /// - Solo se activa cuando el jugador está en una AssemblyStation
    /// - Escucha el evento OnEditModeStarted de la estación
    /// - Modo ModifyCurrent: Crea snapshot, valida al salir
    /// - Modo CreateNew: Sin snapshot, puede salir incompleto
    /// </summary>
    public class AssemblyTester : MonoBehaviour
    {
        [Header("Piezas de Prueba - Armadura")]
        [Tooltip("Lista de piezas de armadura para probar")]
        [SerializeField] private List<ArmorPartData> testArmorParts = new List<ArmorPartData>();
        
        [Header("Piezas de Prueba - Estructurales")]
        [Tooltip("Lista de piezas estructurales para probar")]
        [SerializeField] private List<StructuralPartData> testStructuralParts = new List<StructuralPartData>();
        
        [Header("Configuración")]
        [SerializeField] private float gridDetectionRadius = 0.5f;
        [SerializeField] private LayerMask gridLayerMask = -1;
        [SerializeField] private float editModeRotationSpeed = 100f;
        
        [Header("Referencias (Auto-asignadas)")]
        [SerializeField] private Robot targetRobot;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private PlayerCamera playerCamera;
        
        // Referencia a la estación actual
        private AssemblyStation currentStation;
        private StationEditMode stationEditMode = StationEditMode.None;
        
        // Estado interno - Modo
        private AssemblyMode currentMode = AssemblyMode.Armor;
        private bool isActive = false;
        
        // Snapshot para restaurar si la configuración es inválida
        private RobotSnapshot editSnapshot = null;
        private bool pendingValidation = false;
        
        // Estado interno - Armor
        private List<GridHead> availableGrids = new List<GridHead>();
        private GridHead hoveredGrid = null;
        private int currentArmorIndex = 0;
        private int currentPositionX = 0;
        private int currentPositionY = 0;
        private GridRotation.Rotation currentRotation = GridRotation.Rotation.Deg0;
        
        // Estado interno - Structural
        private List<StructuralSocket> availableSockets = new List<StructuralSocket>();
        private StructuralSocket hoveredSocket = null;
        private int currentStructuralIndex = 0;
        
        // Preview con pivote centrado (para armor)
        private GameObject previewObject;
        private GameObject pivotContainer;
        private GameObject modelContainer;
        private List<MeshRenderer> previewRenderers = new List<MeshRenderer>();
        private Material previewMaterial;
        private ArmorPartData currentPreviewData;
        
        // Preview para structural
        private GameObject structuralPreviewObject;
        private StructuralPartData currentStructuralPreviewData;
        
        // Outline shader y materiales
        private Shader outlineShader;
        
        // Grid outline (para armor) - verde
        private Dictionary<MeshRenderer, Material[]> originalGridMaterials = new Dictionary<MeshRenderer, Material[]>();
        private Dictionary<MeshRenderer, List<Material>> gridOutlineMaterials = new Dictionary<MeshRenderer, List<Material>>();
        
        // Socket outline (para structural) - azul
        private Dictionary<StructuralSocket, Material[]> originalSocketMaterials = new Dictionary<StructuralSocket, Material[]>();
        private Dictionary<StructuralSocket, List<Material>> socketOutlineMaterials = new Dictionary<StructuralSocket, List<Material>>();
        
        private void Start()
        {
            CreatePreviewObjects();
            CreateHighlightMaterials();
            
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
            
            // Suscribirse a todas las estaciones existentes
            SubscribeToAllStations();
        }
        
        private void SubscribeToAllStations()
        {
            var stations = FindObjectsOfType<AssemblyStation>();
            foreach (var station in stations)
            {
                station.OnEditModeStarted += OnStationEditModeStarted;
                station.OnEditModeEnded += OnStationEditModeEnded;
            }
            Debug.Log($"AssemblyTester: Suscrito a {stations.Length} estaciones de ensamblaje");
        }
        
        private void UnsubscribeFromAllStations()
        {
            var stations = FindObjectsOfType<AssemblyStation>();
            foreach (var station in stations)
            {
                station.OnEditModeStarted -= OnStationEditModeStarted;
                station.OnEditModeEnded -= OnStationEditModeEnded;
            }
        }
        
        private void OnStationEditModeStarted(AssemblyStation station, StationEditMode mode)
        {
            currentStation = station;
            stationEditMode = mode;
            
            Debug.Log($"AssemblyTester: Estación activó modo {mode}");
            
            if (mode == StationEditMode.EditCurrentRobot)
            {
                // Editar robot con Core (plataforma del jugador) - usa snapshot
                ActivateTesterForCurrentRobot();
            }
            else if (mode == StationEditMode.EditOtherPlatform)
            {
                // Editar cascarón (otra plataforma) - sin snapshot
                ActivateTesterForOtherPlatform();
            }
        }
        
        private void OnStationEditModeEnded(AssemblyStation station)
        {
            if (station == currentStation && isActive)
            {
                DeactivateTester();
            }
        }
        
        private void Update()
        {
            // La tecla P ya no activa/desactiva directamente
            // Ahora se maneja a través de AssemblyStation
            
            // ESC para salir (si está activo)
            if (isActive && Input.GetKeyDown(KeyCode.Escape))
            {
                // Notificar a la estación que queremos salir
                if (currentStation != null)
                {
                    currentStation.ExitEditMode();
                }
                return;
            }
            
            if (!isActive) return;
            
            // Rotación del robot en modo edición (A/D) - DESHABILITADO
            // Ahora la cámara se controla con click derecho sostenido
            // HandleEditModeRotation();
            
            // Insertar/Extraer Core con C
            if (Input.GetKeyDown(KeyCode.C))
            {
                TogglePlayerCore();
            }
            
            // Cambiar modo con Tab
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                ToggleMode();
            }
            
            // Input común
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                DeactivateTester();
            }
            
            // Input específico del modo
            if (currentMode == AssemblyMode.Armor)
            {
                UpdateArmorMode();
            }
            else
            {
                UpdateStructuralMode();
            }
        }
        
        private void TogglePlayerCore()
        {
            if (targetRobot == null) return;
            
            // Buscar el Core del jugador (marcado como isPlayerCore)
            RobotCore playerCore = FindPlayerCore();
            
            // Si no hay core del jugador marcado, usar el core del robot actual
            if (playerCore == null)
            {
                playerCore = targetRobot.Core;
                if (playerCore != null)
                {
                    // Marcar este core como del jugador
                    playerCore.SetAsPlayerCore(true);
                    Debug.Log("Core del robot marcado como Core del jugador.");
                    return;
                }
                else
                {
                    Debug.LogWarning("El robot no tiene Core.");
                    return;
                }
            }
            
            if (playerCore.IsActive && playerCore.CurrentRobot == targetRobot)
            {
                // Extraer el Core del robot actual
                playerCore.Extract();
                Debug.Log("Core extraído del robot.");
            }
            else if (!playerCore.IsActive)
            {
                // Insertar el Core en el robot
                if (playerCore.InsertInto(targetRobot))
                {
                    Debug.Log("Core insertado en el robot.");
                }
            }
            else
            {
                // El Core está en otro robot - transferirlo
                playerCore.Extract();
                if (playerCore.InsertInto(targetRobot))
                {
                    Debug.Log("Core transferido al robot.");
                }
            }
        }
        
        private RobotCore FindPlayerCore()
        {
            RobotCore[] cores = FindObjectsOfType<RobotCore>();
            foreach (var core in cores)
            {
                if (core.IsPlayerCore)
                {
                    return core;
                }
            }
            return null;
        }
        
        private void HandleEditModeRotation()
        {
            if (targetRobot == null) return;
            
            float rotation = 0f;
            
            if (Input.GetKey(KeyCode.A))
            {
                rotation = -1f;
            }
            else if (Input.GetKey(KeyCode.D))
            {
                rotation = 1f;
            }
            
            if (rotation != 0f)
            {
                targetRobot.transform.Rotate(0f, rotation * editModeRotationSpeed * Time.deltaTime, 0f);
            }
        }
        
        #region Mode Management
        
        private void ToggleMode()
        {
            if (currentMode == AssemblyMode.Armor)
            {
                currentMode = AssemblyMode.Structural;
                HideArmorHighlights();
                CollectAvailableSockets();
                UpdateSocketHighlights();
            }
            else
            {
                currentMode = AssemblyMode.Armor;
                HideSocketHighlights();
                CollectAvailableGrids();
                UpdateGridHighlights();
            }
            
            // Reset selection
            hoveredGrid = null;
            hoveredSocket = null;
            currentPositionX = 0;
            currentPositionY = 0;
        }
        
        #endregion
        
        #region Armor Mode
        
        private void UpdateArmorMode()
        {
            DetectGridUnderMouse();
            
            if (Input.GetMouseButtonDown(0))
            {
                TryPlaceArmor();
            }
            
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                ChangeArmorPiece(-1);
            }
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                ChangeArmorPiece(1);
            }
            
            // Mover posición en grilla con flechas
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                MovePosition(0, 1);
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                MovePosition(0, -1);
            }
            
            // Nota: A/D ahora rotan el robot, no mueven la posición
            // Usar flechas izq/der con Shift para mover posición X si se necesita
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    MovePosition(-1, 0);
                }
                if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    MovePosition(1, 0);
                }
            }
            
            if (Input.GetKeyDown(KeyCode.R))
            {
                RotatePiece();
            }
            
            // Remover pieza con X, Delete, o Click derecho
            if (Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Delete))
            {
                TryRemoveArmor();
            }
            if (Input.GetMouseButtonDown(1))
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
            
            // QueryTriggerInteraction.Collide para detectar colliders trigger
            if (Physics.Raycast(ray, out hit, 100f, gridLayerMask, QueryTriggerInteraction.Collide))
            {
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
                
                UpdateGridHighlights();
            }
        }
        
        private void CalculateGridPosition(GridHead grid, Vector3 worldPoint)
        {
            Vector3 localPoint = grid.transform.InverseTransformPoint(worldPoint);
            
            int cellX = Mathf.FloorToInt(localPoint.x / 0.1f);
            int cellY = Mathf.FloorToInt(localPoint.y / 0.1f);
            
            var armorData = GetCurrentArmorData();
            if (armorData == null) return;
            
            var rotatedSize = GetRotatedSize(armorData);
            
            int maxX = Mathf.Max(0, grid.GridInfo.sizeX - rotatedSize.x);
            int maxY = Mathf.Max(0, grid.GridInfo.sizeY - rotatedSize.y);
            
            currentPositionX = Mathf.Clamp(cellX, 0, maxX);
            currentPositionY = Mathf.Clamp(cellY, 0, maxY);
        }
        
        private void ChangeArmorPiece(int direction)
        {
            if (testArmorParts.Count == 0) return;
            
            currentArmorIndex += direction;
            
            if (currentArmorIndex < 0)
                currentArmorIndex = testArmorParts.Count - 1;
            else if (currentArmorIndex >= testArmorParts.Count)
                currentArmorIndex = 0;
            
            currentPositionX = 0;
            currentPositionY = 0;
            currentRotation = GridRotation.Rotation.Deg0;
            currentPreviewData = null;
        }
        
        private void RotatePiece()
        {
            currentRotation = GridRotation.RotateClockwise(currentRotation);
            
            if (hoveredGrid != null)
            {
                var armorData = GetCurrentArmorData();
                if (armorData != null)
                {
                    var rotatedSize = GetRotatedSize(armorData);
                    int maxX = Mathf.Max(0, hoveredGrid.GridInfo.sizeX - rotatedSize.x);
                    int maxY = Mathf.Max(0, hoveredGrid.GridInfo.sizeY - rotatedSize.y);
                    
                    currentPositionX = Mathf.Clamp(currentPositionX, 0, maxX);
                    currentPositionY = Mathf.Clamp(currentPositionY, 0, maxY);
                }
            }
        }
        
        private void MovePosition(int deltaX, int deltaY)
        {
            if (hoveredGrid == null) return;
            
            var armor = GetCurrentArmorData();
            if (armor == null) return;
            
            var rotatedSize = GetRotatedSize(armor);
            
            int maxX = Mathf.Max(0, hoveredGrid.GridInfo.sizeX - rotatedSize.x);
            int maxY = Mathf.Max(0, hoveredGrid.GridInfo.sizeY - rotatedSize.y);
            
            currentPositionX = Mathf.Clamp(currentPositionX + deltaX, 0, maxX);
            currentPositionY = Mathf.Clamp(currentPositionY + deltaY, 0, maxY);
        }
        
        private void TryPlaceArmor()
        {
            if (hoveredGrid == null) return;
            
            var armorData = GetCurrentArmorData();
            if (armorData == null) return;
            
            // Las armaduras no tienen restricción de tier (son estéticas)
            
            if (!hoveredGrid.CanPlace(armorData, currentPositionX, currentPositionY, currentRotation))
            {
                return;
            }
            
            ArmorPart armorPart = RobotFactory.Instance.CreateArmorPart(armorData);
            
            if (armorPart == null)
            {
                Debug.LogError("Error al crear la pieza de armadura.");
                return;
            }
            
            if (hoveredGrid.TryPlace(armorPart, currentPositionX, currentPositionY, currentRotation))
            {
                if (armorPart.AdditionalGrids != null && armorPart.AdditionalGrids.Count > 0)
                {
                    RefreshAvailableGrids();
                }
            }
            else
            {
                Debug.LogError("Error al colocar la pieza en la grilla.");
                Destroy(armorPart.gameObject);
            }
        }
        
        private void TryRemoveArmor()
        {
            if (hoveredGrid == null) return;
            
            ArmorPart partToRemove = hoveredGrid.GetPartAtCell(currentPositionX, currentPositionY);
            
            if (partToRemove == null) return;
            
            bool hadAdditionalGrids = partToRemove.AdditionalGrids != null && partToRemove.AdditionalGrids.Count > 0;
            
            if (hoveredGrid.Remove(partToRemove, true))
            {
                if (hadAdditionalGrids)
                {
                    RefreshAvailableGrids();
                }
            }
        }
        
        private ArmorPartData GetCurrentArmorData()
        {
            if (testArmorParts.Count == 0) return null;
            return testArmorParts[currentArmorIndex];
        }
        
        private Vector2Int GetRotatedSize(ArmorPartData armorData)
        {
            if (armorData == null || armorData.tailGrid == null)
            {
                return Vector2Int.zero;
            }
            
            return GridRotation.RotateSize(
                armorData.tailGrid.gridInfo.sizeX,
                armorData.tailGrid.gridInfo.sizeY,
                currentRotation
            );
        }
        
        #endregion
        
        #region Structural Mode
        
        private void UpdateStructuralMode()
        {
            DetectSocketUnderMouse();
            
            if (Input.GetMouseButtonDown(0))
            {
                TryPlaceStructural();
            }
            
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                ChangeStructuralPiece(-1);
            }
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                ChangeStructuralPiece(1);
            }
            
            // Remover pieza con X, Delete, o Click derecho
            if (Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Delete))
            {
                TryRemoveStructural();
            }
            if (Input.GetMouseButtonDown(1))
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
            
            // QueryTriggerInteraction.Collide para detectar colliders trigger
            if (Physics.Raycast(ray, out hit, 100f, ~0, QueryTriggerInteraction.Collide))
            {
                // Buscar socket en el objeto golpeado o sus padres
                StructuralSocket socket = hit.collider.GetComponent<StructuralSocket>();
                if (socket == null)
                {
                    socket = hit.collider.GetComponentInParent<StructuralSocket>();
                }
                
                if (socket != null && availableSockets.Contains(socket))
                {
                    newHoveredSocket = socket;
                }
            }
            
            if (newHoveredSocket != hoveredSocket)
            {
                hoveredSocket = newHoveredSocket;
                UpdateSocketHighlights();
            }
        }
        
        private void ChangeStructuralPiece(int direction)
        {
            if (testStructuralParts.Count == 0) return;
            
            currentStructuralIndex += direction;
            
            if (currentStructuralIndex < 0)
                currentStructuralIndex = testStructuralParts.Count - 1;
            else if (currentStructuralIndex >= testStructuralParts.Count)
                currentStructuralIndex = 0;
            
            currentStructuralPreviewData = null;
            
            // Actualizar sockets visibles para la nueva pieza
            UpdateSocketHighlights();
        }
        
        private void TryPlaceStructural()
        {
            if (hoveredSocket == null) return;
            
            var structuralData = GetCurrentStructuralData();
            if (structuralData == null) return;
            
            // Verificar compatibilidad de tipo
            if (structuralData.partType != hoveredSocket.SocketType)
            {
                return;
            }
            
            // Validar compatibilidad de tier
            if (!structuralData.IsCompatibleWith(targetRobot.CurrentTier))
            {
                Debug.LogWarning($"Pieza '{structuralData.displayName}' (Tier {structuralData.tier}) no es compatible con el robot (Tier {targetRobot.CurrentTier})");
                return;
            }
            
            // Verificar si el socket está ocupado
            if (hoveredSocket.IsOccupied)
            {
                return;
            }
            
            // Crear la pieza (el parent es el socket)
            StructuralPart part = RobotFactory.Instance.CreateStructuralPart(structuralData, hoveredSocket.transform);
            
            if (part == null)
            {
                Debug.LogError("Error al crear la pieza estructural.");
                return;
            }
            
            // Caso especial: si es el HipsSocket, usar AttachHips del Robot
            if (hoveredSocket == targetRobot.HipsSocket)
            {
                if (targetRobot.AttachHips(part))
                {
                    // Forzar sincronización de física para que los nuevos colliders sean detectables
                    Physics.SyncTransforms();
                    
                    RefreshAvailableSockets();
                    RefreshAvailableGrids();
                }
                else
                {
                    Debug.LogError("Error al conectar las Hips al robot.");
                    Destroy(part.gameObject);
                }
            }
            else
            {
                // Intentar conectar al socket (esto reposiciona la pieza)
                if (hoveredSocket.TryAttach(part))
                {
                    // Forzar sincronización de física para que los nuevos colliders sean detectables
                    Physics.SyncTransforms();
                    
                    RefreshAvailableSockets();
                    RefreshAvailableGrids();
                }
                else
                {
                    Debug.LogError("Error al conectar la pieza al socket.");
                    Destroy(part.gameObject);
                }
            }
        }
        
        private void TryRemoveStructural()
        {
            if (hoveredSocket == null) return;
            
            if (!hoveredSocket.IsOccupied) return;
            
            StructuralPart partToRemove = hoveredSocket.AttachedPart;
            
            if (partToRemove == null) return;
            
            // Caso especial: si es el HipsSocket
            bool isHips = (hoveredSocket == targetRobot.HipsSocket);
            
            // IMPORTANTE: Extraer el Core ANTES de destruir si está en esta rama
            ExtractCoreIfPresent(partToRemove);
            
            // Remover recursivamente las piezas hijas y armaduras
            RemoveStructuralPartChildren(partToRemove);
            
            // Desconectar del socket
            if (isHips)
            {
                targetRobot.DetachHips();
            }
            else
            {
                hoveredSocket.Detach();
            }
            
            // Destruir el GameObject
            Destroy(partToRemove.gameObject);
            
            // Refrescar sockets y grillas
            RefreshAvailableSockets();
            RefreshAvailableGrids();
        }
        
        /// <summary>
        /// Extrae el Core del jugador si está dentro de esta parte o sus hijos.
        /// </summary>
        private void ExtractCoreIfPresent(StructuralPart part)
        {
            // Buscar el Core del jugador
            RobotCore playerCore = FindPlayerCore();
            
            if (playerCore == null || !playerCore.IsActive) return;
            
            // Verificar si el Core está dentro de esta parte o sus hijos
            if (playerCore.transform.IsChildOf(part.transform))
            {
                // Extraer el Core antes de destruir
                playerCore.Extract();
                
                // Mover el Core a una posición visible (encima del robot)
                playerCore.transform.position = targetRobot.transform.position + Vector3.up * 2f;
                
                Debug.Log("Core extraído automáticamente antes de destruir la estructura.");
            }
        }
        
        /// <summary>
        /// Remueve recursivamente los hijos y armaduras de una parte estructural.
        /// NO desconecta ni destruye la parte en sí.
        /// </summary>
        private void RemoveStructuralPartChildren(StructuralPart part)
        {
            // Primero remover hijos recursivamente
            foreach (var childSocket in part.ChildSockets)
            {
                if (childSocket.IsOccupied && childSocket.AttachedPart != null)
                {
                    StructuralPart childPart = childSocket.AttachedPart;
                    RemoveStructuralPartChildren(childPart);
                    childSocket.Detach();
                    Destroy(childPart.gameObject);
                }
            }
            
            // Remover todas las armaduras de esta parte
            foreach (var grid in part.ArmorGrids)
            {
                grid.RemoveAll(true);
            }
        }
        
        private StructuralPartData GetCurrentStructuralData()
        {
            if (testStructuralParts.Count == 0) return null;
            return testStructuralParts[currentStructuralIndex];
        }
        
        #endregion
        
        #region Collection Methods
        
        private void CollectAvailableGrids()
        {
            availableGrids.Clear();
            
            var structuralParts = targetRobot.GetAllStructuralParts();
            
            foreach (var part in structuralParts)
            {
                foreach (var grid in part.ArmorGrids)
                {
                    availableGrids.Add(grid);
                    EnsureGridCollider(grid);
                }
                
                CollectArmorPartGrids(part.ArmorGrids);
            }
        }
        
        private void CollectArmorPartGrids(IReadOnlyList<GridHead> grids)
        {
            foreach (var grid in grids)
            {
                foreach (var armorPart in grid.PlacedParts)
                {
                    if (armorPart.AdditionalGrids != null && armorPart.AdditionalGrids.Count > 0)
                    {
                        foreach (var additionalGrid in armorPart.AdditionalGrids)
                        {
                            if (!availableGrids.Contains(additionalGrid))
                            {
                                availableGrids.Add(additionalGrid);
                                EnsureGridCollider(additionalGrid);
                                
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
            
            // Agregar el HipsSocket del robot (siempre disponible)
            if (targetRobot.HipsSocket != null)
            {
                availableSockets.Add(targetRobot.HipsSocket);
                EnsureSocketCollider(targetRobot.HipsSocket);
            }
            
            // Agregar sockets de todas las partes estructurales
            var structuralParts = targetRobot.GetAllStructuralParts();
            
            foreach (var part in structuralParts)
            {
                foreach (var socket in part.ChildSockets)
                {
                    availableSockets.Add(socket);
                    EnsureSocketCollider(socket);
                }
            }
        }
        
        private void RefreshAvailableGrids()
        {
            DestroyGridHighlights();
            CollectAvailableGrids();
            if (currentMode == AssemblyMode.Armor)
            {
                CreateGridHighlights();
            }
        }
        
        private void RefreshAvailableSockets()
        {
            RestoreSocketMaterials();
            CollectAvailableSockets();
            if (currentMode == AssemblyMode.Structural)
            {
                UpdateSocketHighlights();
            }
        }
        
        #endregion
        
        #region Activation
        
        private void ActivateTester()
        {
            // Método legacy - ya no se usa directamente
            ActivateTesterForCurrentRobot();
        }
        
        /// <summary>
        /// Activa el modo edición para el robot actual (donde está el jugador, con Core).
        /// Usa snapshot - si sale con config inválida, restaura.
        /// </summary>
        private void ActivateTesterForCurrentRobot()
        {
            if (currentStation == null)
            {
                Debug.LogError("AssemblyTester: No hay estación activa");
                return;
            }
            
            // El robot a editar es el de la plataforma del jugador
            targetRobot = currentStation.PlayerPlatform?.CurrentRobot;
            
            if (targetRobot == null)
            {
                Debug.LogError("AssemblyTester: No hay robot en tu plataforma");
                return;
            }
            
            if (!PrepareForActivation()) return;
            
            // CON snapshot
            editSnapshot = RobotSnapshot.Capture(targetRobot);
            pendingValidation = true;
            
            FinishActivation();
            
            Debug.Log("AssemblyTester activado - Robot actual (CON snapshot)");
        }
        
        /// <summary>
        /// Activa el modo edición para la otra plataforma (cascarón).
        /// Sin snapshot - puede salir incompleto.
        /// Centra la cámara en el robot de la otra plataforma.
        /// </summary>
        private void ActivateTesterForOtherPlatform()
        {
            if (currentStation == null)
            {
                Debug.LogError("AssemblyTester: No hay estación activa");
                return;
            }
            
            // El robot a editar es el de la otra plataforma
            targetRobot = currentStation.OtherPlatform?.CurrentRobot;
            
            if (targetRobot == null)
            {
                Debug.LogError("AssemblyTester: No hay robot en la otra plataforma");
                return;
            }
            
            if (!PrepareForActivation()) return;
            
            // SIN snapshot
            editSnapshot = null;
            pendingValidation = false;
            
            // Centrar cámara en el robot de la otra plataforma
            SetCameraTarget(targetRobot.transform);
            
            FinishActivation();
            
            Debug.Log("AssemblyTester activado - Otra plataforma (SIN snapshot)");
        }
        
        /// <summary>
        /// Preparación común para ambos modos de activación.
        /// </summary>
        private bool PrepareForActivation()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    Debug.LogError("AssemblyTester: No se encontró cámara principal.");
                    return false;
                }
            }
            
            // Buscar PlayerCamera
            if (playerCamera == null)
            {
                playerCamera = FindObjectOfType<PlayerCamera>();
            }
            
            // Desactivar movimiento del Core del jugador
            RobotCore playerCore = FindPlayerCore();
            if (playerCore != null)
            {
                playerCore.DisableMovement();
            }
            
            // Notificar a la cámara que entramos en modo edición
            if (playerCamera != null)
            {
                playerCamera.EnterEditMode();
            }
            
            return true;
        }
        
        /// <summary>
        /// Cambia el objetivo de la cámara.
        /// </summary>
        private void SetCameraTarget(Transform newTarget)
        {
            if (playerCamera != null)
            {
                playerCamera.SetTarget(newTarget, false);
            }
        }
        
        /// <summary>
        /// Finalización común para ambos modos de activación.
        /// </summary>
        private void FinishActivation()
        {
            isActive = true;
            currentMode = AssemblyMode.Armor;
            
            CollectAvailableGrids();
            CollectAvailableSockets();
            CreateGridHighlights();
            previewObject.SetActive(false);
        }
        
        private void DeactivateTester()
        {
            // Si hay snapshot (modo ModifyCurrent), validar antes de salir
            if (editSnapshot != null && pendingValidation)
            {
                if (!ValidateAndHandleExit())
                {
                    return; // No salir si el usuario cancela
                }
            }
            
            // Limpiar referencia a la estación
            currentStation = null;
            stationEditMode = StationEditMode.None;
            
            FinishDeactivation();
        }
        
        private bool ValidateAndHandleExit()
        {
            // Capturar estado actual
            RobotSnapshot currentState = RobotSnapshot.Capture(targetRobot);
            
            if (currentState == null)
            {
                // No hay robot, restaurar
                Debug.LogWarning("No hay robot. Restaurando desde snapshot...");
                RestoreFromSnapshot();
                return true;
            }
            
            // Validar configuración
            List<string> errors;
            if (!currentState.IsValid(out errors))
            {
                // Configuración inválida
                string errorMessage = "Configuración inválida:\n";
                foreach (var error in errors)
                {
                    errorMessage += $"• {error}\n";
                }
                Debug.LogWarning(errorMessage + "\nRestaurando configuración anterior...");
                
                RestoreFromSnapshot();
                return true;
            }
            
            // Configuración válida, guardar cambios
            editSnapshot = null;
            return true;
        }
        
        private void RestoreFromSnapshot()
        {
            if (editSnapshot == null)
            {
                Debug.LogWarning("No hay snapshot para restaurar.");
                return;
            }
            
            RobotCore playerCore = FindPlayerCore();
            Robot restoredRobot = editSnapshot.Restore(targetRobot, playerCore);
            
            if (restoredRobot != null)
            {
                targetRobot = restoredRobot;
                Debug.Log("Configuración restaurada desde snapshot.");
            }
            else
            {
                Debug.LogError("Error al restaurar desde snapshot.");
            }
            
            editSnapshot = null;
        }
        
        private void FinishDeactivation()
        {
            isActive = false;
            previewObject.SetActive(false);
            if (structuralPreviewObject != null)
            {
                structuralPreviewObject.SetActive(false);
            }
            DestroyGridHighlights();
            RestoreSocketMaterials();
            hoveredGrid = null;
            hoveredSocket = null;
            
            // Reactivar movimiento del Core del jugador
            RobotCore playerCore = FindPlayerCore();
            if (playerCore != null)
            {
                playerCore.EnableMovement();
            }
            
            // La cámara SIEMPRE debe volver al robot con Core (el que controla el jugador)
            Robot playerRobot = null;
            if (playerCore != null && playerCore.CurrentRobot != null)
            {
                playerRobot = playerCore.CurrentRobot;
            }
            
            // Salir del modo edición de la cámara y centrar en robot del jugador
            if (playerCamera != null)
            {
                playerCamera.ExitEditMode();
                if (playerRobot != null)
                {
                    playerCamera.SetTarget(playerRobot.transform, false);
                }
            }
        }
        
        #endregion
        
        #region Colliders
        
        private void EnsureGridCollider(GridHead grid)
        {
            if (grid == null) return;
            
            // Usar el método del componente GridHead
            grid.EnsureCollider();
        }
        
        private void EnsureSocketCollider(StructuralSocket socket)
        {
            if (socket == null) return;
            
            // Usar el método del componente StructuralSocket
            socket.EnsureCollider();
        }
        
        #endregion
        
        #region Visual Highlights - Grids
        
        private void CreateGridHighlights()
        {
            // Los highlights de grillas ahora se aplican a los meshes hijos
            // No creamos objetos nuevos, solo aplicamos el outline shader
            foreach (var grid in availableGrids)
            {
                ApplyGridOutline(grid, false);
            }
        }
        
        private void ApplyGridOutline(GridHead grid, bool isHovered)
        {
            // Solo aplicar outline al MeshRenderer del GameObject de la grilla
            // NO a los hijos (que serían las piezas de armadura colocadas)
            MeshRenderer renderer = grid.GetComponent<MeshRenderer>();
            
            if (renderer == null) return;
            
            // Guardar materiales originales si no los tenemos
            if (!originalGridMaterials.ContainsKey(renderer))
            {
                originalGridMaterials[renderer] = renderer.sharedMaterials;
            }
            
            // Crear material de outline si no existe
            if (outlineShader == null)
            {
                outlineShader = Shader.Find("RobotGame/OutlineHighlight");
                if (outlineShader == null)
                {
                    // Fallback si no encuentra el shader
                    outlineShader = Shader.Find("Sprites/Default");
                }
            }
            
            // Aplicar outline verde para grillas
            Material outlineMat = new Material(outlineShader);
            Color outlineColor = isHovered ? 
                new Color(0f, 1f, 0.3f, 1f) :  // Verde brillante cuando hover
                new Color(0f, 0.8f, 0.2f, 0.8f); // Verde normal
            outlineMat.SetColor("_OutlineColor", outlineColor);
            outlineMat.SetFloat("_OutlineWidth", isHovered ? 0.025f : 0.015f);
            
            // Agregar el material de outline además del original
            Material[] newMats = new Material[renderer.sharedMaterials.Length + 1];
            for (int i = 0; i < renderer.sharedMaterials.Length; i++)
            {
                newMats[i] = renderer.sharedMaterials[i];
            }
            newMats[newMats.Length - 1] = outlineMat;
            renderer.materials = newMats;
            
            // Guardar referencia al material de outline para limpieza
            if (!gridOutlineMaterials.ContainsKey(renderer))
            {
                gridOutlineMaterials[renderer] = new List<Material>();
            }
            gridOutlineMaterials[renderer].Add(outlineMat);
        }
        
        private void UpdateGridHighlights()
        {
            // Limpiar outlines anteriores
            CleanupGridOutlines();
            
            // Aplicar outlines a todas las grillas
            foreach (var grid in availableGrids)
            {
                ApplyGridOutline(grid, grid == hoveredGrid);
            }
        }
        
        private void CleanupGridOutlines()
        {
            // Restaurar materiales originales
            foreach (var kvp in originalGridMaterials)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.materials = kvp.Value;
                }
            }
            originalGridMaterials.Clear();
            
            // Destruir materiales de outline creados
            foreach (var kvp in gridOutlineMaterials)
            {
                foreach (var mat in kvp.Value)
                {
                    if (mat != null) Destroy(mat);
                }
            }
            gridOutlineMaterials.Clear();
        }
        
        private void HideArmorHighlights()
        {
            CleanupGridOutlines();
        }
        
        private void DestroyGridHighlights()
        {
            CleanupGridOutlines();
        }
        
        #endregion
        
        #region Visual Highlights - Sockets
        
        private void CreateHighlightMaterials()
        {
            // Cargar el shader de outline
            outlineShader = Shader.Find("RobotGame/OutlineHighlight");
            if (outlineShader == null)
            {
                Debug.LogWarning("AssemblyTester: No se encontró el shader RobotGame/OutlineHighlight. Usando fallback.");
                outlineShader = Shader.Find("Sprites/Default");
            }
        }
        
        private void UpdateSocketHighlights()
        {
            // Restaurar materiales anteriores
            RestoreSocketMaterials();
            
            var currentStructural = GetCurrentStructuralData();
            if (currentStructural == null) return;
            
            // Aplicar highlight a sockets compatibles
            foreach (var socket in availableSockets)
            {
                // Solo mostrar sockets del tipo correcto y no ocupados
                if (socket.SocketType == currentStructural.partType && !socket.IsOccupied)
                {
                    ApplySocketHighlight(socket, socket == hoveredSocket);
                }
            }
        }
        
        private void ApplySocketHighlight(StructuralSocket socket, bool isHovered)
        {
            MeshRenderer renderer = socket.GetComponent<MeshRenderer>();
            if (renderer == null) return;
            
            // Guardar materiales originales
            if (!originalSocketMaterials.ContainsKey(socket))
            {
                originalSocketMaterials[socket] = renderer.sharedMaterials;
            }
            
            // Crear material de outline azul para sockets estructurales
            Material outlineMat = new Material(outlineShader);
            Color outlineColor = isHovered ? 
                new Color(0.3f, 0.5f, 1f, 1f) :  // Azul brillante cuando hover
                new Color(0.2f, 0.4f, 0.9f, 0.8f); // Azul normal
            outlineMat.SetColor("_OutlineColor", outlineColor);
            outlineMat.SetFloat("_OutlineWidth", isHovered ? 0.025f : 0.015f);
            
            // Agregar el material de outline además del original
            Material[] newMats = new Material[renderer.sharedMaterials.Length + 1];
            for (int i = 0; i < renderer.sharedMaterials.Length; i++)
            {
                newMats[i] = renderer.sharedMaterials[i];
            }
            newMats[newMats.Length - 1] = outlineMat;
            renderer.materials = newMats;
            
            // Guardar referencia al material para limpieza
            if (!socketOutlineMaterials.ContainsKey(socket))
            {
                socketOutlineMaterials[socket] = new List<Material>();
            }
            socketOutlineMaterials[socket].Add(outlineMat);
        }
        
        private void RestoreSocketMaterials()
        {
            // Restaurar materiales originales
            foreach (var kvp in originalSocketMaterials)
            {
                if (kvp.Key != null)
                {
                    MeshRenderer renderer = kvp.Key.GetComponent<MeshRenderer>();
                    if (renderer != null && kvp.Value != null)
                    {
                        renderer.materials = kvp.Value;
                    }
                }
            }
            originalSocketMaterials.Clear();
            
            // Destruir materiales de outline creados
            foreach (var kvp in socketOutlineMaterials)
            {
                foreach (var mat in kvp.Value)
                {
                    if (mat != null) Destroy(mat);
                }
            }
            socketOutlineMaterials.Clear();
        }
        
        private void HideSocketHighlights()
        {
            RestoreSocketMaterials();
        }
        
        #endregion
        
        #region Preview - Armor
        
        private void CreatePreviewObjects()
        {
            // Preview para armor
            previewObject = new GameObject("ArmorPreview");
            
            pivotContainer = new GameObject("PivotContainer");
            pivotContainer.transform.SetParent(previewObject.transform);
            
            modelContainer = new GameObject("ModelContainer");
            modelContainer.transform.SetParent(pivotContainer.transform);
            
            previewObject.SetActive(false);
            
            previewMaterial = new Material(Shader.Find("Sprites/Default"));
            previewMaterial.color = new Color(0f, 0.5f, 1f, 0.5f);
            
            // Preview para structural
            structuralPreviewObject = new GameObject("StructuralPreview");
            structuralPreviewObject.SetActive(false);
        }
        
        private void UpdateArmorPreview()
        {
            var armorData = GetCurrentArmorData();
            
            UpdateArmorPreviewModel();
            
            if (armorData == null || armorData.prefab == null)
            {
                previewObject.SetActive(false);
                return;
            }
            
            previewObject.SetActive(true);
            
            int originalSizeX = armorData.tailGrid.gridInfo.sizeX;
            int originalSizeY = armorData.tailGrid.gridInfo.sizeY;
            
            var rotatedSize = GetRotatedSize(armorData);
            
            float centerX = rotatedSize.x * 0.05f;
            float centerY = rotatedSize.y * 0.05f;
            
            pivotContainer.transform.localPosition = new Vector3(centerX, centerY, 0f);
            
            float originalCenterX = originalSizeX * 0.05f;
            float originalCenterY = originalSizeY * 0.05f;
            modelContainer.transform.localPosition = new Vector3(-originalCenterX, -originalCenterY, 0f);
            
            pivotContainer.transform.localRotation = GridRotation.ToQuaternion(currentRotation);
            
            Color previewColor;
            
            // Las armaduras no tienen restricción de tier (son estéticas)
            // Solo validamos si cabe en la grilla
            
            if (hoveredGrid == null)
            {
                // Sin grid: azul (holográfico)
                previewColor = new Color(0f, 0.5f, 1f, 0.5f);
                
                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                Vector3 worldPos = ray.origin + ray.direction * 2f;
                previewObject.transform.position = worldPos;
                previewObject.transform.rotation = Quaternion.identity;
            }
            else
            {
                bool canPlace = hoveredGrid.CanPlace(armorData, currentPositionX, currentPositionY, currentRotation);
                
                // Verde si puede colocar, rojo si no
                if (canPlace)
                {
                    previewColor = new Color(0f, 1f, 0f, 0.5f); // Verde
                }
                else
                {
                    previewColor = new Color(1f, 0f, 0f, 0.5f); // Rojo
                }
                
                Vector3 cellPos = hoveredGrid.CellToWorldPosition(currentPositionX, currentPositionY);
                previewObject.transform.position = cellPos;
                previewObject.transform.rotation = hoveredGrid.transform.rotation;
            }
            
            previewMaterial.color = previewColor;
        }
        
        private void UpdateArmorPreviewModel()
        {
            var armorData = GetCurrentArmorData();
            
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
                    modelInstance.transform.localScale = Vector3.one;
                    
                    var components = modelInstance.GetComponentsInChildren<MonoBehaviour>();
                    foreach (var comp in components)
                    {
                        comp.enabled = false;
                    }
                    
                    var colliders = modelInstance.GetComponentsInChildren<Collider>();
                    foreach (var col in colliders)
                    {
                        col.enabled = false;
                    }
                    
                    var renderers = modelInstance.GetComponentsInChildren<MeshRenderer>();
                    foreach (var renderer in renderers)
                    {
                        Material[] mats = new Material[renderer.sharedMaterials.Length];
                        for (int i = 0; i < mats.Length; i++)
                        {
                            mats[i] = previewMaterial;
                        }
                        renderer.materials = mats;
                        previewRenderers.Add(renderer);
                    }
                    
                    var skinnedRenderers = modelInstance.GetComponentsInChildren<SkinnedMeshRenderer>();
                    foreach (var renderer in skinnedRenderers)
                    {
                        Material[] mats = new Material[renderer.sharedMaterials.Length];
                        for (int i = 0; i < mats.Length; i++)
                        {
                            mats[i] = previewMaterial;
                        }
                        renderer.materials = mats;
                    }
                }
            }
        }
        
        #endregion
        
        #region Preview - Structural
        
        private void UpdateStructuralPreview()
        {
            var structuralData = GetCurrentStructuralData();
            
            UpdateStructuralPreviewModel();
            
            if (structuralData == null || structuralData.prefab == null)
            {
                structuralPreviewObject.SetActive(false);
                return;
            }
            
            // Verificar compatibilidad de tier
            bool tierCompatible = structuralData.IsCompatibleWith(targetRobot.CurrentTier);
            
            if (hoveredSocket == null || hoveredSocket.IsOccupied || 
                hoveredSocket.SocketType != structuralData.partType)
            {
                structuralPreviewObject.SetActive(false);
                return;
            }
            
            structuralPreviewObject.SetActive(true);
            structuralPreviewObject.transform.position = hoveredSocket.transform.position;
            structuralPreviewObject.transform.rotation = hoveredSocket.transform.rotation;
            
            // Actualizar color según compatibilidad de tier
            Color previewColor = tierCompatible ? 
                new Color(0f, 1f, 0.5f, 0.5f) :  // Verde/cyan si compatible
                new Color(1f, 0.3f, 0f, 0.5f);   // Naranja/rojo si incompatible
            
            UpdateStructuralPreviewColor(previewColor);
        }
        
        private void UpdateStructuralPreviewColor(Color color)
        {
            if (structuralPreviewObject == null) return;
            
            var renderers = structuralPreviewObject.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in renderers)
            {
                foreach (var mat in renderer.materials)
                {
                    mat.color = color;
                }
            }
            
            var skinnedRenderers = structuralPreviewObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var renderer in skinnedRenderers)
            {
                foreach (var mat in renderer.materials)
                {
                    mat.color = color;
                }
            }
        }
        
        private void UpdateStructuralPreviewModel()
        {
            var structuralData = GetCurrentStructuralData();
            
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
                    modelInstance.transform.localScale = Vector3.one;
                    
                    var components = modelInstance.GetComponentsInChildren<MonoBehaviour>();
                    foreach (var comp in components)
                    {
                        comp.enabled = false;
                    }
                    
                    var colliders = modelInstance.GetComponentsInChildren<Collider>();
                    foreach (var col in colliders)
                    {
                        col.enabled = false;
                    }
                    
                    // Aplicar material transparente
                    Material previewMat = new Material(Shader.Find("Sprites/Default"));
                    previewMat.color = new Color(0f, 1f, 0.5f, 0.5f);
                    
                    var renderers = modelInstance.GetComponentsInChildren<MeshRenderer>();
                    foreach (var renderer in renderers)
                    {
                        Material[] mats = new Material[renderer.sharedMaterials.Length];
                        for (int i = 0; i < mats.Length; i++)
                        {
                            mats[i] = previewMat;
                        }
                        renderer.materials = mats;
                    }
                    
                    var skinnedRenderers = modelInstance.GetComponentsInChildren<SkinnedMeshRenderer>();
                    foreach (var renderer in skinnedRenderers)
                    {
                        Material[] mats = new Material[renderer.sharedMaterials.Length];
                        for (int i = 0; i < mats.Length; i++)
                        {
                            mats[i] = previewMat;
                        }
                        renderer.materials = mats;
                    }
                }
            }
        }
        
        #endregion
        
        #region GUI
        
        private void OnGUI()
        {
            if (!isActive) return;
            
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.fontSize = 14;
            style.alignment = TextAnchor.UpperLeft;
            
            string info = "=== ASSEMBLY TESTER ===\n\n";
            info += $"Modo: {(currentMode == AssemblyMode.Armor ? "ARMADURA" : "ESTRUCTURAL")} (Tab para cambiar)\n\n";
            
            if (currentMode == AssemblyMode.Armor)
            {
                info += BuildArmorGUI();
            }
            else
            {
                info += BuildStructuralGUI();
            }
            
            GUI.Box(new Rect(10, 10, 350, 350), info, style);
        }
        
        private string BuildArmorGUI()
        {
            string info = "";
            
            if (hoveredGrid != null)
            {
                info += $"Grilla: {hoveredGrid.GridInfo.gridName}\n";
                info += $"Tamaño: {hoveredGrid.GridInfo.sizeX}x{hoveredGrid.GridInfo.sizeY}\n";
                info += $"Surrounding: {hoveredGrid.GridInfo.surrounding}\n\n";
            }
            else
            {
                info += "Grilla: (mueve el mouse sobre una grilla)\n\n";
            }
            
            var armorData = GetCurrentArmorData();
            if (armorData != null)
            {
                var rotatedInfo = GridRotation.RotateGridInfo(armorData.tailGrid.gridInfo, currentRotation);
                
                info += $"Pieza: {armorData.displayName}\n";
                info += $"Tamaño: {rotatedInfo.sizeX}x{rotatedInfo.sizeY}\n";
                info += $"Surrounding: {rotatedInfo.surrounding}\n";
                info += $"Rotación: {currentRotation}\n\n";
            }
            
            info += $"Posición: ({currentPositionX}, {currentPositionY})\n\n";
            
            if (hoveredGrid != null)
            {
                ArmorPart partAtCell = hoveredGrid.GetPartAtCell(currentPositionX, currentPositionY);
                if (partAtCell != null)
                {
                    info += $"Remover: {partAtCell.ArmorData.displayName}\n";
                    info += "(Click Der / X / Del)\n\n";
                }
                
                if (armorData != null)
                {
                    bool canPlace = hoveredGrid.CanPlace(armorData, currentPositionX, currentPositionY, currentRotation);
                    info += canPlace ? "Estado: VÁLIDO (Click Izq)" : "Estado: INVÁLIDO";
                }
            }
            
            return info;
        }
        
        private string BuildStructuralGUI()
        {
            string info = "";
            
            var structuralData = GetCurrentStructuralData();
            if (structuralData != null)
            {
                info += $"Pieza: {structuralData.displayName}\n";
                info += $"Tipo: {structuralData.partType}\n";
                info += $"Tier: {structuralData.tier}\n\n";
            }
            else
            {
                info += "Pieza: (ninguna seleccionada)\n\n";
            }
            
            if (hoveredSocket != null)
            {
                info += $"Socket: {hoveredSocket.SocketType}\n";
                info += $"Ocupado: {(hoveredSocket.IsOccupied ? "Sí" : "No")}\n\n";
                
                if (hoveredSocket.IsOccupied && hoveredSocket.AttachedPart != null)
                {
                    info += $"Remover: {hoveredSocket.AttachedPart.PartData.displayName}\n";
                    info += "(Click Der / X / Del)\n\n";
                }
                else if (structuralData != null && hoveredSocket.SocketType == structuralData.partType)
                {
                    info += "Estado: VÁLIDO (Click Izq)\n";
                }
                else
                {
                    info += "Estado: INCOMPATIBLE\n";
                }
            }
            else
            {
                info += "Socket: (mueve el mouse sobre un socket)\n\n";
            }
            
            // Mostrar sockets disponibles
            info += "--- Sockets disponibles ---\n";
            int compatibleCount = 0;
            foreach (var socket in availableSockets)
            {
                if (structuralData != null && socket.SocketType == structuralData.partType && !socket.IsOccupied)
                {
                    compatibleCount++;
                }
            }
            info += $"Compatibles: {compatibleCount}\n";
            
            return info;
        }
        
        #endregion
        
        private void OnDestroy()
        {
            // Desuscribirse de todas las estaciones
            UnsubscribeFromAllStations();
            
            DestroyGridHighlights();
            RestoreSocketMaterials();
            
            if (previewMaterial != null)
            {
                Destroy(previewMaterial);
            }
            if (previewObject != null)
            {
                Destroy(previewObject);
            }
            if (structuralPreviewObject != null)
            {
                Destroy(structuralPreviewObject);
            }
            previewRenderers.Clear();
        }
    }
}
