using System;
using System.Text.RegularExpressions;
using RobotGame.Data;
using RobotGame.Enums;
using UnityEngine;

namespace RobotGame.Utils
{
    /// <summary>
    /// Parser para la nomenclatura de Blender.
    /// 
    /// NUEVO FORMATO CON TIER:
    ///   Head_T1_NxM_SX_nombre           (tier 1, solo SN o SX)
    ///   Head_T2_NxM_SX_L_nombre         (tier 2, borde izquierdo abierto)
    ///   Head_T1_NxM_SX_LRTB_nombre      (tier 1, todos los bordes abiertos)
    ///   Head_T3_NxM_SXFH_nombre         (tier 3, Full horizontal)
    ///   Tail_T1_NxM_SN_nombre           (tier 1, plana)
    ///   Tail_T2_NxM_SX_R_nombre         (tier 2, envuelve derecha)
    ///   Tail_T4_NxM_SXF_nombre          (tier 4, envuelve completamente)
    /// 
    /// FORMATO LEGACY (sin tier, asume T1):
    ///   Head_NxM_SX_nombre
    ///   Tail_NxM_SX_nombre
    /// </summary>
    public static class NomenclatureParser
    {
        // Regex para el NUEVO formato con Tier
        // Head_T2_4x7_S5FH_RaptorThighR
        // Grupos: 1=Type, 2=Tier, 3=SizeX, 4=SizeY, 5=SurroundingLevel(número), 6=FullType(opcional), 7=Edges(opcional), 8=Name
        private static readonly Regex TierNomenclatureRegex = new Regex(
            @"^(Head|Tail)_T(\d+)_(\d+)x(\d+)_(SN|S(\d+)(FH|FV|F)?)(?:_([LRTB]+))?_(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );
        
        // Regex para formato LEGACY (sin tier)
        // Head_4x7_S5FH_RaptorThighR
        // Grupos: 1=Type, 2=SizeX, 3=SizeY, 4=SurroundingLevel(número), 5=FullType(opcional), 6=Edges(opcional), 7=Name
        private static readonly Regex NomenclatureRegex = new Regex(
            @"^(Head|Tail)_(\d+)x(\d+)_(SN|S(\d+)(FH|FV|F)?)(?:_([LRTB]+))?_(.+)$",
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
            
            // Intentar con el NUEVO formato con tier primero
            Match match = TierNomenclatureRegex.Match(objectName);
            if (match.Success)
            {
                return ParseTierFormat(match, out gridInfo);
            }
            
            // Intentar con formato sin tier (asume T1)
            match = NomenclatureRegex.Match(objectName);
            if (match.Success)
            {
                return ParseLegacyFormat(match, out gridInfo);
            }
            
            return false;
        }
        
        /// <summary>
        /// Parsea el nuevo formato con Tier explícito.
        /// Ejemplo: Head_T2_4x7_S5FH_RaptorThighR
        /// Regex grupos: 1=Type, 2=Tier, 3=SizeX, 4=SizeY, 5=SurroundingFull, 6=Level, 7=FullType, 8=Edges, 9=Name
        /// </summary>
        private static bool ParseTierFormat(Match match, out GridInfo gridInfo)
        {
            gridInfo = default;
            
            try
            {
                bool isHead = match.Groups[1].Value.Equals("Head", StringComparison.OrdinalIgnoreCase);
                int tier = int.Parse(match.Groups[2].Value);
                int sizeX = int.Parse(match.Groups[3].Value);
                int sizeY = int.Parse(match.Groups[4].Value);
                
                // Validar tier
                tier = Mathf.Clamp(tier, 1, 6);
                
                // Parsear surrounding
                string surroundingFull = match.Groups[5].Value.ToUpper(); // SN o S5FH etc
                int level = 0;
                FullType fullType = FullType.None;
                
                if (surroundingFull != "SN")
                {
                    // Grupo 6 es el número del level
                    if (match.Groups[6].Success)
                    {
                        level = int.Parse(match.Groups[6].Value);
                    }
                    
                    // Grupo 7 es el tipo Full (FH, FV, F)
                    string fullStr = match.Groups[7].Value.ToUpper();
                    if (!string.IsNullOrEmpty(fullStr))
                    {
                        if (fullStr == "FH") fullType = FullType.FH;
                        else if (fullStr == "FV") fullType = FullType.FV;
                        else if (fullStr == "F") fullType = FullType.FH; // F solo = FH por defecto
                    }
                }
                
                // Parsear edges (L, R, T, B, LR, LRTB, etc.) - Grupo 8
                EdgeFlags edges = EdgeFlags.None;
                string edgesStr = match.Groups[8].Value;
                if (!string.IsNullOrEmpty(edgesStr))
                {
                    edges = SurroundingLevel.ParseEdges(edgesStr);
                }
                
                // Nombre - Grupo 9
                string gridName = match.Groups[9].Value;
                
                gridInfo = new GridInfo
                {
                    isHead = isHead,
                    tier = tier,
                    sizeX = sizeX,
                    sizeY = sizeY,
                    surrounding = SurroundingLevel.Create(level, edges, fullType),
                    gridName = gridName
                };
                
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"NomenclatureParser: Error parseando '{match.Value}': {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Parsea formato sin tier explícito (asume Tier 1).
        /// Ejemplo: Head_4x7_S5FH_RaptorThighR
        /// Regex grupos: 1=Type, 2=SizeX, 3=SizeY, 4=SurroundingFull, 5=Level, 6=FullType, 7=Edges, 8=Name
        /// </summary>
        private static bool ParseLegacyFormat(Match match, out GridInfo gridInfo)
        {
            gridInfo = default;
            
            try
            {
                bool isHead = match.Groups[1].Value.Equals("Head", StringComparison.OrdinalIgnoreCase);
                int sizeX = int.Parse(match.Groups[2].Value);
                int sizeY = int.Parse(match.Groups[3].Value);
                
                // Parsear surrounding
                string surroundingFull = match.Groups[4].Value.ToUpper(); // SN o S5FH etc
                int level = 0;
                FullType fullType = FullType.None;
                
                if (surroundingFull != "SN")
                {
                    // Grupo 5 es el número del level
                    if (match.Groups[5].Success)
                    {
                        level = int.Parse(match.Groups[5].Value);
                    }
                    
                    // Grupo 6 es el tipo Full (FH, FV, F)
                    string fullStr = match.Groups[6].Value.ToUpper();
                    if (!string.IsNullOrEmpty(fullStr))
                    {
                        if (fullStr == "FH") fullType = FullType.FH;
                        else if (fullStr == "FV") fullType = FullType.FV;
                        else if (fullStr == "F") fullType = FullType.FH;
                    }
                }
                
                // Parsear edges - Grupo 7
                EdgeFlags edges = EdgeFlags.None;
                string edgesStr = match.Groups[7].Value;
                if (!string.IsNullOrEmpty(edgesStr))
                {
                    edges = SurroundingLevel.ParseEdges(edgesStr);
                }
                
                // Nombre - Grupo 8
                string gridName = match.Groups[8].Value;
                
                gridInfo = new GridInfo
                {
                    isHead = isHead,
                    tier = 1, // Default tier 1 para formato legacy
                    sizeX = sizeX,
                    sizeY = sizeY,
                    surrounding = SurroundingLevel.Create(level, edges, fullType),
                    gridName = gridName
                };
                
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"NomenclatureParser: Error parseando legacy '{match.Value}': {e.Message}");
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
                "  Head_T1_NxM_SX_name (nuevo con tier)\n" +
                "  Head_T2_NxM_S5FH_name (tier 2, surrounding 5, full horizontal)\n" +
                "  Head_NxM_SX_name (legacy, asume T1)\n" +
                "  Head_NxM_SX_LRTB_name\n" +
                "  Tail_T2_NxM_SX_R_name");
        }
        
        /// <summary>
        /// Valida si un nombre sigue el formato de nomenclatura.
        /// </summary>
        public static bool IsValidNomenclature(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) return false;
            return TierNomenclatureRegex.IsMatch(objectName) || 
                   NomenclatureRegex.IsMatch(objectName);
        }
        
        /// <summary>
        /// Genera un nombre de nomenclatura a partir de los componentes (con tier).
        /// </summary>
        public static string Generate(bool isHead, int tier, int sizeX, int sizeY, SurroundingLevel surrounding, string name)
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
                return $"{type}_T{tier}_{sizeX}x{sizeY}_{surroundingStr}_{edgesStr}_{name}";
            }
            
            return $"{type}_T{tier}_{sizeX}x{sizeY}_{surroundingStr}_{name}";
        }
        
        /// <summary>
        /// Genera un nombre de nomenclatura (versión legacy sin tier, para compatibilidad).
        /// </summary>
        public static string Generate(bool isHead, int sizeX, int sizeY, SurroundingLevel surrounding, string name)
        {
            return Generate(isHead, 1, sizeX, sizeY, surrounding, name);
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
        
        /// <summary>
        /// Extrae el tier de un nombre de nomenclatura.
        /// Retorna 1 si no tiene tier explícito (formato legacy).
        /// </summary>
        public static int GetTier(string objectName)
        {
            if (TryParse(objectName, out GridInfo info))
            {
                return info.tier;
            }
            return 1; // Default
        }
    }
}
