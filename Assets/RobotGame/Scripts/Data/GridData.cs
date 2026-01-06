using System;
using UnityEngine;
using RobotGame.Enums;
using RobotGame.Config;

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
    /// 
    /// Nueva nomenclatura con Tier:
    ///   Head_T1_2x3_S1F_nombre
    ///   Tail_T2_2x3_S1_nombre
    /// 
    /// El tier determina el tamaño físico de cada celda (configurado en GridTierConfig).
    /// Una pieza armor solo puede colocarse en una grilla del mismo tier.
    /// </summary>
    [Serializable]
    public struct GridInfo
    {
        public bool isHead;             // True = Head (receptora), False = Tail (ocupación)
        
        [Tooltip("Tier de la grilla (1-6). Determina el tamaño de celda y compatibilidad con piezas armor.")]
        [Range(1, 6)]
        public int tier;                // Tier de la grilla (1-6)
        
        public int sizeX;               // Ancho de la grilla en celdas
        public int sizeY;               // Alto de la grilla en celdas
        public SurroundingLevel surrounding;
        public string gridName;         // Nombre identificador
        
        /// <summary>
        /// Tier efectivo de la grilla. Retorna 1 si tier es 0 (datos legacy sin tier).
        /// </summary>
        public int Tier => tier <= 0 ? 1 : tier;
        
        /// <summary>
        /// Tamaño de cada celda en unidades de Unity (obtenido del GridTierConfig).
        /// </summary>
        public float CellSize => GridTierConfig.Instance.GetCellSize(Tier);
        
        /// <summary>
        /// Tamaño total en unidades Unity.
        /// </summary>
        public Vector2 WorldSize => new Vector2(sizeX * CellSize, sizeY * CellSize);
        
        /// <summary>
        /// Cantidad total de celdas.
        /// </summary>
        public int TotalCells => sizeX * sizeY;
        
        /// <summary>
        /// Verifica si esta grilla puede aceptar una pieza del tier especificado.
        /// Solo acepta piezas del mismo tier. Tier 0 se trata como 1.
        /// </summary>
        public bool CanAcceptTier(int pieceTier)
        {
            int effectivePieceTier = pieceTier <= 0 ? 1 : pieceTier;
            return Tier == effectivePieceTier;
        }
        
        public override string ToString()
        {
            string type = isHead ? "Head" : "Tail";
            return $"{type}_T{Tier}_{sizeX}x{sizeY}_{surrounding}_{gridName}";
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
        /// Usa el cellSize del tier de la grilla.
        /// </summary>
        public Vector3 CellToWorldPosition(int cellX, int cellY)
        {
            float cellSize = info.CellSize;
            Vector3 localOffset = new Vector3(cellX * cellSize, cellY * cellSize, 0);
            return worldPosition + worldRotation * localOffset;
        }
    }
}
