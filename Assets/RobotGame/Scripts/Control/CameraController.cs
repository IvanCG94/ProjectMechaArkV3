using UnityEngine;

namespace RobotGame.Control
{
    /// <summary>
    /// Controlador de cámara con dos modos:
    /// 
    /// MODO NORMAL (jugando):
    /// - Cursor bloqueado (invisible)
    /// - Mouse mueve la cámara directamente (sin click)
    /// - Mantener Alt para liberar el cursor temporalmente
    /// - Zoom con rueda
    /// 
    /// MODO EDICIÓN:
    /// - Cursor libre (visible)
    /// - Click derecho sostenido para rotar la cámara
    /// - Zoom con rueda
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Objetivo")]
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
        [SerializeField] private float positionSmoothTime = 0.15f;
        [SerializeField] private float rotationSmoothTime = 0.1f;
        
        [Header("Control de Mouse")]
        [Tooltip("Tecla para liberar el mouse en modo normal")]
        [SerializeField] private KeyCode freeCursorKey = KeyCode.LeftAlt;
        
        [Header("Estado")]
        [SerializeField] private bool isInEditMode = false;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugUI = false;
        
        // Estado interno
        private float currentDistance;
        private float currentHorizontalAngle = 0f;
        private float currentVerticalAngle = 30f;
        
        // SmoothDamp velocities
        private Vector3 positionVelocity;
        private float rotationVelocityX;
        private float rotationVelocityY;
        private float rotationVelocityZ;
        
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
                currentHorizontalAngle = target.eulerAngles.y;
                UpdateCameraPosition(true);
            }
            
            // Por defecto empezamos en modo normal (cursor bloqueado)
            if (!isInEditMode)
            {
                LockCursor();
            }
        }
        
        private void LateUpdate()
        {
            if (target == null) return;
            
            HandleZoom();
            
            if (isInEditMode)
            {
                HandleEditModeOrbit();
            }
            else
            {
                HandleNormalModeOrbit();
            }
            
            UpdateCameraPosition(false);
        }
        
        #region Cursor Management
        
        private void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        
        private void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        
        #endregion
        
        #region Input Handling
        
        private void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            
            if (scroll != 0f)
            {
                currentDistance -= scroll * zoomSpeed;
                currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);
            }
        }
        
        /// <summary>
        /// Modo normal: mouse mueve la cámara directamente, Alt libera el cursor.
        /// </summary>
        private void HandleNormalModeOrbit()
        {
            // Alt libera el cursor temporalmente
            bool altPressed = Input.GetKey(freeCursorKey);
            
            if (altPressed)
            {
                // Alt presionado: cursor libre, no mover cámara
                if (Cursor.lockState != CursorLockMode.None)
                {
                    UnlockCursor();
                }
                return;
            }
            else
            {
                // Alt no presionado: cursor bloqueado
                if (Cursor.lockState != CursorLockMode.Locked)
                {
                    LockCursor();
                }
            }
            
            // Mouse mueve la cámara directamente
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");
            
            if (Mathf.Abs(mouseX) > 0.01f || Mathf.Abs(mouseY) > 0.01f)
            {
                currentHorizontalAngle -= mouseX * orbitSpeed;
                currentVerticalAngle -= mouseY * orbitSpeed;
                currentVerticalAngle = Mathf.Clamp(currentVerticalAngle, minVerticalAngle, maxVerticalAngle);
            }
        }
        
        /// <summary>
        /// Modo edición: click derecho sostenido para rotar.
        /// </summary>
        private void HandleEditModeOrbit()
        {
            if (Input.GetMouseButton(1))
            {
                float mouseX = Input.GetAxis("Mouse X");
                float mouseY = Input.GetAxis("Mouse Y");
                
                if (Mathf.Abs(mouseX) > 0.01f || Mathf.Abs(mouseY) > 0.01f)
                {
                    currentHorizontalAngle -= mouseX * orbitSpeed;
                    currentVerticalAngle -= mouseY * orbitSpeed;
                    currentVerticalAngle = Mathf.Clamp(currentVerticalAngle, minVerticalAngle, maxVerticalAngle);
                }
            }
        }
        
        #endregion
        
        #region Camera Position
        
        private void UpdateCameraPosition(bool instant)
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
                positionVelocity = Vector3.zero;
            }
            else
            {
                // Posición suave con SmoothDamp
                transform.position = Vector3.SmoothDamp(
                    transform.position, 
                    desiredPosition, 
                    ref positionVelocity, 
                    positionSmoothTime
                );
                
                // Rotación suave - mirar al punto focal
                Quaternion desiredRotation = Quaternion.LookRotation(focusPoint - transform.position);
                
                // Suavizar cada componente del euler por separado para evitar problemas de gimbal
                Vector3 currentEuler = transform.rotation.eulerAngles;
                Vector3 targetEuler = desiredRotation.eulerAngles;
                
                float smoothX = Mathf.SmoothDampAngle(currentEuler.x, targetEuler.x, ref rotationVelocityX, rotationSmoothTime);
                float smoothY = Mathf.SmoothDampAngle(currentEuler.y, targetEuler.y, ref rotationVelocityY, rotationSmoothTime);
                float smoothZ = Mathf.SmoothDampAngle(currentEuler.z, targetEuler.z, ref rotationVelocityZ, rotationSmoothTime);
                
                transform.rotation = Quaternion.Euler(smoothX, smoothY, smoothZ);
            }
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Entra en modo edición.
        /// </summary>
        public void EnterEditMode()
        {
            isInEditMode = true;
            UnlockCursor();
            Debug.Log("CameraController: Entrando a modo edición");
        }
        
        /// <summary>
        /// Sale del modo edición.
        /// </summary>
        public void ExitEditMode()
        {
            isInEditMode = false;
            LockCursor();
            Debug.Log("CameraController: Saliendo de modo edición");
        }
        
        /// <summary>
        /// Establece el objetivo de la cámara.
        /// </summary>
        public void SetTarget(Transform newTarget, bool instant = false)
        {
            target = newTarget;
            
            if (target != null)
            {
                if (instant)
                {
                    currentHorizontalAngle = target.eulerAngles.y;
                    UpdateCameraPosition(true);
                }
            }
        }
        
        /// <summary>
        /// Ajusta el zoom.
        /// </summary>
        public void SetZoom(float distance)
        {
            currentDistance = Mathf.Clamp(distance, minDistance, maxDistance);
        }
        
        #endregion
        
        #region Debug
        
        private void OnGUI()
        {
            if (!showDebugUI) return;
            
            GUILayout.BeginArea(new Rect(Screen.width - 260, 10, 250, 100));
            GUILayout.BeginVertical("box");
            
            GUILayout.Label("=== CameraController Debug ===");
            GUILayout.Label($"Modo: {(isInEditMode ? "EDICIÓN" : "NORMAL")}");
            GUILayout.Label($"Cursor: {(Cursor.lockState == CursorLockMode.Locked ? "Bloqueado" : "Libre")}");
            GUILayout.Label($"H Angle: {currentHorizontalAngle:F1}°");
            GUILayout.Label($"V Angle: {currentVerticalAngle:F1}°");
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        
        #endregion
    }
}
