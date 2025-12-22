using System.Collections.Generic;
using UnityEngine;
using RobotGame.Data;
using RobotGame.Enums;

namespace RobotGame.Components
{
    /// <summary>
    /// Componente runtime que representa una grilla Head donde se pueden insertar piezas de armadura.
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
        }
        
        /// <summary>
        /// Verifica si una pieza puede colocarse en la posición especificada.
        /// </summary>
        public bool CanPlace(ArmorPartData armorData, int startX, int startY)
        {
            if (armorData == null) return false;
            
            int sizeX = armorData.tailGrid.gridInfo.sizeX;
            int sizeY = armorData.tailGrid.gridInfo.sizeY;
            SurroundingLevel surrounding = armorData.tailGrid.gridInfo.surrounding;
            
            return CanPlaceInternal(startX, startY, sizeX, sizeY, surrounding);
        }
        
        /// <summary>
        /// Verifica si un tamaño específico puede colocarse en la posición.
        /// </summary>
        public bool CanPlace(int startX, int startY, int sizeX, int sizeY, SurroundingLevel surrounding)
        {
            return CanPlaceInternal(startX, startY, sizeX, sizeY, surrounding);
        }
        
        private bool CanPlaceInternal(int startX, int startY, int sizeX, int sizeY, SurroundingLevel surrounding)
        {
            // Verificar compatibilidad de Surrounding
            if (!gridInfo.surrounding.CanAccept(surrounding))
            {
                return false;
            }
            
            // Verificar límites
            if (startX < 0 || startY < 0)
            {
                return false;
            }
            
            if (startX + sizeX > gridInfo.sizeX || startY + sizeY > gridInfo.sizeY)
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
        /// Intenta colocar una pieza de armadura en la posición especificada.
        /// </summary>
        public bool TryPlace(ArmorPart armorPart, int startX, int startY)
        {
            if (armorPart == null || armorPart.ArmorData == null)
            {
                Debug.LogWarning("Intentando colocar una pieza de armadura null.");
                return false;
            }
            
            ArmorPartData data = armorPart.ArmorData;
            int sizeX = data.tailGrid.gridInfo.sizeX;
            int sizeY = data.tailGrid.gridInfo.sizeY;
            SurroundingLevel surrounding = data.tailGrid.gridInfo.surrounding;
            
            if (!CanPlaceInternal(startX, startY, sizeX, sizeY, surrounding))
            {
                return false;
            }
            
            // Marcar celdas como ocupadas
            for (int x = startX; x < startX + sizeX; x++)
            {
                for (int y = startY; y < startY + sizeY; y++)
                {
                    occupiedCells[x, y] = true;
                    cellOccupants[x, y] = armorPart.InstanceId;
                }
            }
            
            // Posicionar la pieza
            armorPart.transform.SetParent(transform);
            armorPart.transform.localPosition = CellToLocalPosition(startX, startY);
            armorPart.transform.localRotation = Quaternion.identity;
            
            armorPart.OnPlaced(this, startX, startY);
            placedParts.Add(armorPart);
            
            return true;
        }
        
        /// <summary>
        /// Remueve una pieza de armadura de la grilla.
        /// </summary>
        public bool Remove(ArmorPart armorPart)
        {
            if (armorPart == null || !placedParts.Contains(armorPart))
            {
                return false;
            }
            
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
            armorPart.transform.SetParent(null);
            placedParts.Remove(armorPart);
            
            return true;
        }
        
        /// <summary>
        /// Convierte una posición de celda a posición local.
        /// El pivote está en la esquina inferior-izquierda-trasera (-X, -Y, -Z).
        /// </summary>
        public Vector3 CellToLocalPosition(int cellX, int cellY)
        {
            // Cada celda es 0.1 unidades
            return new Vector3(cellX * 0.1f, cellY * 0.1f, 0f);
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
        /// Obtiene todas las posiciones válidas donde una pieza puede colocarse.
        /// </summary>
        public List<Vector2Int> GetValidPlacements(ArmorPartData armorData)
        {
            List<Vector2Int> validPositions = new List<Vector2Int>();
            
            if (armorData == null) return validPositions;
            
            int sizeX = armorData.tailGrid.gridInfo.sizeX;
            int sizeY = armorData.tailGrid.gridInfo.sizeY;
            SurroundingLevel surrounding = armorData.tailGrid.gridInfo.surrounding;
            
            // Verificar compatibilidad de Surrounding primero
            if (!gridInfo.surrounding.CanAccept(surrounding))
            {
                return validPositions;
            }
            
            // Probar todas las posiciones posibles
            for (int x = 0; x <= gridInfo.sizeX - sizeX; x++)
            {
                for (int y = 0; y <= gridInfo.sizeY - sizeY; y++)
                {
                    if (CanPlaceInternal(x, y, sizeX, sizeY, surrounding))
                    {
                        validPositions.Add(new Vector2Int(x, y));
                    }
                }
            }
            
            return validPositions;
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
            
            float cellSize = 0.1f;
            
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
        }
    }
}
