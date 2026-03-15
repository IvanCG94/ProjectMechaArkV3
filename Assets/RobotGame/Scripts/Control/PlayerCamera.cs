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
    /// MODO EDICIÓN (ensamblaje):
    /// - Cursor libre (visible) para seleccionar partes
    /// - WASD mueve el punto focal en el plano horizontal (Shift para rápido)
    /// - Q/E o Ctrl/Space sube/baja el punto focal
    /// - Click medio + arrastrar para orbitar alrededor del punto focal
    /// - Rueda del mouse para zoom
    /// - Click derecho reservado para remover piezas
    /// - El punto focal no puede bajar del suelo
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
        [SerializeField] private float zoomSpeed = 3f;
        [SerializeField] private float zoomSmoothTime = 0.15f;
        
        [Header("Órbita")]
        [SerializeField] private float orbitSensitivity = 2f;
        [SerializeField] private float minVerticalAngle = -20f;
        [SerializeField] private float maxVerticalAngle = 70f;
        
        [Header("Control de Mouse")]
        [SerializeField] private KeyCode freeCursorKey = KeyCode.LeftAlt;
        
        [Header("Auto-Recentrado")]
        [SerializeField] private bool enableAutoRecenter = true;
        [SerializeField] private float autoRecenterDelay = 3f;
        [SerializeField] private float autoRecenterSpeed = 0.5f;
        [SerializeField] private float autoRecenterMinSpeed = 0.5f;
        
        [Header("Suavizado")]
        [SerializeField] private float followSmoothTime = 0.1f;
        
        [Header("Colisión")]
        [SerializeField] private bool enableCollision = true;
        [SerializeField] private float collisionRadius = 0.2f;
        [SerializeField] private LayerMask collisionLayers = ~0;
        [SerializeField] private float collisionPullInSpeed = 10f;
        [SerializeField] private float collisionPushOutSpeed = 2f;
        
        [Header("Estado")]
        [SerializeField] private bool isInEditMode = false;
        
        [Header("Modo Edición - Cámara Libre")]
        [SerializeField] private float editMoveSpeed = 10f;
        [SerializeField] private float editFastMultiplier = 2f;
        [SerializeField] private float editMinHeight = 0.5f;
        [SerializeField] private float editOrbitSensitivity = 3f;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugUI = false;
        
        #endregion
        
        #region Private Fields
        
        // Ángulos
        private float horizontalAngle;
        private float verticalAngle = 30f;
        
        // Distancia
        private float currentDistance;
        private float targetDistance;
        private float distanceVelocity;
        
        // Posición del punto de enfoque
        private Vector3 currentFocusPoint;
        private Vector3 focusVelocity;
        
        // Colisión
        private float currentCollisionDistance;
        
        // Auto-recentrado
        private float timeSinceLastInput;
        
        // Modo edición - punto de enfoque libre
        private Vector3 editFocusPoint;
        private bool editModeInitialized = false;
        
        // Referencias
        private PlayerController playerController;
        
        #endregion
        
        #region Properties
        
        public Transform Target => target;
        public bool IsInEditMode => isInEditMode;
        
        #endregion
        
        #region Public Methods
        
        public void SetTarget(Transform newTarget, bool instant = false)
        {
            target = newTarget;
            
            if (target != null)
            {
                playerController = FindObjectOfType<PlayerController>();
                
                if (instant)
                {
                    InitializeCamera();
                }
            }
        }
        
        public void EnterEditMode()
        {
            isInEditMode = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            // Guardar el punto de enfoque actual para el modo libre
            editFocusPoint = currentFocusPoint;
            editModeInitialized = true;
        }
        
        public void ExitEditMode()
        {
            isInEditMode = false;
            editModeInitialized = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        
        public void SetZoom(float distance)
        {
            targetDistance = Mathf.Clamp(distance, minDistance, maxDistance);
        }
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Start()
        {
            currentDistance = defaultDistance;
            targetDistance = defaultDistance;
            currentCollisionDistance = defaultDistance;
            
            if (target != null)
            {
                InitializeCamera();
                playerController = FindObjectOfType<PlayerController>();
            }
            
            if (!isInEditMode)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        
        private void LateUpdate()
        {
            if (target == null) return;
            
            HandleInput();
            UpdateFocusPoint();
            UpdateDistance();
            UpdateCollision();
            ApplyCameraTransform();
        }
        
        #endregion
        
        #region Initialization
        
        private void InitializeCamera()
        {
            currentFocusPoint = target.position + targetOffset;
            horizontalAngle = target.eulerAngles.y;
            currentCollisionDistance = defaultDistance;
        }
        
        #endregion
        
        #region Input
        
        private void HandleInput()
        {
            // Zoom
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                targetDistance -= scroll * zoomSpeed;
                targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
            }
            
            // Órbita según modo
            if (isInEditMode)
            {
                HandleEditModeInput();
            }
            else
            {
                HandleNormalModeInput();
            }
            
            // Auto-recentrado
            if (enableAutoRecenter && !isInEditMode)
            {
                HandleAutoRecenter();
            }
        }
        
        private void HandleNormalModeInput()
        {
            // Alt libera cursor temporalmente
            if (Input.GetKeyDown(freeCursorKey))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (Input.GetKeyUp(freeCursorKey))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            
            // Si Alt está presionado, no mover cámara
            if (Input.GetKey(freeCursorKey))
                return;
            
            // Mouse mueve cámara
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");
            
            if (Mathf.Abs(mouseX) > 0.001f || Mathf.Abs(mouseY) > 0.001f)
            {
                horizontalAngle -= mouseX * orbitSensitivity;
                verticalAngle -= mouseY * orbitSensitivity;
                verticalAngle = Mathf.Clamp(verticalAngle, minVerticalAngle, maxVerticalAngle);
                
                timeSinceLastInput = 0f;
            }
            else
            {
                timeSinceLastInput += Time.deltaTime;
            }
        }
        
        private void HandleEditModeInput()
        {
            // === ÓRBITA: Click medio + arrastrar para orbitar alrededor del punto focal ===
            if (Input.GetMouseButton(2))
            {
                float mouseX = Input.GetAxis("Mouse X");
                float mouseY = Input.GetAxis("Mouse Y");
                
                horizontalAngle -= mouseX * editOrbitSensitivity;
                verticalAngle -= mouseY * editOrbitSensitivity;
                verticalAngle = Mathf.Clamp(verticalAngle, minVerticalAngle, maxVerticalAngle);
                
                timeSinceLastInput = 0f;
            }
            
            // === MOVIMIENTO HORIZONTAL: WASD mueve el punto focal en el plano XZ ===
            float moveH = 0f;
            float moveV = 0f;
            float moveY = 0f;
            
            if (Input.GetKey(KeyCode.W)) moveV = 1f;
            if (Input.GetKey(KeyCode.S)) moveV = -1f;
            if (Input.GetKey(KeyCode.A)) moveH = -1f;
            if (Input.GetKey(KeyCode.D)) moveH = 1f;
            
            // === MOVIMIENTO VERTICAL: Q/E o Space/Ctrl sube/baja el punto focal ===
            if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space)) moveY = 1f;
            if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftControl)) moveY = -1f;
            
            if (Mathf.Abs(moveH) > 0.01f || Mathf.Abs(moveV) > 0.01f || Mathf.Abs(moveY) > 0.01f)
            {
                // Velocidad con Shift
                float speed = editMoveSpeed;
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    speed *= editFastMultiplier;
                }
                
                // Direcciones horizontales basadas en la orientación de la cámara (sin componente Y)
                Vector3 forward = transform.forward;
                forward.y = 0f;
                forward.Normalize();
                
                Vector3 right = transform.right;
                right.y = 0f;
                right.Normalize();
                
                // Mover el punto focal
                Vector3 movement = (forward * moveV + right * moveH + Vector3.up * moveY) * speed * Time.deltaTime;
                editFocusPoint += movement;
                
                // Colisión con suelo: el punto focal no puede bajar de la altura mínima
                if (editFocusPoint.y < editMinHeight)
                {
                    editFocusPoint.y = editMinHeight;
                }
                
                timeSinceLastInput = 0f;
            }
        }
        
        private void HandleAutoRecenter()
        {
            // No recentrar si el jugador no se mueve o si el usuario movió la cámara recientemente
            if (timeSinceLastInput < autoRecenterDelay)
                return;
            
            if (playerController == null || playerController.CurrentSpeed < autoRecenterMinSpeed)
                return;
            
            // Recentrar suavemente hacia donde mira el personaje
            float targetAngle = target.eulerAngles.y;
            float delta = Mathf.DeltaAngle(horizontalAngle, targetAngle);
            
            // Solo recentrar si la diferencia es significativa
            if (Mathf.Abs(delta) > 5f)
            {
                horizontalAngle += delta * autoRecenterSpeed * Time.deltaTime;
            }
        }
        
        #endregion
        
        #region Focus Point
        
        private void UpdateFocusPoint()
        {
            if (isInEditMode && editModeInitialized)
            {
                // En modo edición, usar el punto de enfoque libre (sin seguir al target)
                currentFocusPoint = Vector3.SmoothDamp(currentFocusPoint, editFocusPoint, ref focusVelocity, followSmoothTime);
            }
            else
            {
                // En modo normal, seguir al target
                Vector3 targetPoint = target.position + targetOffset;
                currentFocusPoint = Vector3.SmoothDamp(currentFocusPoint, targetPoint, ref focusVelocity, followSmoothTime);
            }
        }
        
        #endregion
        
        #region Distance
        
        private void UpdateDistance()
        {
            currentDistance = Mathf.SmoothDamp(currentDistance, targetDistance, ref distanceVelocity, zoomSmoothTime);
        }
        
        #endregion
        
        #region Collision
        
        private void UpdateCollision()
        {
            if (!enableCollision)
            {
                currentCollisionDistance = currentDistance;
                return;
            }
            
            Vector3 direction = GetCameraDirection();
            float desiredDistance = currentDistance;
            
            // Raycast desde el punto de enfoque hacia atrás
            if (Physics.SphereCast(currentFocusPoint, collisionRadius, direction, out RaycastHit hit, 
                currentDistance, collisionLayers, QueryTriggerInteraction.Ignore))
            {
                // Hay obstáculo - acercarse
                desiredDistance = Mathf.Max(hit.distance - collisionRadius, minDistance * 0.5f);
            }
            
            // Suavizado diferente para acercarse vs alejarse
            if (desiredDistance < currentCollisionDistance)
            {
                // Acercarse rápido (evitar atravesar paredes)
                currentCollisionDistance = Mathf.MoveTowards(currentCollisionDistance, desiredDistance, 
                    collisionPullInSpeed * Time.deltaTime);
            }
            else
            {
                // Alejarse lento (evitar rebotes)
                currentCollisionDistance = Mathf.MoveTowards(currentCollisionDistance, desiredDistance, 
                    collisionPushOutSpeed * Time.deltaTime);
            }
        }
        
        #endregion
        
        #region Camera Transform
        
        private Vector3 GetCameraDirection()
        {
            float hRad = horizontalAngle * Mathf.Deg2Rad;
            float vRad = verticalAngle * Mathf.Deg2Rad;
            
            // Dirección desde el personaje hacia la cámara
            return new Vector3(
                -Mathf.Sin(hRad) * Mathf.Cos(vRad),
                Mathf.Sin(vRad),
                Mathf.Cos(hRad) * Mathf.Cos(vRad)
            );
        }
        
        private void ApplyCameraTransform()
        {
            Vector3 direction = GetCameraDirection();
            float effectiveDistance = Mathf.Min(currentDistance, currentCollisionDistance);
            
            // Posición
            Vector3 newPosition = currentFocusPoint + direction * effectiveDistance;
            transform.position = newPosition;
            
            // Rotación - mirar al punto de enfoque
            transform.LookAt(currentFocusPoint);
        }
        
        #endregion
        
        #region Debug
        
        private void OnGUI()
        {
            if (!showDebugUI) return;
            
            GUILayout.BeginArea(new Rect(Screen.width - 220, 10, 210, 140));
            GUILayout.BeginVertical("box");
            
            GUILayout.Label("=== Camera ===");
            GUILayout.Label($"Mode: {(isInEditMode ? "EDIT" : "NORMAL")}");
            GUILayout.Label($"H: {horizontalAngle:F1}° V: {verticalAngle:F1}°");
            GUILayout.Label($"Distance: {currentDistance:F1}");
            GUILayout.Label($"Collision: {currentCollisionDistance:F1}");
            GUILayout.Label($"Idle: {timeSinceLastInput:F1}s");
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        
        #endregion
    }
}
