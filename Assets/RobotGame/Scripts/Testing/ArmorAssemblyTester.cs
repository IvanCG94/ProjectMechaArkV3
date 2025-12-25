using System.Collections.Generic;
using UnityEngine;
using RobotGame.Components;
using RobotGame.Data;
using RobotGame.Systems;
using RobotGame.Utils;
using RobotGame.Enums;

namespace RobotGame.Testing
{
    /// <summary>
    /// Script de prueba para ensamblar piezas de armadura interactivamente.
    /// Permite probar el sistema de grillas en runtime usando el mouse para seleccionar grillas.
    /// </summary>
    public class ArmorAssemblyTester : MonoBehaviour
    {
        [Header("Piezas de Prueba")]
        [Tooltip("Lista de piezas de armadura para probar")]
        [SerializeField] private List<ArmorPartData> testArmorParts = new List<ArmorPartData>();
        
        [Header("Configuración")]
        [SerializeField] private float gridDetectionRadius = 0.5f;
        [SerializeField] private LayerMask gridLayerMask = -1;
        
        [Header("Referencias (Auto-asignadas)")]
        [SerializeField] private Robot targetRobot;
        [SerializeField] private Camera mainCamera;
        
        // Estado interno
        private List<GridHead> availableGrids = new List<GridHead>();
        private GridHead hoveredGrid = null;
        private int currentArmorIndex = 0;
        private int currentPositionX = 0;
        private int currentPositionY = 0;
        private GridRotation.Rotation currentRotation = GridRotation.Rotation.Deg0;
        
        // Preview
        private GameObject previewObject;
        private List<MeshRenderer> previewRenderers = new List<MeshRenderer>();
        private Material previewMaterial;
        private ArmorPartData currentPreviewData;
        
        // Grid highlight objects
        private Dictionary<GridHead, GameObject> gridHighlights = new Dictionary<GridHead, GameObject>();
        private Dictionary<GridHead, Material> gridMaterials = new Dictionary<GridHead, Material>();
        
        // UI
        private bool isActive = false;
        
        private void Start()
        {
            CreatePreviewObject();
            
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
        }
        
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.P))
            {
                if (!isActive)
                {
                    ActivateTester();
                }
            }
            
            if (!isActive) return;
            
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
            
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                MovePosition(0, 1);
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                MovePosition(0, -1);
            }
            
            if (Input.GetKeyDown(KeyCode.A))
            {
                MovePosition(-1, 0);
            }
            if (Input.GetKeyDown(KeyCode.D))
            {
                MovePosition(1, 0);
            }
            
            if (Input.GetKeyDown(KeyCode.R))
            {
                RotatePiece();
            }
            
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                DeactivateTester();
            }
            
            UpdatePreview();
        }
        
        private void ActivateTester()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    Debug.LogError("ArmorAssemblyTester: No se encontró cámara principal.");
                    return;
                }
            }
            
            if (targetRobot == null)
            {
                targetRobot = FindObjectOfType<Robot>();
            }
            
            if (targetRobot == null)
            {
                Debug.LogError("ArmorAssemblyTester: No se encontró ningún robot en la escena.");
                return;
            }
            
            if (testArmorParts.Count == 0)
            {
                Debug.LogError("ArmorAssemblyTester: No hay piezas de armadura asignadas para probar.");
                return;
            }
            
            CollectAvailableGrids();
            
            if (availableGrids.Count == 0)
            {
                Debug.LogError("ArmorAssemblyTester: El robot no tiene grillas de armadura disponibles.");
                return;
            }
            
            isActive = true;
            currentArmorIndex = 0;
            currentPositionX = 0;
            currentPositionY = 0;
            currentRotation = GridRotation.Rotation.Deg0;
            hoveredGrid = null;
            
            CreateGridHighlights();
            previewObject.SetActive(false);
            
            Debug.Log("=== ARMOR ASSEMBLY TESTER ACTIVADO ===");
            Debug.Log("Controles:");
            Debug.Log("  Mouse       : Seleccionar grilla");
            Debug.Log("  Click Izq   : Colocar pieza");
            Debug.Log("  ← →         : Cambiar pieza de armadura");
            Debug.Log("  ↑ ↓ A D     : Mover posición en grilla");
            Debug.Log("  R           : Rotar pieza (90°)");
            Debug.Log("  Esc         : Cancelar");
            Debug.Log("======================================");
        }
        
        private void DeactivateTester()
        {
            isActive = false;
            previewObject.SetActive(false);
            DestroyGridHighlights();
            hoveredGrid = null;
            Debug.Log("ArmorAssemblyTester desactivado.");
        }
        
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
                    Debug.Log($"Grilla encontrada: {grid.GridInfo.gridName} ({grid.GridInfo.sizeX}x{grid.GridInfo.sizeY}) en {part.PartData.displayName}");
                }
            }
            
            Debug.Log($"Total de grillas disponibles: {availableGrids.Count}");
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
            collider.center = new Vector3(sizeX / 2f, sizeY / 2f, 0.05f);
            collider.isTrigger = true;
        }
        
        private void DetectGridUnderMouse()
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            GridHead newHoveredGrid = null;
            
            if (Physics.Raycast(ray, out hit, 100f, gridLayerMask))
            {
                GridHead hitGrid = hit.collider.GetComponent<GridHead>();
                
                if (hitGrid != null && availableGrids.Contains(hitGrid))
                {
                    newHoveredGrid = hitGrid;
                    CalculateGridPosition(hitGrid, hit.point);
                }
            }
            
            if (newHoveredGrid == null)
            {
                newHoveredGrid = FindClosestGridToRay(ray);
            }
            
            if (newHoveredGrid != hoveredGrid)
            {
                hoveredGrid = newHoveredGrid;
                currentPositionX = 0;
                currentPositionY = 0;
                
                if (hoveredGrid != null)
                {
                    Debug.Log($"Grilla seleccionada: {hoveredGrid.GridInfo.gridName}");
                }
                
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
        
        private GridHead FindClosestGridToRay(Ray ray)
        {
            GridHead closest = null;
            float closestDistance = gridDetectionRadius;
            
            foreach (var grid in availableGrids)
            {
                Vector3 gridCenter = grid.transform.position + 
                    grid.transform.rotation * new Vector3(
                        grid.GridInfo.sizeX * 0.05f,
                        grid.GridInfo.sizeY * 0.05f,
                        0f
                    );
                
                Vector3 toGrid = gridCenter - ray.origin;
                float projectedDistance = Vector3.Dot(toGrid, ray.direction);
                Vector3 closestPointOnRay = ray.origin + ray.direction * projectedDistance;
                float distance = Vector3.Distance(closestPointOnRay, gridCenter);
                
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = grid;
                }
            }
            
            return closest;
        }
        
        private Material CreateTransparentMaterial(Color color)
        {
            Material mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = color;
            return mat;
        }
        
        private void CreateGridHighlights()
        {
            DestroyGridHighlights();
            
            Color gridColor = new Color(0f, 0.8f, 1f, 0.3f);
            
            foreach (var grid in availableGrids)
            {
                GameObject highlight = GameObject.CreatePrimitive(PrimitiveType.Cube);
                highlight.name = $"GridHighlight_{grid.GridInfo.gridName}";
                
                var collider = highlight.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                
                Material mat = CreateTransparentMaterial(gridColor);
                var renderer = highlight.GetComponent<MeshRenderer>();
                renderer.material = mat;
                
                float sizeX = grid.GridInfo.sizeX * 0.1f;
                float sizeY = grid.GridInfo.sizeY * 0.1f;
                highlight.transform.SetParent(grid.transform);
                highlight.transform.localPosition = new Vector3(sizeX / 2f, sizeY / 2f, 0.01f);
                highlight.transform.localRotation = Quaternion.identity;
                highlight.transform.localScale = new Vector3(sizeX, sizeY, 0.02f);
                
                gridHighlights[grid] = highlight;
                gridMaterials[grid] = mat;
            }
        }
        
        private void UpdateGridHighlights()
        {
            Color normalColor = new Color(0f, 0.8f, 1f, 0.3f);
            Color selectedColor = new Color(0f, 0.9f, 1f, 0.5f);
            
            foreach (var kvp in gridMaterials)
            {
                kvp.Value.color = (kvp.Key == hoveredGrid) ? selectedColor : normalColor;
            }
        }
        
        private void DestroyGridHighlights()
        {
            foreach (var kvp in gridHighlights)
            {
                if (kvp.Value != null) Destroy(kvp.Value);
            }
            gridHighlights.Clear();
            
            foreach (var kvp in gridMaterials)
            {
                if (kvp.Value != null) Destroy(kvp.Value);
            }
            gridMaterials.Clear();
        }
        
        private void ChangeArmorPiece(int direction)
        {
            currentArmorIndex += direction;
            
            if (currentArmorIndex < 0)
                currentArmorIndex = testArmorParts.Count - 1;
            else if (currentArmorIndex >= testArmorParts.Count)
                currentArmorIndex = 0;
            
            currentPositionX = 0;
            currentPositionY = 0;
            currentRotation = GridRotation.Rotation.Deg0;
            currentPreviewData = null; // Forzar recreación del preview
            
            Debug.Log($"Pieza seleccionada: {GetCurrentArmorData().displayName}");
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
            
            var armor = GetCurrentArmorData();
            if (armor != null)
            {
                var rotatedInfo = GridRotation.RotateGridInfo(armor.tailGrid.gridInfo, currentRotation);
                string edgesStr = rotatedInfo.surrounding.HasEdges ? 
                    $"_{SurroundingLevel.EdgesToString(rotatedInfo.surrounding.edges)}" : "";
                string fullStr = rotatedInfo.surrounding.IsFull ? 
                    rotatedInfo.surrounding.fullType.ToString() : "";
                    
                Debug.Log($"Rotación: {currentRotation} → {rotatedInfo.sizeX}x{rotatedInfo.sizeY} S{rotatedInfo.surrounding.level}{fullStr}{edgesStr}");
            }
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
            if (hoveredGrid == null)
            {
                Debug.LogWarning("No hay grilla seleccionada. Mueve el mouse sobre una grilla.");
                return;
            }
            
            var armorData = GetCurrentArmorData();
            if (armorData == null) return;
            
            if (!hoveredGrid.CanPlace(armorData, currentPositionX, currentPositionY, currentRotation))
            {
                Debug.LogWarning($"No se puede colocar {armorData.displayName} en posición ({currentPositionX}, {currentPositionY}) con rotación {currentRotation}");
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
                Debug.Log($"✓ {armorData.displayName} colocada en {hoveredGrid.GridInfo.gridName} posición ({currentPositionX}, {currentPositionY}) rotación {currentRotation}");
                PrintGridState(hoveredGrid);
            }
            else
            {
                Debug.LogError("Error al colocar la pieza en la grilla.");
                Destroy(armorPart.gameObject);
            }
        }
        
        private void PrintGridState(GridHead grid)
        {
            var info = grid.GridInfo;
            Debug.Log($"--- Estado de grilla {info.gridName} ({info.sizeX}x{info.sizeY}) ---");
            
            string gridVisual = "";
            for (int y = info.sizeY - 1; y >= 0; y--)
            {
                for (int x = 0; x < info.sizeX; x++)
                {
                    gridVisual += grid.IsCellOccupied(x, y) ? "[X]" : "[ ]";
                }
                gridVisual += "\n";
            }
            Debug.Log(gridVisual);
        }
        
        private ArmorPartData GetCurrentArmorData()
        {
            if (testArmorParts.Count == 0) return null;
            return testArmorParts[currentArmorIndex];
        }
        
        private void CreatePreviewObject()
        {
            previewObject = new GameObject("ArmorPreview");
            previewObject.SetActive(false);
            
            // Crear material transparente
            previewMaterial = new Material(Shader.Find("Sprites/Default"));
            previewMaterial.color = new Color(0f, 0.5f, 1f, 0.5f); // Azul por defecto
        }
        
        private void UpdatePreviewModel()
        {
            var armorData = GetCurrentArmorData();
            
            // Si la pieza cambió, recrear el preview
            if (armorData != currentPreviewData)
            {
                currentPreviewData = armorData;
                
                // Limpiar hijos anteriores
                foreach (Transform child in previewObject.transform)
                {
                    Destroy(child.gameObject);
                }
                previewRenderers.Clear();
                
                if (armorData != null && armorData.prefab != null)
                {
                    // Instanciar el prefab como hijo del preview
                    GameObject modelInstance = Instantiate(armorData.prefab, previewObject.transform);
                    modelInstance.transform.localPosition = Vector3.zero;
                    modelInstance.transform.localRotation = Quaternion.identity;
                    modelInstance.transform.localScale = Vector3.one;
                    
                    // Desactivar cualquier componente que no sea visual
                    var components = modelInstance.GetComponentsInChildren<MonoBehaviour>();
                    foreach (var comp in components)
                    {
                        comp.enabled = false;
                    }
                    
                    // Desactivar colliders
                    var colliders = modelInstance.GetComponentsInChildren<Collider>();
                    foreach (var col in colliders)
                    {
                        col.enabled = false;
                    }
                    
                    // Recolectar todos los renderers y aplicar material transparente
                    var renderers = modelInstance.GetComponentsInChildren<MeshRenderer>();
                    foreach (var renderer in renderers)
                    {
                        // Crear copia del material para cada renderer
                        Material[] mats = new Material[renderer.sharedMaterials.Length];
                        for (int i = 0; i < mats.Length; i++)
                        {
                            mats[i] = previewMaterial;
                        }
                        renderer.materials = mats;
                        previewRenderers.Add(renderer);
                    }
                    
                    // También para SkinnedMeshRenderer si existe
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
        
        private void UpdatePreview()
        {
            var armorData = GetCurrentArmorData();
            
            // Actualizar el modelo si cambió la pieza
            UpdatePreviewModel();
            
            if (armorData == null || armorData.prefab == null)
            {
                previewObject.SetActive(false);
                return;
            }
            
            previewObject.SetActive(true);
            
            // Obtener tamaño rotado para calcular posición
            var rotatedSize = GetRotatedSize(armorData);
            int sizeX = rotatedSize.x;
            int sizeY = rotatedSize.y;
            
            // Determinar color según estado
            Color previewColor;
            
            if (hoveredGrid == null)
            {
                // Azul transparente - no está sobre ninguna grilla
                previewColor = new Color(0f, 0.5f, 1f, 0.5f);
                
                // Posicionar en el centro de la pantalla o seguir el mouse
                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                Vector3 worldPos = ray.origin + ray.direction * 2f;
                previewObject.transform.position = worldPos;
                previewObject.transform.rotation = Quaternion.identity * GridRotation.ToQuaternion(currentRotation);
            }
            else
            {
                bool canPlace = hoveredGrid.CanPlace(armorData, currentPositionX, currentPositionY, currentRotation);
                
                if (canPlace)
                {
                    // Verde transparente - posición válida
                    previewColor = new Color(0f, 1f, 0f, 0.5f);
                }
                else
                {
                    // Rojo transparente - posición inválida
                    previewColor = new Color(1f, 0f, 0f, 0.5f);
                }
                
                // Posicionar en la grilla
                Vector3 cellPos = hoveredGrid.CellToWorldPosition(currentPositionX, currentPositionY);
                previewObject.transform.position = cellPos;
                previewObject.transform.rotation = hoveredGrid.transform.rotation * GridRotation.ToQuaternion(currentRotation);
            }
            
            // Aplicar color al material
            previewMaterial.color = previewColor;
        }
        
        private void OnGUI()
        {
            if (!isActive) return;
            
            var armorData = GetCurrentArmorData();
            
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.fontSize = 14;
            style.alignment = TextAnchor.UpperLeft;
            
            string info = "=== ARMOR ASSEMBLY TESTER ===\n\n";
            
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
            
            if (armorData != null)
            {
                var rotatedInfo = GridRotation.RotateGridInfo(armorData.tailGrid.gridInfo, currentRotation);
                
                info += $"Pieza: {armorData.displayName}\n";
                info += $"Tamaño original: {armorData.tailGrid.gridInfo.sizeX}x{armorData.tailGrid.gridInfo.sizeY}\n";
                info += $"Tamaño rotado: {rotatedInfo.sizeX}x{rotatedInfo.sizeY}\n";
                info += $"Surrounding: {rotatedInfo.surrounding}\n";
                info += $"Rotación: {currentRotation}\n\n";
            }
            
            info += $"Posición: ({currentPositionX}, {currentPositionY})\n\n";
            
            if (hoveredGrid != null && armorData != null)
            {
                bool canPlace = hoveredGrid.CanPlace(armorData, currentPositionX, currentPositionY, currentRotation);
                info += canPlace ? "Estado: VÁLIDO" : "Estado: INVÁLIDO";
            }
            
            GUI.Box(new Rect(10, 10, 320, 280), info, style);
        }
        
        private void OnDestroy()
        {
            DestroyGridHighlights();
            
            if (previewMaterial != null)
            {
                Destroy(previewMaterial);
            }
            if (previewObject != null)
            {
                Destroy(previewObject);
            }
            previewRenderers.Clear();
        }
    }
}
