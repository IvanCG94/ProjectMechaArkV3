using System;
using System.Collections.Generic;
using UnityEngine;

namespace RobotGame.Config
{
    /// <summary>
    /// Configuración global de tamaños de celda por tier de grilla.
    /// 
    /// El tier de grilla determina el tamaño físico de cada celda:
    /// - Tier 1: 0.1 unidades (robots pequeños, tamaño jugador)
    /// - Tier 2: configurable (velociraptors)
    /// - Tier 3: configurable (trikes)
    /// - Tier 4: configurable (T-Rex)
    /// - etc.
    /// 
    /// Una pieza de 1x4 en Tier 1 mide 0.1 x 0.4 unidades.
    /// Una pieza de 1x4 en Tier 2 (con cellSize 0.25) mide 0.25 x 1.0 unidades.
    /// 
    /// IMPORTANTE: Las piezas armor de un tier solo pueden colocarse en grillas del mismo tier.
    /// </summary>
    [CreateAssetMenu(fileName = "GridTierConfig", menuName = "RobotGame/Config/Grid Tier Config")]
    public class GridTierConfig : ScriptableObject
    {
        [Serializable]
        public class TierCellSize
        {
            [Tooltip("Número del tier (1-6)")]
            [Range(1, 6)]
            public int tier = 1;
            
            [Tooltip("Tamaño de cada celda en unidades de Unity")]
            [Min(0.01f)]
            public float cellSize = 0.1f;
            
            [Tooltip("Descripción del tier (para referencia)")]
            public string description;
        }
        
        [Header("Configuración de Tiers")]
        [Tooltip("Define el tamaño de celda para cada tier de grilla")]
        [SerializeField]
        private List<TierCellSize> tierSettings = new List<TierCellSize>()
        {
            new TierCellSize { tier = 1, cellSize = 0.1f, description = "Tier 1 - Robots pequeños (jugador)" },
            new TierCellSize { tier = 2, cellSize = 0.25f, description = "Tier 2 - Velociraptors" },
            new TierCellSize { tier = 3, cellSize = 0.5f, description = "Tier 3 - Trikes" },
            new TierCellSize { tier = 4, cellSize = 1.0f, description = "Tier 4 - T-Rex" },
            new TierCellSize { tier = 5, cellSize = 1.5f, description = "Tier 5 - Robots muy grandes" },
            new TierCellSize { tier = 6, cellSize = 2.0f, description = "Tier 6 - Robots colosales" }
        };
        
        // Cache para acceso rápido
        private Dictionary<int, float> _cellSizeCache;
        
        #region Singleton Access
        
        private static GridTierConfig _instance;
        
        /// <summary>
        /// Instancia global del config. Se carga automáticamente desde Resources.
        /// </summary>
        public static GridTierConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<GridTierConfig>("GridTierConfig");
                    
                    if (_instance == null)
                    {
                        Debug.LogWarning("GridTierConfig no encontrado en Resources. Usando valores por defecto.");
                        _instance = CreateInstance<GridTierConfig>();
                    }
                    
                    _instance.BuildCache();
                }
                
                return _instance;
            }
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Obtiene el tamaño de celda para un tier específico.
        /// </summary>
        /// <param name="tier">Tier de la grilla (1-6)</param>
        /// <returns>Tamaño de celda en unidades de Unity</returns>
        public float GetCellSize(int tier)
        {
            if (_cellSizeCache == null)
            {
                BuildCache();
            }
            
            if (_cellSizeCache.TryGetValue(tier, out float size))
            {
                return size;
            }
            
            // Si no existe, usar tier 1 como fallback
            Debug.LogWarning($"GridTierConfig: Tier {tier} no configurado. Usando tier 1 como fallback.");
            return GetCellSize(1);
        }
        
        /// <summary>
        /// Calcula el tamaño en unidades de Unity para una grilla.
        /// </summary>
        /// <param name="tier">Tier de la grilla</param>
        /// <param name="cellsX">Cantidad de celdas en X</param>
        /// <param name="cellsY">Cantidad de celdas en Y</param>
        /// <returns>Tamaño en unidades de Unity</returns>
        public Vector2 GetWorldSize(int tier, int cellsX, int cellsY)
        {
            float cellSize = GetCellSize(tier);
            return new Vector2(cellsX * cellSize, cellsY * cellSize);
        }
        
        /// <summary>
        /// Convierte una posición de celda a posición local.
        /// </summary>
        public Vector3 CellToLocalPosition(int tier, int cellX, int cellY)
        {
            float cellSize = GetCellSize(tier);
            return new Vector3(cellX * cellSize, cellY * cellSize, 0f);
        }
        
        /// <summary>
        /// Verifica si un tier está configurado.
        /// </summary>
        public bool HasTier(int tier)
        {
            if (_cellSizeCache == null)
            {
                BuildCache();
            }
            
            return _cellSizeCache.ContainsKey(tier);
        }
        
        /// <summary>
        /// Obtiene todos los tiers configurados.
        /// </summary>
        public int[] GetConfiguredTiers()
        {
            if (_cellSizeCache == null)
            {
                BuildCache();
            }
            
            int[] tiers = new int[_cellSizeCache.Count];
            _cellSizeCache.Keys.CopyTo(tiers, 0);
            return tiers;
        }
        
        #endregion
        
        #region Private Methods
        
        private void BuildCache()
        {
            _cellSizeCache = new Dictionary<int, float>();
            
            foreach (var setting in tierSettings)
            {
                if (!_cellSizeCache.ContainsKey(setting.tier))
                {
                    _cellSizeCache[setting.tier] = setting.cellSize;
                }
                else
                {
                    Debug.LogWarning($"GridTierConfig: Tier {setting.tier} duplicado. Ignorando.");
                }
            }
            
            // Asegurar que tier 1 siempre exista
            if (!_cellSizeCache.ContainsKey(1))
            {
                _cellSizeCache[1] = 0.1f;
            }
        }
        
        private void OnValidate()
        {
            // Reconstruir cache cuando se modifica en el inspector
            _cellSizeCache = null;
        }
        
        #endregion
    }
}
