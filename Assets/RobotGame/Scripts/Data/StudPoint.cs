using System;
using UnityEngine;
using RobotGame.Enums;

namespace RobotGame.Data
{
    /// <summary>
    /// Representa un punto de stud individual detectado desde un Empty.
    /// La posición es la posición local relativa al padre.
    /// 
    /// Nomenclatura: Head_T{tier}-{subtier}_{nombre} o Tail_T{tier}-{subtier}_{nombre}
    /// </summary>
    [Serializable]
    public class StudPoint
    {
        public string name;
        public string transformName; // Nombre completo del transform (ej: "Head_T1-2_Front1")
        public TierInfo tierInfo;
        public Vector3 localPosition;
        public bool isHead; // true = Head (receptor), false = Tail (ocupador)
        
        /// <summary>
        /// ID del grupo al que pertenece este stud.
        /// Para Heads, es el nombre del hueso padre (ej: "Bone_thigh_L").
        /// Todos los Heads de una misma parte del cuerpo comparten el mismo groupId.
        /// </summary>
        public string groupId;
        
        [NonSerialized]
        public Transform sourceTransform;
        
        public StudPoint(string name, TierInfo tierInfo, Vector3 localPosition, bool isHead, Transform source = null)
        {
            this.name = name;
            this.transformName = source?.name ?? name;
            this.tierInfo = tierInfo;
            this.localPosition = localPosition;
            this.isHead = isHead;
            this.sourceTransform = source;
            
            // Obtener groupId del nombre del padre
            if (source != null && source.parent != null)
            {
                this.groupId = source.parent.name;
            }
            else
            {
                this.groupId = "default";
            }
        }
        
        /// <summary>
        /// Constructor completo con groupId explícito.
        /// </summary>
        public StudPoint(string name, TierInfo tierInfo, Vector3 localPosition, bool isHead, string groupId, Transform source = null)
        {
            this.name = name;
            this.transformName = source?.name ?? name;
            this.tierInfo = tierInfo;
            this.localPosition = localPosition;
            this.isHead = isHead;
            this.groupId = groupId;
            this.sourceTransform = source;
        }
        
        /// <summary>
        /// Verifica si este stud coincide en posición con otro (dentro de tolerancia).
        /// </summary>
        public bool MatchesPosition(Vector3 otherLocalPos, float tolerance = 0.01f)
        {
            return Vector3.Distance(localPosition, otherLocalPos) <= tolerance;
        }
        
        /// <summary>
        /// Verifica si este stud coincide en posición con otro stud.
        /// </summary>
        public bool MatchesPosition(StudPoint other, float tolerance = 0.01f)
        {
            return MatchesPosition(other.localPosition, tolerance);
        }
        
        public override string ToString()
        {
            return $"{(isHead ? "Head" : "Tail")}_{tierInfo}_{name} @ {localPosition} [Group:{groupId}]";
        }
    }
}
