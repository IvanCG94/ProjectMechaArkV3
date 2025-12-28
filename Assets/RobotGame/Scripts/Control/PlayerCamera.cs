using UnityEngine;

namespace RobotGame.Control
{
    /// <summary>
    /// Sistema de cámara del jugador.
    /// Este script es INDEPENDIENTE del sistema modular y puede ser reemplazado
    /// sin afectar la lógica de ensamblaje, Core, o edición.
    /// 
    /// INTEGRACIÓN:
    /// - Recibe el Transform a seguir via SetTarget()
    /// - Modo edición via EnterEditMode()/ExitEditMode()
    /// 
    /// PARA REEMPLAZAR:
    /// - Mantén la interfaz pública (SetTarget, EnterEditMode, ExitEditMode, IsInEditMode, Target)
    /// - Implementa tu propio sistema de cámara en LateUpdate()
    /// </summary>
    public class PlayerCamera : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private float heightOffset = 1.5f;
        
        [Header("Órbita")]
        [SerializeField] private float orbitSpeed = 3f;
        [SerializeField] private float minVerticalAngle = -20f;
        [SerializeField] private float maxVerticalAngle = 60f;
        
        [Header("Zoom")]
        [SerializeField] private float zoomSpeed = 3f;
        [SerializeField] private float minDistance = 2f;
        [SerializeField] private float maxDistance = 15f;
        [SerializeField] private float defaultDistance = 6f;
        
        [Header("Suavizado")]
        [SerializeField] private float followSpeed = 10f;
        
        [Header("Estado")]
        [SerializeField] private bool isInEditMode = false;
        
        // Estado interno
        private float currentDistance;
        private float currentHorizontalAngle = 0f;
        private float currentVerticalAngle = 30f;
        
        /// <summary>
        /// El objetivo que sigue la cámara.
        /// </summary>
        public Transform Target => target;
        
        /// <summary>
        /// Si está en modo edición.
        /// </summary>
        public bool IsInEditMode => isInEditMode;
        
        private void Start()
        {
            currentDistance = defaultDistance;
            
            if (target != null)
            {
                UpdateCameraPosition(true);
            }
        }
        
        private void LateUpdate()
        {
            if (target == null) return;
            
            HandleZoom();
            
            if (!isInEditMode)
            {
                HandleOrbit();
            }
            
            UpdateCameraPosition(false);
        }
        
        /// <summary>
        /// Lógica de zoom. Puede ser sobrescrita.
        /// </summary>
        protected virtual void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            
            if (scroll != 0f)
            {
                currentDistance -= scroll * zoomSpeed;
                currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);
            }
        }
        
        /// <summary>
        /// Lógica de órbita. Puede ser sobrescrita.
        /// </summary>
        protected virtual void HandleOrbit()
        {
            if (Input.GetMouseButton(1))
            {
                float mouseX = Input.GetAxis("Mouse X");
                float mouseY = Input.GetAxis("Mouse Y");
                
                currentHorizontalAngle += mouseX * orbitSpeed;
                currentVerticalAngle -= mouseY * orbitSpeed;
                currentVerticalAngle = Mathf.Clamp(currentVerticalAngle, minVerticalAngle, maxVerticalAngle);
            }
        }
        
        /// <summary>
        /// Actualiza la posición de la cámara. Puede ser sobrescrita.
        /// </summary>
        protected virtual void UpdateCameraPosition(bool instant)
        {
            if (target == null) return;
            
            Vector3 focusPoint = target.position + Vector3.up * heightOffset;
            
            float horizontalRad = currentHorizontalAngle * Mathf.Deg2Rad;
            float verticalRad = currentVerticalAngle * Mathf.Deg2Rad;
            
            Vector3 direction = new Vector3(
                Mathf.Sin(horizontalRad) * Mathf.Cos(verticalRad),
                Mathf.Sin(verticalRad),
                -Mathf.Cos(horizontalRad) * Mathf.Cos(verticalRad)
            );
            
            Vector3 desiredPosition = focusPoint + direction * currentDistance;
            
            if (instant)
            {
                transform.position = desiredPosition;
                transform.LookAt(focusPoint);
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);
                Quaternion desiredRotation = Quaternion.LookRotation(focusPoint - transform.position);
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, followSpeed * Time.deltaTime);
            }
        }
        
        /// <summary>
        /// Entra en modo edición.
        /// </summary>
        public void EnterEditMode()
        {
            isInEditMode = true;
        }
        
        /// <summary>
        /// Sale del modo edición.
        /// </summary>
        public void ExitEditMode()
        {
            isInEditMode = false;
        }
        
        /// <summary>
        /// Establece el objetivo de la cámara.
        /// </summary>
        public void SetTarget(Transform newTarget, bool instant = false)
        {
            target = newTarget;
            
            if (target != null && instant)
            {
                UpdateCameraPosition(true);
            }
        }
        
        /// <summary>
        /// Ajusta el zoom.
        /// </summary>
        public void SetZoom(float distance)
        {
            currentDistance = Mathf.Clamp(distance, minDistance, maxDistance);
        }
    }
}
