using System.Collections.Generic;
using UnityEngine;

namespace RobotGame.Control
{
    /// <summary>
    /// Sistema de física kinematic universal para robots de cualquier tamaño.
    /// 
    /// Características:
    /// - Cálculo automático de bounds basado en el mesh
    /// - Colisiones mediante múltiples raycasts distribuidos
    /// - Depenetración automática de colliders
    /// - Detección de suelo adaptativa
    /// - Sistema de plataformas móviles (pararse sobre robots grandes)
    /// - Gravedad configurable
    /// - Se puede habilitar/deshabilitar (para modo edición)
    /// 
    /// SETUP:
    /// - Requiere Rigidbody (IsKinematic = true)
    /// - Calcula bounds automáticamente de los renderers hijos
    /// - Llamar RecalculateBounds() después de modificar el robot
    /// 
    /// USO:
    /// - Controllers (PlayerController/RobotController) setean velocity
    /// - KinematicBody maneja colisiones y física
    /// - Llamar SetEnabled(false) durante modo edición
    /// </summary>
    public class KinematicBody : MonoBehaviour
    {
        #region Serialized Fields
        
        [Header("Configuración de Física")]
        [Tooltip("Gravedad aplicada cuando no está en el suelo")]
        [SerializeField] private float gravity = 20f;
        
        [Tooltip("Velocidad máxima de caída")]
        [SerializeField] private float maxFallSpeed = 30f;
        
        [Tooltip("Ángulo máximo que se considera caminable")]
        [SerializeField] private float maxWalkableAngle = 45f;
        
        [Header("Colisiones")]
        [Tooltip("Espacio mínimo entre el cuerpo y los obstáculos")]
        [SerializeField] private float skinWidth = 0.02f;
        
        [Tooltip("Iteraciones máximas para resolver colisiones")]
        [SerializeField] private int maxCollisionIterations = 3;
        
        [Tooltip("Layers que bloquean el movimiento")]
        [SerializeField] private LayerMask collisionLayers = ~0;
        
        [Header("Detección de Suelo")]
        [Tooltip("Distancia extra para detectar suelo")]
        [SerializeField] private float groundCheckDistance = 0.15f;
        
        [Tooltip("Offset desde la base para el ground check")]
        [SerializeField] private float groundCheckOffset = 0.05f;
        
        [Header("Plataformas Móviles")]
        [Tooltip("Habilitar seguimiento de plataformas móviles")]
        [SerializeField] private bool enableMovingPlatforms = true;
        
        [Header("Bounds Override")]
        [Tooltip("Si true, usa los valores manuales en lugar de calcular")]
        [SerializeField] private bool useManualBounds = false;
        
        [Tooltip("Centro del cuerpo (relativo al transform)")]
        [SerializeField] private Vector3 manualCenter = new Vector3(0f, 1f, 0f);
        
        [Tooltip("Tamaño del cuerpo (width, height, length)")]
        [SerializeField] private Vector3 manualSize = new Vector3(0.8f, 2f, 0.8f);
        
        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = true;
        [SerializeField] private bool logCollisions = false;
        
        #endregion
        
        #region Public Properties
        
        /// <summary>
        /// ¿Está el sistema de física habilitado?
        /// </summary>
        public bool IsEnabled => isEnabled;
        
        /// <summary>
        /// ¿Está tocando el suelo?
        /// </summary>
        public bool IsGrounded => isGrounded;
        
        /// <summary>
        /// Normal del suelo actual.
        /// </summary>
        public Vector3 GroundNormal => groundNormal;
        
        /// <summary>
        /// Ángulo del suelo actual.
        /// </summary>
        public float GroundAngle => groundAngle;
        
        /// <summary>
        /// Velocidad actual (horizontal + vertical).
        /// </summary>
        public Vector3 Velocity => velocity;
        
        /// <summary>
        /// Velocidad horizontal actual.
        /// </summary>
        public Vector3 HorizontalVelocity => horizontalVelocity;
        
        /// <summary>
        /// Velocidad vertical actual.
        /// </summary>
        public float VerticalVelocity => verticalVelocity;
        
        /// <summary>
        /// Plataforma sobre la que está parado (null si no hay).
        /// </summary>
        public Transform CurrentPlatform => currentPlatform;
        
        /// <summary>
        /// Bounds calculados del cuerpo.
        /// </summary>
        public Bounds BodyBounds => bodyBounds;
        
        /// <summary>
        /// ¿Está en una pendiente deslizable?
        /// </summary>
        public bool IsOnSlope => isGrounded && groundAngle > maxWalkableAngle;
        
        /// <summary>
        /// Radio del cuerpo (para cálculos externos).
        /// </summary>
        public float BodyRadius => bodyRadius;
        
        /// <summary>
        /// Altura del cuerpo (para cálculos externos).
        /// </summary>
        public float BodyHeight => bodyHeight;
        
        /// <summary>
        /// Centro del cuerpo relativo al transform.
        /// </summary>
        public Vector3 BodyCenter => bodyCenter;
        
        /// <summary>
        /// Collider principal del cuerpo.
        /// </summary>
        public Collider BodyCollider => bodyCollider;
        
        #endregion
        
        #region Private Fields
        
        // Estado
        private bool isEnabled = true;
        
        // Componentes
        private Rigidbody rb;
        private Collider bodyCollider;
        
        // Bounds calculados
        private Bounds bodyBounds;
        private float bodyRadius;    // Radio horizontal (max de width/length / 2)
        private float bodyHeight;    // Altura total
        private Vector3 bodyCenter;  // Centro relativo al transform
        
        // Estado del suelo
        private bool isGrounded;
        private bool wasGrounded;
        private Vector3 groundNormal = Vector3.up;
        private float groundAngle;
        private RaycastHit groundHit;
        
        // Velocidad
        private Vector3 velocity;
        private Vector3 horizontalVelocity;
        private float verticalVelocity;
        
        // Plataformas móviles
        private Transform currentPlatform;
        private Vector3 lastPlatformPosition;
        private Quaternion lastPlatformRotation;
        
        // Cache para raycasts
        private Vector3[] groundCheckPoints;
        private Vector3[] horizontalCheckPoints;
        private Collider[] overlapResults = new Collider[16];
        
        // Layers a ignorar
        private int selfLayer;
        private LayerMask effectiveCollisionLayers;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            SetupRigidbody();
            SetupCollider();
            
            selfLayer = gameObject.layer;
            UpdateEffectiveCollisionLayers();
            
            RecalculateBounds();
            
            Debug.Log($"KinematicBody [{gameObject.name}]: Inicializado - Layer={selfLayer}, CollisionLayers={collisionLayers.value}, EffectiveLayers={effectiveCollisionLayers.value}, BodyRadius={bodyRadius:F2}, BodyHeight={bodyHeight:F2}");
        }
        
        private void FixedUpdate()
        {
            if (!isEnabled) return;
            
            // 1. Actualizar plataforma móvil primero
            if (enableMovingPlatforms)
            {
                UpdatePlatformMovement();
            }
            
            // 2. Detectar suelo
            CheckGround();
            
            // 3. Aplicar gravedad
            ApplyGravity();
            
            // 4. Calcular desplazamiento
            Vector3 displacement = CalculateDisplacement();
            
            // 5. Resolver colisiones
            displacement = ResolveCollisions(displacement);
            
            // 6. Resolver penetración
            ResolvePenetration();
            
            // 7. Aplicar movimiento
            ApplyMovement(displacement);
            
            // 8. Actualizar referencia de plataforma
            UpdatePlatformReference();
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Habilita o deshabilita el sistema de física.
        /// Cuando está deshabilitado, no aplica gravedad ni colisiones.
        /// Útil para modo edición o cuando el jugador está montado.
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            isEnabled = enabled;
            
            if (!enabled)
            {
                // Resetear velocidades al deshabilitar
                ResetVelocity();
                currentPlatform = null;
            }
            
            // Habilitar/deshabilitar el collider
            if (bodyCollider != null)
            {
                bodyCollider.enabled = enabled;
            }
            
            Debug.Log($"KinematicBody [{gameObject.name}]: {(enabled ? "HABILITADO" : "DESHABILITADO")}");
        }
        
        /// <summary>
        /// Establece la velocidad horizontal deseada.
        /// La física se encarga de colisiones y gravedad.
        /// </summary>
        public void SetHorizontalVelocity(Vector3 velocity)
        {
            horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
        }
        
        /// <summary>
        /// Establece la velocidad vertical (para saltos).
        /// </summary>
        public void SetVerticalVelocity(float velocity)
        {
            verticalVelocity = velocity;
            
            // Si salta, ya no está grounded
            if (velocity > 0)
            {
                isGrounded = false;
            }
        }
        
        /// <summary>
        /// Añade velocidad vertical (impulso).
        /// </summary>
        public void AddVerticalImpulse(float impulse)
        {
            verticalVelocity += impulse;
            
            if (impulse > 0)
            {
                isGrounded = false;
            }
        }
        
        /// <summary>
        /// Recalcula los bounds basándose en los renderers hijos.
        /// Llamar después de modificar la estructura del robot.
        /// </summary>
        public void RecalculateBounds()
        {
            if (useManualBounds)
            {
                bodyCenter = manualCenter;
                bodyBounds = new Bounds(transform.position + bodyCenter, manualSize);
                bodyRadius = Mathf.Max(manualSize.x, manualSize.z) / 2f;
                bodyHeight = manualSize.y;
            }
            else
            {
                CalculateBoundsFromRenderers();
            }
            
            // Actualizar collider con los nuevos bounds
            UpdateColliderSize();
            
            // Generar puntos de check
            GenerateCheckPoints();
            
            Debug.Log($"KinematicBody [{gameObject.name}]: Bounds recalculados - " +
                     $"Center: {bodyCenter}, Size: {bodyBounds.size}, Radius: {bodyRadius:F2}, Height: {bodyHeight:F2}");
        }
        
        /// <summary>
        /// Fuerza una posición sin colisiones (teletransporte).
        /// </summary>
        public void Teleport(Vector3 position)
        {
            transform.position = position;
            if (rb != null)
            {
                rb.MovePosition(position);
            }
            currentPlatform = null;
            ResetVelocity();
        }
        
        /// <summary>
        /// Resetea la velocidad.
        /// </summary>
        public void ResetVelocity()
        {
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            velocity = Vector3.zero;
        }
        
        /// <summary>
        /// Fuerza un ground check inmediato.
        /// Útil después de teletransportar.
        /// </summary>
        public void ForceGroundCheck()
        {
            CheckGround();
            if (isGrounded)
            {
                verticalVelocity = 0f;
            }
        }
        
        #endregion
        
        #region Setup
        
        private void SetupRigidbody()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }
        
        private void SetupCollider()
        {
            // Buscar collider existente o crear uno
            bodyCollider = GetComponent<Collider>();
            
            if (bodyCollider == null)
            {
                // Crear CapsuleCollider por defecto
                CapsuleCollider capsule = gameObject.AddComponent<CapsuleCollider>();
                capsule.radius = 0.4f;
                capsule.height = 2f;
                capsule.center = new Vector3(0f, 1f, 0f);
                bodyCollider = capsule;
                
                Debug.Log($"KinematicBody [{gameObject.name}]: CapsuleCollider creado automáticamente");
            }
        }
        
        private void UpdateColliderSize()
        {
            if (bodyCollider == null) return;
            
            if (bodyCollider is CapsuleCollider capsule)
            {
                capsule.radius = bodyRadius;
                capsule.height = bodyHeight;
                capsule.center = bodyCenter;
            }
            else if (bodyCollider is BoxCollider box)
            {
                box.size = new Vector3(bodyRadius * 2f, bodyHeight, bodyRadius * 2f);
                box.center = bodyCenter;
            }
        }
        
        #endregion
        
        #region Bounds Calculation
        
        private void CalculateBoundsFromRenderers()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            
            if (renderers.Length == 0)
            {
                // Fallback: usar valores por defecto
                bodyCenter = new Vector3(0f, 1f, 0f);
                bodyBounds = new Bounds(transform.position + bodyCenter, new Vector3(0.8f, 2f, 0.8f));
                bodyRadius = 0.4f;
                bodyHeight = 2f;
                return;
            }
            
            // Calcular bounds combinados
            Bounds combinedBounds = new Bounds();
            bool first = true;
            
            foreach (var renderer in renderers)
            {
                // Ignorar renderers de partículas y trails
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer)
                    continue;
                
                if (first)
                {
                    combinedBounds = renderer.bounds;
                    first = false;
                }
                else
                {
                    combinedBounds.Encapsulate(renderer.bounds);
                }
            }
            
            // Convertir a espacio local
            Vector3 localCenter = transform.InverseTransformPoint(combinedBounds.center);
            Vector3 localSize = combinedBounds.size;
            
            bodyCenter = localCenter;
            bodyBounds = new Bounds(transform.position + bodyCenter, localSize);
            bodyRadius = Mathf.Max(localSize.x, localSize.z) / 2f;
            bodyHeight = localSize.y;
            
            // Asegurar valores mínimos
            bodyRadius = Mathf.Max(bodyRadius, 0.2f);
            bodyHeight = Mathf.Max(bodyHeight, 0.5f);
        }
        
        private void GenerateCheckPoints()
        {
            // Puntos para ground check (distribuidos en la base)
            List<Vector3> groundPoints = new List<Vector3>();
            
            // Centro
            groundPoints.Add(Vector3.zero);
            
            // Si el robot es grande, añadir puntos adicionales
            if (bodyRadius > 0.5f)
            {
                float checkRadius = bodyRadius * 0.7f;
                
                // 4 puntos cardinales
                groundPoints.Add(new Vector3(checkRadius, 0f, 0f));
                groundPoints.Add(new Vector3(-checkRadius, 0f, 0f));
                groundPoints.Add(new Vector3(0f, 0f, checkRadius));
                groundPoints.Add(new Vector3(0f, 0f, -checkRadius));
                
                // Si es muy grande, añadir diagonales
                if (bodyRadius > 1f)
                {
                    float diagRadius = checkRadius * 0.707f;
                    groundPoints.Add(new Vector3(diagRadius, 0f, diagRadius));
                    groundPoints.Add(new Vector3(-diagRadius, 0f, diagRadius));
                    groundPoints.Add(new Vector3(diagRadius, 0f, -diagRadius));
                    groundPoints.Add(new Vector3(-diagRadius, 0f, -diagRadius));
                }
            }
            
            groundCheckPoints = groundPoints.ToArray();
            
            // Puntos para horizontal check (a diferentes alturas)
            List<Vector3> horizPoints = new List<Vector3>();
            
            float stepHeight = bodyHeight / 3f;
            for (int i = 0; i < 3; i++)
            {
                float height = stepHeight * (i + 0.5f);
                horizPoints.Add(new Vector3(0f, height, 0f));
            }
            
            horizontalCheckPoints = horizPoints.ToArray();
        }
        
        #endregion
        
        #region Ground Detection
        
        private void CheckGround()
        {
            wasGrounded = isGrounded;
            isGrounded = false;
            groundNormal = Vector3.up;
            groundAngle = 0f;
            
            if (groundCheckPoints == null || groundCheckPoints.Length == 0)
            {
                Debug.LogError($"KinematicBody [{gameObject.name}]: groundCheckPoints es null o vacío!");
                return;
            }
            
            float checkRadius = Mathf.Min(bodyRadius * 0.9f, 0.4f);
            float checkDistance = groundCheckDistance + groundCheckOffset;
            
            if (logCollisions)
            {
                Debug.Log($"KinematicBody [{gameObject.name}]: CheckGround - radius={checkRadius:F2}, distance={checkDistance:F2}, points={groundCheckPoints.Length}, layers={effectiveCollisionLayers.value}");
            }
            
            // Verificar múltiples puntos para robots grandes
            int groundedPoints = 0;
            Vector3 averageNormal = Vector3.zero;
            float closestDistance = float.MaxValue;
            RaycastHit closestHit = default;
            
            foreach (var point in groundCheckPoints)
            {
                Vector3 origin = transform.position + transform.TransformDirection(point) + Vector3.up * (checkRadius + groundCheckOffset);
                
                if (Physics.SphereCast(origin, checkRadius, Vector3.down, out RaycastHit hit,
                    checkDistance, effectiveCollisionLayers, QueryTriggerInteraction.Ignore))
                {
                    groundedPoints++;
                    averageNormal += hit.normal;
                    
                    if (hit.distance < closestDistance)
                    {
                        closestDistance = hit.distance;
                        closestHit = hit;
                    }
                }
            }
            
            // Considerarse grounded si al menos un punto toca suelo
            if (groundedPoints > 0)
            {
                groundNormal = (averageNormal / groundedPoints).normalized;
                groundAngle = Vector3.Angle(Vector3.up, groundNormal);
                groundHit = closestHit;
                
                // Solo grounded si el ángulo es caminable O estamos cayendo
                isGrounded = groundAngle <= maxWalkableAngle || verticalVelocity <= 0;
                
                // Snap al suelo si estamos grounded y cayendo
                if (isGrounded && verticalVelocity <= 0)
                {
                    float distanceToGround = closestHit.distance - groundCheckOffset;
                    if (distanceToGround > skinWidth && distanceToGround < groundCheckDistance)
                    {
                        Vector3 snapPosition = transform.position - Vector3.up * (distanceToGround - skinWidth);
                        rb.MovePosition(snapPosition);
                    }
                }
            }
            
            // Eventos de aterrizaje
            if (isGrounded && !wasGrounded)
            {
                OnLanded();
            }
        }
        
        private void OnLanded()
        {
            // Reset velocidad vertical al aterrizar
            if (verticalVelocity < 0)
            {
                verticalVelocity = -2f; // Pequeña velocidad hacia abajo para mantener contacto
            }
            
            if (logCollisions)
            {
                Debug.Log($"KinematicBody [{gameObject.name}]: Landed");
            }
        }
        
        #endregion
        
        #region Gravity
        
        private void ApplyGravity()
        {
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
        
        #endregion
        
        #region Movement
        
        private Vector3 CalculateDisplacement()
        {
            velocity = horizontalVelocity + Vector3.up * verticalVelocity;
            return velocity * Time.fixedDeltaTime;
        }
        
        private void ApplyMovement(Vector3 displacement)
        {
            if (displacement.magnitude > 0.0001f)
            {
                Vector3 newPosition = rb.position + displacement;
                rb.MovePosition(newPosition);
            }
        }
        
        #endregion
        
        #region Collision Resolution
        
        private Vector3 ResolveCollisions(Vector3 displacement)
        {
            if (displacement.magnitude < 0.001f)
                return displacement;
            
            // Separar horizontal y vertical
            Vector3 horizontalDisp = new Vector3(displacement.x, 0f, displacement.z);
            float verticalDisp = displacement.y;
            
            // Resolver horizontal
            if (horizontalDisp.magnitude > skinWidth)
            {
                horizontalDisp = ResolveHorizontalCollisions(horizontalDisp);
            }
            
            // Resolver vertical
            verticalDisp = ResolveVerticalCollisions(verticalDisp);
            
            return new Vector3(horizontalDisp.x, verticalDisp, horizontalDisp.z);
        }
        
        private Vector3 ResolveHorizontalCollisions(Vector3 horizontalDisp)
        {
            float checkRadius = Mathf.Min(bodyRadius * 0.8f, 0.35f);
            
            for (int iter = 0; iter < maxCollisionIterations; iter++)
            {
                if (horizontalDisp.magnitude < skinWidth)
                    break;
                
                bool hitSomething = false;
                Vector3 moveDir = horizontalDisp.normalized;
                float moveDist = horizontalDisp.magnitude;
                
                // Verificar desde múltiples alturas
                foreach (var heightOffset in horizontalCheckPoints)
                {
                    Vector3 origin = transform.position + heightOffset;
                    
                    if (Physics.SphereCast(origin, checkRadius, moveDir, out RaycastHit hit,
                        moveDist + skinWidth, effectiveCollisionLayers, QueryTriggerInteraction.Ignore))
                    {
                        float hitAngle = Vector3.Angle(Vector3.up, hit.normal);
                        
                        // Solo bloquear si es una pared (no rampa caminable)
                        if (hitAngle > maxWalkableAngle)
                        {
                            hitSomething = true;
                            
                            float allowedDist = Mathf.Max(0f, hit.distance - skinWidth);
                            
                            if (allowedDist < moveDist)
                            {
                                // Slide along wall
                                Vector3 moveToWall = moveDir * allowedDist;
                                Vector3 remaining = horizontalDisp - moveToWall;
                                Vector3 slideDir = Vector3.ProjectOnPlane(remaining, hit.normal);
                                
                                horizontalDisp = moveToWall + slideDir * 0.9f;
                                
                                if (logCollisions)
                                {
                                    Debug.Log($"KinematicBody [{gameObject.name}]: Wall collision, sliding");
                                }
                                
                                break;
                            }
                        }
                    }
                }
                
                if (!hitSomething)
                    break;
            }
            
            return horizontalDisp;
        }
        
        private float ResolveVerticalCollisions(float verticalDisp)
        {
            // Solo verificar cuando cae
            if (verticalDisp >= 0)
                return verticalDisp;
            
            float checkRadius = Mathf.Min(bodyRadius * 0.8f, 0.35f);
            float fallDistance = Mathf.Abs(verticalDisp);
            Vector3 origin = transform.position + Vector3.up * (checkRadius + groundCheckOffset);
            
            if (Physics.SphereCast(origin, checkRadius, Vector3.down, out RaycastHit hit,
                fallDistance + skinWidth + groundCheckOffset, effectiveCollisionLayers, QueryTriggerInteraction.Ignore))
            {
                float allowedFall = Mathf.Max(0f, hit.distance - skinWidth - groundCheckOffset);
                
                if (allowedFall < fallDistance)
                {
                    verticalVelocity = 0f;
                    isGrounded = true;
                    return -allowedFall;
                }
            }
            
            return verticalDisp;
        }
        
        #endregion
        
        #region Penetration Resolution
        
        private void ResolvePenetration()
        {
            if (bodyCollider == null) return;
            
            // Usar OverlapSphere para detectar si estamos dentro de algo
            float checkRadius = bodyRadius * 0.9f;
            Vector3 center = transform.position + bodyCenter;
            
            int numOverlaps = Physics.OverlapSphereNonAlloc(center, checkRadius, overlapResults, 
                effectiveCollisionLayers, QueryTriggerInteraction.Ignore);
            
            for (int i = 0; i < numOverlaps; i++)
            {
                Collider other = overlapResults[i];
                
                // Ignorar propios colliders
                if (other.transform.IsChildOf(transform) || other.transform == transform)
                    continue;
                
                // Ignorar triggers
                if (other.isTrigger)
                    continue;
                
                // Ignorar si es el mismo collider
                if (other == bodyCollider)
                    continue;
                
                // Calcular dirección de salida usando ComputePenetration
                if (Physics.ComputePenetration(
                    bodyCollider, transform.position, transform.rotation,
                    other, other.transform.position, other.transform.rotation,
                    out Vector3 direction, out float distance))
                {
                    if (distance > skinWidth)
                    {
                        // Empujar fuera
                        Vector3 pushOut = direction * (distance + skinWidth);
                        rb.MovePosition(rb.position + pushOut);
                        
                        if (logCollisions)
                        {
                            Debug.Log($"KinematicBody [{gameObject.name}]: Depenetration from {other.name}, dist: {distance:F3}");
                        }
                    }
                }
            }
        }
        
        #endregion
        
        #region Moving Platforms
        
        private void UpdatePlatformMovement()
        {
            if (currentPlatform == null)
                return;
            
            // Verificar que la plataforma sigue existiendo
            if (currentPlatform == null)
            {
                currentPlatform = null;
                return;
            }
            
            // Calcular delta de movimiento de la plataforma
            Vector3 platformDelta = currentPlatform.position - lastPlatformPosition;
            
            // Calcular delta de rotación
            Quaternion rotationDelta = currentPlatform.rotation * Quaternion.Inverse(lastPlatformRotation);
            
            // Aplicar rotación alrededor del centro de la plataforma
            if (rotationDelta != Quaternion.identity)
            {
                Vector3 offset = transform.position - currentPlatform.position;
                Vector3 rotatedOffset = rotationDelta * offset;
                platformDelta += rotatedOffset - offset;
                
                // También rotar al personaje
                Vector3 euler = rotationDelta.eulerAngles;
                if (euler.y != 0)
                {
                    rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, euler.y, 0f));
                }
            }
            
            // Aplicar movimiento de plataforma
            if (platformDelta.magnitude > 0.0001f)
            {
                rb.MovePosition(rb.position + platformDelta);
            }
            
            // Actualizar para siguiente frame
            lastPlatformPosition = currentPlatform.position;
            lastPlatformRotation = currentPlatform.rotation;
        }
        
        private void UpdatePlatformReference()
        {
            Transform newPlatform = null;
            
            if (isGrounded && groundHit.collider != null)
            {
                // Verificar si el suelo es una plataforma móvil
                Rigidbody groundRb = groundHit.collider.attachedRigidbody;
                KinematicBody groundBody = groundHit.collider.GetComponentInParent<KinematicBody>();
                
                // Es plataforma si tiene Rigidbody (kinematic o no) o KinematicBody
                if (groundRb != null || groundBody != null)
                {
                    newPlatform = groundHit.collider.transform;
                    
                    // Preferir el root del KinematicBody si existe
                    if (groundBody != null)
                    {
                        newPlatform = groundBody.transform;
                    }
                }
            }
            
            // Cambio de plataforma
            if (newPlatform != currentPlatform)
            {
                currentPlatform = newPlatform;
                
                if (currentPlatform != null)
                {
                    lastPlatformPosition = currentPlatform.position;
                    lastPlatformRotation = currentPlatform.rotation;
                    
                    if (logCollisions)
                    {
                        Debug.Log($"KinematicBody [{gameObject.name}]: Now on platform '{currentPlatform.name}'");
                    }
                }
            }
        }
        
        #endregion
        
        #region Layer Management
        
        private void UpdateEffectiveCollisionLayers()
        {
            // Solo remover la propia layer si NO es el layer Default (0)
            // porque el suelo y otros objetos suelen estar en Default
            if (selfLayer != 0)
            {
                effectiveCollisionLayers = collisionLayers & ~(1 << selfLayer);
            }
            else
            {
                effectiveCollisionLayers = collisionLayers;
            }
            
            Debug.Log($"KinematicBody [{gameObject.name}]: Layers - self={selfLayer}, collision={collisionLayers.value}, effective={effectiveCollisionLayers.value}");
        }
        
        /// <summary>
        /// Actualiza las layers de colisión.
        /// </summary>
        public void SetCollisionLayers(LayerMask layers)
        {
            collisionLayers = layers;
            UpdateEffectiveCollisionLayers();
        }
        
        #endregion
        
        #region Debug
        
        private void OnDrawGizmos()
        {
            if (!showDebugGizmos)
                return;
            
            // Dibujar bounds
            Gizmos.color = isGrounded ? Color.green : Color.red;
            
            if (Application.isPlaying)
            {
                Gizmos.DrawWireCube(transform.position + bodyCenter, bodyBounds.size);
                
                // Dibujar radio en la base
                Gizmos.color = Color.yellow;
                DrawCircle(transform.position + Vector3.up * 0.1f, bodyRadius, 16);
                
                // Dibujar puntos de ground check
                Gizmos.color = Color.cyan;
                if (groundCheckPoints != null)
                {
                    foreach (var point in groundCheckPoints)
                    {
                        Vector3 worldPoint = transform.position + transform.TransformDirection(point);
                        Gizmos.DrawWireSphere(worldPoint + Vector3.up * groundCheckOffset, 0.05f);
                    }
                }
                
                // Dibujar plataforma actual
                if (currentPlatform != null)
                {
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawLine(transform.position, currentPlatform.position);
                }
            }
            else
            {
                // En editor, mostrar bounds manuales o estimados
                Vector3 center = useManualBounds ? manualCenter : new Vector3(0f, 1f, 0f);
                Vector3 size = useManualBounds ? manualSize : new Vector3(0.8f, 2f, 0.8f);
                Gizmos.DrawWireCube(transform.position + center, size);
            }
        }
        
        private void DrawCircle(Vector3 center, float radius, int segments)
        {
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + new Vector3(radius, 0f, 0f);
            
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 point = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }
        }
        
        #endregion
    }
}
