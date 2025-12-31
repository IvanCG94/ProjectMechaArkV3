using UnityEngine;

namespace RobotGame.Control
{
    /// <summary>
    /// Sistema de cámara third-person con dos modos de control:
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
    public class PlayerCamera : MonoBehaviour
    {
        #region Serialized Fields
        
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.5f, 0f);
        
        [Header("Distancia")]
        [SerializeField] private float defaultDistance = 6f;
        [SerializeField] private float minDistance = 2f;
        [SerializeField] private float maxDistance = 15f;
        [SerializeField] private float zoomSpeed = 5f;
        [SerializeField] private float zoomSmoothTime = 0.1f;
        
        [Header("Órbita")]
        [SerializeField] private float orbitSensitivity = 3f;
        [Tooltip("Ángulo mínimo vertical (negativo = mirar arriba). Valores muy negativos causan que la cámara colisione con el suelo.")]
        [SerializeField] private float minVerticalAngle = -10f;
        [SerializeField] private float maxVerticalAngle = 70f;
        
        [Header("Control de Mouse")]
        [Tooltip("Tecla para liberar el mouse en modo normal")]
        [SerializeField] private KeyCode freeCursorKey = KeyCode.LeftAlt;
        
        [Header("Auto-Recentrado")]
        [SerializeField] private bool enableAutoRecenter = true;
        [SerializeField] private float autoRecenterDelay = 2f;
        [SerializeField] private float autoRecenterSpeed = 1f;
        [SerializeField] private float autoRecenterMinSpeed = 0.5f;
        
        [Header("Suavizado")]
        [SerializeField] private float positionSmoothTime = 0.1f;
        [SerializeField] private float rotationSmoothTime = 0.05f;
        
        [Header("Colisión")]
        [SerializeField] private bool enableCollision = true;
        [SerializeField] private float collisionRadius = 0.2f;
        [Tooltip("Layers con los que la cámara colisiona. IMPORTANTE: Excluir el layer 'Player' para evitar colisión con el propio robot.")]
        [SerializeField] private LayerMask collisionLayers = ~0;
        [SerializeField] private float collisionSmoothTime = 0.1f;
        
        [Header("Estado (Debug)")]
        [SerializeField] private bool isInEditMode = false;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = false;
        [SerializeField] private bool showDebugUI = false;
        
        #endregion
        
        #region Private Fields
        
        // Ángulos actuales
        private float horizontalAngle;
        private float verticalAngle = 30f;
        
        // Distancia
        private float currentDistance;
        private float targetDistance;
        private float distanceVelocity;
        
        // Posición suave
        private Vector3 currentFocusPoint;
        private Vector3 focusVelocity;
        
        // Colisión
        private float collisionDistance;
        private float collisionVelocity;
        
        // Auto-recentrado
        private float timeSinceLastInput;
        private bool isOrbiting;
        
        // Referencia al PlayerMovement para detectar movimiento
        private PlayerMovement playerMovement;
        
        #endregion
        
        #region Properties
        
        public Transform Target => target;
        public bool IsInEditMode => isInEditMode;
        public float HorizontalAngle => horizontalAngle;
        public float VerticalAngle => verticalAngle;
        
        #endregion
        
        #region Public Methods
        
        public void SetTarget(Transform newTarget, bool instant = false)
        {
            target = newTarget;
            
            if (target != null)
            {
                playerMovement = FindObjectOfType<PlayerMovement>();
                
                if (instant)
                {
                    InitializeCameraPosition();
                }
            }
        }
        
        public void EnterEditMode()
        {
            isInEditMode = true;
            UnlockCursor();
            Debug.Log("PlayerCamera: Entrando a modo edición");
        }
        
        public void ExitEditMode()
        {
            isInEditMode = false;
            LockCursor();
            Debug.Log("PlayerCamera: Saliendo de modo edición");
        }
        
        public void SetZoom(float distance)
        {
            targetDistance = Mathf.Clamp(distance, minDistance, maxDistance);
        }
        
        public void SetAngles(float horizontal, float vertical)
        {
            horizontalAngle = horizontal;
            verticalAngle = Mathf.Clamp(vertical, minVerticalAngle, maxVerticalAngle);
        }
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Start()
        {
            currentDistance = defaultDistance;
            targetDistance = defaultDistance;
            collisionDistance = defaultDistance;
            
            if (target != null)
            {
                InitializeCameraPosition();
                playerMovement = FindObjectOfType<PlayerMovement>();
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
            
            HandleInput();
            UpdateFocusPoint();
            UpdateDistance();
            HandleCollision();
            UpdateCameraTransform();
        }
        
        #endregion
        
        #region Initialization
        
        private void InitializeCameraPosition()
        {
            if (target == null) return;
            
            currentFocusPoint = target.position + targetOffset;
            horizontalAngle = target.eulerAngles.y;
            UpdateCameraTransform();
        }
        
        #endregion
        
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
        
        #region Input
        
        private void HandleInput()
        {
            // Zoom con rueda (siempre disponible)
            float scrollInput = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scrollInput) > 0.01f)
            {
                targetDistance -= scrollInput * zoomSpeed;
                targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
            }
            
            // Lógica de órbita según el modo
            if (isInEditMode)
            {
                // MODO EDICIÓN: Click derecho para rotar
                HandleEditModeInput();
            }
            else
            {
                // MODO NORMAL: Mouse directo, Alt para liberar
                HandleNormalModeInput();
            }
            
            // Auto-recentrado
            timeSinceLastInput += Time.deltaTime;
            
            if (enableAutoRecenter && !isOrbiting && !isInEditMode)
            {
                HandleAutoRecenter();
            }
        }
        
        private void HandleNormalModeInput()
        {
            // Alt libera el cursor temporalmente
            if (Input.GetKeyDown(freeCursorKey))
            {
                UnlockCursor();
            }
            if (Input.GetKeyUp(freeCursorKey))
            {
                LockCursor();
            }
            
            // Si Alt está presionado, no mover la cámara
            if (Input.GetKey(freeCursorKey))
            {
                isOrbiting = false;
                return;
            }
            
            // Mouse mueve la cámara directamente
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");
            
            if (Mathf.Abs(mouseX) > 0.01f || Mathf.Abs(mouseY) > 0.01f)
            {
                horizontalAngle -= mouseX * orbitSensitivity; // Invertido: mouse izquierda = cámara izquierda
                verticalAngle -= mouseY * orbitSensitivity;
                verticalAngle = Mathf.Clamp(verticalAngle, minVerticalAngle, maxVerticalAngle);
                
                timeSinceLastInput = 0f;
                isOrbiting = true;
            }
            else
            {
                isOrbiting = false;
            }
        }
        
        private void HandleEditModeInput()
        {
            // En modo edición: click derecho sostenido para rotar
            if (Input.GetMouseButton(1))
            {
                float mouseX = Input.GetAxis("Mouse X");
                float mouseY = Input.GetAxis("Mouse Y");
                
                if (Mathf.Abs(mouseX) > 0.01f || Mathf.Abs(mouseY) > 0.01f)
                {
                    horizontalAngle -= mouseX * orbitSensitivity; // Invertido: mouse izquierda = cámara izquierda
                    verticalAngle -= mouseY * orbitSensitivity;
                    verticalAngle = Mathf.Clamp(verticalAngle, minVerticalAngle, maxVerticalAngle);
                    
                    timeSinceLastInput = 0f;
                    isOrbiting = true;
                }
            }
            else
            {
                isOrbiting = false;
            }
        }
        
        private void HandleAutoRecenter()
        {
            if (timeSinceLastInput < autoRecenterDelay) return;
            if (playerMovement == null || playerMovement.CurrentSpeed < autoRecenterMinSpeed) return;
            
            float targetHorizontalAngle = target.eulerAngles.y;
            horizontalAngle = Mathf.LerpAngle(horizontalAngle, targetHorizontalAngle, 
                autoRecenterSpeed * Time.deltaTime);
        }
        
        #endregion
        
        #region Focus Point
        
        private void UpdateFocusPoint()
        {
            Vector3 targetFocusPoint = target.position + targetOffset;
            currentFocusPoint = Vector3.SmoothDamp(currentFocusPoint, targetFocusPoint, 
                ref focusVelocity, positionSmoothTime);
        }
        
        #endregion
        
        #region Distance
        
        private void UpdateDistance()
        {
            currentDistance = Mathf.SmoothDamp(currentDistance, targetDistance, 
                ref distanceVelocity, zoomSmoothTime);
        }
        
        #endregion
        
        #region Collision
        
        private void HandleCollision()
        {
            if (!enableCollision)
            {
                collisionDistance = currentDistance;
                return;
            }
            
            Vector3 cameraDirection = CalculateCameraDirection();
            float desiredDistance = currentDistance;
            
            if (Physics.SphereCast(currentFocusPoint, collisionRadius, -cameraDirection, 
                out RaycastHit hit, currentDistance, collisionLayers, QueryTriggerInteraction.Ignore))
            {
                desiredDistance = hit.distance - collisionRadius * 0.5f;
                desiredDistance = Mathf.Max(desiredDistance, minDistance * 0.5f);
            }
            
            float smoothTime = desiredDistance < collisionDistance ? 0.01f : collisionSmoothTime;
            collisionDistance = Mathf.SmoothDamp(collisionDistance, desiredDistance, 
                ref collisionVelocity, smoothTime);
        }
        
        #endregion
        
        #region Camera Transform
        
        private Vector3 CalculateCameraDirection()
        {
            float horizontalRad = horizontalAngle * Mathf.Deg2Rad;
            float verticalRad = verticalAngle * Mathf.Deg2Rad;
            
            return new Vector3(
                Mathf.Sin(horizontalRad) * Mathf.Cos(verticalRad),
                Mathf.Sin(verticalRad),
                -Mathf.Cos(horizontalRad) * Mathf.Cos(verticalRad)
            ).normalized;
        }
        
        private void UpdateCameraTransform()
        {
            Vector3 cameraDirection = CalculateCameraDirection();
            float effectiveDistance = Mathf.Min(currentDistance, collisionDistance);
            Vector3 desiredPosition = currentFocusPoint + cameraDirection * effectiveDistance;
            
            transform.position = desiredPosition;
            
            Quaternion desiredRotation = Quaternion.LookRotation(currentFocusPoint - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, 
                (1f / rotationSmoothTime) * Time.deltaTime);
        }
        
        #endregion
        
        #region Debug
        
        private void OnGUI()
        {
            if (!showDebugUI) return;
            
            GUILayout.BeginArea(new Rect(Screen.width - 260, 10, 250, 120));
            GUILayout.BeginVertical("box");
            
            GUILayout.Label("=== PlayerCamera Debug ===");
            GUILayout.Label($"Modo: {(isInEditMode ? "EDICIÓN" : "NORMAL")}");
            GUILayout.Label($"Cursor: {(Cursor.lockState == CursorLockMode.Locked ? "Bloqueado" : "Libre")}");
            GUILayout.Label($"Orbiting: {isOrbiting}");
            GUILayout.Label($"H Angle: {horizontalAngle:F1}°");
            GUILayout.Label($"V Angle: {verticalAngle:F1}°");
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        
        private void OnDrawGizmos()
        {
            if (!showDebugGizmos || target == null) return;
            
            Vector3 focusPoint = Application.isPlaying ? currentFocusPoint : target.position + targetOffset;
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(focusPoint, 0.2f);
            
            Gizmos.color = Color.green;
            Gizmos.DrawLine(focusPoint, target.position);
            
            if (enableCollision)
            {
                Vector3 cameraDir = CalculateCameraDirection();
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(focusPoint + cameraDir * (Application.isPlaying ? collisionDistance : defaultDistance), 
                    collisionRadius);
            }
        }
        
        #endregion
    }
}
