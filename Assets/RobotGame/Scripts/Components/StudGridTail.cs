using System.Collections.Generic;
using UnityEngine;
using RobotGame.Data;
using RobotGame.Utils;
using RobotGame.Enums;

namespace RobotGame.Components
{
    /// <summary>
    /// Componente para piezas de armadura basado en posiciones físicas de studs.
    /// 
    /// USO:
    /// 1. En Blender, crea Empties hijos con nombres:
    ///    Tail_T1-2_Point1, Tail_T1-2_Point2, etc.
    /// 2. Posiciona los Empties con el espaciado correcto para el tier
    /// 3. Agrega este componente al objeto padre
    /// 4. Click "Detectar Studs" en el Inspector
    /// 
    /// EJEMPLO PIEZA EN L (T1-2, espaciado 0.125):
    ///   Tail_T1-2_P1 @ (0, 0, 0)
    ///   Tail_T1-2_P2 @ (0, 0.125, 0)
    ///   Tail_T1-2_P3 @ (0, 0.25, 0)
    ///   Tail_T1-2_P4 @ (0.125, 0, 0)
    /// </summary>
    public class StudGridTail : MonoBehaviour
    {
        [Header("Studs Detectados")]
        [SerializeField] private List<StudPoint> tailStuds = new List<StudPoint>();
        
        [Header("Visualización")]
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private float gizmoRadius = 0.025f;
        [SerializeField] private Color studColor = Color.cyan;
        
        #region Properties
        
        public IReadOnlyList<StudPoint> Studs => tailStuds;
        public int StudCount => tailStuds.Count;
        
        /// <summary>
        /// Obtiene el TierInfo del primer stud (asume todos son del mismo tier).
        /// </summary>
        public TierInfo TierInfo
        {
            get
            {
                if (tailStuds.Count > 0)
                    return tailStuds[0].tierInfo;
                return TierInfo.Default;
            }
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Detecta todos los studs Tail en los hijos de este objeto.
        /// </summary>
        public void DetectStuds()
        {
            tailStuds = StudDetector.DetectTailStuds(transform);
            
            // Debug.Log($"StudGridTail: Detectados {tailStuds.Count} studs Tail");
            
            foreach (var stud in tailStuds)
            {
                // Debug.Log($"  - {stud.name} (T{stud.tierInfo}) @ {stud.localPosition}");
            }
        }
        
        /// <summary>
        /// Verifica si puede colocarse en un Head con el offset dado.
        /// El offset es la posición donde el origen del Tail se colocará en el Head.
        /// </summary>
        public bool CanPlaceOn(StudGridHead head, Vector3 offset)
        {
            if (head == null || tailStuds.Count == 0)
                return false;
            
            return head.CanPlace(tailStuds, offset);
        }
        
        /// <summary>
        /// Obtiene la lista de studs como List (para pasar al Head).
        /// </summary>
        public List<StudPoint> GetStudsList()
        {
            return new List<StudPoint>(tailStuds);
        }
        
        /// <summary>
        /// Obtiene la lista de studs directamente (referencia, no copia).
        /// </summary>
        public List<StudPoint> StudsList => tailStuds;
        
        /// <summary>
        /// Convierte posición local a mundial.
        /// </summary>
        public Vector3 LocalToWorld(Vector3 localPos)
        {
            return transform.TransformPoint(localPos);
        }
        
        #endregion
        
        #region Gizmos
        
        private void OnDrawGizmos()
        {
            if (!showGizmos || tailStuds == null)
                return;
            
            Gizmos.color = studColor;
            
            foreach (var stud in tailStuds)
            {
                Vector3 worldPos = LocalToWorld(stud.localPosition);
                Gizmos.DrawSphere(worldPos, gizmoRadius);
                
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(worldPos, gizmoRadius * 1.2f);
                Gizmos.color = studColor;
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            if (tailStuds == null || tailStuds.Count == 0)
                return;
            
            // Dibujar líneas conectando studs
            Gizmos.color = Color.yellow;
            for (int i = 0; i < tailStuds.Count - 1; i++)
            {
                Vector3 from = LocalToWorld(tailStuds[i].localPosition);
                Vector3 to = LocalToWorld(tailStuds[i + 1].localPosition);
                Gizmos.DrawLine(from, to);
            }
            
            // Dibujar etiquetas
            #if UNITY_EDITOR
            foreach (var stud in tailStuds)
            {
                Vector3 worldPos = LocalToWorld(stud.localPosition);
                UnityEditor.Handles.Label(worldPos + Vector3.up * 0.04f, $"{stud.name}\nT{stud.tierInfo}");
            }
            #endif
        }
        
        #endregion
    }
}
