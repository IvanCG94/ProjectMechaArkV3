using System.Collections.Generic;
using UnityEngine;
using RobotGame.Components;
using RobotGame.Control;
using RobotGame.Data;
using RobotGame.AI;
using RobotGame.Enums;
using RobotGame.Utils;
using RobotGame.Systems;

namespace RobotGame.Assembly
{
    /// <summary>
    /// Tester para editar mechas (Tier 2+) en MechaAssemblyStation.
    /// Similar a AssemblyTester pero adaptado para editar robots salvajes domesticados.
    /// 
    /// Se subscribe a los eventos de MechaAssemblyStation para activarse/desactivarse.
    /// </summary>
    public class MechaAssemblyTester : MonoBehaviour
    {
        [Header("Estación")]
        [SerializeField] private MechaAssemblyStation station;
        
        [Header("Piezas de Prueba")]
        [SerializeField] private List<ArmorPartData> testArmorParts = new List<ArmorPartData>();
        [SerializeField] private List<StructuralPartData> testStructuralParts = new List<StructuralPartData>();
        
        [Header("Estado")]
        [SerializeField] private bool isActive = false;
        [SerializeField] private AssemblyMode currentMode = AssemblyMode.Armor;
        
        // Referencias
        private Camera mainCamera;
        private PlayerCamera playerCamera;
        private WildRobot targetMecha;
        private Robot targetRobot;
        
        // Modo Armor
        private int currentArmorIndex = 0;
        private GridHead hoveredGrid;
        private List<GridHead> availableGrids = new List<GridHead>();
        private int currentPositionX = 0;
        private int currentPositionY = 0;
        private GridRotation.Rotation currentRotation = GridRotation.Rotation.Deg0;
        
        // Preview Armor
        private GameObject armorPreviewObject;
        private GameObject pivotContainer;
        private GameObject modelContainer;
        private List<MeshRenderer> previewRenderers = new List<MeshRenderer>();
        private Material previewMaterial;
        private ArmorPartData currentPreviewData;
        
        // Modo Structural
        private int currentStructuralIndex = 0;
        private StructuralSocket hoveredSocket;
        private List<StructuralSocket> availableSockets = new List<StructuralSocket>();
        
        // Preview Structural
        private GameObject structuralPreviewObject;
        private StructuralPartData currentStructuralPreviewData;
        
        #region Unity Lifecycle
        
        private void Start()
        {
            // Auto-buscar estación si no está asignada
            if (station == null)
            {
                station = FindObjectOfType<MechaAssemblyStation>();
            }
            
            // Subscribirse a eventos
            if (station != null)
            {
                station.OnEditModeStarted += OnEditModeStarted;
                station.OnEditModeEnded += OnEditModeEnded;
            }
            
            mainCamera = Camera.main;
            playerCamera = FindObjectOfType<PlayerCamera>();
            
            // Crear objetos de preview
            CreatePreviewObjects();
        }
        
        private void OnDestroy()
        {
            if (station != null)
            {
                station.OnEditModeStarted -= OnEditModeStarted;
                station.OnEditModeEnded -= OnEditModeEnded;
            }
            
            // Limpiar previews
            if (armorPreviewObject != null) Destroy(armorPreviewObject);
            if (structuralPreviewObject != null) Destroy(structuralPreviewObject);
            if (previewMaterial != null) Destroy(previewMaterial);
        }
        
        private void Update()
        {
            if (!isActive) return;
            
            // Cambiar modo con Tab
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                ToggleMode();
            }
            
            // Input según modo
            if (currentMode == AssemblyMode.Armor)
            {
                HandleArmorInput();
            }
            else
            {
                HandleStructuralInput();
            }
        }
        
        #endregion
        
        #region Station Events
        
        private void OnEditModeStarted(MechaAssemblyStation s)
        {
            if (s != station) return;
            ActivateTester();
        }
        
        private void OnEditModeEnded(MechaAssemblyStation s)
        {
            if (s != station) return;
            DeactivateTester();
        }
        
        #endregion
        
        #region Activation
        
        private void ActivateTester()
        {
            targetMecha = station.CurrentMecha;
            if (targetMecha == null || targetMecha.Robot == null)
            {
                Debug.LogError("MechaAssemblyTester: No hay mecha válido");
                return;
            }
            
            targetRobot = targetMecha.Robot;
            isActive = true;
            
            // Configurar cámara
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
            
            if (playerCamera == null)
            {
                playerCamera = FindObjectOfType<PlayerCamera>();
            }
            
            if (playerCamera != null)
            {
                playerCamera.EnterEditMode();
                playerCamera.SetTarget(targetRobot.transform, false);
            }
            
            // Resetear estado
            currentRotation = GridRotation.Rotation.Deg0;
            currentPreviewData = null;
            currentStructuralPreviewData = null;
            
            // Inicializar según modo
            if (currentMode == AssemblyMode.Armor)
            {
                CollectAvailableGrids();
            }
            else
            {
                CollectAvailableSockets();
            }
            
            Debug.Log($"MechaAssemblyTester: Activado para '{targetMecha.WildData?.speciesName}'");
        }
        
        private void DeactivateTester()
        {
            isActive = false;
            
            // Ocultar previews
            if (armorPreviewObject != null) armorPreviewObject.SetActive(false);
            if (structuralPreviewObject != null) structuralPreviewObject.SetActive(false);
            
            // Restaurar cámara
            if (playerCamera != null)
            {
                playerCamera.ExitEditMode();
                
                // Volver a seguir al jugador
                var playerCore = FindPlayerCore();
                if (playerCore != null && playerCore.CurrentRobot != null)
                {
                    playerCamera.SetTarget(playerCore.CurrentRobot.transform, false);
                }
            }
            
            targetMecha = null;
            targetRobot = null;
            
            Debug.Log("MechaAssemblyTester: Desactivado");
        }
        
        private RobotCore FindPlayerCore()
        {
            var cores = FindObjectsOfType<RobotCore>();
            foreach (var core in cores)
            {
                if (core.IsPlayerCore)
                {
                    return core;
                }
            }
            return null;
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
            
            // Inicializar nuevo modo
            if (currentMode == AssemblyMode.Armor)
            {
                CollectAvailableGrids();
            }
            else
            {
                CollectAvailableSockets();
            }
            
            Debug.Log($"MechaAssemblyTester: Modo cambiado a {currentMode}");
        }
        
        #endregion
        
        #region Preview Setup
        
        private void CreatePreviewObjects()
        {
            // Preview para armor
            armorPreviewObject = new GameObject("MechaArmorPreview");
            
            pivotContainer = new GameObject("PivotContainer");
            pivotContainer.transform.SetParent(armorPreviewObject.transform);
            
            modelContainer = new GameObject("ModelContainer");
            modelContainer.transform.SetParent(pivotContainer.transform);
            
            armorPreviewObject.SetActive(false);
            
            previewMaterial = new Material(Shader.Find("Sprites/Default"));
            previewMaterial.color = new Color(0f, 0.5f, 1f, 0.5f);
            
            // Preview para structural
            structuralPreviewObject = new GameObject("MechaStructuralPreview");
            structuralPreviewObject.SetActive(false);
        }
        
        #endregion
        
        #region Armor Mode
        
        private void HandleArmorInput()
        {
            // Detectar grid bajo el mouse
            DetectGridUnderMouse();
            
            // Cambiar pieza con scroll
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0)
            {
                ChangeArmorPiece(scroll > 0 ? 1 : -1);
            }
            
            // Rotar con R
            if (Input.GetKeyDown(KeyCode.R))
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
            
            // Actualizar preview
            UpdateArmorPreview();
        }
        
        private void DetectGridUnderMouse()
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            GridHead newHoveredGrid = null;
            
            if (Physics.Raycast(ray, out hit, 100f, ~0, QueryTriggerInteraction.Collide))
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
            if (testArmorParts.Count == 0) return;
            
            currentArmorIndex += direction;
            
            if (currentArmorIndex < 0)
                currentArmorIndex = testArmorParts.Count - 1;
            else if (currentArmorIndex >= testArmorParts.Count)
                currentArmorIndex = 0;
        }
        
        private ArmorPartData GetCurrentArmorData()
        {
            if (testArmorParts.Count == 0) return null;
            return testArmorParts[currentArmorIndex];
        }
        
        private void TryPlaceArmor()
        {
            if (hoveredGrid == null || testArmorParts.Count == 0) return;
            
            ArmorPartData armorData = GetCurrentArmorData();
            if (armorData == null) return;
            
            // Validar tier
            if (!armorData.IsCompatibleWith(targetRobot.CurrentTier))
            {
                Debug.LogWarning($"Armadura no compatible con robot Tier {targetRobot.CurrentTier}");
                return;
            }
            
            // Verificar si se puede colocar
            if (!hoveredGrid.CanPlace(armorData, currentPositionX, currentPositionY, currentRotation))
            {
                return;
            }
            
            // Crear la pieza
            ArmorPart armorPart = RobotFactory.Instance.CreateArmorPart(armorData);
            if (armorPart == null)
            {
                Debug.LogError("Error al crear la pieza de armadura");
                return;
            }
            
            // Intentar colocar
            if (hoveredGrid.TryPlace(armorPart, currentPositionX, currentPositionY, currentRotation))
            {
                Debug.Log($"Armadura '{armorData.displayName}' colocada");
                
                // Si tiene grillas adicionales, actualizar lista
                if (armorPart.AdditionalGrids != null && armorPart.AdditionalGrids.Count > 0)
                {
                    CollectAvailableGrids();
                }
            }
            else
            {
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
                
                if (hoveredGrid.Remove(part, true))
                {
                    Debug.Log("Armadura removida");
                    
                    if (hadAdditionalGrids)
                    {
                        CollectAvailableGrids();
                    }
                }
            }
        }
        
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
                    EnsureGridCollider(grid);
                }
                
                // También recolectar grillas de armaduras existentes
                CollectArmorPartGrids(part.ArmorGrids);
            }
            
            Debug.Log($"MechaAssemblyTester: {availableGrids.Count} grillas disponibles");
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
        
        private void EnsureGridCollider(GridHead grid)
        {
            BoxCollider collider = grid.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = grid.gameObject.AddComponent<BoxCollider>();
            }
            
            float sizeX = grid.GridInfo.sizeX * 0.1f;
            float sizeY = grid.GridInfo.sizeY * 0.1f;
            collider.size = new Vector3(sizeX, sizeY, 0.1f);
            collider.center = new Vector3(sizeX / 2f, sizeY / 2f, -0.05f);
            collider.isTrigger = true;
        }
        
        #endregion
        
        #region Armor Preview
        
        private void UpdateArmorPreview()
        {
            var armorData = GetCurrentArmorData();
            
            // Actualizar modelo si cambió la pieza
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
            
            // Determinar color del preview
            Color previewColor;
            bool tierCompatible = armorData.IsCompatibleWith(targetRobot.CurrentTier);
            
            if (hoveredGrid == null)
            {
                // Sin grid: azul si compatible, naranja si no
                previewColor = tierCompatible ? 
                    new Color(0f, 0.5f, 1f, 0.5f) : 
                    new Color(1f, 0.3f, 0f, 0.5f);
                
                // Posicionar en el espacio frente a la cámara
                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                Vector3 worldPos = ray.origin + ray.direction * 2f;
                armorPreviewObject.transform.position = worldPos;
                armorPreviewObject.transform.rotation = Quaternion.identity;
            }
            else
            {
                bool canPlace = hoveredGrid.CanPlace(armorData, currentPositionX, currentPositionY, currentRotation);
                
                // Verde si puede colocar Y es compatible, rojo si no
                if (canPlace && tierCompatible)
                {
                    previewColor = new Color(0f, 1f, 0f, 0.5f); // Verde
                }
                else
                {
                    previewColor = new Color(1f, 0f, 0f, 0.5f); // Rojo
                }
                
                Vector3 cellPos = hoveredGrid.CellToWorldPosition(currentPositionX, currentPositionY);
                armorPreviewObject.transform.position = cellPos;
                armorPreviewObject.transform.rotation = hoveredGrid.transform.rotation;
            }
            
            previewMaterial.color = previewColor;
        }
        
        private void UpdateArmorPreviewModel()
        {
            var armorData = GetCurrentArmorData();
            
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
                    // Crear nuevo modelo
                    GameObject modelInstance = Instantiate(armorData.prefab, modelContainer.transform);
                    modelInstance.transform.localPosition = Vector3.zero;
                    modelInstance.transform.localRotation = Quaternion.identity;
                    modelInstance.transform.localScale = Vector3.one;
                    
                    // Desactivar componentes
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
                    
                    // Aplicar material de preview
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
        
        #region Structural Mode
        
        private void HandleStructuralInput()
        {
            // Detectar socket bajo el mouse
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
            
            // Actualizar preview
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
                if (socket == null)
                {
                    socket = hit.collider.GetComponentInParent<StructuralSocket>();
                }
                
                if (socket != null && availableSockets.Contains(socket))
                {
                    newHoveredSocket = socket;
                }
            }
            
            hoveredSocket = newHoveredSocket;
        }
        
        private void ChangeStructuralPiece(int direction)
        {
            if (testStructuralParts.Count == 0) return;
            
            currentStructuralIndex += direction;
            
            if (currentStructuralIndex < 0)
                currentStructuralIndex = testStructuralParts.Count - 1;
            else if (currentStructuralIndex >= testStructuralParts.Count)
                currentStructuralIndex = 0;
        }
        
        private StructuralPartData GetCurrentStructuralData()
        {
            if (testStructuralParts.Count == 0) return null;
            return testStructuralParts[currentStructuralIndex];
        }
        
        private void TryPlaceStructural()
        {
            if (hoveredSocket == null || testStructuralParts.Count == 0) return;
            if (hoveredSocket.IsOccupied) return;
            
            StructuralPartData partData = GetCurrentStructuralData();
            if (partData == null) return;
            
            // Validar tipo de socket
            if (partData.partType != hoveredSocket.SocketType)
            {
                Debug.LogWarning($"Tipo de pieza no coincide con socket");
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
            
            // Intentar conectar
            if (hoveredSocket.TryAttach(part))
            {
                Debug.Log($"Pieza '{partData.displayName}' colocada");
                CollectAvailableSockets();
            }
            else
            {
                Destroy(part.gameObject);
            }
        }
        
        private void TryRemoveStructural()
        {
            if (hoveredSocket == null || !hoveredSocket.IsOccupied) return;
            
            StructuralPart part = hoveredSocket.AttachedPart;
            if (part != null)
            {
                // Verificar que no tenga hijos
                bool hasChildren = false;
                foreach (var childSocket in part.ChildSockets)
                {
                    if (childSocket.IsOccupied)
                    {
                        hasChildren = true;
                        break;
                    }
                }
                
                if (hasChildren)
                {
                    Debug.LogWarning("No se puede remover: tiene piezas hijas conectadas");
                    return;
                }
                
                hoveredSocket.Detach();
                Destroy(part.gameObject);
                Debug.Log("Pieza estructural removida");
                CollectAvailableSockets();
            }
        }
        
        private void CollectAvailableSockets()
        {
            availableSockets.Clear();
            
            if (targetRobot == null) return;
            
            // Agregar el HipsSocket del robot (siempre disponible si existe)
            if (targetRobot.HipsSocket != null)
            {
                availableSockets.Add(targetRobot.HipsSocket);
                EnsureSocketCollider(targetRobot.HipsSocket);
            }
            
            // Agregar sockets de todas las partes estructurales
            var parts = targetRobot.GetAllStructuralParts();
            foreach (var part in parts)
            {
                foreach (var socket in part.ChildSockets)
                {
                    availableSockets.Add(socket);
                    EnsureSocketCollider(socket);
                }
            }
            
            Debug.Log($"MechaAssemblyTester: {availableSockets.Count} sockets disponibles");
        }
        
        private void EnsureSocketCollider(StructuralSocket socket)
        {
            Collider collider = socket.GetComponent<Collider>();
            if (collider == null)
            {
                SphereCollider sphereCollider = socket.gameObject.AddComponent<SphereCollider>();
                sphereCollider.radius = 0.15f;
                sphereCollider.isTrigger = true;
            }
        }
        
        #endregion
        
        #region Structural Preview
        
        private void UpdateStructuralPreview()
        {
            var structuralData = GetCurrentStructuralData();
            
            // Actualizar modelo si cambió la pieza
            UpdateStructuralPreviewModel();
            
            if (structuralData == null || structuralData.prefab == null)
            {
                structuralPreviewObject.SetActive(false);
                return;
            }
            
            bool tierCompatible = structuralData.IsCompatibleWith(targetRobot.CurrentTier);
            
            // Solo mostrar preview si hay socket válido
            if (hoveredSocket == null || hoveredSocket.IsOccupied || 
                hoveredSocket.SocketType != structuralData.partType)
            {
                structuralPreviewObject.SetActive(false);
                return;
            }
            
            structuralPreviewObject.SetActive(true);
            structuralPreviewObject.transform.position = hoveredSocket.transform.position;
            structuralPreviewObject.transform.rotation = hoveredSocket.transform.rotation;
            
            // Color según compatibilidad
            Color previewColor = tierCompatible ? 
                new Color(0f, 1f, 0.5f, 0.5f) :  // Verde/cyan
                new Color(1f, 0.3f, 0f, 0.5f);   // Naranja/rojo
            
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
                
                // Limpiar modelo anterior
                foreach (Transform child in structuralPreviewObject.transform)
                {
                    Destroy(child.gameObject);
                }
                
                if (structuralData != null && structuralData.prefab != null)
                {
                    // Crear nuevo modelo
                    GameObject modelInstance = Instantiate(structuralData.prefab, structuralPreviewObject.transform);
                    modelInstance.transform.localPosition = Vector3.zero;
                    modelInstance.transform.localRotation = Quaternion.identity;
                    modelInstance.transform.localScale = Vector3.one;
                    
                    // Desactivar componentes
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
                    
                    // Aplicar material de preview
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
        
        #region Debug UI
        
        private void OnGUI()
        {
            if (!isActive) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 350, 280));
            GUILayout.BeginVertical("box");
            
            GUILayout.Label($"=== EDITANDO MECHA ===");
            GUILayout.Label($"Robot: {targetMecha?.WildData?.speciesName ?? "Desconocido"}");
            GUILayout.Label($"Tier: {targetRobot?.CurrentTier}");
            GUILayout.Space(5);
            
            GUILayout.Label($"Modo: {(currentMode == AssemblyMode.Armor ? "ARMADURA" : "ESTRUCTURAL")}");
            GUILayout.Label("[Tab] Cambiar modo");
            GUILayout.Space(5);
            
            if (currentMode == AssemblyMode.Armor)
            {
                var armorData = GetCurrentArmorData();
                if (armorData != null)
                {
                    GUILayout.Label($"Pieza: {armorData.displayName}");
                    GUILayout.Label($"Tier: {armorData.tier}");
                    GUILayout.Label($"Tamaño: {armorData.tailGrid.gridInfo.sizeX}x{armorData.tailGrid.gridInfo.sizeY}");
                }
                else
                {
                    GUILayout.Label("No hay piezas de armadura");
                }
                GUILayout.Space(5);
                GUILayout.Label("[Scroll] Cambiar pieza");
                GUILayout.Label("[R] Rotar");
                GUILayout.Label("[Click Izq] Colocar");
                GUILayout.Label("[Click Der] Remover");
                GUILayout.Label($"Rotación: {currentRotation}");
                
                if (hoveredGrid != null)
                {
                    GUILayout.Label($"Grid: {hoveredGrid.name}");
                    GUILayout.Label($"Posición: ({currentPositionX}, {currentPositionY})");
                }
            }
            else
            {
                var structuralData = GetCurrentStructuralData();
                if (structuralData != null)
                {
                    GUILayout.Label($"Pieza: {structuralData.displayName}");
                    GUILayout.Label($"Tipo: {structuralData.partType}");
                    GUILayout.Label($"Tier: {structuralData.tier}");
                }
                else
                {
                    GUILayout.Label("No hay piezas estructurales");
                }
                GUILayout.Space(5);
                GUILayout.Label("[Scroll] Cambiar pieza");
                GUILayout.Label("[Click Izq] Colocar");
                GUILayout.Label("[Click Der] Remover");
                
                if (hoveredSocket != null)
                {
                    GUILayout.Label($"Socket: {hoveredSocket.SocketType}");
                    GUILayout.Label($"Ocupado: {(hoveredSocket.IsOccupied ? "Sí" : "No")}");
                }
            }
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        
        #endregion
    }
}
