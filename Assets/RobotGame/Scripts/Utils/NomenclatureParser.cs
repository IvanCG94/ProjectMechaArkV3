using System;
using System.Text.RegularExpressions;
using RobotGame.Data;
using RobotGame.Enums;
using UnityEngine;

namespace RobotGame.Utils
{
    /// <summary>
    /// Parser para la nomenclatura de Blender.
    /// Formatos soportados:
    ///   Head_NxM_SX_nombre           (solo piezas SN)
    ///   Head_NxM_SX_L_nombre         (borde izquierdo abierto)
    ///   Head_NxM_SX_LRTB_nombre      (todos los bordes abiertos)
    ///   Head_NxM_SXFH_nombre         (Full horizontal)
    ///   Head_NxM_SXFV_nombre         (Full vertical)
    ///   Tail_NxM_SN_nombre           (plana)
    ///   Tail_NxM_SX_R_nombre         (envuelve derecha)
    ///   Tail_NxM_SXF_nombre          (envuelve completamente)
    /// </summary>
    public static class NomenclatureParser
    {
        // Regex actualizado para la nueva nomenclatura
        // Grupos: 1=Type, 2=SizeX, 3=SizeY, 4=SurroundingBase, 5=FullType(opcional), 6=Edges(opcional), 7=Name
        private static readonly Regex NomenclatureRegex = new Regex(
            @"^(Head|Tail)_(\d+)x(\d+)_(SN|S\d+)(FH|FV|F)?(?:_([LRTB]+))?_(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );
        
        // Regex alternativo para nomenclatura sin bordes (compatibilidad hacia atrás)
        private static readonly Regex LegacyRegex = new Regex(
            @"^(Head|Tail)_(\d+)x(\d+)_(SN|S\d+F?)_(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );
        
        /// <summary>
        /// Parsea un nombre de objeto de Blender a GridInfo.
        /// </summary>
        public static bool TryParse(string objectName, out GridInfo gridInfo)
        {
            gridInfo = default;
            
            if (string.IsNullOrEmpty(objectName))
            {
                return false;
            }
            
            // Intentar con el nuevo formato primero
            Match match = NomenclatureRegex.Match(objectName);
            
            if (match.Success)
            {
                return ParseNewFormat(match, out gridInfo);
            }
            
            // Intentar con formato legacy
            match = LegacyRegex.Match(objectName);
            
            if (match.Success)
            {
                return ParseLegacyFormat(match, out gridInfo);
            }
            
            return false;
        }
        
        private static bool ParseNewFormat(Match match, out GridInfo gridInfo)
        {
            gridInfo = default;
            
            try
            {
                bool isHead = match.Groups[1].Value.Equals("Head", StringComparison.OrdinalIgnoreCase);
                int sizeX = int.Parse(match.Groups[2].Value);
                int sizeY = int.Parse(match.Groups[3].Value);
                
                // Parsear surrounding base (SN, S1, S2, etc.)
                string surroundingBase = match.Groups[4].Value.ToUpper();
                int level = 0;
                if (surroundingBase != "SN")
                {
                    level = int.Parse(surroundingBase.Substring(1));
                }
                
                // Parsear Full type (FH, FV)
                FullType fullType = FullType.None;
                string fullStr = match.Groups[5].Value.ToUpper();
                if (!string.IsNullOrEmpty(fullStr))
                {
                    if (fullStr == "FH") fullType = FullType.FH;
                    else if (fullStr == "FV") fullType = FullType.FV;
                    // Nota: "F" solo ya no es válido, debe ser FH o FV
                }
                
                // Parsear edges (L, R, T, B, LR, LRTB, etc.)
                EdgeFlags edges = EdgeFlags.None;
                string edgesStr = match.Groups[6].Value;
                if (!string.IsNullOrEmpty(edgesStr))
                {
                    edges = SurroundingLevel.ParseEdges(edgesStr);
                }
                
                string gridName = match.Groups[7].Value;
                
                gridInfo = new GridInfo
                {
                    isHead = isHead,
                    sizeX = sizeX,
                    sizeY = sizeY,
                    surrounding = SurroundingLevel.Create(level, edges, fullType),
                    gridName = gridName
                };
                
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        private static bool ParseLegacyFormat(Match match, out GridInfo gridInfo)
        {
            gridInfo = default;
            
            try
            {
                bool isHead = match.Groups[1].Value.Equals("Head", StringComparison.OrdinalIgnoreCase);
                int sizeX = int.Parse(match.Groups[2].Value);
                int sizeY = int.Parse(match.Groups[3].Value);
                
                // Parsear surrounding (SN, S1, S2F, etc.) - formato legacy
                string surroundingStr = match.Groups[4].Value;
                SurroundingLevel surrounding = SurroundingLevel.Parse(surroundingStr);
                
                string gridName = match.Groups[5].Value;
                
                gridInfo = new GridInfo
                {
                    isHead = isHead,
                    sizeX = sizeX,
                    sizeY = sizeY,
                    surrounding = surrounding,
                    gridName = gridName
                };
                
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Parsea un nombre de objeto de Blender a GridInfo. Lanza excepción si falla.
        /// </summary>
        public static GridInfo Parse(string objectName)
        {
            if (TryParse(objectName, out GridInfo gridInfo))
            {
                return gridInfo;
            }
            
            throw new FormatException($"Invalid nomenclature format: '{objectName}'.\n" +
                "Expected formats:\n" +
                "  Head_NxM_SX_name\n" +
                "  Head_NxM_SX_LRTB_name\n" +
                "  Head_NxM_SXFH_name\n" +
                "  Tail_NxM_SX_R_name\n" +
                "  Tail_NxM_SXF_name");
        }
        
        /// <summary>
        /// Valida si un nombre sigue el formato de nomenclatura.
        /// </summary>
        public static bool IsValidNomenclature(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) return false;
            return NomenclatureRegex.IsMatch(objectName) || LegacyRegex.IsMatch(objectName);
        }
        
        /// <summary>
        /// Genera un nombre de nomenclatura a partir de los componentes.
        /// </summary>
        public static string Generate(bool isHead, int sizeX, int sizeY, SurroundingLevel surrounding, string name)
        {
            string type = isHead ? "Head" : "Tail";
            string surroundingStr;
            
            if (surrounding.level == 0)
            {
                surroundingStr = "SN";
            }
            else
            {
                surroundingStr = $"S{surrounding.level}";
                
                if (surrounding.IsFull)
                {
                    surroundingStr += surrounding.fullType.ToString();
                }
            }
            
            if (surrounding.HasEdges && !surrounding.IsFull)
            {
                string edgesStr = SurroundingLevel.EdgesToString(surrounding.edges);
                return $"{type}_{sizeX}x{sizeY}_{surroundingStr}_{edgesStr}_{name}";
            }
            
            return $"{type}_{sizeX}x{sizeY}_{surroundingStr}_{name}";
        }
        
        /// <summary>
        /// Extrae solo el tipo (Head/Tail) de un nombre de nomenclatura.
        /// </summary>
        public static bool TryGetType(string objectName, out bool isHead)
        {
            isHead = false;
            
            if (string.IsNullOrEmpty(objectName))
            {
                return false;
            }
            
            if (objectName.StartsWith("Head_", StringComparison.OrdinalIgnoreCase))
            {
                isHead = true;
                return true;
            }
            
            if (objectName.StartsWith("Tail_", StringComparison.OrdinalIgnoreCase))
            {
                isHead = false;
                return true;
            }
            
            return false;
        }
    }
}
