using System.Collections.Generic;
using UnityEngine;
using RobotGame.Data;
using RobotGame.Enums;

namespace RobotGame.Utils
{
    /// <summary>
    /// Utilidad para auto-detectar grillas Head y Tail desde los Empties de un prefab.
    /// Busca objetos con nomenclatura Head_NxM_SX_nombre o Tail_NxM_SX_nombre.
    /// </summary>
    public static class GridAutoDetector
    {
        /// <summary>
        /// Busca todos los Empties con prefijo Head_ en un prefab y retorna las definiciones de grilla.
        /// </summary>
        public static List<HeadGridDefinition> DetectHeadGrids(GameObject prefab)
        {
            List<HeadGridDefinition> grids = new List<HeadGridDefinition>();
            
            if (prefab == null) return grids;
            
            // Primero verificar el objeto raíz
            if (NomenclatureParser.TryParse(prefab.name, out GridInfo rootGridInfo))
            {
                if (rootGridInfo.isHead)
                {
                    HeadGridDefinition def = new HeadGridDefinition
                    {
                        gridInfo = rootGridInfo,
                        transformName = prefab.name
                    };
                    grids.Add(def);
                }
            }
            
            // Buscar recursivamente todos los transforms hijos
            SearchForHeadGrids(prefab.transform, grids);
            
            return grids;
        }
        
        /// <summary>
        /// Busca todos los Empties con prefijo Tail_ en un prefab y retorna las definiciones de grilla.
        /// </summary>
        public static List<TailGridDefinition> DetectTailGrids(GameObject prefab)
        {
            List<TailGridDefinition> grids = new List<TailGridDefinition>();
            
            if (prefab == null) return grids;
            
            // Primero verificar el objeto raíz
            if (NomenclatureParser.TryParse(prefab.name, out GridInfo rootGridInfo))
            {
                if (!rootGridInfo.isHead) // Es Tail
                {
                    TailGridDefinition def = new TailGridDefinition
                    {
                        gridInfo = rootGridInfo,
                        transformName = prefab.name
                    };
                    grids.Add(def);
                }
            }
            
            // Buscar recursivamente todos los transforms hijos
            SearchForTailGrids(prefab.transform, grids);
            
            return grids;
        }
        
        /// <summary>
        /// Busca el primer Empty con prefijo Tail_ en un prefab (para piezas de armadura que solo tienen uno).
        /// </summary>
        public static TailGridDefinition DetectSingleTailGrid(GameObject prefab)
        {
            var grids = DetectTailGrids(prefab);
            
            if (grids.Count > 0)
            {
                return grids[0];
            }
            
            return null;
        }
        
        private static void SearchForHeadGrids(Transform parent, List<HeadGridDefinition> grids)
        {
            foreach (Transform child in parent)
            {
                // Verificar si el nombre sigue la nomenclatura Head_
                if (NomenclatureParser.TryParse(child.name, out GridInfo gridInfo))
                {
                    if (gridInfo.isHead)
                    {
                        HeadGridDefinition def = new HeadGridDefinition
                        {
                            gridInfo = gridInfo,
                            transformName = child.name
                        };
                        grids.Add(def);
                    }
                }
                
                // Buscar recursivamente en hijos
                SearchForHeadGrids(child, grids);
            }
        }
        
        private static void SearchForTailGrids(Transform parent, List<TailGridDefinition> grids)
        {
            foreach (Transform child in parent)
            {
                // Verificar si el nombre sigue la nomenclatura Tail_
                if (NomenclatureParser.TryParse(child.name, out GridInfo gridInfo))
                {
                    if (!gridInfo.isHead) // Es Tail
                    {
                        TailGridDefinition def = new TailGridDefinition
                        {
                            gridInfo = gridInfo,
                            transformName = child.name
                        };
                        grids.Add(def);
                    }
                }
                
                // Buscar recursivamente en hijos
                SearchForTailGrids(child, grids);
            }
        }
        
        /// <summary>
        /// Auto-configura las grillas de armadura de un StructuralPartData basándose en los Empties del prefab.
        /// </summary>
        public static void AutoConfigureStructuralPart(StructuralPartData partData)
        {
            if (partData == null || partData.prefab == null)
            {
                Debug.LogWarning("GridAutoDetector: PartData o prefab es null.");
                return;
            }
            
            var detectedGrids = DetectHeadGrids(partData.prefab);
            
            if (detectedGrids.Count > 0)
            {
                partData.armorGrids.Clear();
                partData.armorGrids.AddRange(detectedGrids);
                Debug.Log($"GridAutoDetector: Configuradas {detectedGrids.Count} grillas en '{partData.displayName}'");
            }
            else
            {
                Debug.Log($"GridAutoDetector: No se encontraron grillas Head_ en '{partData.displayName}'");
            }
        }
        
        /// <summary>
        /// Auto-configura la grilla Tail de un ArmorPartData basándose en los Empties del prefab.
        /// </summary>
        public static void AutoConfigureArmorPart(ArmorPartData armorData)
        {
            if (armorData == null || armorData.prefab == null)
            {
                Debug.LogWarning("GridAutoDetector: ArmorData o prefab es null.");
                return;
            }
            
            var tailGrid = DetectSingleTailGrid(armorData.prefab);
            
            if (tailGrid != null)
            {
                armorData.tailGrid = tailGrid;
                Debug.Log($"GridAutoDetector: Configurada grilla Tail en '{armorData.displayName}' - {tailGrid.gridInfo.sizeX}x{tailGrid.gridInfo.sizeY}");
            }
            else
            {
                Debug.LogWarning($"GridAutoDetector: No se encontró grilla Tail_ en '{armorData.displayName}'");
            }
            
            // También buscar grillas Head adicionales (para apilar)
            var headGrids = DetectHeadGrids(armorData.prefab);
            if (headGrids.Count > 0)
            {
                armorData.additionalHeadGrids = headGrids.ToArray();
                Debug.Log($"GridAutoDetector: Configuradas {headGrids.Count} grillas Head adicionales en '{armorData.displayName}'");
            }
        }
    }
}
