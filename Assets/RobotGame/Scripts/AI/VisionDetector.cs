using UnityEngine;

namespace RobotGame.AI
{
    /// <summary>
    /// Sistema de detección visual por campo de visión (FOV).
    /// 
    /// Detecta targets dentro de un cono frontal.
    /// Usa raycast para verificar línea de visión (obstáculos).
    /// 
    /// A futuro se pueden agregar otros detectores (radar, sonido, etc.)
    /// que implementen una interfaz común.
    /// </summary>
    public class VisionDetector : MonoBehaviour
    {
        [Header("Configuración de Visión")]
        [Tooltip("Distancia máxima de detección")]
        [SerializeField] private float detectionRange = 15f;
        
        [Tooltip("Ángulo del campo de visión (grados)")]
        [Range(10f, 360f)]
        [SerializeField] private float fieldOfView = 120f;
        
        [Tooltip("Altura del origen del raycast (ojos del robot)")]
        [SerializeField] private float eyeHeight = 1.5f;
        
        [Header("Raycast")]
        [Tooltip("Layers que bloquean la visión (se auto-configuran para ignorar RobotParts y RobotHitbox)")]
        [SerializeField] private LayerMask obstacleLayers = ~0;
        
        [Tooltip("Layers donde buscar targets")]
        [SerializeField] private LayerMask targetLayers = ~0;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = true;
        [SerializeField] private Color gizmoColorNoTarget = new Color(1f, 1f, 0f, 0.3f);
        [SerializeField] private Color gizmoColorHasTarget = new Color(1f, 0f, 0f, 0.3f);
        
        // Target actual
        private Transform currentTarget;
        private bool hasLineOfSight = false;
        
        // Layers que se ignoran automáticamente (RobotParts=10, RobotHitbox=11)
        private const int LAYER_ROBOT_PARTS = 10;
        private const int LAYER_ROBOT_HITBOX = 11;
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            // Auto-configurar obstacleLayers para ignorar las partes de robot
            // Esto evita que el raycast golpee el propio robot
            int robotLayersMask = (1 << LAYER_ROBOT_PARTS) | (1 << LAYER_ROBOT_HITBOX);
            obstacleLayers &= ~robotLayersMask;
        }
        
        #endregion
        
        #region Properties
        
        /// <summary>
        /// Distancia máxima de detección.
        /// </summary>
        public float DetectionRange
        {
            get => detectionRange;
            set => detectionRange = Mathf.Max(0f, value);
        }
        
        /// <summary>
        /// Ángulo del campo de visión en grados.
        /// </summary>
        public float FieldOfView
        {
            get => fieldOfView;
            set => fieldOfView = Mathf.Clamp(value, 10f, 360f);
        }
        
        /// <summary>
        /// Si actualmente tiene un target en línea de visión.
        /// </summary>
        public bool HasLineOfSight => hasLineOfSight;
        
        /// <summary>
        /// El target actual (null si no hay ninguno visible).
        /// </summary>
        public Transform CurrentTarget => hasLineOfSight ? currentTarget : null;
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Verifica si un target específico está dentro del campo de visión.
        /// </summary>
        /// <param name="target">Transform del target a verificar</param>
        /// <returns>True si está visible</returns>
        public bool CanSeeTarget(Transform target)
        {
            if (target == null) return false;
            
            Vector3 eyePosition = GetEyePosition();
            Vector3 targetPosition = target.position;
            
            // Verificar distancia
            float distance = Vector3.Distance(eyePosition, targetPosition);
            if (distance > detectionRange) return false;
            
            // Verificar ángulo (campo de visión)
            Vector3 directionToTarget = (targetPosition - eyePosition).normalized;
            float angle = Vector3.Angle(transform.forward, directionToTarget);
            
            if (angle > fieldOfView / 2f) return false;
            
            // Verificar línea de visión (raycast)
            // QueryTriggerInteraction.Ignore evita que triggers (como AttackZones) bloqueen la visión
            if (Physics.Raycast(eyePosition, directionToTarget, out RaycastHit hit, distance, obstacleLayers, QueryTriggerInteraction.Ignore))
            {
                // Si el raycast golpeó algo, verificar si es el target
                if (hit.transform != target && !hit.transform.IsChildOf(target))
                {
                    // Hay un obstáculo bloqueando la visión
                    return false;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Actualiza el estado de detección para un target específico.
        /// Llamar cada frame para mantener actualizado.
        /// </summary>
        /// <param name="target">Target a trackear</param>
        /// <returns>True si el target es visible</returns>
        public bool UpdateDetection(Transform target)
        {
            currentTarget = target;
            hasLineOfSight = CanSeeTarget(target);
            return hasLineOfSight;
        }
        
        /// <summary>
        /// Busca el target más cercano visible dentro del FOV.
        /// </summary>
        /// <param name="tag">Tag de los objetos a buscar (ej: "Player")</param>
        /// <returns>Transform del target más cercano o null</returns>
        public Transform FindNearestVisibleTarget(string tag)
        {
            GameObject[] potentialTargets = GameObject.FindGameObjectsWithTag(tag);
            
            Transform nearest = null;
            float nearestDistance = float.MaxValue;
            
            foreach (var obj in potentialTargets)
            {
                if (CanSeeTarget(obj.transform))
                {
                    float distance = Vector3.Distance(transform.position, obj.transform.position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = obj.transform;
                    }
                }
            }
            
            currentTarget = nearest;
            hasLineOfSight = nearest != null;
            
            return nearest;
        }
        
        /// <summary>
        /// Obtiene la posición de los "ojos" del robot.
        /// </summary>
        public Vector3 GetEyePosition()
        {
            return transform.position + Vector3.up * eyeHeight;
        }
        
        /// <summary>
        /// Limpia el target actual.
        /// </summary>
        public void ClearTarget()
        {
            currentTarget = null;
            hasLineOfSight = false;
        }
        
        #endregion
        
        #region Debug Gizmos
        
        private void OnDrawGizmosSelected()
        {
            if (!showDebugGizmos) return;
            
            DrawVisionCone();
        }
        
        private void DrawVisionCone()
        {
            Vector3 eyePos = GetEyePosition();
            
            // Color según si tiene target
            Gizmos.color = hasLineOfSight ? gizmoColorHasTarget : gizmoColorNoTarget;
            
            // Dibujar arco del FOV
            int segments = 20;
            float halfFOV = fieldOfView / 2f;
            
            Vector3 forward = transform.forward * detectionRange;
            
            // Rotar para obtener los bordes del cono
            Vector3 leftBoundary = Quaternion.Euler(0, -halfFOV, 0) * forward;
            Vector3 rightBoundary = Quaternion.Euler(0, halfFOV, 0) * forward;
            
            // Dibujar líneas de los bordes
            Gizmos.DrawLine(eyePos, eyePos + leftBoundary);
            Gizmos.DrawLine(eyePos, eyePos + rightBoundary);
            
            // Dibujar arco
            Vector3 previousPoint = eyePos + leftBoundary;
            float angleStep = fieldOfView / segments;
            
            for (int i = 1; i <= segments; i++)
            {
                float angle = -halfFOV + (angleStep * i);
                Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward * detectionRange;
                Vector3 point = eyePos + direction;
                
                Gizmos.DrawLine(previousPoint, point);
                previousPoint = point;
            }
            
            // Dibujar línea al target actual si es visible
            if (hasLineOfSight && currentTarget != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(eyePos, currentTarget.position);
                Gizmos.DrawWireSphere(currentTarget.position, 0.5f);
            }
        }
        
        #endregion
    }
}
