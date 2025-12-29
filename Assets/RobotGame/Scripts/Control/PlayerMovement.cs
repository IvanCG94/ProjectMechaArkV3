using UnityEngine;

namespace RobotGame.Control
{
    /// <summary>
    /// Sistema de movimiento del jugador estilo Zelda BotW/TotK.
    /// 
    /// Características:
    /// - Movimiento relativo a la cámara
    /// - Rotación suave hacia dirección de movimiento
    /// - Aceleración y deceleración
    /// - Ground detection con raycasts
    /// - Pendientes (caminar/resbalar según ángulo)
    /// - Gravedad y caída
    /// - Salto
    /// - Sprint
    /// - Colisiones horizontales
    /// 
    /// INTEGRACIÓN:
    /// - Recibe el Transform a mover via SetTarget()
    /// - Se habilita/deshabilita via Enable()/Disable()
    /// - El sistema de Core llama a estos métodos cuando se inserta/extrae
    /// 
    /// FUTURO:
    /// - Modo combate: SetLookTarget(Transform) para mirar al enemigo mientras se mueve
    /// - Escalada: Sistema de gravedad dinámica para paredes/techos
    /// </summary>
    public class PlayerMovement : MonoBehaviour
    {
        #region Serialized Fields
        
        [Header("Target")]
        [SerializeField] private Transform target;
        
        [Header("Velocidad")]
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float sprintSpeed = 9f;
        [SerializeField] private float acceleration = 10f;
        [SerializeField] private float deceleration = 12f;
        
        [Header("Rotación")]
        [SerializeField] private float rotationSpeed = 720f;
        [SerializeField] private float rotationSmoothTime = 0.1f;
        
        [Header("Salto")]
        [SerializeField] private float jumpForce = 8f;
        [SerializeField] private float coyoteTime = 0.15f;
        [SerializeField] private float jumpBufferTime = 0.1f;
        
        [Header("Gravedad")]
        [SerializeField] private float gravity = 20f;
        [SerializeField] private float maxFallSpeed = 30f;
        
        [Header("Ground Detection")]
        [SerializeField] private float groundCheckDistance = 0.2f;
        [SerializeField] private float groundCheckRadius = 0.3f;
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField] private float groundOffset = 0.05f;
        
        [Header("Pendientes")]
        [SerializeField] private float maxWalkableAngle = 45f;
        [SerializeField] private float slideSpeed = 8f;
        [SerializeField] private float slideControl = 0.3f;
        
        [Header("Colisiones")]
        [SerializeField] private float collisionCheckDistance = 0.1f;
        [SerializeField] private float skinWidth = 0.02f;
        [SerializeField] private int maxCollisionIterations = 3;
        
        [Header("Estado")]
        [SerializeField] private bool isEnabled = true;
        [SerializeField] private bool isInEditMode = false;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = true;
        
        #endregion
        
        #region Private Fields
        
        // Referencia a la cámara
        private Transform cameraTransform;
        
        // Estado del movimiento
        private Vector3 velocity;
        private Vector3 horizontalVelocity;
        private float verticalVelocity;
        private float currentSpeed;
        
        // Estado del suelo
        private bool isGrounded;
        private bool wasGrounded;
        private Vector3 groundNormal = Vector3.up;
        private float groundAngle;
        private RaycastHit groundHit;
        
        // Timers
        private float timeSinceGrounded;
        private float timeSinceJumpPressed;
        
        // Rotación suave
        private float rotationVelocity;
        private float targetRotation;
        
        // Input
        private Vector2 inputDirection;
        private bool sprintInput;
        private bool jumpInput;
        private bool jumpInputDown;
        
        // Futuro: Modo combate
        private Transform lookTarget;
        private bool isInCombatMode;
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Evento disparado cuando el personaje salta.
        /// </summary>
        public event System.Action OnJump;
        
        /// <summary>
        /// Evento disparado cuando el personaje aterriza.
        /// </summary>
        public event System.Action OnLand;
        
        #endregion
        
        #region Properties
        
        /// <summary>
        /// Si el movimiento está habilitado.
        /// </summary>
        public bool IsEnabled => isEnabled && target != null;
        
        /// <summary>
        /// El Transform que se está moviendo.
        /// </summary>
        public Transform Target => target;
        
        /// <summary>
        /// Si el personaje está en el suelo.
        /// </summary>
        public bool IsGrounded => isGrounded;
        
        /// <summary>
        /// Velocidad actual del personaje.
        /// </summary>
        public Vector3 Velocity => velocity;
        
        /// <summary>
        /// Velocidad horizontal actual (magnitud).
        /// </summary>
        public float CurrentSpeed => currentSpeed;
        
        /// <summary>
        /// Si está corriendo.
        /// </summary>
        public bool IsSprinting => sprintInput && currentSpeed > walkSpeed * 0.9f;
        
        /// <summary>
        /// Normal del suelo actual.
        /// </summary>
        public Vector3 GroundNormal => groundNormal;
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Establece el objetivo a mover.
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            
            // Reset del estado cuando cambia el target
            velocity = Vector3.zero;
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            currentSpeed = 0f;
            isGrounded = false;
        }
        
        /// <summary>
        /// Habilita el movimiento (solo si no está en modo edición).
        /// </summary>
        public void Enable()
        {
            if (isInEditMode)
            {
                Debug.Log("PlayerMovement: No se puede habilitar en modo edición");
                return;
            }
            
            isEnabled = true;
            Debug.Log("PlayerMovement: Movimiento HABILITADO");
        }
        
        /// <summary>
        /// Deshabilita el movimiento.
        /// </summary>
        public void Disable()
        {
            isEnabled = false;
            
            // Detener movimiento al deshabilitar
            velocity = Vector3.zero;
            horizontalVelocity = Vector3.zero;
            currentSpeed = 0f;
            
            Debug.Log("PlayerMovement: Movimiento DESHABILITADO");
        }
        
        /// <summary>
        /// Entra en modo edición (bloquea el movimiento hasta salir).
        /// </summary>
        public void EnterEditMode()
        {
            isInEditMode = true;
            Disable();
            Debug.Log("PlayerMovement: Entrando a modo edición");
        }
        
        /// <summary>
        /// Sale del modo edición.
        /// </summary>
        public void ExitEditMode()
        {
            isInEditMode = false;
            Enable();
            Debug.Log("PlayerMovement: Saliendo de modo edición");
        }
        
        /// <summary>
        /// Si está en modo edición.
        /// </summary>
        public bool IsInEditMode => isInEditMode;
        
        /// <summary>
        /// [FUTURO] Establece un objetivo para mirar (modo combate).
        /// Cuando está activo, el personaje mira al objetivo en lugar de girar hacia el movimiento.
        /// </summary>
        public void SetLookTarget(Transform target)
        {
            lookTarget = target;
            isInCombatMode = target != null;
        }
        
        /// <summary>
        /// [FUTURO] Limpia el objetivo de mirada, volviendo al modo normal.
        /// </summary>
        public void ClearLookTarget()
        {
            lookTarget = null;
            isInCombatMode = false;
        }
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            // Inicializar layers si no están configurados
            if (groundLayers == 0)
            {
                groundLayers = ~0; // Todas las layers
            }
        }
        
        private void Start()
        {
            // Obtener referencia a la cámara
            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }
        
        private void Update()
        {
            if (!IsEnabled) return;
            
            // Actualizar referencia a cámara si es necesario
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
            
            GatherInput();
            UpdateTimers();
        }
        
        private void FixedUpdate()
        {
            if (!IsEnabled) return;
            
            CheckGround();
            HandleMovement();
            HandleRotation();
            HandleGravityAndJump();
            ApplyMovement();
        }
        
        #endregion
        
        #region Input
        
        private void GatherInput()
        {
            // Movimiento
            float horizontal = 0f;
            float vertical = 0f;
            
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) vertical = 1f;
            else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) vertical = -1f;
            
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) horizontal = 1f;
            else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) horizontal = -1f;
            
            inputDirection = new Vector2(horizontal, vertical).normalized;
            
            // Sprint
            sprintInput = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            
            // Salto
            jumpInputDown = Input.GetKeyDown(KeyCode.Space);
            jumpInput = Input.GetKey(KeyCode.Space);
            
            // Buffer de salto
            if (jumpInputDown)
            {
                timeSinceJumpPressed = 0f;
            }
        }
        
        #endregion
        
        #region Timers
        
        private void UpdateTimers()
        {
            // Coyote time
            if (isGrounded)
            {
                timeSinceGrounded = 0f;
            }
            else
            {
                timeSinceGrounded += Time.deltaTime;
            }
            
            // Jump buffer
            timeSinceJumpPressed += Time.deltaTime;
        }
        
        #endregion
        
        #region Ground Detection
        
        private void CheckGround()
        {
            wasGrounded = isGrounded;
            
            Vector3 origin = target.position + Vector3.up * (groundCheckRadius + groundOffset);
            
            // SphereCast hacia abajo para detectar el suelo
            if (Physics.SphereCast(origin, groundCheckRadius, Vector3.down, out groundHit, 
                groundCheckDistance + groundOffset, groundLayers, QueryTriggerInteraction.Ignore))
            {
                groundNormal = groundHit.normal;
                groundAngle = Vector3.Angle(Vector3.up, groundNormal);
                
                // Solo consideramos suelo si el ángulo es caminable
                // o si estamos cayendo (para aterrizar aunque sea una pendiente)
                isGrounded = groundAngle <= maxWalkableAngle || verticalVelocity <= 0;
                
                // Ajustar posición al suelo si estamos muy cerca
                if (isGrounded && verticalVelocity <= 0)
                {
                    float distanceToGround = groundHit.distance - groundOffset;
                    if (distanceToGround > skinWidth)
                    {
                        // Estamos flotando un poco, ajustar
                        target.position -= Vector3.up * (distanceToGround - skinWidth);
                    }
                }
            }
            else
            {
                isGrounded = false;
                groundNormal = Vector3.up;
                groundAngle = 0f;
            }
            
            // Evento de aterrizaje (para futuras animaciones)
            if (isGrounded && !wasGrounded)
            {
                OnLanded();
            }
        }
        
        private void OnLanded()
        {
            // Reset de velocidad vertical al aterrizar
            if (verticalVelocity < 0)
            {
                verticalVelocity = 0f;
            }
            
            // Notificar que aterrizó
            OnLand?.Invoke();
        }
        
        #endregion
        
        #region Movement
        
        private void HandleMovement()
        {
            if (cameraTransform == null) return;
            
            // Calcular dirección de movimiento relativa a la cámara
            Vector3 cameraForward = cameraTransform.forward;
            Vector3 cameraRight = cameraTransform.right;
            
            // Proyectar en el plano horizontal
            cameraForward.y = 0f;
            cameraRight.y = 0f;
            cameraForward.Normalize();
            cameraRight.Normalize();
            
            // Dirección deseada
            Vector3 moveDirection = (cameraForward * inputDirection.y + cameraRight * inputDirection.x);
            
            // Velocidad objetivo
            float targetSpeed = 0f;
            if (moveDirection.magnitude > 0.1f)
            {
                moveDirection.Normalize();
                targetSpeed = sprintInput ? sprintSpeed : walkSpeed;
                
                // Proyectar movimiento en la superficie si estamos en el suelo
                if (isGrounded)
                {
                    moveDirection = Vector3.ProjectOnPlane(moveDirection, groundNormal).normalized;
                }
            }
            
            // Manejar pendientes pronunciadas (resbalar)
            if (isGrounded && groundAngle > maxWalkableAngle)
            {
                HandleSliding(ref moveDirection, ref targetSpeed);
            }
            
            // Aplicar aceleración/deceleración
            float accel = targetSpeed > currentSpeed ? acceleration : deceleration;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accel * Time.fixedDeltaTime);
            
            // Calcular velocidad horizontal
            if (currentSpeed > 0.01f && moveDirection.magnitude > 0.1f)
            {
                horizontalVelocity = moveDirection * currentSpeed;
            }
            else
            {
                horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, Vector3.zero, 
                    deceleration * Time.fixedDeltaTime);
            }
        }
        
        private void HandleSliding(ref Vector3 moveDirection, ref float targetSpeed)
        {
            // Calcular dirección de deslizamiento (hacia abajo de la pendiente)
            Vector3 slideDirection = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;
            
            // Combinar con el input del jugador (control limitado)
            if (inputDirection.magnitude > 0.1f)
            {
                moveDirection = Vector3.Lerp(slideDirection, moveDirection, slideControl);
            }
            else
            {
                moveDirection = slideDirection;
            }
            
            targetSpeed = slideSpeed;
        }
        
        #endregion
        
        #region Rotation
        
        private void HandleRotation()
        {
            // En modo combate, mirar al objetivo
            if (isInCombatMode && lookTarget != null)
            {
                HandleCombatRotation();
                return;
            }
            
            // Modo normal: girar hacia la dirección de movimiento
            if (horizontalVelocity.magnitude > 0.1f)
            {
                // Calcular rotación objetivo
                Vector3 flatVelocity = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z);
                if (flatVelocity.magnitude > 0.1f)
                {
                    targetRotation = Mathf.Atan2(flatVelocity.x, flatVelocity.z) * Mathf.Rad2Deg;
                    
                    // Rotación suave
                    float currentYRotation = target.eulerAngles.y;
                    float newRotation = Mathf.SmoothDampAngle(currentYRotation, targetRotation, 
                        ref rotationVelocity, rotationSmoothTime, rotationSpeed);
                    
                    target.rotation = Quaternion.Euler(0f, newRotation, 0f);
                }
            }
        }
        
        private void HandleCombatRotation()
        {
            // [FUTURO] Rotación hacia el enemigo
            Vector3 directionToTarget = lookTarget.position - target.position;
            directionToTarget.y = 0f;
            
            if (directionToTarget.magnitude > 0.1f)
            {
                Quaternion targetRot = Quaternion.LookRotation(directionToTarget);
                target.rotation = Quaternion.RotateTowards(target.rotation, targetRot, 
                    rotationSpeed * Time.fixedDeltaTime);
            }
        }
        
        #endregion
        
        #region Gravity and Jump
        
        private void HandleGravityAndJump()
        {
            // Salto con coyote time y buffer
            bool canJump = isGrounded || timeSinceGrounded < coyoteTime;
            bool wantsToJump = timeSinceJumpPressed < jumpBufferTime;
            
            if (canJump && wantsToJump)
            {
                Jump();
            }
            
            // Aplicar gravedad
            if (!isGrounded)
            {
                verticalVelocity -= gravity * Time.fixedDeltaTime;
                verticalVelocity = Mathf.Max(verticalVelocity, -maxFallSpeed);
            }
            else if (verticalVelocity < 0)
            {
                // Pequeña velocidad negativa para mantener pegado al suelo
                verticalVelocity = -2f;
            }
        }
        
        private void Jump()
        {
            verticalVelocity = jumpForce;
            isGrounded = false;
            timeSinceGrounded = coyoteTime; // Consumir coyote time
            timeSinceJumpPressed = jumpBufferTime; // Consumir buffer
            
            // Notificar que saltó
            OnJump?.Invoke();
        }
        
        #endregion
        
        #region Apply Movement
        
        private void ApplyMovement()
        {
            // Combinar velocidades
            velocity = horizontalVelocity + Vector3.up * verticalVelocity;
            
            // Calcular desplazamiento
            Vector3 displacement = velocity * Time.fixedDeltaTime;
            
            // Resolver colisiones horizontales
            displacement = ResolveCollisions(displacement);
            
            // Aplicar movimiento
            target.position += displacement;
        }
        
        private Vector3 ResolveCollisions(Vector3 displacement)
        {
            if (displacement.magnitude < 0.001f) return displacement;
            
            Vector3 origin = target.position + Vector3.up * (groundCheckRadius + 0.1f);
            Vector3 horizontalDisplacement = new Vector3(displacement.x, 0f, displacement.z);
            
            for (int i = 0; i < maxCollisionIterations; i++)
            {
                if (horizontalDisplacement.magnitude < skinWidth) break;
                
                // Raycast en la dirección del movimiento
                if (Physics.SphereCast(origin, groundCheckRadius * 0.9f, horizontalDisplacement.normalized, 
                    out RaycastHit hit, horizontalDisplacement.magnitude + skinWidth, 
                    groundLayers, QueryTriggerInteraction.Ignore))
                {
                    // Verificar que no sea el suelo
                    float hitAngle = Vector3.Angle(Vector3.up, hit.normal);
                    if (hitAngle > maxWalkableAngle)
                    {
                        // Es una pared, deslizar a lo largo de ella
                        float distanceToWall = hit.distance - skinWidth;
                        if (distanceToWall > 0)
                        {
                            // Moverse hasta la pared
                            Vector3 moveToWall = horizontalDisplacement.normalized * distanceToWall;
                            
                            // Calcular el resto del movimiento proyectado en la pared
                            Vector3 remaining = horizontalDisplacement - moveToWall;
                            Vector3 slideDirection = Vector3.ProjectOnPlane(remaining, hit.normal);
                            
                            horizontalDisplacement = moveToWall + slideDirection;
                        }
                        else
                        {
                            // Ya estamos en la pared, solo deslizar
                            horizontalDisplacement = Vector3.ProjectOnPlane(horizontalDisplacement, hit.normal);
                        }
                    }
                }
                else
                {
                    // No hay colisión
                    break;
                }
            }
            
            return new Vector3(horizontalDisplacement.x, displacement.y, horizontalDisplacement.z);
        }
        
        #endregion
        
        #region Debug
        
        private void OnDrawGizmos()
        {
            if (!showDebugGizmos || target == null) return;
            
            Vector3 origin = target.position + Vector3.up * (groundCheckRadius + groundOffset);
            
            // Ground check sphere
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(origin, groundCheckRadius);
            Gizmos.DrawLine(origin, origin + Vector3.down * (groundCheckDistance + groundOffset));
            
            // Ground normal
            if (isGrounded)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(target.position, target.position + groundNormal);
            }
            
            // Velocity
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(target.position + Vector3.up, 
                target.position + Vector3.up + horizontalVelocity.normalized * 2f);
            
            // Collision check
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(origin, groundCheckRadius * 0.9f);
        }
        
        #endregion
    }
}
