using UnityEngine;

namespace RobotGame.Control
{
    /// <summary>
    /// Sistema de movimiento del jugador estilo Zelda BotW/TotK.
    /// Usa Rigidbody Kinematic para mejor integración con el sistema de física de Unity.
    /// 
    /// Características:
    /// - Movimiento relativo a la cámara
    /// - Rotación suave hacia dirección de movimiento
    /// - Aceleración y deceleración
    /// - Ground detection con raycasts
    /// - Pendientes (caminar/resbalar según ángulo)
    /// - Gravedad y caída
    /// - Salto con coyote time y jump buffer
    /// - Sprint
    /// - Colisiones horizontales
    /// 
    /// SETUP REQUERIDO:
    /// - El robot necesita un Rigidbody (Is Kinematic = true)
    /// - El robot necesita un Collider (CapsuleCollider recomendado)
    /// - El script auto-configura estos componentes si no existen
    /// 
    /// INTEGRACIÓN:
    /// - Recibe el Transform a mover via SetTarget()
    /// - Se habilita/deshabilita via Enable()/Disable()
    /// - Funciona con triggers (OnTriggerEnter, etc.)
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
        
        [Header("Collider")]
        [SerializeField] private float colliderHeight = 2f;
        [SerializeField] private float colliderRadius = 0.4f;
        [SerializeField] private Vector3 colliderCenter = new Vector3(0f, 1f, 0f);
        
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
        [SerializeField] private float skinWidth = 0.02f;
        [SerializeField] private int maxCollisionIterations = 3;
        
        [Header("Estado")]
        [SerializeField] private bool isEnabled = true;
        [SerializeField] private bool isInEditMode = false;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = true;
        
        #endregion
        
        #region Private Fields
        
        // Componentes
        private Rigidbody rb;
        private CapsuleCollider capsuleCollider;
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
        /// Rigidbody del robot (para referencia externa).
        /// </summary>
        public Rigidbody Rigidbody => rb;
        
        /// <summary>
        /// Collider del robot (para referencia externa).
        /// </summary>
        public CapsuleCollider Collider => capsuleCollider;
        
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
        
        /// <summary>
        /// Si está en modo edición.
        /// </summary>
        public bool IsInEditMode => isInEditMode;
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Establece el objetivo a mover.
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            
            if (target != null)
            {
                SetupRigidbodyAndCollider();
            }
            
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
        /// Fuerza un ground check inmediato y resetea velocidades.
        /// Útil después de teletransportar o reactivar el jugador.
        /// </summary>
        public void ForceGroundCheck()
        {
            // Resetear velocidades
            verticalVelocity = 0f;
            horizontalVelocity = Vector3.zero;
            velocity = Vector3.zero;
            
            // Forzar ground check
            CheckGround();
            
            // Si está en el suelo, asegurar que verticalVelocity sea 0
            if (isGrounded)
            {
                verticalVelocity = 0f;
            }
            
            Debug.Log($"PlayerMovement: ForceGroundCheck - IsGrounded: {isGrounded}, Position: {target?.position}");
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
            
            // Desactivar collider para que los raycasts puedan alcanzar las grillas
            if (capsuleCollider != null)
            {
                capsuleCollider.enabled = false;
            }
            
            Debug.Log("PlayerMovement: Entrando a modo edición (collider desactivado)");
        }
        
        /// <summary>
        /// Sale del modo edición.
        /// </summary>
        public void ExitEditMode()
        {
            isInEditMode = false;
            
            // Reactivar collider
            if (capsuleCollider != null)
            {
                capsuleCollider.enabled = true;
            }
            
            Enable();
            Debug.Log("PlayerMovement: Saliendo de modo edición (collider activado)");
        }
        
        /// <summary>
        /// [FUTURO] Establece un objetivo para mirar (modo combate).
        /// </summary>
        public void SetLookTarget(Transform target)
        {
            lookTarget = target;
            isInCombatMode = target != null;
        }
        
        /// <summary>
        /// [FUTURO] Limpia el objetivo de mirada.
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
            if (groundLayers == 0)
            {
                groundLayers = ~0;
            }
        }
        
        private void Start()
        {
            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
            
            if (target != null)
            {
                SetupRigidbodyAndCollider();
            }
        }
        
        private void Update()
        {
            if (!IsEnabled) return;
            
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
            if (rb == null) return;
            
            CheckGround();
            HandleMovement();
            HandleRotation();
            HandleGravityAndJump();
            ApplyMovement();
        }
        
        #endregion
        
        #region Setup
        
        /// <summary>
        /// Configura el Rigidbody y Collider en el robot.
        /// </summary>
        private void SetupRigidbodyAndCollider()
        {
            if (target == null) return;
            
            // Buscar o crear Rigidbody
            rb = target.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = target.gameObject.AddComponent<Rigidbody>();
                Debug.Log("PlayerMovement: Rigidbody creado automáticamente");
            }
            
            // Configurar Rigidbody como Kinematic
            rb.isKinematic = true;
            rb.useGravity = false; // Usamos nuestra propia gravedad
            rb.interpolation = RigidbodyInterpolation.Interpolate; // Suaviza el movimiento
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            
            // Buscar o crear CapsuleCollider
            capsuleCollider = target.GetComponent<CapsuleCollider>();
            if (capsuleCollider == null)
            {
                capsuleCollider = target.gameObject.AddComponent<CapsuleCollider>();
                Debug.Log("PlayerMovement: CapsuleCollider creado automáticamente");
            }
            
            // Configurar Collider
            capsuleCollider.height = colliderHeight;
            capsuleCollider.radius = colliderRadius;
            capsuleCollider.center = colliderCenter;
            
            Debug.Log($"PlayerMovement: Rigidbody y Collider configurados en {target.name}");
        }
        
        #endregion
        
        #region Input
        
        private void GatherInput()
        {
            float horizontal = 0f;
            float vertical = 0f;
            
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) vertical = 1f;
            else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) vertical = -1f;
            
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) horizontal = 1f;
            else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) horizontal = -1f;
            
            inputDirection = new Vector2(horizontal, vertical).normalized;
            
            sprintInput = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            
            jumpInputDown = Input.GetKeyDown(KeyCode.Space);
            jumpInput = Input.GetKey(KeyCode.Space);
            
            if (jumpInputDown)
            {
                timeSinceJumpPressed = 0f;
            }
        }
        
        #endregion
        
        #region Timers
        
        private void UpdateTimers()
        {
            if (isGrounded)
            {
                timeSinceGrounded = 0f;
            }
            else
            {
                timeSinceGrounded += Time.deltaTime;
            }
            
            timeSinceJumpPressed += Time.deltaTime;
        }
        
        #endregion
        
        #region Ground Detection
        
        private void CheckGround()
        {
            wasGrounded = isGrounded;
            
            Vector3 origin = target.position + Vector3.up * (groundCheckRadius + groundOffset);
            
            if (Physics.SphereCast(origin, groundCheckRadius, Vector3.down, out groundHit, 
                groundCheckDistance + groundOffset, groundLayers, QueryTriggerInteraction.Ignore))
            {
                groundNormal = groundHit.normal;
                groundAngle = Vector3.Angle(Vector3.up, groundNormal);
                
                isGrounded = groundAngle <= maxWalkableAngle || verticalVelocity <= 0;
                
                // Ajustar posición al suelo
                if (isGrounded && verticalVelocity <= 0)
                {
                    float distanceToGround = groundHit.distance - groundOffset;
                    if (distanceToGround > skinWidth)
                    {
                        Vector3 newPos = target.position - Vector3.up * (distanceToGround - skinWidth);
                        rb.MovePosition(newPos);
                    }
                }
            }
            else
            {
                isGrounded = false;
                groundNormal = Vector3.up;
                groundAngle = 0f;
            }
            
            if (isGrounded && !wasGrounded)
            {
                OnLanded();
            }
        }
        
        private void OnLanded()
        {
            if (verticalVelocity < 0)
            {
                verticalVelocity = 0f;
            }
            
            OnLand?.Invoke();
        }
        
        #endregion
        
        #region Movement
        
        private void HandleMovement()
        {
            if (cameraTransform == null) return;
            
            Vector3 cameraForward = cameraTransform.forward;
            Vector3 cameraRight = cameraTransform.right;
            
            cameraForward.y = 0f;
            cameraRight.y = 0f;
            cameraForward.Normalize();
            cameraRight.Normalize();
            
            Vector3 moveDirection = (cameraForward * inputDirection.y + cameraRight * inputDirection.x);
            
            float targetSpeed = 0f;
            if (moveDirection.magnitude > 0.1f)
            {
                moveDirection.Normalize();
                targetSpeed = sprintInput ? sprintSpeed : walkSpeed;
                
                if (isGrounded)
                {
                    moveDirection = Vector3.ProjectOnPlane(moveDirection, groundNormal).normalized;
                }
            }
            
            if (isGrounded && groundAngle > maxWalkableAngle)
            {
                HandleSliding(ref moveDirection, ref targetSpeed);
            }
            
            float accel = targetSpeed > currentSpeed ? acceleration : deceleration;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accel * Time.fixedDeltaTime);
            
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
            Vector3 slideDirection = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;
            
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
            if (isInCombatMode && lookTarget != null)
            {
                HandleCombatRotation();
                return;
            }
            
            if (horizontalVelocity.magnitude > 0.1f)
            {
                Vector3 flatVelocity = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z);
                if (flatVelocity.magnitude > 0.1f)
                {
                    targetRotation = Mathf.Atan2(flatVelocity.x, flatVelocity.z) * Mathf.Rad2Deg;
                    
                    float currentYRotation = target.eulerAngles.y;
                    float newRotation = Mathf.SmoothDampAngle(currentYRotation, targetRotation, 
                        ref rotationVelocity, rotationSmoothTime, rotationSpeed);
                    
                    rb.MoveRotation(Quaternion.Euler(0f, newRotation, 0f));
                }
            }
        }
        
        private void HandleCombatRotation()
        {
            Vector3 directionToTarget = lookTarget.position - target.position;
            directionToTarget.y = 0f;
            
            if (directionToTarget.magnitude > 0.1f)
            {
                Quaternion targetRot = Quaternion.LookRotation(directionToTarget);
                Quaternion newRot = Quaternion.RotateTowards(target.rotation, targetRot, 
                    rotationSpeed * Time.fixedDeltaTime);
                rb.MoveRotation(newRot);
            }
        }
        
        #endregion
        
        #region Gravity and Jump
        
        private void HandleGravityAndJump()
        {
            bool canJump = isGrounded || timeSinceGrounded < coyoteTime;
            bool wantsToJump = timeSinceJumpPressed < jumpBufferTime;
            
            if (canJump && wantsToJump)
            {
                Jump();
            }
            
            if (!isGrounded)
            {
                verticalVelocity -= gravity * Time.fixedDeltaTime;
                verticalVelocity = Mathf.Max(verticalVelocity, -maxFallSpeed);
            }
            else if (verticalVelocity < 0)
            {
                verticalVelocity = -2f;
            }
        }
        
        private void Jump()
        {
            verticalVelocity = jumpForce;
            isGrounded = false;
            timeSinceGrounded = coyoteTime;
            timeSinceJumpPressed = jumpBufferTime;
            
            OnJump?.Invoke();
        }
        
        #endregion
        
        #region Apply Movement
        
        private void ApplyMovement()
        {
            velocity = horizontalVelocity + Vector3.up * verticalVelocity;
            
            Vector3 displacement = velocity * Time.fixedDeltaTime;
            
            displacement = ResolveCollisions(displacement);
            
            // Usar MovePosition en lugar de modificar transform.position
            Vector3 newPosition = rb.position + displacement;
            rb.MovePosition(newPosition);
        }
        
        private Vector3 ResolveCollisions(Vector3 displacement)
        {
            if (displacement.magnitude < 0.001f) return displacement;
            
            Vector3 origin = target.position + Vector3.up * (groundCheckRadius + 0.1f);
            
            // Resolver colisiones horizontales
            Vector3 horizontalDisplacement = new Vector3(displacement.x, 0f, displacement.z);
            horizontalDisplacement = ResolveHorizontalCollisions(origin, horizontalDisplacement);
            
            // Resolver colisiones verticales (importante para caídas rápidas)
            float verticalDisplacement = displacement.y;
            verticalDisplacement = ResolveVerticalCollisions(origin, verticalDisplacement);
            
            return new Vector3(horizontalDisplacement.x, verticalDisplacement, horizontalDisplacement.z);
        }
        
        private Vector3 ResolveHorizontalCollisions(Vector3 origin, Vector3 horizontalDisplacement)
        {
            for (int i = 0; i < maxCollisionIterations; i++)
            {
                if (horizontalDisplacement.magnitude < skinWidth) break;
                
                if (Physics.SphereCast(origin, groundCheckRadius * 0.9f, horizontalDisplacement.normalized, 
                    out RaycastHit hit, horizontalDisplacement.magnitude + skinWidth, 
                    groundLayers, QueryTriggerInteraction.Ignore))
                {
                    float hitAngle = Vector3.Angle(Vector3.up, hit.normal);
                    if (hitAngle > maxWalkableAngle)
                    {
                        float distanceToWall = hit.distance - skinWidth;
                        if (distanceToWall > 0)
                        {
                            Vector3 moveToWall = horizontalDisplacement.normalized * distanceToWall;
                            Vector3 remaining = horizontalDisplacement - moveToWall;
                            Vector3 slideDirection = Vector3.ProjectOnPlane(remaining, hit.normal);
                            
                            horizontalDisplacement = moveToWall + slideDirection;
                        }
                        else
                        {
                            horizontalDisplacement = Vector3.ProjectOnPlane(horizontalDisplacement, hit.normal);
                        }
                    }
                }
                else
                {
                    break;
                }
            }
            
            return horizontalDisplacement;
        }
        
        private float ResolveVerticalCollisions(Vector3 origin, float verticalDisplacement)
        {
            // Solo verificar cuando está cayendo (movimiento hacia abajo)
            if (verticalDisplacement >= 0) return verticalDisplacement;
            
            float fallDistance = Mathf.Abs(verticalDisplacement);
            
            // SphereCast hacia abajo para detectar el suelo
            if (Physics.SphereCast(origin, groundCheckRadius * 0.9f, Vector3.down, 
                out RaycastHit hit, fallDistance + skinWidth, 
                groundLayers, QueryTriggerInteraction.Ignore))
            {
                // Hay algo debajo, limitar la caída
                float maxFall = hit.distance - skinWidth;
                
                if (maxFall < fallDistance)
                {
                    // Iba a atravesar, detener en el punto de impacto
                    verticalVelocity = 0f;
                    isGrounded = true;
                    
                    return -Mathf.Max(maxFall, 0f);
                }
            }
            
            return verticalDisplacement;
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
            
            // Capsule collider preview
            Gizmos.color = Color.white;
            Vector3 colliderBottom = target.position + colliderCenter - Vector3.up * (colliderHeight / 2 - colliderRadius);
            Vector3 colliderTop = target.position + colliderCenter + Vector3.up * (colliderHeight / 2 - colliderRadius);
            Gizmos.DrawWireSphere(colliderBottom, colliderRadius);
            Gizmos.DrawWireSphere(colliderTop, colliderRadius);
        }
        
        #endregion
    }
}
