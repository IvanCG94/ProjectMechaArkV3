using System;
using System.Text.RegularExpressions;
using RobotGame.Data;
using RobotGame.Enums;
using UnityEngine;

namespace RobotGame.Utils
{
    /// <summary>
    /// Parser para la nomenclatura de Blender.
    /// Formato: Head_NxM_SX[F]_nombre o Tail_NxM_SX[F]_nombre
    /// Ejemplos: Head_2x2_S1_torso, Tail_3x1_S2F_armor, Head_1x4_SN_back
    /// </summary>
    public static class NomenclatureParser
    {
        // Regex para parsear la nomenclatura
        // Grupos: 1=Type(Head/Tail), 2=SizeX, 3=SizeY, 4=Surrounding(SN/S1/S2F/etc), 5=Name
        private static readonly Regex NomenclatureRegex = new Regex(
            @"^(Head|Tail)_(\d+)x(\d+)_(SN|S\d+F?)_(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );
        
        /// <summary>
        /// Parsea un nombre de objeto de Blender a GridInfo.
        /// </summary>
        /// <param name="objectName">Nombre del objeto en Blender</param>
        /// <param name="gridInfo">GridInfo resultante</param>
        /// <returns>True si el parseo fue exitoso</returns>
        public static bool TryParse(string objectName, out GridInfo gridInfo)
        {
            gridInfo = default;
            
            if (string.IsNullOrEmpty(objectName))
            {
                return false;
            }
            
            Match match = NomenclatureRegex.Match(objectName);
            
            if (!match.Success)
            {
                return false;
            }
            
            try
            {
                gridInfo = new GridInfo
                {
                    isHead = match.Groups[1].Value.Equals("Head", StringComparison.OrdinalIgnoreCase),
                    sizeX = int.Parse(match.Groups[2].Value),
                    sizeY = int.Parse(match.Groups[3].Value),
                    surrounding = SurroundingLevel.Parse(match.Groups[4].Value),
                    gridName = match.Groups[5].Value
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
            
            throw new FormatException($"Invalid nomenclature format: '{objectName}'. Expected format: Head_NxM_SX[F]_name or Tail_NxM_SX[F]_name");
        }
        
        /// <summary>
        /// Valida si un nombre sigue el formato de nomenclatura.
        /// </summary>
        public static bool IsValidNomenclature(string objectName)
        {
            return !string.IsNullOrEmpty(objectName) && NomenclatureRegex.IsMatch(objectName);
        }
        
        /// <summary>
        /// Genera un nombre de nomenclatura a partir de los componentes.
        /// </summary>
        public static string Generate(bool isHead, int sizeX, int sizeY, SurroundingLevel surrounding, string name)
        {
            string type = isHead ? "Head" : "Tail";
            return $"{type}_{sizeX}x{sizeY}_{surrounding}_{name}";
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
