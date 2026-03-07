using System.Collections.Generic;
using UnityEngine;

namespace RobotGame.Components
{
    /// <summary>
    /// Valida colisiones de piezas durante el ensamblaje usando BoxColliders.
    /// 
    /// NOMENCLATURA EN BLENDER:
    /// Crear Empties con nombre que empiece con "Box_" y ajustar su ESCALA para definir el tamaño.
    /// 
    /// EJEMPLO:
    /// PiezaArmadura
    /// ├── Visual_Mesh
    /// ├── Box_Main (Empty con escala 0.5, 0.3, 0.2)
    /// ├── Box_Extension (Empty con escala 0.2, 0.4, 0.1)
    /// ├── Tail_T1-2_P1
    /// └── Tail_T1-2_P2
    /// 
    /// VALIDACIÓN:
    /// - Piezas EXISTENTES → BoxColliders activos (obstáculos)
    /// - Pieza NUEVA → Physics.CheckBox por cada Box_ (sin collider físico)
    /// </summary>
    public class CollisionValidator : MonoBehaviour
    {
        [Header("Configuración")]
        [SerializeField] private string boxPrefix = "Box_";
        
        [Tooltip("Reducir el tamaño de detección para evitar falsos positivos en bordes")]
        [Range(0.8f, 1.0f)]
        [SerializeField] private float collisionMargin = 0.95f;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private bool showGizmos = true;
        
        // Colliders activados durante edición
        private List<BoxCollider> activatedColliders = new List<BoxCollider>();
        
        // Pieza seleccionada
        private GameObject selectedPiece;
        
        #region Singleton
        
        private static CollisionValidator _instance;
        public static CollisionValidator Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<CollisionValidator>();
                }
                return _instance;
            }
        }
        
        private void Awake()
        {
            _instance = this;
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Activa los BoxColliders de todas las piezas del robot para validación.
        /// </summary>
        public void EnableEditMode(Transform robotRoot)
        {
            if (robotRoot == null) return;
            
            activatedColliders.Clear();
            
            // Buscar todos los Box_ en el robot
            var allTransforms = robotRoot.GetComponentsInChildren<Transform>(true);
            
            foreach (var t in allTransforms)
            {
                if (t.name.StartsWith(boxPrefix))
                {
                    BoxCollider bc = GetOrCreateBoxCollider(t);
                    if (bc != null)
                    {
                        bc.enabled = true;
                        activatedColliders.Add(bc);
                        
                        if (showDebugInfo)
                        {
                            // Debug.Log($"CollisionValidator: Activado BoxCollider en {t.name} (size={t.lossyScale})");
                        }
                    }
                }
            }
            
            if (showDebugInfo)
            {
                // Debug.Log($"CollisionValidator: Modo edición activado. {activatedColliders.Count} BoxColliders activos.");
            }
        }
        
        /// <summary>
        /// Desactiva todos los colliders de validación.
        /// </summary>
        public void DisableEditMode()
        {
            foreach (var bc in activatedColliders)
            {
                if (bc != null)
                {
                    bc.enabled = false;
                }
            }
            
            activatedColliders.Clear();
            selectedPiece = null;
            
            if (showDebugInfo)
            {
                // Debug.Log("CollisionValidator: Modo edición desactivado.");
            }
        }
        
        /// <summary>
        /// Configura la pieza seleccionada (detecta sus Box_).
        /// </summary>
        public void SetSelectedPiece(GameObject piece)
        {
            selectedPiece = piece;
            
            if (piece == null) return;
            
            // Contar boxes para debug
            int boxCount = 0;
            var allTransforms = piece.GetComponentsInChildren<Transform>(true);
            foreach (var t in allTransforms)
            {
                if (t.name.StartsWith(boxPrefix))
                {
                    boxCount++;
                }
            }
            
            if (showDebugInfo)
            {
                // Debug.Log($"CollisionValidator: Pieza seleccionada tiene {boxCount} Box_");
            }
        }
        
        /// <summary>
        /// Verifica si la pieza seleccionada colisiona en su posición actual.
        /// </summary>
        public bool CheckCollision(GameObject pieceToCheck = null)
        {
            GameObject piece = pieceToCheck ?? selectedPiece;
            
            if (piece == null) return false;
            
            // Buscar todos los Box_ en la pieza y verificar cada uno
            var allTransforms = piece.GetComponentsInChildren<Transform>(true);
            
            foreach (var t in allTransforms)
            {
                if (t.name.StartsWith(boxPrefix))
                {
                    // Usar la escala del transform como tamaño del box
                    Vector3 halfExtents = t.lossyScale * 0.5f * collisionMargin;
                    
                    // Verificar colisión
                    Collider[] overlaps = Physics.OverlapBox(
                        t.position,
                        halfExtents,
                        t.rotation,
                        ~0,
                        QueryTriggerInteraction.Ignore
                    );
                    
                    foreach (var col in overlaps)
                    {
                        // Ignorar colliders de la propia pieza
                        if (col.transform.IsChildOf(piece.transform))
                            continue;
                        
                        // Verificar si es un collider activado
                        BoxCollider bc = col as BoxCollider;
                        if (bc != null && activatedColliders.Contains(bc))
                        {
                            if (showDebugInfo)
                            {
                                // Debug.Log($"CollisionValidator: ¡COLISIÓN! {t.name} con {col.gameObject.name}");
                            }
                            return true;
                        }
                    }
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Registra los BoxColliders de una pieza recién colocada.
        /// </summary>
        public void RegisterPlacedPieceCollider(GameObject placedPiece)
        {
            if (placedPiece == null) return;
            
            var allTransforms = placedPiece.GetComponentsInChildren<Transform>(true);
            
            foreach (var t in allTransforms)
            {
                if (t.name.StartsWith(boxPrefix))
                {
                    BoxCollider bc = GetOrCreateBoxCollider(t);
                    if (bc != null && !activatedColliders.Contains(bc))
                    {
                        bc.enabled = true;
                        activatedColliders.Add(bc);
                        
                        if (showDebugInfo)
                        {
                            // Debug.Log($"CollisionValidator: Registrado BoxCollider de pieza colocada: {t.name}");
                        }
                    }
                }
            }
        }
        
        #endregion
        
        #region Private Methods
        
        private BoxCollider GetOrCreateBoxCollider(Transform t)
        {
            BoxCollider bc = t.GetComponent<BoxCollider>();
            
            if (bc == null)
            {
                bc = t.gameObject.AddComponent<BoxCollider>();
                // El tamaño del BoxCollider es 1,1,1 y la escala del transform define el tamaño real
                bc.size = Vector3.one;
                bc.center = Vector3.zero;
            }
            
            return bc;
        }
        
        #endregion
        
        #region Debug Gizmos
        
        private void OnDrawGizmos()
        {
            if (!showGizmos) return;
            
            // Dibujar BoxColliders activados (amarillo)
            Gizmos.color = Color.yellow;
            foreach (var bc in activatedColliders)
            {
                if (bc != null && bc.enabled)
                {
                    Matrix4x4 matrix = Matrix4x4.TRS(bc.transform.position, bc.transform.rotation, bc.transform.lossyScale);
                    Gizmos.matrix = matrix;
                    Gizmos.DrawWireCube(bc.center, bc.size);
                }
            }
            
            Gizmos.matrix = Matrix4x4.identity;
            
            // Dibujar boxes de la pieza seleccionada (verde/rojo)
            if (selectedPiece != null)
            {
                var allTransforms = selectedPiece.GetComponentsInChildren<Transform>(true);
                
                foreach (var t in allTransforms)
                {
                    if (t.name.StartsWith(boxPrefix))
                    {
                        // Verificar si este box específico colisiona
                        Vector3 halfExtents = t.lossyScale * 0.5f * collisionMargin;
                        Collider[] overlaps = Physics.OverlapBox(t.position, halfExtents, t.rotation, ~0, QueryTriggerInteraction.Ignore);
                        
                        bool hasCollision = false;
                        foreach (var col in overlaps)
                        {
                            if (!col.transform.IsChildOf(selectedPiece.transform))
                            {
                                BoxCollider bc = col as BoxCollider;
                                if (bc != null && activatedColliders.Contains(bc))
                                {
                                    hasCollision = true;
                                    break;
                                }
                            }
                        }
                        
                        Gizmos.color = hasCollision ? Color.red : Color.green;
                        Matrix4x4 matrix = Matrix4x4.TRS(t.position, t.rotation, t.lossyScale * collisionMargin);
                        Gizmos.matrix = matrix;
                        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
                    }
                }
                
                Gizmos.matrix = Matrix4x4.identity;
            }
        }
        
        #endregion
    }
}
