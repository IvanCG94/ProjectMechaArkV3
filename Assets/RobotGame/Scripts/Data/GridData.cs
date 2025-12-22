using System;
using UnityEngine;
using RobotGame.Enums;

namespace RobotGame.Data
{
    /// <summary>
    /// Definición de una grilla Head en una pieza (estructural o armadura).
    /// Las grillas Head son receptoras donde se insertan otras piezas.
    /// </summary>
    [Serializable]
    public class HeadGridDefinition
    {
        [Tooltip("Información de la grilla (parseada desde Blender o definida manualmente)")]
        public GridInfo gridInfo;
        
        [Tooltip("Nombre del transform hijo en el prefab que representa esta grilla")]
        public string transformName;
    }
    
    /// <summary>
    /// Definición de la grilla Tail de una pieza de armadura.
    /// Las grillas Tail definen el espacio que ocupa una pieza.
    /// </summary>
    [Serializable]
    public class TailGridDefinition
    {
        [Tooltip("Información de la grilla (parseada desde Blender o definida manualmente)")]
        public GridInfo gridInfo;
        
        [Tooltip("Nombre del transform hijo en el prefab que representa esta grilla")]
        public string transformName;
    }
    
    /// <summary>
    /// Representa la información de una grilla parseada desde la nomenclatura de Blender.
    /// Nomenclatura: Head_2x3_S1F_nombre o Tail_2x3_S1_nombre
    /// </summary>
    [Serializable]
    public struct GridInfo
    {
        public bool isHead;             // True = Head (receptora), False = Tail (ocupación)
        public int sizeX;               // Ancho de la grilla
        public int sizeY;               // Alto de la grilla
        public SurroundingLevel surrounding;
        public string gridName;         // Nombre identificador
        
        /// <summary>
        /// Tamaño total en unidades Unity (cada celda = 0.1)
        /// </summary>
        public Vector2 WorldSize => new Vector2(sizeX * 0.1f, sizeY * 0.1f);
        
        /// <summary>
        /// Cantidad total de celdas
        /// </summary>
        public int TotalCells => sizeX * sizeY;
        
        public override string ToString()
        {
            string type = isHead ? "Head" : "Tail";
            return $"{type}_{sizeX}x{sizeY}_{surrounding}_{gridName}";
        }
    }
    
    /// <summary>
    /// Representa una celda ocupada en una grilla.
    /// </summary>
    [Serializable]
    public struct GridCell
    {
        public int x;
        public int y;
        public bool isOccupied;
        public string occupiedByPartId;  // ID de la pieza que ocupa esta celda
        
        public GridCell(int x, int y)
        {
            this.x = x;
            this.y = y;
            this.isOccupied = false;
            this.occupiedByPartId = null;
        }
        
        public Vector2Int Position => new Vector2Int(x, y);
    }
    
    /// <summary>
    /// Estado de una grilla Head en runtime, con información de ocupación.
    /// </summary>
    [Serializable]
    public class GridState
    {
        public GridInfo info;
        public GridCell[,] cells;
        public Vector3 worldPosition;   // Posición del pivote en el mundo
        public Quaternion worldRotation;
        
        public GridState(GridInfo info, Vector3 position, Quaternion rotation)
        {
            this.info = info;
            this.worldPosition = position;
            this.worldRotation = rotation;
            
            cells = new GridCell[info.sizeX, info.sizeY];
            for (int x = 0; x < info.sizeX; x++)
            {
                for (int y = 0; y < info.sizeY; y++)
                {
                    cells[x, y] = new GridCell(x, y);
                }
            }
        }
        
        /// <summary>
        /// Verifica si una pieza con el tamaño dado puede colocarse en la posición especificada.
        /// </summary>
        public bool CanPlace(int startX, int startY, int pieceSizeX, int pieceSizeY, SurroundingLevel pieceSurrounding)
        {
            // Verificar compatibilidad de Surrounding
            if (!info.surrounding.CanAccept(pieceSurrounding))
            {
                return false;
            }
            
            // Verificar que la pieza cabe dentro de la grilla
            if (startX + pieceSizeX > info.sizeX || startY + pieceSizeY > info.sizeY)
            {
                return false;
            }
            
            // Verificar que no esté fuera de los límites negativos
            if (startX < 0 || startY < 0)
            {
                return false;
            }
            
            // Verificar que todas las celdas necesarias estén libres
            for (int x = startX; x < startX + pieceSizeX; x++)
            {
                for (int y = startY; y < startY + pieceSizeY; y++)
                {
                    if (cells[x, y].isOccupied)
                    {
                        return false;
                    }
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Coloca una pieza en la grilla. Retorna true si fue exitoso.
        /// </summary>
        public bool Place(int startX, int startY, int pieceSizeX, int pieceSizeY, string partId, SurroundingLevel pieceSurrounding)
        {
            if (!CanPlace(startX, startY, pieceSizeX, pieceSizeY, pieceSurrounding))
            {
                return false;
            }
            
            for (int x = startX; x < startX + pieceSizeX; x++)
            {
                for (int y = startY; y < startY + pieceSizeY; y++)
                {
                    cells[x, y].isOccupied = true;
                    cells[x, y].occupiedByPartId = partId;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Remueve una pieza de la grilla.
        /// </summary>
        public void Remove(string partId)
        {
            for (int x = 0; x < info.sizeX; x++)
            {
                for (int y = 0; y < info.sizeY; y++)
                {
                    if (cells[x, y].occupiedByPartId == partId)
                    {
                        cells[x, y].isOccupied = false;
                        cells[x, y].occupiedByPartId = null;
                    }
                }
            }
        }
        
        /// <summary>
        /// Convierte una posición de celda a posición en el mundo.
        /// </summary>
        public Vector3 CellToWorldPosition(int cellX, int cellY)
        {
            Vector3 localOffset = new Vector3(cellX * 0.1f, cellY * 0.1f, 0);
            return worldPosition + worldRotation * localOffset;
        }
    }
}
