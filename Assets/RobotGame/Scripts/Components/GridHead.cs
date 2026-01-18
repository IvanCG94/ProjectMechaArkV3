using System.Collections.Generic;
using UnityEngine;
using RobotGame.Data;
using RobotGame.Enums;
using RobotGame.Utils;
using RobotGame.Config;

namespace RobotGame.Components
{
    /// <summary>
    /// Componente runtime que representa una grilla Head donde se pueden insertar piezas de armadura.
    /// 
    /// El tier de la grilla determina:
    /// - El tamaño físico de cada celda (configurado en GridTierConfig)
    /// - Qué piezas armor pueden colocarse (solo del mismo tier)
    /// </summary>
    public class GridHead : MonoBehaviour
    {
        [Header("Configuración")]
        [SerializeField] private GridInfo gridInfo;
        
        [Header("Estado")]
        [SerializeField] private List<ArmorPart> placedParts = new List<ArmorPart>();
        
        // Estado interno de ocupación
        private bool[,] occupiedCells;
        private string[,] cellOccupants;
        
        /// <summary>
        /// Información de la grilla.
        /// </summary>
        public GridInfo GridInfo => gridInfo;
        
        /// <summary>
        /// Tier de la grilla (1-6). Retorna 1 si no está configurado.
        /// </summary>
        public int Tier => gridInfo.Tier;
        
        /// <summary>
        /// Tamaño de cada celda en unidades de Unity.
        /// </summary>
        public float CellSize => gridInfo.CellSize;
        
        /// <summary>
        /// Piezas de armadura colocadas en esta grilla.
        /// </summary>
        public IReadOnlyList<ArmorPart> PlacedParts => placedParts;
        
        /// <summary>
        /// Inicializa la grilla con su información.
        /// </summary>
        public void Initialize(GridInfo info)
        {
            gridInfo = info;
            
            // Inicializar matriz de ocupación
            occupiedCells = new bool[info.sizeX, info.sizeY];
            cellOccupants = new string[info.sizeX, info.sizeY];
            
            for (int x = 0; x < info.sizeX; x++)
            {
                for (int y = 0; y < info.sizeY; y++)
                {
                    occupiedCells[x, y] = false;
                    cellOccupants[x, y] = null;
                }
            }
            
            // Crear collider para raycast detection
            EnsureCollider();
        }
        
        /// <summary>
        /// Asegura que la grilla tenga un BoxCollider configurado correctamente.
        /// Usa el cellSize del tier de la grilla.
        /// </summary>
        public void EnsureCollider()
        {
            BoxCollider collider = GetComponent<BoxCollider>();
            bool wasCreated = collider == null;
            
            if (collider == null)
            {
                collider = gameObject.AddComponent<BoxCollider>();
            }
            
            float cellSize = CellSize;
            float sizeX = gridInfo.sizeX * cellSize;
            float sizeY = gridInfo.sizeY * cellSize;
            collider.size = new Vector3(sizeX, sizeY, cellSize);
            collider.center = new Vector3(sizeX / 2f, sizeY / 2f, -cellSize / 2f);
            collider.isTrigger = true;
            
            Debug.Log($"GridHead.EnsureCollider: {gameObject.name} (Tier {Tier}) - {(wasCreated ? "CREADO" : "actualizado")} - Size: {collider.size}, CellSize: {cellSize}");
        }
        
        /// <summary>
        /// Verifica si una pieza puede colocarse en la posición especificada (sin rotación).
        /// </summary>
        public bool CanPlace(ArmorPartData armorData, int startX, int startY)
        {
            return CanPlace(armorData, startX, startY, GridRotation.Rotation.Deg0);
        }
        
        /// <summary>
        /// Verifica si una pieza puede colocarse en la posición especificada con una rotación.
        /// </summary>
        public bool CanPlace(ArmorPartData armorData, int startX, int startY, GridRotation.Rotation rotation)
        {
            if (armorData == null || armorData.tailGrid == null) return false;
            
            // Verificar compatibilidad de tier
            if (armorData.ArmorTier != Tier)
            {
                Debug.LogWarning($"GridHead.CanPlace: Pieza tier {armorData.ArmorTier} no compatible con grilla tier {Tier}");
                return false;
            }
            
            // Obtener GridInfo rotado
            GridInfo rotatedTail = GridRotation.RotateGridInfo(armorData.tailGrid.gridInfo, rotation);
            
            return CanPlaceInternal(startX, startY, rotatedTail);
        }
        
        /// <summary>
        /// Verifica si un GridInfo específico puede colocarse en la posición.
        /// </summary>
        private bool CanPlaceInternal(int startX, int startY, GridInfo tailInfo)
        {
            int sizeX = tailInfo.sizeX;
            int sizeY = tailInfo.sizeY;
            SurroundingLevel tailSurrounding = tailInfo.surrounding;
            
            // Verificar límites
            if (startX < 0 || startY < 0)
            {
                return false;
            }
            
            if (startX + sizeX > gridInfo.sizeX || startY + sizeY > gridInfo.sizeY)
            {
                return false;
            }
            
            // Verificar compatibilidad de Surrounding level
            if (!gridInfo.surrounding.CanAcceptLevel(tailSurrounding))
            {
                return false;
            }
            
            // Verificar compatibilidad de bordes/Full
            if (!CanAcceptEdgesAtPosition(startX, startY, tailInfo))
            {
                return false;
            }
            
            // Verificar que todas las celdas estén libres
            for (int x = startX; x < startX + sizeX; x++)
            {
                for (int y = startY; y < startY + sizeY; y++)
                {
                    if (occupiedCells[x, y])
                    {
                        return false;
                    }
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Verifica si los bordes del Tail son válidos en la posición dada.
        /// Las piezas con bordes solo pueden colocarse en los bordes correspondientes de la grilla.
        /// </summary>
        private bool CanAcceptEdgesAtPosition(int startX, int startY, GridInfo tailInfo)
        {
            SurroundingLevel tailSurrounding = tailInfo.surrounding;
            SurroundingLevel headSurrounding = gridInfo.surrounding;
            
            // Si el Tail no tiene bordes ni es Full (SN plano o SX sin especificación), siempre es compatible en posición
            if (!tailSurrounding.HasEdges && !tailSurrounding.IsFull)
            {
                return true;
            }
            
            // Caso especial: Head es Full (FH o FV)
            if (headSurrounding.IsFull)
            {
                // Primero verificar compatibilidad básica de bordes
                if (!headSurrounding.CanAcceptEdges(tailSurrounding))
                {
                    return false;
                }
                
                // Para piezas Full Tail
                if (tailSurrounding.IsFull)
                {
                    // FH: debe tener el mismo ancho (N) y posición X debe ser 0
                    if (tailSurrounding.fullType == FullType.FH)
                    {
                        if (tailInfo.sizeX != gridInfo.sizeX)
                        {
                            return false;
                        }
                        // FH implica LR, entonces startX debe ser 0
                        if (startX != 0)
                        {
                            return false;
                        }
                        // La posición Y puede variar libremente (siempre que quepa)
                        return true;
                    }
                    // FV: debe tener la misma altura (M) y posición Y debe ser 0
                    else if (tailSurrounding.fullType == FullType.FV)
                    {
                        if (tailInfo.sizeY != gridInfo.sizeY)
                        {
                            return false;
                        }
                        // FV implica TB, entonces startY debe ser 0
                        if (startY != 0)
                        {
                            return false;
                        }
                        // La posición X puede variar libremente (siempre que quepa)
                        return true;
                    }
                }
                
                // Para piezas con LR en Head FH, deben ocupar todo el ancho
                if (headSurrounding.fullType == FullType.FH)
                {
                    if ((tailSurrounding.edges & EdgeFlags.LR) == EdgeFlags.LR)
                    {
                        // LR debe ocupar todo el ancho
                        if (tailInfo.sizeX != gridInfo.sizeX)
                        {
                            return false;
                        }
                    }
                    // Verificar posición para bordes individuales L o R
                    return ValidateSingleEdgePosition(startX, startY, tailInfo, tailSurrounding.edges);
                }
                
                // Para piezas con TB en Head FV, deben ocupar toda la altura
                if (headSurrounding.fullType == FullType.FV)
                {
                    if ((tailSurrounding.edges & EdgeFlags.TB) == EdgeFlags.TB)
                    {
                        // TB debe ocupar toda la altura
                        if (tailInfo.sizeY != gridInfo.sizeY)
                        {
                            return false;
                        }
                    }
                    // Verificar posición para bordes individuales T o B
                    return ValidateSingleEdgePosition(startX, startY, tailInfo, tailSurrounding.edges);
                }
                
                return false;
            }
            
            // Si el Tail es Full pero el Head no es Full, no es compatible
            if (tailSurrounding.IsFull)
            {
                return false;
            }
            
            // Caso especial LRTB: debe ocupar toda la grilla y Head debe tener LRTB
            if (tailSurrounding.HasAllEdges)
            {
                bool occupiesFullGrid = startX == 0 && startY == 0 && 
                                        tailInfo.sizeX == gridInfo.sizeX && 
                                        tailInfo.sizeY == gridInfo.sizeY;
                return headSurrounding.HasAllEdges && occupiesFullGrid;
            }
            
            // Verificar cada borde que el Tail necesita
            EdgeFlags tailEdges = tailSurrounding.edges;
            EdgeFlags headEdges = headSurrounding.edges;
            
            // El Head debe tener los bordes que el Tail necesita
            if ((headEdges & tailEdges) != tailEdges)
            {
                return false;
            }
            
            // Verificar que piezas con bordes opuestos ocupen todo el ancho/alto
            if ((tailEdges & EdgeFlags.LR) == EdgeFlags.LR)
            {
                // LR debe ocupar todo el ancho
                if (tailInfo.sizeX != gridInfo.sizeX)
                {
                    return false;
                }
            }
            
            if ((tailEdges & EdgeFlags.TB) == EdgeFlags.TB)
            {
                // TB debe ocupar toda la altura
                if (tailInfo.sizeY != gridInfo.sizeY)
                {
                    return false;
                }
            }
            
            // Verificar posición para bordes individuales
            return ValidateSingleEdgePosition(startX, startY, tailInfo, tailEdges);
        }
        
        /// <summary>
        /// Valida que la pieza esté posicionada en los bordes correctos.
        /// </summary>
        private bool ValidateSingleEdgePosition(int startX, int startY, GridInfo tailInfo, EdgeFlags tailEdges)
        {
            int endX = startX + tailInfo.sizeX - 1;
            int endY = startY + tailInfo.sizeY - 1;
            
            // Si necesita borde L, debe estar en x = 0
            if ((tailEdges & EdgeFlags.L) != 0 && startX != 0)
            {
                return false;
            }
            
            // Si necesita borde R, debe terminar en el borde derecho
            if ((tailEdges & EdgeFlags.R) != 0 && endX != gridInfo.sizeX - 1)
            {
                return false;
            }
            
            // Si necesita borde B, debe estar en y = 0
            if ((tailEdges & EdgeFlags.B) != 0 && startY != 0)
            {
                return false;
            }
            
            // Si necesita borde T, debe terminar en el borde superior
            if ((tailEdges & EdgeFlags.T) != 0 && endY != gridInfo.sizeY - 1)
            {
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Intenta colocar una pieza de armadura en la posición especificada (sin rotación).
        /// </summary>
        public bool TryPlace(ArmorPart armorPart, int startX, int startY)
        {
            return TryPlace(armorPart, startX, startY, GridRotation.Rotation.Deg0);
        }
        
        /// <summary>
        /// Intenta colocar una pieza de armadura en la posición especificada con rotación.
        /// </summary>
        public bool TryPlace(ArmorPart armorPart, int startX, int startY, GridRotation.Rotation rotation)
        {
            if (armorPart == null || armorPart.ArmorData == null)
            {
                Debug.LogWarning("Intentando colocar una pieza de armadura null.");
                return false;
            }
            
            ArmorPartData data = armorPart.ArmorData;
            GridInfo rotatedTail = GridRotation.RotateGridInfo(data.tailGrid.gridInfo, rotation);
            
            if (!CanPlaceInternal(startX, startY, rotatedTail))
            {
                return false;
            }
            
            // Marcar celdas como ocupadas
            for (int x = startX; x < startX + rotatedTail.sizeX; x++)
            {
                for (int y = startY; y < startY + rotatedTail.sizeY; y++)
                {
                    occupiedCells[x, y] = true;
                    cellOccupants[x, y] = armorPart.InstanceId;
                }
            }
            
            // Posicionar la pieza - SINCRONIZADO CON EL PREVIEW
            armorPart.transform.SetParent(transform);
            
            // Usar el cellSize de la grilla
            float cellSize = CellSize;
            float halfCell = cellSize * 0.5f;
            
            // Tamaño original y rotado
            int originalSizeX = data.tailGrid.gridInfo.sizeX;
            int originalSizeY = data.tailGrid.gridInfo.sizeY;
            
            // Posición base de la celda
            Vector3 cellPos = CellToLocalPosition(startX, startY);
            
            // Calcular posición del pivote (centro del área rotada)
            float pivotX = rotatedTail.sizeX * halfCell;
            float pivotY = rotatedTail.sizeY * halfCell;
            
            // Calcular offset del modelo (centro del tamaño original, negativo)
            float modelOffsetX = -originalSizeX * halfCell;
            float modelOffsetY = -originalSizeY * halfCell;
            
            // El modelo se rota alrededor del pivote
            // Primero trasladamos al pivote, luego aplicamos la rotación al offset del modelo
            Quaternion rot = GridRotation.ToQuaternion(rotation);
            Vector3 rotatedModelOffset = rot * new Vector3(modelOffsetX, modelOffsetY, 0f);
            
            Vector3 finalPos = cellPos + new Vector3(pivotX, pivotY, 0f) + rotatedModelOffset;
            
            // Posición final = celda + pivote + offset rotado
            armorPart.transform.localPosition = finalPos;
            armorPart.transform.localRotation = rot;
            
            armorPart.OnPlaced(this, startX, startY, rotation);
            placedParts.Add(armorPart);
            
            return true;
        }
        
        /// <summary>
        /// Remueve una pieza de armadura de la grilla.
        /// </summary>
        /// <param name="armorPart">La pieza a remover</param>
        /// <param name="destroyGameObject">Si es true, destruye el GameObject. Si es false, solo lo desvincula (para inventario)</param>
        /// <returns>True si se removió exitosamente</returns>
        public bool Remove(ArmorPart armorPart, bool destroyGameObject = true)
        {
            if (armorPart == null || !placedParts.Contains(armorPart))
            {
                return false;
            }
            
            // Primero remover recursivamente todas las piezas hijas
            RemoveChildParts(armorPart, destroyGameObject);
            
            // Liberar celdas
            for (int x = 0; x < gridInfo.sizeX; x++)
            {
                for (int y = 0; y < gridInfo.sizeY; y++)
                {
                    if (cellOccupants[x, y] == armorPart.InstanceId)
                    {
                        occupiedCells[x, y] = false;
                        cellOccupants[x, y] = null;
                    }
                }
            }
            
            armorPart.OnRemoved(this);
            placedParts.Remove(armorPart);
            
            if (destroyGameObject)
            {
                GameObject.Destroy(armorPart.gameObject);
            }
            else
            {
                armorPart.transform.SetParent(null);
            }
            
            return true;
        }
        
        /// <summary>
        /// Remueve recursivamente todas las piezas colocadas en las grillas adicionales de una pieza.
        /// </summary>
        private void RemoveChildParts(ArmorPart parentPart, bool destroyGameObject)
        {
            if (parentPart.AdditionalGrids == null || parentPart.AdditionalGrids.Count == 0)
            {
                return;
            }
            
            foreach (var childGrid in parentPart.AdditionalGrids)
            {
                // Crear copia de la lista porque vamos a modificarla
                var partsToRemove = new List<ArmorPart>(childGrid.PlacedParts);
                
                foreach (var childPart in partsToRemove)
                {
                    childGrid.Remove(childPart, destroyGameObject);
                }
            }
        }
        
        /// <summary>
        /// Remueve todas las piezas de la grilla.
        /// </summary>
        /// <param name="destroyGameObjects">Si es true, destruye los GameObjects</param>
        /// <returns>Lista de piezas removidas (vacía si destroyGameObjects es true)</returns>
        public List<ArmorPart> RemoveAll(bool destroyGameObjects = true)
        {
            var removedParts = new List<ArmorPart>();
            var partsToRemove = new List<ArmorPart>(placedParts);
            
            foreach (var part in partsToRemove)
            {
                if (!destroyGameObjects)
                {
                    removedParts.Add(part);
                }
                Remove(part, destroyGameObjects);
            }
            
            return removedParts;
        }
        
        /// <summary>
        /// Obtiene la pieza en una celda específica.
        /// </summary>
        public ArmorPart GetPartAtCell(int cellX, int cellY)
        {
            if (cellX < 0 || cellX >= gridInfo.sizeX || cellY < 0 || cellY >= gridInfo.sizeY)
            {
                return null;
            }
            
            string occupantId = cellOccupants[cellX, cellY];
            if (string.IsNullOrEmpty(occupantId))
            {
                return null;
            }
            
            return placedParts.Find(p => p.InstanceId == occupantId);
        }
        
        /// <summary>
        /// Convierte una posición de celda a posición local.
        /// El pivote está en la esquina inferior-izquierda-trasera (-X, -Y, -Z).
        /// Usa el cellSize del tier de la grilla.
        /// </summary>
        public Vector3 CellToLocalPosition(int cellX, int cellY)
        {
            float cellSize = CellSize;
            return new Vector3(cellX * cellSize, cellY * cellSize, 0f);
        }
        
        /// <summary>
        /// Convierte una posición de celda a posición mundial.
        /// </summary>
        public Vector3 CellToWorldPosition(int cellX, int cellY)
        {
            Vector3 localPos = CellToLocalPosition(cellX, cellY);
            return transform.TransformPoint(localPos);
        }
        
        /// <summary>
        /// Obtiene todas las posiciones válidas donde una pieza puede colocarse (sin rotación).
        /// Nota: Solo considera piezas del mismo tier que la grilla.
        /// </summary>
        public List<Vector2Int> GetValidPlacements(ArmorPartData armorData)
        {
            return GetValidPlacements(armorData, GridRotation.Rotation.Deg0);
        }
        
        /// <summary>
        /// Obtiene todas las posiciones válidas donde una pieza puede colocarse con rotación.
        /// </summary>
        public List<Vector2Int> GetValidPlacements(ArmorPartData armorData, GridRotation.Rotation rotation)
        {
            List<Vector2Int> validPositions = new List<Vector2Int>();
            
            if (armorData == null || armorData.tailGrid == null) return validPositions;
            
            // Verificar compatibilidad de tier primero
            if (armorData.ArmorTier != Tier)
            {
                return validPositions; // Lista vacía si tiers no coinciden
            }
            
            GridInfo rotatedTail = GridRotation.RotateGridInfo(armorData.tailGrid.gridInfo, rotation);
            
            // Probar todas las posiciones posibles
            for (int x = 0; x <= gridInfo.sizeX - rotatedTail.sizeX; x++)
            {
                for (int y = 0; y <= gridInfo.sizeY - rotatedTail.sizeY; y++)
                {
                    if (CanPlaceInternal(x, y, rotatedTail))
                    {
                        validPositions.Add(new Vector2Int(x, y));
                    }
                }
            }
            
            return validPositions;
        }
        
        /// <summary>
        /// Obtiene todas las rotaciones válidas para una pieza en esta grilla.
        /// </summary>
        public GridRotation.Rotation[] GetValidRotations(ArmorPartData armorData)
        {
            if (armorData == null || armorData.tailGrid == null)
            {
                return new GridRotation.Rotation[0];
            }
            
            // Verificar compatibilidad de tier
            if (armorData.ArmorTier != Tier)
            {
                return new GridRotation.Rotation[0];
            }
            
            return GridRotation.GetValidRotations(armorData.tailGrid.gridInfo, gridInfo);
        }
        
        /// <summary>
        /// Verifica si una celda específica está ocupada.
        /// </summary>
        public bool IsCellOccupied(int x, int y)
        {
            if (x < 0 || x >= gridInfo.sizeX || y < 0 || y >= gridInfo.sizeY)
            {
                return true; // Fuera de límites se considera ocupado
            }
            
            return occupiedCells[x, y];
        }
        
        private void OnDrawGizmosSelected()
        {
            if (gridInfo.sizeX == 0 || gridInfo.sizeY == 0) return;
            
            // Dibujar la grilla
            Gizmos.color = Color.cyan;
            
            float cellSize = CellSize;
            
            for (int x = 0; x <= gridInfo.sizeX; x++)
            {
                Vector3 start = transform.TransformPoint(new Vector3(x * cellSize, 0, 0));
                Vector3 end = transform.TransformPoint(new Vector3(x * cellSize, gridInfo.sizeY * cellSize, 0));
                Gizmos.DrawLine(start, end);
            }
            
            for (int y = 0; y <= gridInfo.sizeY; y++)
            {
                Vector3 start = transform.TransformPoint(new Vector3(0, y * cellSize, 0));
                Vector3 end = transform.TransformPoint(new Vector3(gridInfo.sizeX * cellSize, y * cellSize, 0));
                Gizmos.DrawLine(start, end);
            }
            
            // Marcar celdas ocupadas
            if (occupiedCells != null)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
                for (int x = 0; x < gridInfo.sizeX; x++)
                {
                    for (int y = 0; y < gridInfo.sizeY; y++)
                    {
                        if (occupiedCells[x, y])
                        {
                            Vector3 center = transform.TransformPoint(new Vector3((x + 0.5f) * cellSize, (y + 0.5f) * cellSize, 0));
                            Gizmos.DrawCube(center, Vector3.one * cellSize * 0.8f);
                        }
                    }
                }
            }
            
            // Dibujar indicadores de bordes abiertos
            DrawEdgeIndicators();
        }
        
        private void DrawEdgeIndicators()
        {
            float cellSize = CellSize;
            EdgeFlags edges = gridInfo.surrounding.edges;
            
            Gizmos.color = Color.green;
            float indicatorSize = cellSize * 0.2f; // Proporcional al tamaño de celda
            
            // Indicador L (borde izquierdo)
            if ((edges & EdgeFlags.L) != 0 || gridInfo.surrounding.IsFull)
            {
                for (int y = 0; y < gridInfo.sizeY; y++)
                {
                    Vector3 pos = transform.TransformPoint(new Vector3(-indicatorSize, (y + 0.5f) * cellSize, 0));
                    Gizmos.DrawCube(pos, Vector3.one * indicatorSize);
                }
            }
            
            // Indicador R (borde derecho)
            if ((edges & EdgeFlags.R) != 0 || gridInfo.surrounding.IsFull)
            {
                for (int y = 0; y < gridInfo.sizeY; y++)
                {
                    Vector3 pos = transform.TransformPoint(new Vector3(gridInfo.sizeX * cellSize + indicatorSize, (y + 0.5f) * cellSize, 0));
                    Gizmos.DrawCube(pos, Vector3.one * indicatorSize);
                }
            }
            
            // Indicador B (borde inferior)
            if ((edges & EdgeFlags.B) != 0 || gridInfo.surrounding.IsFull)
            {
                for (int x = 0; x < gridInfo.sizeX; x++)
                {
                    Vector3 pos = transform.TransformPoint(new Vector3((x + 0.5f) * cellSize, -indicatorSize, 0));
                    Gizmos.DrawCube(pos, Vector3.one * indicatorSize);
                }
            }
            
            // Indicador T (borde superior)
            if ((edges & EdgeFlags.T) != 0 || gridInfo.surrounding.IsFull)
            {
                for (int x = 0; x < gridInfo.sizeX; x++)
                {
                    Vector3 pos = transform.TransformPoint(new Vector3((x + 0.5f) * cellSize, gridInfo.sizeY * cellSize + indicatorSize, 0));
                    Gizmos.DrawCube(pos, Vector3.one * indicatorSize);
                }
            }
        }
    }
}
