using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using RobotGame.Data;
using RobotGame.Enums;

namespace RobotGame.Utils
{
    /// <summary>
    /// Detecta studs desde Empties en prefabs/objetos.
    /// 
    /// NOMENCLATURA:
    ///   Head_T{tier}-{subtier}_{nombre}   → Stud receptor (donde se conectan piezas)
    ///   Tail_T{tier}-{subtier}_{nombre}   → Stud ocupador (lo que tiene la pieza)
    /// 
    /// EJEMPLO EN BLENDER (brazo con grilla 1x3):
    ///   Arm
    ///   ├── ArmMesh
    ///   ├── Head_T1-2_Front1    ← posición (0, 0, 0)
    ///   ├── Head_T1-2_Front2    ← posición (0, 0.125, 0)
    ///   └── Head_T1-2_Front3    ← posición (0, 0.25, 0)
    /// 
    /// EJEMPLO PIEZA ARMADURA:
    ///   ChestPlate
    ///   ├── ChestMesh
    ///   ├── Tail_T1-2_Point1    ← posición (0, 0, 0)
    ///   └── Tail_T1-2_Point2    ← posición (0, 0.125, 0)
    /// 
    /// La validación es por POSICIÓN FÍSICA, no por coordenadas de grilla.
    /// </summary>
    public static class StudDetector
    {
        // Head_T{tier}-{subtier}_{nombre}
        private static readonly Regex HeadRegex = new Regex(
            @"^Head_T(\d+)-(\d+)_(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );
        
        // Head_T{tier}_{nombre} (sin subtier, asume 1)
        private static readonly Regex HeadNoSubTierRegex = new Regex(
            @"^Head_T(\d+)_(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );
        
        // Tail_T{tier}-{subtier}_{nombre}
        private static readonly Regex TailRegex = new Regex(
            @"^Tail_T(\d+)-(\d+)_(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );
        
        // Tail_T{tier}_{nombre} (sin subtier, asume 1)
        private static readonly Regex TailNoSubTierRegex = new Regex(
            @"^Tail_T(\d+)_(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );
        
        #region Parsing
        
        /// <summary>
        /// Intenta parsear un nombre como Head_T{tier}-{subtier}_{nombre}.
        /// </summary>
        public static bool TryParseHead(string objectName, out TierInfo tierInfo, out string studName)
        {
            tierInfo = TierInfo.Default;
            studName = null;
            
            if (string.IsNullOrEmpty(objectName))
                return false;
            
            // Formato completo: Head_T1-2_nombre
            Match match = HeadRegex.Match(objectName);
            if (match.Success)
            {
                int tier = int.Parse(match.Groups[1].Value);
                int subTier = int.Parse(match.Groups[2].Value);
                tierInfo = new TierInfo(tier, subTier);
                studName = match.Groups[3].Value;
                return true;
            }
            
            // Formato sin subtier: Head_T1_nombre
            match = HeadNoSubTierRegex.Match(objectName);
            if (match.Success)
            {
                int tier = int.Parse(match.Groups[1].Value);
                tierInfo = new TierInfo(tier, 1);
                studName = match.Groups[2].Value;
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Intenta parsear un nombre como Tail_T{tier}-{subtier}_{nombre}.
        /// </summary>
        public static bool TryParseTail(string objectName, out TierInfo tierInfo, out string studName)
        {
            tierInfo = TierInfo.Default;
            studName = null;
            
            if (string.IsNullOrEmpty(objectName))
                return false;
            
            // Formato completo: Tail_T1-2_nombre
            Match match = TailRegex.Match(objectName);
            if (match.Success)
            {
                int tier = int.Parse(match.Groups[1].Value);
                int subTier = int.Parse(match.Groups[2].Value);
                tierInfo = new TierInfo(tier, subTier);
                studName = match.Groups[3].Value;
                return true;
            }
            
            // Formato sin subtier: Tail_T1_nombre
            match = TailNoSubTierRegex.Match(objectName);
            if (match.Success)
            {
                int tier = int.Parse(match.Groups[1].Value);
                tierInfo = new TierInfo(tier, 1);
                studName = match.Groups[2].Value;
                return true;
            }
            
            return false;
        }
        
        #endregion
        
        #region Detection
        
        /// <summary>
        /// Detecta todos los studs Head en un objeto y sus hijos.
        /// Retorna posiciones LOCALES relativas al transform raíz.
        /// </summary>
        public static List<StudPoint> DetectHeadStuds(Transform root)
        {
            var studs = new List<StudPoint>();
            
            if (root == null)
                return studs;
            
            SearchForStuds(root, root, studs, isHead: true);
            
            return studs;
        }
        
        /// <summary>
        /// Detecta todos los studs Tail en un objeto y sus hijos.
        /// Retorna posiciones LOCALES relativas al transform raíz.
        /// </summary>
        public static List<StudPoint> DetectTailStuds(Transform root)
        {
            var studs = new List<StudPoint>();
            
            if (root == null)
                return studs;
            
            SearchForStuds(root, root, studs, isHead: false);
            
            return studs;
        }
        
        /// <summary>
        /// Detecta todos los studs (Head y Tail) en un objeto.
        /// </summary>
        public static List<StudPoint> DetectAllStuds(Transform root)
        {
            var studs = new List<StudPoint>();
            
            if (root == null)
                return studs;
            
            SearchForStuds(root, root, studs, isHead: true);
            SearchForStuds(root, root, studs, isHead: false);
            
            return studs;
        }
        
        private static void SearchForStuds(Transform root, Transform current, List<StudPoint> studs, bool isHead)
        {
            // Verificar el objeto actual
            bool parsed;
            TierInfo tierInfo;
            string studName;
            
            if (isHead)
            {
                parsed = TryParseHead(current.name, out tierInfo, out studName);
            }
            else
            {
                parsed = TryParseTail(current.name, out tierInfo, out studName);
            }
            
            if (parsed)
            {
                // Calcular posición local relativa al root
                Vector3 localPos = root.InverseTransformPoint(current.position);
                
                studs.Add(new StudPoint(studName, tierInfo, localPos, isHead, current));
            }
            
            // Buscar en hijos
            foreach (Transform child in current)
            {
                SearchForStuds(root, child, studs, isHead);
            }
        }
        
        #endregion
        
        #region Utility
        
        /// <summary>
        /// Obtiene el tipo de objeto por su nombre.
        /// </summary>
        public static string GetObjectType(string objectName)
        {
            if (TryParseHead(objectName, out _, out _))
                return "Head";
            
            if (TryParseTail(objectName, out _, out _))
                return "Tail";
            
            return "Unknown";
        }
        
        /// <summary>
        /// Filtra studs por TierInfo.
        /// </summary>
        public static List<StudPoint> FilterByTier(List<StudPoint> studs, TierInfo tierInfo)
        {
            return studs.FindAll(s => s.tierInfo == tierInfo);
        }
        
        #endregion
    }
}
