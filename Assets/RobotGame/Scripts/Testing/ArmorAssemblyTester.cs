using System.Collections.Generic;
using UnityEngine;
using RobotGame.Components;
using RobotGame.Data;
using RobotGame.Systems;

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
        
        // Preview
        private GameObject previewObject;
        private MeshRenderer previewRenderer;
        private Material previewMaterial;
        
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
            // Activar con P (primera vez)
            if (Input.GetKeyDown(KeyCode.P))
            {
                if (!isActive)
                {
                    ActivateTester();
                }
            }
            
            if (!isActive) return;
            
            // Detectar grilla bajo el mouse
            DetectGridUnderMouse();
            
            // Click izquierdo para colocar pieza
            if (Input.GetMouseButtonDown(0))
            {
                TryPlaceArmor();
            }
            
            // Cambiar pieza con flechas izquierda/derecha
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                ChangeArmorPiece(-1);
            }
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                ChangeArmorPiece(1);
            }
            
            // Mover posición dentro de la grilla con flechas arriba/abajo
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                MovePosition(0, 1);
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                MovePosition(0, -1);
            }
            
            // Mover posición horizontal con A/D (alternativa)
            if (Input.GetKeyDown(KeyCode.A))
            {
                MovePosition(-1, 0);
            }
            if (Input.GetKeyDown(KeyCode.D))
            {
                MovePosition(1, 0);
            }
            
            // Cancelar con Escape
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                DeactivateTester();
            }
            
            // Actualizar preview
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
            hoveredGrid = null;
            
            CreateGridHighlights();
            previewObject.SetActive(false);
            
            Debug.Log("=== ARMOR ASSEMBLY TESTER ACTIVADO ===");
            Debug.Log("Controles:");
            Debug.Log("  Mouse       : Seleccionar grilla");
            Debug.Log("  Click Izq   : Colocar pieza");
            Debug.Log("  ← →         : Cambiar pieza de armadura");
            Debug.Log("  ↑ ↓ A D     : Mover posición en grilla");
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
                    
                    // Agregar un collider temporal a la grilla para detección con raycast
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
            
            // Configurar el tamaño del collider basado en la grilla
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
                // Verificar si el objeto golpeado es una grilla
                GridHead hitGrid = hit.collider.GetComponent<GridHead>();
                
                if (hitGrid != null && availableGrids.Contains(hitGrid))
                {
                    newHoveredGrid = hitGrid;
                    
                    // Calcular posición en la grilla basada en el punto de impacto
                    CalculateGridPosition(hitGrid, hit.point);
                }
            }
            
            // Si no hay hit directo, buscar la grilla más cercana
            if (newHoveredGrid == null)
            {
                newHoveredGrid = FindClosestGridToRay(ray);
            }
            
            // Actualizar grilla seleccionada
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
            // Convertir punto mundial a local de la grilla
            Vector3 localPoint = grid.transform.InverseTransformPoint(worldPoint);
            
            // Calcular celda (cada celda es 0.1 unidades)
            int cellX = Mathf.FloorToInt(localPoint.x / 0.1f);
            int cellY = Mathf.FloorToInt(localPoint.y / 0.1f);
            
            var armorData = GetCurrentArmorData();
            if (armorData == null) return;
            
            int armorSizeX = armorData.tailGrid.gridInfo.sizeX;
            int armorSizeY = armorData.tailGrid.gridInfo.sizeY;
            
            // Ajustar para que la pieza no se salga de la grilla
            int maxX = Mathf.Max(0, grid.GridInfo.sizeX - armorSizeX);
            int maxY = Mathf.Max(0, grid.GridInfo.sizeY - armorSizeY);
            
            currentPositionX = Mathf.Clamp(cellX, 0, maxX);
            currentPositionY = Mathf.Clamp(cellY, 0, maxY);
        }
        
        private GridHead FindClosestGridToRay(Ray ray)
        {
            GridHead closest = null;
            float closestDistance = gridDetectionRadius;
            
            foreach (var grid in availableGrids)
            {
                // Calcular el centro de la grilla
                Vector3 gridCenter = grid.transform.position + 
                    grid.transform.rotation * new Vector3(
                        grid.GridInfo.sizeX * 0.05f,
                        grid.GridInfo.sizeY * 0.05f,
                        0f
                    );
                
                // Calcular distancia del rayo al centro de la grilla
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
            // Usar Unlit shader para colores más precisos
            Material mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = color;
            return mat;
        }
        
        private void CreateGridHighlights()
        {
            DestroyGridHighlights();
            
            // Color azul celeste transparente para grillas
            Color gridColor = new Color(0f, 0.8f, 1f, 0.3f);
            
            foreach (var grid in availableGrids)
            {
                GameObject highlight = GameObject.CreatePrimitive(PrimitiveType.Cube);
                highlight.name = $"GridHighlight_{grid.GridInfo.gridName}";
                
                // Desactivar collider del highlight
                var collider = highlight.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                
                // Crear material azul celeste transparente
                Material mat = CreateTransparentMaterial(gridColor);
                var renderer = highlight.GetComponent<MeshRenderer>();
                renderer.material = mat;
                
                // Posicionar y escalar
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
            // Color azul celeste para grillas no seleccionadas
            Color normalColor = new Color(0f, 0.8f, 1f, 0.3f);
            // Color azul celeste más brillante para grilla seleccionada
            Color selectedColor = new Color(0f, 0.9f, 1f, 0.5f);
            
            foreach (var kvp in gridMaterials)
            {
                if (kvp.Key == hoveredGrid)
                {
                    kvp.Value.color = selectedColor;
                }
                else
                {
                    kvp.Value.color = normalColor;
                }
            }
        }
        
        private void DestroyGridHighlights()
        {
            foreach (var kvp in gridHighlights)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value);
                }
            }
            gridHighlights.Clear();
            
            foreach (var kvp in gridMaterials)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value);
                }
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
            
            Debug.Log($"Pieza seleccionada: {GetCurrentArmorData().displayName}");
        }
        
        private void MovePosition(int deltaX, int deltaY)
        {
            if (hoveredGrid == null) return;
            
            var armor = GetCurrentArmorData();
            if (armor == null) return;
            
            int maxX = Mathf.Max(0, hoveredGrid.GridInfo.sizeX - armor.tailGrid.gridInfo.sizeX);
            int maxY = Mathf.Max(0, hoveredGrid.GridInfo.sizeY - armor.tailGrid.gridInfo.sizeY);
            
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
            
            if (!hoveredGrid.CanPlace(armorData, currentPositionX, currentPositionY))
            {
                Debug.LogWarning($"No se puede colocar {armorData.displayName} en posición ({currentPositionX}, {currentPositionY})");
                return;
            }
            
            ArmorPart armorPart = RobotFactory.Instance.CreateArmorPart(armorData);
            
            if (armorPart == null)
            {
                Debug.LogError("Error al crear la pieza de armadura.");
                return;
            }
            
            if (hoveredGrid.TryPlace(armorPart, currentPositionX, currentPositionY))
            {
                Debug.Log($"✓ {armorData.displayName} colocada en {hoveredGrid.GridInfo.gridName} posición ({currentPositionX}, {currentPositionY})");
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
            previewObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            previewObject.name = "ArmorPreview";
            
            var collider = previewObject.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            
            previewRenderer = previewObject.GetComponent<MeshRenderer>();
            // Usar Unlit shader para colores precisos
            previewMaterial = new Material(Shader.Find("Sprites/Default"));
            previewRenderer.material = previewMaterial;
            
            previewObject.SetActive(false);
        }
        
        private void UpdatePreview()
        {
            var armorData = GetCurrentArmorData();
            
            if (hoveredGrid == null || armorData == null)
            {
                previewObject.SetActive(false);
                return;
            }
            
            previewObject.SetActive(true);
            
            int sizeX = armorData.tailGrid.gridInfo.sizeX;
            int sizeY = armorData.tailGrid.gridInfo.sizeY;
            
            previewObject.transform.localScale = new Vector3(sizeX * 0.1f, sizeY * 0.1f, 0.05f);
            
            Vector3 cellPos = hoveredGrid.CellToWorldPosition(currentPositionX, currentPositionY);
            Vector3 offset = new Vector3(sizeX * 0.05f, sizeY * 0.05f, 0.025f);
            previewObject.transform.position = cellPos + hoveredGrid.transform.rotation * offset;
            previewObject.transform.rotation = hoveredGrid.transform.rotation;
            
            bool canPlace = hoveredGrid.CanPlace(armorData, currentPositionX, currentPositionY);
            
            // Verde transparente si es válido, rojo transparente si no
            if (canPlace)
            {
                previewMaterial.color = new Color(0f, 1f, 0f, 0.5f);
            }
            else
            {
                previewMaterial.color = new Color(1f, 0f, 0f, 0.5f);
            }
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
                info += $"Pieza: {armorData.displayName}\n";
                info += $"Tamaño: {armorData.tailGrid.gridInfo.sizeX}x{armorData.tailGrid.gridInfo.sizeY}\n";
                info += $"Surrounding: {armorData.tailGrid.gridInfo.surrounding}\n\n";
            }
            
            info += $"Posición: ({currentPositionX}, {currentPositionY})\n\n";
            
            if (hoveredGrid != null && armorData != null)
            {
                bool canPlace = hoveredGrid.CanPlace(armorData, currentPositionX, currentPositionY);
                info += canPlace ? "Estado: VÁLIDO" : "Estado: INVÁLIDO";
            }
            
            GUI.Box(new Rect(10, 10, 300, 240), info, style);
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
        }
    }
}
