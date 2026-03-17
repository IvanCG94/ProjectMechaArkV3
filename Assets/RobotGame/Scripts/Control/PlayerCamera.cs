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
    /// - Zoom con rueda del mouse
    /// - La distancia se ajusta automáticamente al tamaño del robot
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
        
        [Header("Distancia Base (se escala con tamaño del robot)")]
        [Tooltip("Distancia por defecto para un robot de tamaño 1")]
        [SerializeField] private float baseDefaultDistance = 5f;
        [Tooltip("Distancia mínima para un robot de tamaño 1")]
        [SerializeField] private float baseMinDistance = 2f;
        [Tooltip("Distancia máxima para un robot de tamaño 1")]
        [SerializeField] private float baseMaxDistance = 12f;
        [Tooltip("Factor de escala de distancia (mayor = más lejos para robots grandes)")]
        [SerializeField] private float distanceScaleFactor = 1.5f;
        
        [Header("Zoom")]
        [SerializeField] private float zoomSpeed = 3f;
        [SerializeField] private float zoomSmoothTime = 0.1f;
        
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
        [SerializeField] private float collisionRadius = 0.3f;
        [SerializeField] private LayerMask collisionLayers = ~0;
        [Tooltip("Qué tan rápido la cámara se acerca al detectar colisión")]
        [SerializeField] private float collisionPullInSpeed = 15f;
        [Tooltip("Qué tan rápido la cámara vuelve a su distancia normal después de colisión")]
        [SerializeField] private float collisionRecoverSpeed = 8f;
        
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
        
        // Distancia - calculada según tamaño del robot
        private float currentMinDistance;
        private float currentMaxDistance;
        private float currentDefaultDistance;
        
        // Distancia actual y objetivo
        private float currentDistance;
        private float targetDistance;
        private float distanceVelocity;
        
        // Distancia efectiva (después de colisión)
        private float effectiveDistance;
        
        // Tamaño del robot
        private float robotSize = 1f;
        
        // Posición del punto de enfoque
        private Vector3 currentFocusPoint;
        private Vector3 focusVelocity;
        
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
                
                // Calcular tamaño del robot y ajustar distancias
                CalculateRobotSize();
                
                if (instant)
                {
                    InitializeCamera();
                }
            }
        }
        
        /// <summary>
        /// Recalcula el tamaño del robot y ajusta las distancias de cámara.
        /// Llamar cuando el robot cambia de tamaño (ej: al agregar/quitar partes).
        /// </summary>
        public void RefreshRobotSize()
        {
            if (target != null)
            {
                float oldDefault = currentDefaultDistance;
                CalculateRobotSize();
                
                // Si la distancia actual estaba en el default, actualizarla
                if (Mathf.Approximately(targetDistance, oldDefault))
                {
                    targetDistance = currentDefaultDistance;
                }
                
                // Clamp al nuevo rango
                targetDistance = Mathf.Clamp(targetDistance, currentMinDistance, currentMaxDistance);
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
            targetDistance = Mathf.Clamp(distance, currentMinDistance, currentMaxDistance);
        }
        
        /// <summary>
        /// Resetea el zoom a la distancia por defecto
        /// </summary>
        public void ResetZoom()
        {
            targetDistance = currentDefaultDistance;
        }
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Start()
        {
            if (target != null)
            {
                playerController = FindObjectOfType<PlayerController>();
                CalculateRobotSize();
                InitializeCamera();
            }
            else
            {
                // Valores por defecto si no hay target
                currentMinDistance = baseMinDistance;
                currentMaxDistance = baseMaxDistance;
                currentDefaultDistance = baseDefaultDistance;
                currentDistance = baseDefaultDistance;
                targetDistance = baseDefaultDistance;
                effectiveDistance = baseDefaultDistance;
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
        
        #region Robot Size Calculation
        
        /// <summary>
        /// Calcula el tamaño del robot basándose en los bounds de sus renderers
        /// </summary>
        private void CalculateRobotSize()
        {
            if (target == null)
            {
                robotSize = 1f;
                ApplyDistanceScale();
                return;
            }
            
            // Intentar obtener bounds del robot
            Bounds robotBounds = new Bounds(target.position, Vector3.zero);
            bool hasBounds = false;
            
            // Primero intentar con Renderers
            var renderers = target.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                // Ignorar renderers de UI, partículas, etc.
                if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                {
                    if (!hasBounds)
                    {
                        robotBounds = renderer.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        robotBounds.Encapsulate(renderer.bounds);
                    }
                }
            }
            
            if (hasBounds)
            {
                // Usar el tamaño más grande (usualmente altura o largo)
                float maxDimension = Mathf.Max(robotBounds.size.x, robotBounds.size.y, robotBounds.size.z);
                
                // Normalizar: un robot de ~2 unidades de alto es "tamaño 1"
                robotSize = Mathf.Max(0.5f, maxDimension / 2f);
                
                // Ajustar el offset del target basado en la altura
                targetOffset = new Vector3(0f, robotBounds.size.y * 0.5f, 0f);
            }
            else
            {
                robotSize = 1f;
            }
            
            ApplyDistanceScale();
        }
        
        /// <summary>
        /// Aplica la escala de distancia basada en el tamaño del robot
        /// </summary>
        private void ApplyDistanceScale()
        {
            float scale = 1f + (robotSize - 1f) * distanceScaleFactor;
            scale = Mathf.Max(0.5f, scale); // Mínimo 0.5x
            
            currentMinDistance = baseMinDistance * scale;
            currentMaxDistance = baseMaxDistance * scale;
            currentDefaultDistance = baseDefaultDistance * scale;
        }
        
        #endregion
        
        #region Initialization
        
        private void InitializeCamera()
        {
            currentFocusPoint = target.position + targetOffset;
            horizontalAngle = target.eulerAngles.y;
            
            currentDistance = currentDefaultDistance;
            targetDistance = currentDefaultDistance;
            effectiveDistance = currentDefaultDistance;
        }
        
        #endregion
        
        #region Input
        
        private void HandleInput()
        {
            // Zoom con rueda del mouse
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                targetDistance -= scroll * zoomSpeed * robotSize; // Escalar zoom con tamaño
                targetDistance = Mathf.Clamp(targetDistance, currentMinDistance, currentMaxDistance);
                timeSinceLastInput = 0f;
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
            // Liberar cursor con Alt
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
            
            // Mouse mueve cámara (órbita)
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");
            
            if (Mathf.Abs(mouseX) > 0.001f || Mathf.Abs(mouseY) > 0.001f)
            {
                horizontalAngle += mouseX * orbitSensitivity;
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
                
                horizontalAngle += mouseX * editOrbitSensitivity;
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
            // Interpolar suavemente hacia la distancia objetivo
            currentDistance = Mathf.SmoothDamp(currentDistance, targetDistance, ref distanceVelocity, zoomSmoothTime);
        }
        
        #endregion
        
        #region Collision
        
        private void UpdateCollision()
        {
            // La distancia efectiva siempre intenta ser igual a currentDistance
            // Solo se reduce temporalmente si hay colisión
            
            if (!enableCollision)
            {
                effectiveDistance = currentDistance;
                return;
            }
            
            Vector3 direction = GetCameraDirection();
            float desiredEffectiveDistance = currentDistance;
            
            // Usar RaycastAll para poder filtrar colisiones con el propio robot
            RaycastHit[] hits = Physics.SphereCastAll(currentFocusPoint, collisionRadius, direction, 
                currentDistance, collisionLayers, QueryTriggerInteraction.Ignore);
            
            // Buscar el hit más cercano que NO sea parte del robot del jugador
            float closestValidHit = currentDistance;
            bool foundValidHit = false;
            
            foreach (var hit in hits)
            {
                // Ignorar colliders que son hijos del target (el robot del jugador)
                if (target != null && hit.collider.transform.IsChildOf(target))
                {
                    continue;
                }
                
                if (hit.distance < closestValidHit)
                {
                    closestValidHit = hit.distance;
                    foundValidHit = true;
                }
            }
            
            if (foundValidHit)
            {
                // Hay obstáculo válido - calcular distancia segura
                float safeDistance = Mathf.Max(closestValidHit - collisionRadius * 0.5f, currentMinDistance * 0.3f);
                desiredEffectiveDistance = Mathf.Min(currentDistance, safeDistance);
            }
            
            // Interpolar hacia la distancia efectiva deseada
            if (desiredEffectiveDistance < effectiveDistance)
            {
                // Acercarse rápido (evitar atravesar paredes)
                effectiveDistance = Mathf.MoveTowards(effectiveDistance, desiredEffectiveDistance, 
                    collisionPullInSpeed * Time.deltaTime);
            }
            else
            {
                // Volver a la distancia normal (después de que la colisión terminó)
                effectiveDistance = Mathf.MoveTowards(effectiveDistance, desiredEffectiveDistance, 
                    collisionRecoverSpeed * Time.deltaTime);
            }
        }
        
        #endregion
        
        #region Camera Transform
        
        private Vector3 GetCameraDirection()
        {
            float hRad = horizontalAngle * Mathf.Deg2Rad;
            float vRad = verticalAngle * Mathf.Deg2Rad;
            
            // Dirección desde el personaje hacia la cámara (hacia atrás y arriba)
            return new Vector3(
                -Mathf.Sin(hRad) * Mathf.Cos(vRad),
                Mathf.Sin(vRad),
                -Mathf.Cos(hRad) * Mathf.Cos(vRad)
            );
        }
        
        private void ApplyCameraTransform()
        {
            Vector3 direction = GetCameraDirection();
            
            // Usar la distancia efectiva (que considera colisiones)
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
            
            GUILayout.BeginArea(new Rect(Screen.width - 250, 10, 240, 200));
            GUILayout.BeginVertical("box");
            
            GUILayout.Label("=== Camera ===");
            GUILayout.Label($"Mode: {(isInEditMode ? "EDIT" : "NORMAL")}");
            GUILayout.Label($"Robot Size: {robotSize:F2}");
            GUILayout.Label($"H: {horizontalAngle:F1}° V: {verticalAngle:F1}°");
            GUILayout.Label($"Target Dist: {targetDistance:F1}");
            GUILayout.Label($"Current Dist: {currentDistance:F1}");
            GUILayout.Label($"Effective Dist: {effectiveDistance:F1}");
            GUILayout.Label($"Range: [{currentMinDistance:F1} - {currentMaxDistance:F1}]");
            GUILayout.Label($"Idle: {timeSinceLastInput:F1}s");
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        
        private void OnDrawGizmosSelected()
        {
            if (target == null) return;
            
            // Dibujar el rango de distancia
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(currentFocusPoint, currentMinDistance);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(currentFocusPoint, currentMaxDistance);
        }
        
        #endregion
    }
}
