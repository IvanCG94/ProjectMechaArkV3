using UnityEngine;
using System;

namespace RobotGame.Control
{
    /// <summary>
    /// Controlador del jugador usando Rigidbody dinámico.
    /// 
    /// Este script se añade al mismo GameObject que controla (el robot).
    /// Unity maneja las colisiones automáticamente.
    /// 
    /// Características:
    /// - Movimiento responsivo con Rigidbody dinámico
    /// - Collider se recalcula automáticamente al modificar el robot
    /// - En modo edición: Rigidbody kinematic + collider desactivado
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        #region Enums
        
        public enum ControlState
        {
            Normal,
            EditMode,
            Mounted,
            Disabled
        }
        
        #endregion
        
        #region Serialized Fields
        
        [Header("Movimiento")]
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float sprintSpeed = 9f;
        [SerializeField] private float gravityMultiplier = 2.5f; // Gravedad más fuerte para mejor game feel
        
        [Header("Salto")]
        [SerializeField] private float jumpForce = 8f;
        [SerializeField] private float coyoteTime = 0.12f;
        [SerializeField] private float jumpBufferTime = 0.1f;
        
        [Header("Rotación")]
        [SerializeField] private float rotationSpeed = 720f;
        
        [Header("Ground Check")]
        [SerializeField] private float groundCheckDistance = 0.15f;
        [SerializeField] private LayerMask groundLayers = ~0;
        
        [Header("Collider")]
        [SerializeField] private float minRadius = 0.3f;
        [SerializeField] private float minHeight = 1f;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;
        
        #endregion
        
        #region Events
        
        public event Action OnJump;
        public event Action OnLand;
        public event Action<ControlState> OnStateChanged;
        
        #endregion
        
        #region Public Properties
        
        public ControlState CurrentState => currentState;
        public Transform Target => targetTransform;
        public bool IsGrounded => isGrounded;
        public bool IsSprinting => isSprinting && horizontalSpeed > 0.1f;
        public float CurrentSpeed => horizontalSpeed;
        public Vector3 Velocity => rb != null ? rb.linearVelocity : Vector3.zero;
        public float VerticalVelocity => rb != null ? rb.linearVelocity.y : 0f;
        public Vector2 InputDirection => inputDirection;
        
        // Compatibilidad con código existente
        public KinematicBody Body => null;
        
        #endregion
        
        #region Private Fields
        
        private ControlState currentState = ControlState.Normal;
        private Transform targetTransform;
        
        // Componentes en el robot
        private Rigidbody rb;
        private CapsuleCollider capsule;
        private Transform cameraTransform;
        private Combat.CombatController combatController;
        
        // Input
        private Vector2 inputDirection;
        private bool isSprinting;
        private bool jumpPressed;
        
        // Estado
        private bool isGrounded;
        private bool wasGrounded;
        private float horizontalSpeed;
        private Vector3 lastMoveDirection = Vector3.forward;
        
        // Timers
        private float coyoteTimer;
        private float jumpBufferTimer;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Update()
        {
            if (currentState != ControlState.Normal)
                return;
            
            ReadInput();
        }
        
        private void FixedUpdate()
        {
            if (currentState != ControlState.Normal)
                return;
            
            if (targetTransform == null || rb == null)
                return;
            
            CheckGround();
            UpdateTimers();
            CheckLanding();
            
            ApplyMovement();
            ApplyRotation();
            ApplyExtraGravity();
            ProcessJump();
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Establece el robot a controlar.
        /// Configura Rigidbody y Collider en el robot.
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            targetTransform = newTarget;
            
            if (targetTransform == null)
            {
                rb = null;
                capsule = null;
                combatController = null;
                return;
            }
            
            SetupRigidbody();
            SetupCollider();
            RecalculateCollider();
            
            // Buscar CombatController
            combatController = targetTransform.GetComponent<Combat.CombatController>();
            
            // Buscar cámara
            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
            
            Debug.Log($"PlayerController: Target -> {targetTransform.name}");
        }
        
        private void SetupRigidbody()
        {
            rb = targetTransform.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = targetTransform.gameObject.AddComponent<Rigidbody>();
            }
            
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.mass = 1f;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.05f;
        }
        
        private void SetupCollider()
        {
            capsule = targetTransform.GetComponent<CapsuleCollider>();
            if (capsule == null)
            {
                capsule = targetTransform.gameObject.AddComponent<CapsuleCollider>();
            }
        }
        
        /// <summary>
        /// Recalcula el tamaño del collider basándose en los bounds del robot.
        /// </summary>
        public void RecalculateCollider()
        {
            if (targetTransform == null || capsule == null)
                return;
            
            Bounds bounds = CalculateBounds();
            
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
            float height = bounds.size.y;
            
            radius = Mathf.Max(radius, minRadius);
            height = Mathf.Max(height, minHeight);
            
            // Centro a la mitad de la altura
            Vector3 center = new Vector3(0f, height / 2f, 0f);
            
            capsule.radius = radius;
            capsule.height = height;
            capsule.center = center;
            
            Debug.Log($"PlayerController: Collider - Radius: {radius:F2}, Height: {height:F2}");
        }
        
        private Bounds CalculateBounds()
        {
            Renderer[] renderers = targetTransform.GetComponentsInChildren<Renderer>();
            
            if (renderers.Length == 0)
            {
                return new Bounds(Vector3.up, new Vector3(0.8f, 2f, 0.8f));
            }
            
            Bounds bounds = new Bounds(targetTransform.position, Vector3.zero);
            bool first = true;
            
            foreach (var renderer in renderers)
            {
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer)
                    continue;
                
                if (first)
                {
                    bounds = renderer.bounds;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            
            // Convertir centro a espacio local del target
            bounds.center = targetTransform.InverseTransformPoint(bounds.center);
            
            return bounds;
        }
        
        public void SetState(ControlState newState)
        {
            if (currentState == newState)
                return;
            
            ControlState prevState = currentState;
            currentState = newState;
            
            if (prevState == ControlState.Mounted)
                OnExitMounted();
            
            switch (newState)
            {
                case ControlState.Normal:
                    OnEnterNormal();
                    break;
                case ControlState.EditMode:
                    OnEnterEditMode();
                    break;
                case ControlState.Mounted:
                    OnEnterMounted();
                    break;
                case ControlState.Disabled:
                    OnEnterDisabled();
                    break;
            }
            
            OnStateChanged?.Invoke(newState);
            Debug.Log($"PlayerController: {prevState} -> {newState}");
        }
        
        public void Enable() => SetState(ControlState.Normal);
        public void Disable() => SetState(ControlState.Disabled);
        public void EnterEditModeState() => SetState(ControlState.EditMode);
        public void ExitEditModeState() => SetState(ControlState.Normal);
        public void EnterMountedState() => SetState(ControlState.Mounted);
        public void ExitMountedState() => SetState(ControlState.Normal);
        
        public void ForceGroundCheck()
        {
            CheckGround();
            if (isGrounded && rb != null)
            {
                Vector3 vel = rb.linearVelocity;
                vel.y = 0f;
                rb.linearVelocity = vel;
            }
        }
        
        public void Teleport(Vector3 position)
        {
            if (targetTransform != null)
            {
                targetTransform.position = position;
            }
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
            }
        }
        
        #endregion
        
        #region State Handlers
        
        private void OnEnterNormal()
        {
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }
            if (capsule != null)
            {
                capsule.enabled = true;
            }
            ForceGroundCheck();
        }
        
        private void OnEnterEditMode()
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
            if (capsule != null)
            {
                capsule.enabled = false;
            }
        }
        
        private void OnEnterMounted()
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
            if (capsule != null)
            {
                capsule.enabled = false;
            }
            if (targetTransform != null)
            {
                targetTransform.gameObject.SetActive(false);
            }
        }
        
        private void OnExitMounted()
        {
            if (targetTransform != null)
            {
                targetTransform.gameObject.SetActive(true);
            }
        }
        
        private void OnEnterDisabled()
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
        }
        
        #endregion
        
        #region Input
        
        private void ReadInput()
        {
            float h = 0f, v = 0f;
            
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v = 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v = -1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h = 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h = -1f;
            
            inputDirection = new Vector2(h, v);
            if (inputDirection.sqrMagnitude > 1f)
                inputDirection.Normalize();
            
            isSprinting = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            
            if (Input.GetKeyDown(KeyCode.Space))
            {
                jumpPressed = true;
                jumpBufferTimer = jumpBufferTime;
            }
        }
        
        #endregion
        
        #region Ground Check
        
        private void CheckGround()
        {
            wasGrounded = isGrounded;
            
            if (capsule == null)
            {
                isGrounded = false;
                return;
            }
            
            float radius = capsule.radius * 0.9f;
            Vector3 origin = targetTransform.position + Vector3.up * (radius + 0.05f);
            
            isGrounded = Physics.SphereCast(
                origin,
                radius,
                Vector3.down,
                out _,
                groundCheckDistance + 0.05f,
                groundLayers,
                QueryTriggerInteraction.Ignore
            );
        }
        
        private void CheckLanding()
        {
            if (isGrounded && !wasGrounded)
            {
                OnLand?.Invoke();
            }
        }
        
        #endregion
        
        #region Movement
        
        private void ApplyExtraGravity()
        {
            // Aplicar gravedad adicional para mejor game feel
            // (Unity solo aplica Physics.gravity, esto lo multiplica)
            if (!isGrounded && gravityMultiplier > 1f)
            {
                Vector3 extraGravity = Physics.gravity * (gravityMultiplier - 1f);
                rb.AddForce(extraGravity, ForceMode.Acceleration);
            }
        }
        
        private void ApplyMovement()
        {
            // Verificar si el ataque actual permite movimiento
            bool canMove = combatController == null || combatController.CanMove;
            
            Vector3 moveDir = canMove ? GetCameraRelativeDirection(inputDirection) : Vector3.zero;
            
            if (moveDir.sqrMagnitude > 0.01f)
            {
                lastMoveDirection = moveDir.normalized;
            }
            
            float targetSpeed = (canMove && inputDirection.sqrMagnitude > 0.01f)
                ? (isSprinting ? sprintSpeed : walkSpeed) 
                : 0f;
            
            horizontalSpeed = targetSpeed;
            
            // Aplicar velocidad
            Vector3 vel = rb.linearVelocity;
            
            if (moveDir.sqrMagnitude > 0.01f)
            {
                vel.x = moveDir.x * targetSpeed;
                vel.z = moveDir.z * targetSpeed;
            }
            else
            {
                vel.x = 0f;
                vel.z = 0f;
            }
            
            rb.linearVelocity = vel;
        }
        
        private Vector3 GetCameraRelativeDirection(Vector2 input)
        {
            if (input.sqrMagnitude < 0.01f)
                return Vector3.zero;
            
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
            
            if (cameraTransform != null)
            {
                Vector3 forward = cameraTransform.forward;
                Vector3 right = cameraTransform.right;
                
                forward.y = 0f;
                right.y = 0f;
                forward.Normalize();
                right.Normalize();
                
                return (forward * input.y + right * input.x).normalized;
            }
            
            return new Vector3(input.x, 0f, input.y).normalized;
        }
        
        private void ApplyRotation()
        {
            // Verificar si el ataque actual permite rotación
            if (combatController != null && !combatController.CanRotate)
                return;
            
            if (lastMoveDirection.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lastMoveDirection, Vector3.up);
                targetTransform.rotation = Quaternion.RotateTowards(
                    targetTransform.rotation,
                    targetRot,
                    rotationSpeed * Time.fixedDeltaTime
                );
            }
        }
        
        #endregion
        
        #region Jump
        
        private void ProcessJump()
        {
            bool canJump = isGrounded || coyoteTimer > 0f;
            bool wantsJump = jumpPressed || jumpBufferTimer > 0f;
            
            if (canJump && wantsJump)
            {
                Vector3 vel = rb.linearVelocity;
                vel.y = jumpForce;
                rb.linearVelocity = vel;
                
                jumpPressed = false;
                jumpBufferTimer = 0f;
                coyoteTimer = 0f;
                isGrounded = false;
                
                OnJump?.Invoke();
            }
            
            jumpPressed = false;
        }
        
        private void UpdateTimers()
        {
            coyoteTimer = isGrounded ? coyoteTime : coyoteTimer - Time.fixedDeltaTime;
            
            if (jumpBufferTimer > 0f)
                jumpBufferTimer -= Time.fixedDeltaTime;
        }
        
        #endregion
        
        #region Debug
        
        private void OnGUI()
        {
            if (!showDebugInfo)
                return;
            
            GUILayout.BeginArea(new Rect(10, 10, 250, 180));
            GUILayout.BeginVertical("box");
            
            GUILayout.Label("=== PlayerController ===");
            GUILayout.Label($"State: {currentState}");
            GUILayout.Label($"Grounded: {isGrounded}");
            GUILayout.Label($"Speed: {horizontalSpeed:F1}");
            GUILayout.Label($"Input: {inputDirection}");
            if (rb != null)
                GUILayout.Label($"Vel Y: {rb.linearVelocity.y:F2}");
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        
        #endregion
    }
}
