using UnityEngine;

namespace RobotGame.Control
{
    /// <summary>
    /// Sistema de movimiento genérico para robots.
    /// Usa Rigidbody Kinematic con ground detection y gravedad.
    /// 
    /// Puede ser usado por:
    /// - IA de robots salvajes (WildRobot)
    /// - Jugador montado en un mecha
    /// - Cualquier entidad que necesite movimiento físico
    /// 
    /// DIFERENCIA CON PlayerMovement:
    /// - PlayerMovement lee input directamente del teclado
    /// - RobotMovement recibe direcciones via métodos públicos (SetMoveDirection, Jump, etc.)
    /// 
    /// SETUP REQUERIDO:
    /// - El robot necesita un Rigidbody (Is Kinematic = true)
    /// - El robot necesita un Collider (CapsuleCollider recomendado)
    /// - El script auto-configura estos componentes si no existen
    /// </summary>
    public class RobotMovement : MonoBehaviour
    {
        #region Serialized Fields
        
        [Header("Velocidad")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float sprintSpeed = 9f;
        [SerializeField] private float acceleration = 10f;
        [SerializeField] private float deceleration = 12f;
        
        [Header("Rotación")]
        [SerializeField] private float rotationSpeed = 720f;
        [SerializeField] private float rotationSmoothTime = 0.1f;
        
        [Header("Salto")]
        [SerializeField] private float jumpForce = 8f;
        [SerializeField] private bool canJump = true;
        
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
        
        [Header("Colisiones")]
        [SerializeField] private float skinWidth = 0.02f;
        [SerializeField] private int maxCollisionIterations = 3;
        
        [Header("Estado")]
        [SerializeField] private bool isEnabled = true;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = true;
        
        #endregion
        
        #region Private Fields
        
        // Componentes
        private Rigidbody rb;
        private CapsuleCollider capsuleCollider;
        
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
        
        // Input (establecido externamente)
        private Vector3 moveDirection;
        private bool isSprinting;
        private bool jumpRequested;
        
        // Rotación suave
        private float rotationVelocity;
        private float targetRotation;
        
        // Flag para inicialización
        private bool isInitialized;
        
        #endregion
        
        #region Events
        
        public event System.Action OnJump;
        public event System.Action OnLand;
        
        #endregion
        
        #region Properties
        
        public bool IsEnabled => isEnabled;
        public bool IsGrounded => isGrounded;
        public Vector3 Velocity => velocity;
        public float CurrentSpeed => currentSpeed;
        public bool IsSprinting => isSprinting && currentSpeed > moveSpeed * 0.9f;
        public Vector3 GroundNormal => groundNormal;
        public Rigidbody Rigidbody => rb;
        public CapsuleCollider Collider => capsuleCollider;
        
        public float MoveSpeed
        {
            get => moveSpeed;
            set => moveSpeed = value;
        }
        
        public float SprintSpeed
        {
            get => sprintSpeed;
            set => sprintSpeed = value;
        }
        
        public float RotationSpeed
        {
            get => rotationSpeed;
            set => rotationSpeed = value;
        }
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            Initialize();
        }
        
        private void FixedUpdate()
        {
            if (!isEnabled) return;
            
            CheckGround();
            HandleMovement();
            HandleRotation();
            HandleGravity();
            ApplyMovement();
            
            // Reset jump request después de procesarlo
            jumpRequested = false;
            
            // Detectar aterrizaje
            if (isGrounded && !wasGrounded)
            {
                OnLand?.Invoke();
            }
            wasGrounded = isGrounded;
        }
        
        #endregion
        
        #region Initialization
        
        public void Initialize()
        {
            if (isInitialized) return;
            
            SetupRigidbodyAndCollider();
            isInitialized = true;
        }
        
        private void SetupRigidbodyAndCollider()
        {
            // Buscar o crear Rigidbody
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            
            // Configurar Rigidbody para movimiento kinematic
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            
            // Buscar o crear CapsuleCollider
            capsuleCollider = GetComponent<CapsuleCollider>();
            if (capsuleCollider == null)
            {
                capsuleCollider = gameObject.AddComponent<CapsuleCollider>();
            }
            
            // Configurar Collider
            capsuleCollider.height = colliderHeight;
            capsuleCollider.radius = colliderRadius;
            capsuleCollider.center = colliderCenter;
        }
        
        #endregion
        
        #region Public Control Methods
        
        /// <summary>
        /// Establece la dirección de movimiento (normalizada o no).
        /// </summary>
        public void SetMoveDirection(Vector3 direction)
        {
            moveDirection = direction.magnitude > 1f ? direction.normalized : direction;
        }
        
        /// <summary>
        /// Establece la dirección de movimiento en 2D (X = horizontal, Y = vertical).
        /// </summary>
        public void SetMoveDirection(Vector2 direction)
        {
            moveDirection = new Vector3(direction.x, 0f, direction.y);
            if (moveDirection.magnitude > 1f)
            {
                moveDirection.Normalize();
            }
        }
        
        /// <summary>
        /// Establece si está corriendo.
        /// </summary>
        public void SetSprinting(bool sprinting)
        {
            isSprinting = sprinting;
        }
        
        /// <summary>
        /// Solicita un salto (se ejecutará en el próximo FixedUpdate si es posible).
        /// </summary>
        public void Jump()
        {
            if (canJump)
            {
                jumpRequested = true;
            }
        }
        
        /// <summary>
        /// Habilita el movimiento.
        /// </summary>
        public void Enable()
        {
            isEnabled = true;
            if (capsuleCollider != null)
            {
                capsuleCollider.enabled = true;
            }
        }
        
        /// <summary>
        /// Deshabilita el movimiento.
        /// </summary>
        public void Disable()
        {
            isEnabled = false;
            moveDirection = Vector3.zero;
            horizontalVelocity = Vector3.zero;
            currentSpeed = 0f;
        }
        
        /// <summary>
        /// Detiene todo el movimiento inmediatamente.
        /// </summary>
        public void Stop()
        {
            moveDirection = Vector3.zero;
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            currentSpeed = 0f;
        }
        
        /// <summary>
        /// Teletransporta el robot a una posición.
        /// </summary>
        public void Teleport(Vector3 position)
        {
            if (rb != null)
            {
                rb.position = position;
            }
            else
            {
                transform.position = position;
            }
            
            Stop();
        }
        
        /// <summary>
        /// Mira hacia una posición específica.
        /// </summary>
        public void LookAt(Vector3 targetPosition)
        {
            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f;
            
            if (direction.magnitude > 0.1f)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction);
                rb.MoveRotation(targetRot);
            }
        }
        
        /// <summary>
        /// Gira gradualmente hacia una posición.
        /// </summary>
        public void RotateTowards(Vector3 targetPosition)
        {
            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f;
            
            if (direction.magnitude > 0.1f)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction);
                Quaternion newRot = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime);
                rb.MoveRotation(newRot);
            }
        }
        
        #endregion
        
        #region Ground Detection
        
        private void CheckGround()
        {
            Vector3 origin = transform.position + Vector3.up * (groundCheckRadius + groundOffset);
            
            if (Physics.SphereCast(origin, groundCheckRadius, Vector3.down, out RaycastHit hit, 
                groundCheckDistance + groundOffset, groundLayers, QueryTriggerInteraction.Ignore))
            {
                groundNormal = hit.normal;
                groundAngle = Vector3.Angle(Vector3.up, groundNormal);
                
                isGrounded = groundAngle <= maxWalkableAngle;
                
                if (isGrounded && verticalVelocity <= 0)
                {
                    // Ajustar posición al suelo
                    float groundY = hit.point.y;
                    Vector3 pos = transform.position;
                    if (pos.y > groundY + 0.01f && pos.y < groundY + groundCheckDistance)
                    {
                        rb.MovePosition(new Vector3(pos.x, groundY, pos.z));
                    }
                }
            }
            else
            {
                isGrounded = false;
                groundNormal = Vector3.up;
                groundAngle = 0f;
            }
        }
        
        #endregion
        
        #region Movement
        
        private void HandleMovement()
        {
            float targetSpeed = 0f;
            Vector3 targetDirection = moveDirection;
            
            if (moveDirection.magnitude > 0.1f)
            {
                targetSpeed = isSprinting ? sprintSpeed : moveSpeed;
            }
            
            // Aplicar aceleración/deceleración
            if (targetSpeed > currentSpeed)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.fixedDeltaTime);
            }
            else
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, deceleration * Time.fixedDeltaTime);
            }
            
            // Calcular velocidad horizontal
            if (targetDirection.magnitude > 0.1f)
            {
                horizontalVelocity = targetDirection.normalized * currentSpeed;
            }
            else
            {
                horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, Vector3.zero, deceleration * Time.fixedDeltaTime);
            }
            
            // Aplicar slope si está en pendiente no caminable
            if (isGrounded && groundAngle > maxWalkableAngle)
            {
                Vector3 slideDirection = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;
                horizontalVelocity = slideDirection * slideSpeed;
            }
        }
        
        private void HandleRotation()
        {
            if (horizontalVelocity.magnitude > 0.1f)
            {
                Vector3 flatVelocity = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z);
                if (flatVelocity.magnitude > 0.1f)
                {
                    targetRotation = Mathf.Atan2(flatVelocity.x, flatVelocity.z) * Mathf.Rad2Deg;
                    
                    float currentYRotation = transform.eulerAngles.y;
                    float newRotation = Mathf.SmoothDampAngle(currentYRotation, targetRotation, 
                        ref rotationVelocity, rotationSmoothTime, rotationSpeed);
                    
                    rb.MoveRotation(Quaternion.Euler(0f, newRotation, 0f));
                }
            }
        }
        
        private void HandleGravity()
        {
            // Procesar salto
            if (jumpRequested && isGrounded && canJump)
            {
                verticalVelocity = jumpForce;
                isGrounded = false;
                OnJump?.Invoke();
            }
            
            // Aplicar gravedad
            if (!isGrounded)
            {
                verticalVelocity -= gravity * Time.fixedDeltaTime;
                verticalVelocity = Mathf.Max(verticalVelocity, -maxFallSpeed);
            }
            else if (verticalVelocity < 0)
            {
                verticalVelocity = -2f; // Pequeña fuerza hacia abajo para mantenerse en el suelo
            }
        }
        
        #endregion
        
        #region Apply Movement
        
        private void ApplyMovement()
        {
            velocity = horizontalVelocity + Vector3.up * verticalVelocity;
            
            Vector3 displacement = velocity * Time.fixedDeltaTime;
            
            displacement = ResolveCollisions(displacement);
            
            Vector3 newPosition = rb.position + displacement;
            rb.MovePosition(newPosition);
        }
        
        private Vector3 ResolveCollisions(Vector3 displacement)
        {
            if (displacement.magnitude < 0.001f) return displacement;
            
            Vector3 origin = transform.position + Vector3.up * (groundCheckRadius + 0.1f);
            
            // Resolver colisiones horizontales
            Vector3 horizontalDisplacement = new Vector3(displacement.x, 0f, displacement.z);
            horizontalDisplacement = ResolveHorizontalCollisions(origin, horizontalDisplacement);
            
            // Resolver colisiones verticales
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
            if (verticalDisplacement >= 0) return verticalDisplacement;
            
            float fallDistance = Mathf.Abs(verticalDisplacement);
            
            if (Physics.SphereCast(origin, groundCheckRadius * 0.9f, Vector3.down, 
                out RaycastHit hit, fallDistance + skinWidth, 
                groundLayers, QueryTriggerInteraction.Ignore))
            {
                float maxFall = hit.distance - skinWidth;
                
                if (maxFall < fallDistance)
                {
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
            if (!showDebugGizmos) return;
            
            Vector3 origin = transform.position + Vector3.up * (groundCheckRadius + groundOffset);
            
            // Ground check sphere
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(origin, groundCheckRadius);
            Gizmos.DrawLine(origin, origin + Vector3.down * (groundCheckDistance + groundOffset));
            
            // Ground normal
            if (isGrounded)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, transform.position + groundNormal);
            }
            
            // Velocity
            if (horizontalVelocity.magnitude > 0.1f)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position + Vector3.up, 
                    transform.position + Vector3.up + horizontalVelocity.normalized * 2f);
            }
        }
        
        #endregion
    }
}
