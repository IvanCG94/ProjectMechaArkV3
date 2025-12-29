using System.Collections.Generic;
using UnityEngine;
using RobotGame.Components;

namespace RobotGame.Control
{
    /// <summary>
    /// Envía parámetros de animación a todas las partes estructurales del robot.
    /// 
    /// Este script funciona como un "broadcaster" de parámetros. Lee el estado
    /// del PlayerMovement y envía los mismos valores a todos los Animators
    /// de las partes estructurales. Cada parte decide si tiene animación
    /// para ese estado o no.
    /// 
    /// PARÁMETROS ENVIADOS:
    /// - Speed (float): Velocidad de movimiento normalizada (0-1)
    /// - IsGrounded (bool): Si está en el suelo
    /// - IsSprinting (bool): Si está corriendo
    /// - VerticalVelocity (float): Velocidad vertical (para salto/caída)
    /// - Jump (trigger): Disparado al saltar
    /// - Land (trigger): Disparado al aterrizar
    /// 
    /// INTEGRACIÓN:
    /// - Se suscribe al evento OnPlayerRobotChanged para detectar cambios de robot
    /// - Recolecta automáticamente todos los Animators de las partes estructurales
    /// </summary>
    public class PlayerAnimator : MonoBehaviour
    {
        #region Serialized Fields
        
        [Header("Referencias")]
        [SerializeField] private PlayerMovement playerMovement;
        
        [Header("Configuración")]
        [Tooltip("Velocidad máxima para normalizar el parámetro Speed (0-1)")]
        [SerializeField] private float maxSpeedReference = 9f;
        
        [Tooltip("Suavizado del parámetro Speed")]
        [SerializeField] private float speedSmoothTime = 0.1f;
        
        [Header("Nombres de Parámetros")]
        [SerializeField] private string speedParam = "Speed";
        [SerializeField] private string isGroundedParam = "IsGrounded";
        [SerializeField] private string isSprintingParam = "IsSprinting";
        [SerializeField] private string verticalVelocityParam = "VerticalVelocity";
        [SerializeField] private string jumpTrigger = "Jump";
        [SerializeField] private string landTrigger = "Land";
        
        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;
        
        #endregion
        
        #region Private Fields
        
        // Lista de todos los Animators de las partes del robot
        private List<Animator> partAnimators = new List<Animator>();
        
        // IDs de parámetros (cacheados para performance)
        private int speedHash;
        private int isGroundedHash;
        private int isSprintingHash;
        private int verticalVelocityHash;
        private int jumpHash;
        private int landHash;
        
        // Estado
        private float currentAnimSpeed;
        private float speedVelocity;
        
        // Referencia al robot actual
        private Robot currentRobot;
        
        #endregion
        
        #region Properties
        
        /// <summary>
        /// Cantidad de Animators actualmente controlados.
        /// </summary>
        public int AnimatorCount => partAnimators.Count;
        
        /// <summary>
        /// Lista de Animators (solo lectura).
        /// </summary>
        public IReadOnlyList<Animator> Animators => partAnimators;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            CacheParameterHashes();
        }
        
        private void OnEnable()
        {
            RobotCore.OnPlayerRobotChanged += OnRobotChanged;
        }
        
        private void OnDisable()
        {
            RobotCore.OnPlayerRobotChanged -= OnRobotChanged;
            UnsubscribeFromMovementEvents();
        }
        
        private void Start()
        {
            if (playerMovement == null)
            {
                playerMovement = FindObjectOfType<PlayerMovement>();
            }
            
            SubscribeToMovementEvents();
            
            // Si ya hay un robot, recolectar sus animators
            if (playerMovement != null && playerMovement.Target != null)
            {
                Robot robot = playerMovement.Target.GetComponent<Robot>();
                if (robot != null)
                {
                    CollectAnimators(robot);
                }
            }
        }
        
        private void Update()
        {
            if (playerMovement == null || partAnimators.Count == 0) return;
            
            UpdateAnimatorParameters();
        }
        
        #endregion
        
        #region Initialization
        
        private void CacheParameterHashes()
        {
            speedHash = Animator.StringToHash(speedParam);
            isGroundedHash = Animator.StringToHash(isGroundedParam);
            isSprintingHash = Animator.StringToHash(isSprintingParam);
            verticalVelocityHash = Animator.StringToHash(verticalVelocityParam);
            jumpHash = Animator.StringToHash(jumpTrigger);
            landHash = Animator.StringToHash(landTrigger);
        }
        
        private void SubscribeToMovementEvents()
        {
            if (playerMovement != null)
            {
                playerMovement.OnJump += HandleJump;
                playerMovement.OnLand += HandleLand;
            }
        }
        
        private void UnsubscribeFromMovementEvents()
        {
            if (playerMovement != null)
            {
                playerMovement.OnJump -= HandleJump;
                playerMovement.OnLand -= HandleLand;
            }
        }
        
        #endregion
        
        #region Robot Changed
        
        private void OnRobotChanged(RobotCore core, Robot robot)
        {
            if (robot != null)
            {
                currentRobot = robot;
                CollectAnimators(robot);
                
                // Reset de estado
                currentAnimSpeed = 0f;
                
                if (showDebugInfo)
                {
                    Debug.Log($"PlayerAnimator: Robot cambiado a {robot.RobotName}, encontrados {partAnimators.Count} Animators");
                }
            }
            else
            {
                currentRobot = null;
                partAnimators.Clear();
            }
        }
        
        /// <summary>
        /// Recolecta todos los Animators de las partes estructurales del robot.
        /// </summary>
        private void CollectAnimators(Robot robot)
        {
            partAnimators.Clear();
            
            if (robot == null) return;
            
            // Obtener todas las partes estructurales
            List<StructuralPart> allParts = robot.GetAllStructuralParts();
            
            foreach (var part in allParts)
            {
                if (part.Animator != null && part.Animator.runtimeAnimatorController != null)
                {
                    partAnimators.Add(part.Animator);
                    
                    if (showDebugInfo)
                    {
                        Debug.Log($"  - Animator encontrado en: {part.name}");
                    }
                }
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"PlayerAnimator: Total {partAnimators.Count} Animators recolectados");
            }
        }
        
        #endregion
        
        #region Animator Updates
        
        private void UpdateAnimatorParameters()
        {
            // Calcular Speed normalizado (0-1)
            float targetSpeed = playerMovement.CurrentSpeed / maxSpeedReference;
            targetSpeed = Mathf.Clamp01(targetSpeed);
            
            // Suavizar el cambio de velocidad
            currentAnimSpeed = Mathf.SmoothDamp(currentAnimSpeed, targetSpeed, 
                ref speedVelocity, speedSmoothTime);
            
            // Obtener otros valores
            bool isGrounded = playerMovement.IsGrounded;
            bool isSprinting = playerMovement.IsSprinting;
            float verticalVelocity = playerMovement.Velocity.y;
            
            // Enviar a todos los Animators
            foreach (var animator in partAnimators)
            {
                if (animator == null) continue;
                
                SetFloatIfExists(animator, speedHash, currentAnimSpeed);
                SetBoolIfExists(animator, isGroundedHash, isGrounded);
                SetBoolIfExists(animator, isSprintingHash, isSprinting);
                SetFloatIfExists(animator, verticalVelocityHash, verticalVelocity);
            }
        }
        
        #endregion
        
        #region Event Handlers
        
        private void HandleJump()
        {
            BroadcastTrigger(jumpHash);
            
            if (showDebugInfo)
            {
                Debug.Log("PlayerAnimator: Jump trigger enviado");
            }
        }
        
        private void HandleLand()
        {
            BroadcastTrigger(landHash);
            
            if (showDebugInfo)
            {
                Debug.Log("PlayerAnimator: Land trigger enviado");
            }
        }
        
        #endregion
        
        #region Broadcast Methods
        
        /// <summary>
        /// Envía un trigger a todos los Animators.
        /// </summary>
        public void BroadcastTrigger(string triggerName)
        {
            int hash = Animator.StringToHash(triggerName);
            BroadcastTrigger(hash);
        }
        
        /// <summary>
        /// Envía un trigger a todos los Animators (por hash).
        /// </summary>
        public void BroadcastTrigger(int hash)
        {
            foreach (var animator in partAnimators)
            {
                if (animator == null) continue;
                SetTriggerIfExists(animator, hash);
            }
        }
        
        /// <summary>
        /// Envía un valor float a todos los Animators.
        /// </summary>
        public void BroadcastFloat(string paramName, float value)
        {
            int hash = Animator.StringToHash(paramName);
            foreach (var animator in partAnimators)
            {
                if (animator == null) continue;
                SetFloatIfExists(animator, hash, value);
            }
        }
        
        /// <summary>
        /// Envía un valor bool a todos los Animators.
        /// </summary>
        public void BroadcastBool(string paramName, bool value)
        {
            int hash = Animator.StringToHash(paramName);
            foreach (var animator in partAnimators)
            {
                if (animator == null) continue;
                SetBoolIfExists(animator, hash, value);
            }
        }
        
        #endregion
        
        #region Safe Parameter Setters
        
        private void SetFloatIfExists(Animator animator, int hash, float value)
        {
            if (HasParameter(animator, hash))
            {
                animator.SetFloat(hash, value);
            }
        }
        
        private void SetBoolIfExists(Animator animator, int hash, bool value)
        {
            if (HasParameter(animator, hash))
            {
                animator.SetBool(hash, value);
            }
        }
        
        private void SetTriggerIfExists(Animator animator, int hash)
        {
            if (HasParameter(animator, hash))
            {
                animator.SetTrigger(hash);
            }
        }
        
        /// <summary>
        /// Verifica si el Animator tiene un parámetro con el hash dado.
        /// </summary>
        private bool HasParameter(Animator animator, int hash)
        {
            if (animator == null || animator.runtimeAnimatorController == null) 
                return false;
            
            foreach (var param in animator.parameters)
            {
                if (param.nameHash == hash)
                    return true;
            }
            
            return false;
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Fuerza la recolección de Animators del robot actual.
        /// Útil si se agregaron/quitaron partes en runtime.
        /// </summary>
        public void RefreshAnimators()
        {
            if (currentRobot != null)
            {
                CollectAnimators(currentRobot);
            }
            else if (playerMovement != null && playerMovement.Target != null)
            {
                Robot robot = playerMovement.Target.GetComponent<Robot>();
                if (robot != null)
                {
                    CollectAnimators(robot);
                }
            }
        }
        
        #endregion
        
        #region Debug
        
        private void OnGUI()
        {
            if (!showDebugInfo) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.BeginVertical("box");
            
            GUILayout.Label("=== PlayerAnimator Debug ===");
            GUILayout.Label($"Animators: {partAnimators.Count}");
            GUILayout.Label($"Speed: {currentAnimSpeed:F2}");
            GUILayout.Label($"Grounded: {playerMovement?.IsGrounded}");
            GUILayout.Label($"Sprinting: {playerMovement?.IsSprinting}");
            GUILayout.Label($"Vertical Vel: {playerMovement?.Velocity.y:F2}");
            
            GUILayout.Space(5);
            GUILayout.Label("Partes con Animator:");
            foreach (var anim in partAnimators)
            {
                if (anim != null)
                {
                    GUILayout.Label($"  • {anim.gameObject.name}");
                }
            }
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        
        #endregion
    }
}
