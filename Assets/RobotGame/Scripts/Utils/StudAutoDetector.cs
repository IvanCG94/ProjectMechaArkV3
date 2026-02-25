using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using RobotGame.Data;
using RobotGame.Enums;

namespace RobotGame.Utils
{
    /// <summary>
    /// Utilidad para auto-detectar patrones de studs desde los Empties de un prefab.
    /// 
    /// NOMENCLATURA EN BLENDER:
    /// 
    /// Para STUDS individuales:
    ///   Stud_X_Y              (ej: Stud_0_0, Stud_1_2, Stud_3_0)
    /// 
    /// Para GRILLAS HEAD (contenedor):
    ///   Head_T{tier}-{subtier}_{nombre}    (ej: Head_T1-2_TorsoMain)
    /// 
    /// Para PIEZAS TAIL (contenedor):
    ///   Tail_T{tier}-{subtier}_{nombre}    (ej: Tail_T1-2_ChestPlate)
    /// 
    /// ESTRUCTURA EN BLENDER:
    /// 
    ///   Head_T1-2_TorsoMain          ← Empty contenedor
    ///   ├── Stud_0_0                 ← Empty marcador
    ///   ├── Stud_0_1
    ///   ├── Stud_1_0
    ///   └── Stud_1_1
    /// 
    ///   Tail_T1-2_ChestPlate         ← Empty contenedor
    ///   ├── Stud_0_0
    ///   ├── Stud_0_1
    ///   ├── Stud_0_2
    ///   ├── Stud_1_2                 ← Forma en L
    ///   └── Visual_Mesh              ← El mesh visual (ignorado)
    /// </summary>
    public static class StudAutoDetector
    {
        // Regex para detectar Stud_X_Y
        private static readonly Regex StudRegex = new Regex(
            @"^Stud_(\d+)_(\d+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );
        
        // Regex para detectar Head_T{tier}-{subtier}_{nombre}
        private static readonly Regex HeadRegex = new Regex(
            @"^Head_T(\d+)-(\d+)_(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );
        
        // Regex para detectar Head_T{tier}_{nombre} (sin subtier, asume 1)
        private static readonly Regex HeadNoSubTierRegex = new Regex(
            @"^Head_T(\d+)_(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );
        
        // Regex para detectar Tail_T{tier}-{subtier}_{nombre}
        private static readonly Regex TailRegex = new Regex(
            @"^Tail_T(\d+)-(\d+)_(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );
        
        // Regex para detectar Tail_T{tier}_{nombre} (sin subtier, asume 1)
        private static readonly Regex TailNoSubTierRegex = new Regex(
            @"^Tail_T(\d+)_(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );
        
        #region Stud Detection
        
        /// <summary>
        /// Intenta parsear un nombre de objeto como Stud_X_Y.
        /// </summary>
        public static bool TryParseStud(string objectName, out int x, out int y)
        {
            x = 0;
            y = 0;
            
            if (string.IsNullOrEmpty(objectName))
                return false;
            
            Match match = StudRegex.Match(objectName);
            if (!match.Success)
                return false;
            
            x = int.Parse(match.Groups[1].Value);
            y = int.Parse(match.Groups[2].Value);
            return true;
        }
        
        /// <summary>
        /// Detecta todos los studs hijos de un transform.
        /// </summary>
        public static StudPattern DetectStudPattern(Transform parent)
        {
            var pattern = new StudPattern();
            
            if (parent == null)
                return pattern;
            
            // Buscar en hijos directos
            foreach (Transform child in parent)
            {
                if (TryParseStud(child.name, out int x, out int y))
                {
                    pattern.AddStud(x, y);
                }
            }
            
            return pattern;
        }
        
        /// <summary>
        /// Detecta todos los studs en un GameObject y sus hijos directos.
        /// </summary>
        public static StudPattern DetectStudPattern(GameObject gameObject)
        {
            if (gameObject == null)
                return new StudPattern();
            
            return DetectStudPattern(gameObject.transform);
        }
        
        #endregion
        
        #region Head Detection
        
        /// <summary>
        /// Resultado de detección de un Head.
        /// </summary>
        public class HeadDetectionResult
        {
            public string name;
            public TierInfo tierInfo;
            public StudPattern pattern;
            public string transformName;
            
            public bool IsValid => pattern != null && pattern.Count > 0;
        }
        
        /// <summary>
        /// Intenta parsear un nombre como Head_T{tier}-{subtier}_{nombre}.
        /// </summary>
        public static bool TryParseHead(string objectName, out TierInfo tierInfo, out string gridName)
        {
            tierInfo = TierInfo.Default;
            gridName = null;
            
            if (string.IsNullOrEmpty(objectName))
                return false;
            
            // Intentar formato completo: Head_T1-2_nombre
            Match match = HeadRegex.Match(objectName);
            if (match.Success)
            {
                int tier = int.Parse(match.Groups[1].Value);
                int subTier = int.Parse(match.Groups[2].Value);
                tierInfo = new TierInfo(tier, subTier);
                gridName = match.Groups[3].Value;
                return true;
            }
            
            // Intentar formato sin subtier: Head_T1_nombre
            match = HeadNoSubTierRegex.Match(objectName);
            if (match.Success)
            {
                int tier = int.Parse(match.Groups[1].Value);
                tierInfo = new TierInfo(tier, 1);
                gridName = match.Groups[2].Value;
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Detecta todas las grillas Head en un prefab.
        /// </summary>
        public static List<HeadDetectionResult> DetectHeads(GameObject prefab)
        {
            var results = new List<HeadDetectionResult>();
            
            if (prefab == null)
                return results;
            
            // Buscar en raíz
            if (TryParseHead(prefab.name, out TierInfo rootTier, out string rootName))
            {
                var pattern = DetectStudPattern(prefab.transform);
                if (pattern.Count > 0)
                {
                    results.Add(new HeadDetectionResult
                    {
                        name = rootName,
                        tierInfo = rootTier,
                        pattern = pattern,
                        transformName = prefab.name
                    });
                }
            }
            
            // Buscar recursivamente
            SearchForHeads(prefab.transform, results);
            
            return results;
        }
        
        private static void SearchForHeads(Transform parent, List<HeadDetectionResult> results)
        {
            foreach (Transform child in parent)
            {
                if (TryParseHead(child.name, out TierInfo tierInfo, out string gridName))
                {
                    var pattern = DetectStudPattern(child);
                    if (pattern.Count > 0)
                    {
                        results.Add(new HeadDetectionResult
                        {
                            name = gridName,
                            tierInfo = tierInfo,
                            pattern = pattern,
                            transformName = child.name
                        });
                    }
                }
                
                // Buscar en hijos
                SearchForHeads(child, results);
            }
        }
        
        #endregion
        
        #region Tail Detection
        
        /// <summary>
        /// Resultado de detección de un Tail.
        /// </summary>
        public class TailDetectionResult
        {
            public string name;
            public TierInfo tierInfo;
            public StudPattern pattern;
            public string transformName;
            
            public bool IsValid => pattern != null && pattern.Count > 0;
        }
        
        /// <summary>
        /// Intenta parsear un nombre como Tail_T{tier}-{subtier}_{nombre}.
        /// </summary>
        public static bool TryParseTail(string objectName, out TierInfo tierInfo, out string partName)
        {
            tierInfo = TierInfo.Default;
            partName = null;
            
            if (string.IsNullOrEmpty(objectName))
                return false;
            
            // Intentar formato completo: Tail_T1-2_nombre
            Match match = TailRegex.Match(objectName);
            if (match.Success)
            {
                int tier = int.Parse(match.Groups[1].Value);
                int subTier = int.Parse(match.Groups[2].Value);
                tierInfo = new TierInfo(tier, subTier);
                partName = match.Groups[3].Value;
                return true;
            }
            
            // Intentar formato sin subtier: Tail_T1_nombre
            match = TailNoSubTierRegex.Match(objectName);
            if (match.Success)
            {
                int tier = int.Parse(match.Groups[1].Value);
                tierInfo = new TierInfo(tier, 1);
                partName = match.Groups[2].Value;
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Detecta todas las grillas Tail en un prefab.
        /// </summary>
        public static List<TailDetectionResult> DetectTails(GameObject prefab)
        {
            var results = new List<TailDetectionResult>();
            
            if (prefab == null)
                return results;
            
            // Buscar en raíz
            if (TryParseTail(prefab.name, out TierInfo rootTier, out string rootName))
            {
                var pattern = DetectStudPattern(prefab.transform);
                if (pattern.Count > 0)
                {
                    results.Add(new TailDetectionResult
                    {
                        name = rootName,
                        tierInfo = rootTier,
                        pattern = pattern,
                        transformName = prefab.name
                    });
                }
            }
            
            // Buscar recursivamente
            SearchForTails(prefab.transform, results);
            
            return results;
        }
        
        /// <summary>
        /// Detecta el primer Tail en un prefab (para piezas de armadura simples).
        /// </summary>
        public static TailDetectionResult DetectSingleTail(GameObject prefab)
        {
            var tails = DetectTails(prefab);
            return tails.Count > 0 ? tails[0] : null;
        }
        
        private static void SearchForTails(Transform parent, List<TailDetectionResult> results)
        {
            foreach (Transform child in parent)
            {
                if (TryParseTail(child.name, out TierInfo tierInfo, out string partName))
                {
                    var pattern = DetectStudPattern(child);
                    if (pattern.Count > 0)
                    {
                        results.Add(new TailDetectionResult
                        {
                            name = partName,
                            tierInfo = tierInfo,
                            pattern = pattern,
                            transformName = child.name
                        });
                    }
                }
                
                // Buscar en hijos
                SearchForTails(child, results);
            }
        }
        
        #endregion
        
        #region Validation
        
        /// <summary>
        /// Valida que un nombre sigue la nomenclatura correcta.
        /// </summary>
        public static bool IsValidNomenclature(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
                return false;
            
            return HeadRegex.IsMatch(objectName) ||
                   HeadNoSubTierRegex.IsMatch(objectName) ||
                   TailRegex.IsMatch(objectName) ||
                   TailNoSubTierRegex.IsMatch(objectName) ||
                   StudRegex.IsMatch(objectName);
        }
        
        /// <summary>
        /// Determina el tipo de un objeto por su nombre.
        /// </summary>
        public static string GetObjectType(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
                return "Unknown";
            
            if (StudRegex.IsMatch(objectName))
                return "Stud";
            
            if (HeadRegex.IsMatch(objectName) || HeadNoSubTierRegex.IsMatch(objectName))
                return "Head";
            
            if (TailRegex.IsMatch(objectName) || TailNoSubTierRegex.IsMatch(objectName))
                return "Tail";
            
            return "Unknown";
        }
        
        #endregion
    }
}
