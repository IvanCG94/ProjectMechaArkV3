using UnityEngine;
using System.Collections.Generic;

namespace RobotGame.Combat
{
    /// <summary>
    /// Zona de detección para ataques.
    /// 
    /// Se coloca en los brazos/partes estructurales que tienen animaciones de ataque.
    /// El collider representa el área que cubre el ataque durante su animación.
    /// 
    /// CONFIGURACIÓN:
    /// 1. Crear GameObject hijo del brazo con un Collider (isTrigger = true)
    /// 2. Agregar componente AttackZone y asignar zoneId
    /// 3. Dimensionar el collider para que cubra el recorrido de la animación
    /// 4. Dejar DESACTIVADO por defecto
    /// 
    /// FLUJO:
    /// - El arma (CombatPart) busca estas zonas y las activa si tiene ataques compatibles
    /// - La IA consulta qué zonas tienen al jugador dentro para seleccionar ataques
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class AttackZone : MonoBehaviour
    {
        [Header("Identificación")]
        [Tooltip("ID único de esta zona. Debe coincidir con el zoneId del AttackData que la usa.")]
        [SerializeField] private string zoneId = "horizontal";
        
        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;
        [SerializeField] private bool showDebugGizmos = true;
        [SerializeField] private Color gizmoColor = new Color(1f, 0.5f, 0f, 0.3f);
        [SerializeField] private Color gizmoColorActive = new Color(1f, 0f, 0f, 0.5f);
        
        // Estado
        private bool isTargetInZone = false;
        private Collider zoneCollider;
        private HashSet<Collider> collidersInZone = new HashSet<Collider>();
        
        // Target actual (asignado por el AI/Controller)
        private Transform currentTarget;
        
        // Vínculo dinámico con ataque
        private AttackData linkedAttack;
        private CombatPart linkedCombatPart;
        
        #region Properties
        
        /// <summary>
        /// ID de esta zona (ej: "horizontal", "vertical", "spin").
        /// </summary>
        public string ZoneId => zoneId;
        
        /// <summary>
        /// Si el target actual está dentro de esta zona.
        /// </summary>
        public bool IsTargetInZone => isTargetInZone;
        
        /// <summary>
        /// [LEGACY] Alias para compatibilidad. Usa IsTargetInZone.
        /// </summary>
        public bool IsPlayerInZone => isTargetInZone;
        
        /// <summary>
        /// El ataque vinculado a esta zona (asignado por CombatPart).
        /// </summary>
        public AttackData LinkedAttack => linkedAttack;
        
        /// <summary>
        /// El CombatPart que activó esta zona.
        /// </summary>
        public CombatPart LinkedCombatPart => linkedCombatPart;
        
        /// <summary>
        /// Si esta zona está activa y vinculada a un ataque.
        /// </summary>
        public bool IsLinked => linkedAttack != null;
        
        /// <summary>
        /// Target actual que la zona está rastreando.
        /// </summary>
        public Transform CurrentTarget => currentTarget;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            zoneCollider = GetComponent<Collider>();
            
            // Asegurar que sea trigger
            if (zoneCollider != null && !zoneCollider.isTrigger)
            {
                zoneCollider.isTrigger = true;
                if (showDebugLogs) Debug.Log($"[AttackZone] '{zoneId}': Collider configurado como trigger automáticamente");
            }
        }
        
        private void OnEnable()
        {
            if (showDebugLogs) Debug.Log($"[AttackZone] '{zoneId}' ACTIVADA. IsLinked: {IsLinked}");
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (!IsLinked) return;
            
            // Ignorar triggers (otros colliders trigger como hitboxes)
            if (other.isTrigger) return;
            
            // Ignorar colliders del propio robot
            if (IsOwnCollider(other)) return;
            
            // Guardar colliders que entran
            collidersInZone.Add(other);
            
            // Verificar si es el target actual
            if (currentTarget != null && BelongsToTarget(other, currentTarget))
            {
                isTargetInZone = true;
                if (showDebugLogs) Debug.Log($"[AttackZone] '{zoneId}' TARGET DETECTADO: {other.name}");
            }
            else if (showDebugLogs)
            {
                Debug.Log($"[AttackZone] '{zoneId}' Collider entró: {other.name} (NO es target. Target: {(currentTarget != null ? currentTarget.name : "NULL")})");
            }
        }
        
        private void OnTriggerStay(Collider other)
        {
            if (!IsLinked) return;
            if (other.isTrigger) return;
            if (IsOwnCollider(other)) return;
            
            // Agregar si no estaba
            if (!collidersInZone.Contains(other))
            {
                collidersInZone.Add(other);
            }
            
            // Re-verificar target (por si cambió después de entrar)
            if (currentTarget != null && !isTargetInZone && BelongsToTarget(other, currentTarget))
            {
                isTargetInZone = true;
                if (showDebugLogs) Debug.Log($"[AttackZone] '{zoneId}' Target detectado en Stay: {other.name}");
            }
        }
        
        private void OnTriggerExit(Collider other)
        {
            if (!IsLinked) return;
            
            collidersInZone.Remove(other);
            
            // Re-evaluar si el target sigue en la zona
            if (currentTarget != null && isTargetInZone)
            {
                bool stillInZone = CheckTargetStillInZone();
                if (!stillInZone)
                {
                    isTargetInZone = false;
                    if (showDebugLogs) Debug.Log($"[AttackZone] '{zoneId}' Target SALIÓ de la zona");
                }
            }
        }
        
        private void OnDisable()
        {
            // Limpiar estado al desactivar
            collidersInZone.Clear();
            isTargetInZone = false;
            if (showDebugLogs) Debug.Log($"[AttackZone] '{zoneId}' DESACTIVADA");
        }
        
        /// <summary>
        /// Verifica si un collider pertenece al propio robot (para ignorarlo).
        /// </summary>
        private bool IsOwnCollider(Collider col)
        {
            Transform myRoot = transform.root;
            Transform colRoot = col.transform.root;
            return myRoot == colRoot;
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Establece el target actual que la zona debe detectar.
        /// Llamado por WildRobot o cualquier AI cuando cambia de objetivo.
        /// </summary>
        public void SetTarget(Transform target)
        {
            // Si es el mismo target, no hacer nada
            if (currentTarget == target) return;
            
            currentTarget = target;
            
            // Resetear estado
            isTargetInZone = false;
            
            // Si hay nuevo target, verificar si ya está en la zona
            if (target != null && collidersInZone.Count > 0)
            {
                isTargetInZone = CheckTargetStillInZone();
                if (showDebugLogs)
                {
                    Debug.Log($"[AttackZone] '{zoneId}' Nuevo target: {target.name}. Ya en zona: {isTargetInZone}");
                }
            }
            else if (showDebugLogs && target == null)
            {
                Debug.Log($"[AttackZone] '{zoneId}' Target removido");
            }
        }
        
        /// <summary>
        /// Limpia el target actual.
        /// </summary>
        public void ClearTarget()
        {
            currentTarget = null;
            isTargetInZone = false;
        }
        
        /// <summary>
        /// Vincula esta zona a un ataque y CombatPart.
        /// Llamado por CombatPart cuando se conecta un arma.
        /// </summary>
        public void Link(AttackData attack, CombatPart combatPart)
        {
            linkedAttack = attack;
            linkedCombatPart = combatPart;
            
            // Activar el GameObject
            gameObject.SetActive(true);
            
            if (showDebugLogs) Debug.Log($"[AttackZone] '{zoneId}' VINCULADA a ataque '{attack.attackName}' desde '{combatPart.name}'");
        }
        
        /// <summary>
        /// Desvincula esta zona.
        /// Llamado cuando se desconecta el arma.
        /// </summary>
        public void Unlink()
        {
            if (showDebugLogs && linkedAttack != null)
            {
                Debug.Log($"[AttackZone] '{zoneId}' desvinculada de '{linkedAttack.attackName}'");
            }
            
            linkedAttack = null;
            linkedCombatPart = null;
            currentTarget = null;
            collidersInZone.Clear();
            isTargetInZone = false;
            
            // Desactivar el GameObject
            gameObject.SetActive(false);
        }
        
        #endregion
        
        #region Private Methods
        
        /// <summary>
        /// Verifica si un collider pertenece al target especificado.
        /// </summary>
        private bool BelongsToTarget(Collider col, Transform target)
        {
            if (col == null || target == null) return false;
            
            // Verificar si el collider está en la jerarquía del target
            Transform current = col.transform;
            while (current != null)
            {
                if (current == target) return true;
                current = current.parent;
            }
            
            // Verificar por root (por si el target es un hijo del robot)
            if (col.transform.root == target.root) return true;
            
            return false;
        }
        
        /// <summary>
        /// Verifica si el target actual sigue teniendo colliders en la zona.
        /// </summary>
        private bool CheckTargetStillInZone()
        {
            if (currentTarget == null) return false;
            
            foreach (var col in collidersInZone)
            {
                if (col != null && BelongsToTarget(col, currentTarget))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        #endregion
        
        #region Debug
        
        private void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;
            
            Collider col = GetComponent<Collider>();
            if (col == null) return;
            
            // Color según estado:
            // - Rojo: Target está dentro de la zona
            // - Naranja: Zona activa pero target no está dentro
            // - Gris semi-transparente: Zona no vinculada o sin target
            Color drawColor;
            if (!IsLinked || currentTarget == null)
            {
                drawColor = new Color(0.5f, 0.5f, 0.5f, 0.2f); // Gris - inactiva
            }
            else if (isTargetInZone)
            {
                drawColor = gizmoColorActive; // Rojo - target dentro
            }
            else
            {
                drawColor = gizmoColor; // Naranja - activa pero target fuera
            }
            
            Gizmos.color = drawColor;
            
            if (col is BoxCollider box)
            {
                Matrix4x4 oldMatrix = Gizmos.matrix;
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.matrix = oldMatrix;
            }
            else if (col is SphereCollider sphere)
            {
                Vector3 center = transform.TransformPoint(sphere.center);
                Gizmos.DrawSphere(center, sphere.radius);
                Gizmos.DrawWireSphere(center, sphere.radius);
            }
            else if (col is CapsuleCollider capsule)
            {
                Vector3 center = transform.TransformPoint(capsule.center);
                Gizmos.DrawSphere(center, capsule.radius);
                Gizmos.DrawWireSphere(center, capsule.radius);
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            #if UNITY_EDITOR
            string targetName = currentTarget != null ? currentTarget.name : "Sin target";
            string status;
            if (!IsLinked)
            {
                status = "No vinculada";
            }
            else if (currentTarget == null)
            {
                status = "Esperando target...";
            }
            else if (isTargetInZone)
            {
                status = $"TARGET DENTRO: {targetName}";
            }
            else
            {
                status = $"Target fuera: {targetName}";
            }
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, $"Zone: {zoneId}\n{status}");
            #endif
        }
        
        #endregion
    }
}
