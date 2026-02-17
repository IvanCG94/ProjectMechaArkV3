using UnityEngine;
using RobotGame.Components;
using RobotGame.Control;
using RobotGame.Data;
using RobotGame.Enums;
using RobotGame.Combat;

namespace RobotGame.AI
{
    /// <summary>
    /// Estado actual del robot salvaje.
    /// </summary>
    public enum WildRobotState
    {
        // Estados salvajes
        Idle,           // Quieto
        Patrol,         // Patrullando
        Alert,          // Alerta (detectó algo)
        Chase,          // Persiguiendo al jugador (acercarse a rango)
        Stalk,          // Acechar al objetivo (manteniendo distancia)
        Circle,         // Rodear al objetivo (buscando apertura)
        Positioning,    // Posicionándose para atacar (strafe/circling)
        Attack,         // Atacando
        Recovery,       // Recuperación post-ataque (evalúa resultado)
        Flee,           // Huyendo
        Dead,           // Muerto/Derrotado
        
        // Estados domesticados
        TamedFollow,    // Siguiendo al dueño
        TamedStay       // Quieto esperando órdenes
    }
    
    /// <summary>
    /// Componente que controla el comportamiento de un robot salvaje.
    /// Se añade al GameObject del robot junto con el componente Robot.
    /// 
    /// Usa RobotMovement para física de movimiento (gravedad, ground detection, etc.)
    /// 
    /// DOMESTICACIÓN:
    /// - Robots salvajes tienen IsTamed = false
    /// - Al domesticar, se asigna un Owner (RobotCore del jugador)
    /// - Robots domesticados no atacan a su dueño y pueden ser editados
    /// </summary>
    [RequireComponent(typeof(Robot))]
    [RequireComponent(typeof(RobotMovement))]
    [RequireComponent(typeof(VisionDetector))]
    public class WildRobot : MonoBehaviour
    {
        [Header("Datos")]
        [SerializeField] private WildRobotData wildData;
        
        [Header("Domesticación")]
        [SerializeField] private bool isTamed = false;
        [SerializeField] private RobotCore owner;
        
        [Header("Configuración Domesticado")]
        [Tooltip("Distancia a la que el mecha considera que está 'cerca' del dueño")]
        [SerializeField] private float followStopDistance = 3f;
        [Tooltip("Distancia a la que el mecha empieza a seguir si está muy lejos")]
        [SerializeField] private float followStartDistance = 5f;
        [Tooltip("Distancia mínima que mantiene del dueño")]
        [SerializeField] private float followMinDistance = 2f;
        
        [Header("Configuración de Alerta")]
        [Tooltip("Tiempo en segundos que el jugador debe permanecer visible para pasar de Alerta a Ataque")]
        [SerializeField] private float alertDuration = 10f;
        
        [Header("Configuración de Patrulla")]
        [Tooltip("Radio máximo de patrulla desde el punto de spawn")]
        [SerializeField] private float patrolRadius = 10f;
        [Tooltip("Tiempo de espera en cada punto de patrulla")]
        [SerializeField] private float patrolWaitTime = 2f;
        [Tooltip("Distancia mínima para considerar que llegó al destino")]
        [SerializeField] private float patrolArrivalDistance = 1f;
        
        [Header("Estado (Solo lectura)")]
        [SerializeField] private WildRobotState currentState = WildRobotState.Idle;
        [SerializeField] private float currentHealth;
        [SerializeField] private Transform targetPlayer;
        [SerializeField] private float alertTimer = 0f;
        
        [Header("Debug")]
        [SerializeField] private bool showGizmos = true;
        
        // Referencias
        private Robot robot;
        private RobotMovement movement;
        private VisionDetector visionDetector;
        private Animator animator;
        private CombatController combatController;
        
        // Estado interno
        private Vector3 spawnPosition;
        private float lastAttackTime;
        private float stateTimer;
        
        // Control externo (para montura)
        private bool isBeingControlled = false;
        
        // Estado de animación de alerta
        private bool isAlertAnimationPlaying = false;
        
        // Estado de patrulla
        private Vector3 patrolDestination;
        private bool hasPatrolDestination = false;
        private float patrolWaitTimer = 0f;
        private bool isWaitingAtPatrolPoint = false;
        
        // Estado de posicionamiento para ataque
        private CombatPart selectedAttackPart;
        private AttackData selectedAttack;
        private AttackZone selectedZone;
        private int strafeDirection = 1; // 1 = derecha, -1 = izquierda
        private float positioningTimeout = 5f; // Tiempo máximo buscando posición
        private float positioningTimer = 0f;
        
        // Sistema de timing de ataque
        private float waitBeforeAttackTimer = 0f;
        private float targetWaitTime = 0f;
        private bool isWaitingToAttack = false;
        
        // Feedback del ataque
        private bool lastAttackHit = false;
        private AttackData lastExecutedAttack;
        
        // Velocidad de aproximación (modificada por ataque)
        private float currentApproachSpeedMultiplier = 1f;
        
        #region Properties
        
        /// <summary>
        /// Datos del robot salvaje.
        /// </summary>
        public WildRobotData WildData => wildData;
        
        /// <summary>
        /// Componente Robot asociado.
        /// </summary>
        public Robot Robot => robot;
        
        /// <summary>
        /// Componente de movimiento.
        /// </summary>
        public RobotMovement Movement => movement;
        
        /// <summary>
        /// Estado actual.
        /// </summary>
        public WildRobotState CurrentState => currentState;
        
        /// <summary>
        /// Salud actual.
        /// </summary>
        public float CurrentHealth => currentHealth;
        
        /// <summary>
        /// Salud máxima (basada en durabilidad total).
        /// </summary>
        public float MaxHealth => wildData != null ? wildData.CalculateTotalDurability() : 100f;
        
        /// <summary>
        /// Si el robot está vivo.
        /// </summary>
        public bool IsAlive => currentState != WildRobotState.Dead && currentHealth > 0;
        
        /// <summary>
        /// Si el robot está siendo controlado por el jugador (montado).
        /// </summary>
        public bool IsBeingControlled => isBeingControlled;
        
        /// <summary>
        /// Si el robot está domesticado.
        /// </summary>
        public bool IsTamed => isTamed;
        
        /// <summary>
        /// El dueño del robot (null si es salvaje).
        /// </summary>
        public RobotCore Owner => owner;
        
        /// <summary>
        /// Controlador de combate del robot.
        /// </summary>
        public CombatController CombatController => combatController;
        
        /// <summary>
        /// Alcance efectivo de ataque.
        /// Usa el CombatReach de las partes de combate si están disponibles,
        /// de lo contrario usa wildData.attackRange como fallback.
        /// </summary>
        public float EffectiveAttackRange
        {
            get
            {
                // Intentar obtener alcance del CombatController
                if (combatController != null && combatController.CombatParts.Count > 0)
                {
                    float maxReach = combatController.MaxCombatReach;
                    if (maxReach > 0f)
                    {
                        return maxReach;
                    }
                }
                
                // Fallback a wildData
                return wildData != null ? wildData.attackRange : 2f;
            }
        }
        
        /// <summary>
        /// Verifica si este robot pertenece a un jugador específico.
        /// </summary>
        public bool BelongsTo(RobotCore playerCore)
        {
            return isTamed && owner == playerCore;
        }
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            robot = GetComponent<Robot>();
            movement = GetComponent<RobotMovement>();
            visionDetector = GetComponent<VisionDetector>();
            
            // Si no existe el componente de movimiento, crearlo
            if (movement == null)
            {
                movement = gameObject.AddComponent<RobotMovement>();
            }
            
            // Si no existe VisionDetector, crearlo
            if (visionDetector == null)
            {
                visionDetector = gameObject.AddComponent<VisionDetector>();
            }
            
            // Buscar Animator en el robot
            animator = GetComponentInChildren<Animator>();
            
            // Obtener o agregar CombatController
            combatController = GetComponent<CombatController>();
            if (combatController == null)
            {
                combatController = gameObject.AddComponent<CombatController>();
            }
        }
        
        private void Start()
        {
            spawnPosition = transform.position;
            
            if (wildData != null)
            {
                currentHealth = MaxHealth;
                
                // Configurar velocidades del movimiento según datos
                movement.MoveSpeed = wildData.moveSpeed;
                movement.SprintSpeed = wildData.moveSpeed * 1.5f;
                movement.RotationSpeed = wildData.rotationSpeed;
                
                // Configurar VisionDetector con los datos del robot
                if (visionDetector != null)
                {
                    visionDetector.DetectionRange = wildData.detectionRadius;
                    // FOV se mantiene el default (120°) o se puede agregar a WildRobotData
                }
            }
            
            // Buscar al jugador
            FindPlayer();
            
            // Refrescar partes de combate después de un frame para asegurar que todo esté configurado
            Invoke(nameof(RefreshCombatParts), 0.1f);
        }
        
        /// <summary>
        /// Refresca las partes de combate del CombatController.
        /// </summary>
        private void RefreshCombatParts()
        {
            if (combatController != null)
            {
                combatController.RefreshCombatParts();
            }
        }
        
        /// <summary>
        /// Establece el objetivo actual y lo propaga a todas las AttackZones.
        /// </summary>
        public void SetCurrentTarget(Transform newTarget)
        {
            targetPlayer = newTarget;
            
            // Propagar el target a todas las AttackZones del robot
            if (combatController != null)
            {
                foreach (var part in combatController.CombatParts)
                {
                    if (part == null) continue;
                    
                    foreach (var zone in part.LinkedAttackZones)
                    {
                        if (zone != null)
                        {
                            zone.SetTarget(newTarget);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Objetivo actual del robot.
        /// </summary>
        public Transform CurrentTarget => targetPlayer;
        
        private void Update()
        {
            if (!IsAlive) return;
            
            // Si está siendo controlado por el jugador, no ejecutar IA
            if (isBeingControlled) return;
            
            // Actualizar timer de estado
            stateTimer += Time.deltaTime;
            
            // Máquina de estados básica
            switch (currentState)
            {
                case WildRobotState.Idle:
                    UpdateIdle();
                    break;
                case WildRobotState.Patrol:
                    UpdatePatrol();
                    break;
                case WildRobotState.Alert:
                    UpdateAlert();
                    break;
                case WildRobotState.Chase:
                    UpdateChase();
                    break;
                case WildRobotState.Stalk:
                    UpdateStalk();
                    break;
                case WildRobotState.Circle:
                    UpdateCircle();
                    break;
                case WildRobotState.Positioning:
                    UpdatePositioning();
                    break;
                case WildRobotState.Attack:
                    UpdateAttack();
                    break;
                case WildRobotState.Recovery:
                    UpdateRecovery();
                    break;
                case WildRobotState.Flee:
                    UpdateFlee();
                    break;
                    
                // Estados domesticados
                case WildRobotState.TamedFollow:
                    UpdateTamedFollow();
                    break;
                case WildRobotState.TamedStay:
                    UpdateTamedStay();
                    break;
            }
        }
        
        #endregion
        
        #region Initialization
        
        /// <summary>
        /// Inicializa el robot salvaje con sus datos.
        /// </summary>
        public void Initialize(WildRobotData data)
        {
            wildData = data;
            currentHealth = MaxHealth;
            currentState = WildRobotState.Idle;
            spawnPosition = transform.position;
        }
        
        private void FindPlayer()
        {
            // Buscar todos los RobotCore y encontrar el del jugador
            var allCores = FindObjectsOfType<RobotCore>();
            
            foreach (var core in allCores)
            {
                // Verificar que sea el core del jugador y no el nuestro
                if (core.IsPlayerCore && core.CurrentRobot != null)
                {
                    // Asegurarse de que no es nuestro propio robot
                    if (core.CurrentRobot.gameObject != this.gameObject && 
                        !core.CurrentRobot.transform.IsChildOf(this.transform) &&
                        !this.transform.IsChildOf(core.CurrentRobot.transform))
                    {
                        SetCurrentTarget(core.CurrentRobot.transform);
                        return;
                    }
                }
            }
        }
        
        #endregion
        
        #region State Machine
        
        private void ChangeState(WildRobotState newState)
        {
            if (currentState == newState) return;
            
            // Salir del estado actual
            OnExitState(currentState);
            
            currentState = newState;
            stateTimer = 0f;
            
            // Entrar al nuevo estado
            OnEnterState(newState);
            
            Debug.Log($"WildRobot '{wildData?.speciesName}': {currentState}");
        }
        
        private void OnEnterState(WildRobotState state)
        {
            switch (state)
            {
                case WildRobotState.Idle:
                    // En idle, permitir rotación automática
                    if (movement != null) movement.AutoRotate = true;
                    break;
                    
                case WildRobotState.Patrol:
                    // En patrulla, permitir rotación automática
                    if (movement != null) movement.AutoRotate = true;
                    break;
                    
                case WildRobotState.Alert:
                    alertTimer = 0f;
                    // En alerta, la IA controla la rotación
                    if (movement != null) movement.AutoRotate = false;
                    if (targetPlayer != null) SetCurrentTarget(targetPlayer);
                    break;
                    
                case WildRobotState.Chase:
                    currentApproachSpeedMultiplier = 1f;
                    // En chase, permitir rotación automática (corre hacia el objetivo)
                    if (movement != null) movement.AutoRotate = true;
                    if (targetPlayer != null) SetCurrentTarget(targetPlayer);
                    break;
                    
                case WildRobotState.Stalk:
                    if (selectedAttack == null) SelectAttackForPositioning();
                    currentApproachSpeedMultiplier = selectedAttack?.approachSpeedMultiplier ?? 0.5f;
                    // En stalk, la IA controla la rotación (mira al objetivo mientras camina lento)
                    if (movement != null) movement.AutoRotate = false;
                    if (targetPlayer != null) SetCurrentTarget(targetPlayer);
                    break;
                    
                case WildRobotState.Circle:
                    if (selectedAttack == null) SelectAttackForPositioning();
                    currentApproachSpeedMultiplier = selectedAttack?.approachSpeedMultiplier ?? 1f;
                    strafeDirection = Random.value > 0.5f ? 1 : -1;
                    // En circle, la IA controla la rotación (strafe mirando al objetivo)
                    if (movement != null) movement.AutoRotate = false;
                    if (targetPlayer != null) SetCurrentTarget(targetPlayer);
                    break;
                    
                case WildRobotState.Positioning:
                    positioningTimer = 0f;
                    isWaitingToAttack = false;
                    waitBeforeAttackTimer = 0f;
                    // Dirección de strafe aleatoria inicial
                    strafeDirection = Random.value > 0.5f ? 1 : -1;
                    SelectAttackForPositioning();
                    currentApproachSpeedMultiplier = selectedAttack?.approachSpeedMultiplier ?? 1f;
                    // En positioning, la IA controla la rotación
                    if (movement != null) movement.AutoRotate = false;
                    if (targetPlayer != null) SetCurrentTarget(targetPlayer);
                    break;
                    
                case WildRobotState.Attack:
                    lastAttackHit = false;
                    // En attack, la IA controla la rotación (congelada)
                    if (movement != null) movement.AutoRotate = false;
                    if (targetPlayer != null) SetCurrentTarget(targetPlayer);
                    break;
                    
                case WildRobotState.Recovery:
                    lastExecutedAttack = selectedAttack;
                    stateTimer = 0f;
                    // En recovery, la IA controla la rotación
                    if (movement != null) movement.AutoRotate = false;
                    break;
                    
                case WildRobotState.Flee:
                    // En huida, permitir rotación automática
                    if (movement != null) movement.AutoRotate = true;
                    break;
                    
                case WildRobotState.Dead:
                    OnDeath();
                    break;
            }
        }
        
        private void OnExitState(WildRobotState state)
        {
            switch (state)
            {
                case WildRobotState.Alert:
                    StopAlertAnimation();
                    break;
                case WildRobotState.Positioning:
                    // No limpiar selectedAttack, Attack lo necesita
                    isWaitingToAttack = false;
                    break;
                case WildRobotState.Attack:
                    // El ataque terminó, pasar a recovery
                    break;
            }
        }
        
        #endregion
        
        #region State Updates
        
        private void UpdateIdle()
        {
            // Asegurar que no se mueve
            StopMoving();
            
            // Verificar si debe cambiar a otro estado
            if (CanSeePlayer())
            {
                HandlePlayerDetected();
            }
            else if (stateTimer > 3f)
            {
                // Después de un tiempo, empezar a patrullar
                ChangeState(WildRobotState.Patrol);
            }
        }
        
        private void UpdatePatrol()
        {
            // Verificar si ve al jugador
            if (CanSeePlayer())
            {
                HandlePlayerDetected();
                return;
            }
            
            // Si está esperando en un punto
            if (isWaitingAtPatrolPoint)
            {
                StopMoving();
                patrolWaitTimer += Time.deltaTime;
                
                if (patrolWaitTimer >= patrolWaitTime)
                {
                    // Terminó de esperar, buscar nuevo destino
                    isWaitingAtPatrolPoint = false;
                    hasPatrolDestination = false;
                }
                return;
            }
            
            // Si no tiene destino, generar uno nuevo
            if (!hasPatrolDestination)
            {
                patrolDestination = GetRandomPatrolPoint();
                hasPatrolDestination = true;
            }
            
            // Moverse hacia el destino
            float distanceToDestination = Vector3.Distance(transform.position, patrolDestination);
            
            if (distanceToDestination <= patrolArrivalDistance)
            {
                // Llegó al destino, esperar
                isWaitingAtPatrolPoint = true;
                patrolWaitTimer = 0f;
                StopMoving();
            }
            else
            {
                // Seguir caminando hacia el destino
                MoveTowards(patrolDestination);
                LookAtTarget(patrolDestination);
            }
        }
        
        /// <summary>
        /// Genera un punto aleatorio dentro del radio de patrulla.
        /// </summary>
        private Vector3 GetRandomPatrolPoint()
        {
            // Generar punto aleatorio en círculo
            Vector2 randomCircle = Random.insideUnitCircle * patrolRadius;
            Vector3 randomPoint = spawnPosition + new Vector3(randomCircle.x, 0f, randomCircle.y);
            
            // Intentar encontrar un punto válido en el NavMesh o terreno
            // Por ahora, usar el punto directamente (asumiendo terreno plano)
            // TODO: Usar NavMesh.SamplePosition si se implementa navegación
            
            return randomPoint;
        }
        
        private void UpdateAlert()
        {
            // Detenerse
            StopMoving();
            
            // Verificar si puede ver al jugador
            bool canSee = CanSeePlayer();
            
            if (canSee && targetPlayer != null)
            {
                // Girar hacia el jugador
                LookAtTarget(targetPlayer.position);
                
                // Incrementar el contador de alerta
                alertTimer += Time.deltaTime;
                
                // Iniciar animación de alerta si no está reproduciéndose
                if (!isAlertAnimationPlaying)
                {
                    StartAlertAnimation();
                }
                
                // Si el jugador ha estado visible el tiempo suficiente, pasar a Chase
                if (alertTimer >= alertDuration)
                {
                    StopAlertAnimation();
                    ChangeState(WildRobotState.Chase);
                }
            }
            else
            {
                // El jugador salió del campo de visión
                // El contador se PAUSA (no se resetea)
                // La animación sigue en loop pero podríamos cambiarla a "buscando"
                
                // Si pasa mucho tiempo sin ver al jugador, volver a Idle
                // (Opcional: agregar un timer secundario para esto)
            }
        }
        
        /// <summary>
        /// Inicia la animación de alerta en loop.
        /// </summary>
        private void StartAlertAnimation()
        {
            if (animator != null)
            {
                animator.SetBool("Alert", true);
            }
            isAlertAnimationPlaying = true;
        }
        
        /// <summary>
        /// Detiene la animación de alerta.
        /// </summary>
        private void StopAlertAnimation()
        {
            if (animator != null)
            {
                animator.SetBool("Alert", false);
            }
            isAlertAnimationPlaying = false;
        }
        
        private void UpdateChase()
        {
            if (targetPlayer == null)
            {
                FindPlayer();
                if (targetPlayer == null)
                {
                    ChangeState(WildRobotState.Idle);
                    return;
                }
            }
            
            float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);
            
            // Si está en rango de ataque, pasar a posicionamiento
            if (distanceToPlayer <= EffectiveAttackRange)
            {
                ChangeState(WildRobotState.Positioning);
                return;
            }
            
            // Si perdió al jugador
            if (!CanSeePlayer() && distanceToPlayer > wildData.detectionRadius * 1.5f)
            {
                ChangeState(WildRobotState.Idle);
                return;
            }
            
            // Moverse hacia el jugador
            MoveTowards(targetPlayer.position);
            LookAtTarget(targetPlayer.position);
        }
        
        private void UpdatePositioning()
        {
            if (targetPlayer == null)
            {
                ChangeState(WildRobotState.Idle);
                return;
            }
            
            float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);
            
            // Si el jugador se alejó mucho, volver a perseguir
            if (distanceToPlayer > EffectiveAttackRange * 1.5f)
            {
                ChangeState(WildRobotState.Chase);
                return;
            }
            
            // Si no hay ataque seleccionado o ya no es válido, re-seleccionar
            if (selectedAttack == null || !IsAttackStillValid(selectedAttackPart, selectedAttack))
            {
                SelectAttackForPositioning();
                
                if (selectedAttack == null)
                {
                    ChangeState(WildRobotState.Chase);
                    return;
                }
            }
            
            // PRIMERO: Verificar si ya estamos en posición para atacar
            // Esto evita que el approach behavior gire al robot justo antes de atacar
            bool inPosition = IsInAttackPosition(distanceToPlayer);
            
            if (inPosition)
            {
                // ¡Ya estamos en posición! No ejecutar approach, ir directo a atacar
                StopMoving(); // Detener movimiento
                
                // Si el ataque tiene tiempo de espera, esperar
                if (selectedAttack.HasWaitTime && !isWaitingToAttack)
                {
                    isWaitingToAttack = true;
                    targetWaitTime = selectedAttack.GetRandomWaitTime();
                    waitBeforeAttackTimer = 0f;
                }
                
                if (isWaitingToAttack)
                {
                    waitBeforeAttackTimer += Time.deltaTime;
                    
                    if (waitBeforeAttackTimer >= targetWaitTime)
                    {
                        ChangeState(WildRobotState.Attack);
                    }
                }
                else
                {
                    // Sin tiempo de espera, atacar inmediatamente
                    ChangeState(WildRobotState.Attack);
                }
                return; // No ejecutar approach behavior
            }
            
            // NO estamos en posición, ejecutar approach behavior para posicionarse
            
            // Verificar timeout de posicionamiento
            positioningTimer += Time.deltaTime;
            if (positioningTimer > positioningTimeout)
            {
                strafeDirection *= -1;
                positioningTimer = 0f;
                SelectAttackForPositioning();
            }
            
            // El ataque seleccionado dicta el comportamiento de aproximación
            switch (selectedAttack.approachBehavior)
            {
                case AttackApproachBehavior.Direct:
                    PerformDirectApproach(distanceToPlayer);
                    break;
                case AttackApproachBehavior.Strafe:
                    PerformStrafeApproach(distanceToPlayer);
                    break;
                case AttackApproachBehavior.Stalk:
                    PerformStalkApproach(distanceToPlayer);
                    break;
                case AttackApproachBehavior.Circle:
                    PerformCircleApproach(distanceToPlayer);
                    break;
                case AttackApproachBehavior.Ambush:
                    PerformAmbushApproach(distanceToPlayer);
                    break;
                case AttackApproachBehavior.Rush:
                    PerformRushApproach(distanceToPlayer);
                    break;
                case AttackApproachBehavior.Retreat:
                    PerformRetreatApproach(distanceToPlayer);
                    break;
            }
        }
        
        /// <summary>
        /// Estado: Acechar al objetivo (aproximación lenta, genera tensión).
        /// Usado independientemente del estado Positioning.
        /// </summary>
        private void UpdateStalk()
        {
            if (targetPlayer == null)
            {
                ChangeState(WildRobotState.Idle);
                return;
            }
            
            float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);
            
            // Si está muy lejos, perseguir
            if (distanceToPlayer > wildData.detectionRadius)
            {
                ChangeState(WildRobotState.Chase);
                return;
            }
            
            // Si está en rango ideal, posicionarse para atacar
            if (selectedAttack != null && selectedAttack.IsDistanceValid(distanceToPlayer))
            {
                ChangeState(WildRobotState.Positioning);
                return;
            }
            
            // Acechar: moverse lentamente hacia el objetivo
            PerformStalkApproach(distanceToPlayer);
        }
        
        /// <summary>
        /// Estado: Rodear al objetivo buscando una apertura.
        /// </summary>
        private void UpdateCircle()
        {
            if (targetPlayer == null)
            {
                ChangeState(WildRobotState.Idle);
                return;
            }
            
            float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);
            
            // Si se alejó mucho, perseguir
            if (distanceToPlayer > EffectiveAttackRange * 1.8f)
            {
                ChangeState(WildRobotState.Chase);
                return;
            }
            
            // Después de cierto tiempo, intentar atacar
            if (stateTimer > 3f)
            {
                ChangeState(WildRobotState.Positioning);
                return;
            }
            
            // Rodear: strafe puro manteniendo distancia
            PerformCircleApproach(distanceToPlayer);
        }
        
        private void UpdateAttack()
        {
            if (targetPlayer == null)
            {
                ChangeState(WildRobotState.Idle);
                return;
            }
            
            // Si el CombatController está atacando, esperar a que termine
            if (combatController != null && combatController.IsAttacking)
            {
                // Seguir mirando al objetivo si el ataque lo permite
                if (combatController.CanRotate)
                {
                    LookAtTarget(targetPlayer.position);
                }
                return;
            }
            
            // El ataque ya terminó o no se ha ejecutado
            // Verificar si el ataque seleccionado sigue siendo válido
            if (selectedAttack != null && IsAttackStillValid(selectedAttackPart, selectedAttack))
            {
                // Verificar condiciones para ejecutar
                bool zoneReady = selectedZone == null || selectedZone.IsTargetInZone;
                
                if (zoneReady)
                {
                    ExecuteSelectedAttack();
                    // Después de iniciar el ataque, pasar a Recovery para evaluar
                    ChangeState(WildRobotState.Recovery);
                    return;
                }
            }
            
            // Si no se pudo atacar, volver a posicionamiento
            ChangeState(WildRobotState.Positioning);
        }
        
        /// <summary>
        /// Estado: Recuperación después de un ataque.
        /// Evalúa el resultado y decide la siguiente acción basándose en el AttackData.
        /// </summary>
        private void UpdateRecovery()
        {
            if (targetPlayer == null)
            {
                ChangeState(WildRobotState.Idle);
                return;
            }
            
            // Esperar a que termine la animación del ataque
            if (combatController != null && combatController.IsAttacking)
            {
                // SIEMPRE detener movimiento mientras ataca
                StopMoving();
                
                // Rotación lenta hacia el objetivo (ya controlada por LookAtTarget)
                LookAtTarget(targetPlayer.position);
                return;
            }
            
            // Ataque terminó, aplicar pausa post-ataque
            float pauseTime = lastExecutedAttack?.postAttackPause ?? 0.5f;
            
            if (stateTimer < pauseTime)
            {
                StopMoving();
                LookAtTarget(targetPlayer.position);
                return;
            }
            
            // Decidir siguiente acción basándose en el resultado y el comportamiento configurado
            DecidePostAttackBehavior();
        }
        
        /// <summary>
        /// Decide qué hacer después de un ataque basándose en el resultado y la configuración.
        /// </summary>
        private void DecidePostAttackBehavior()
        {
            if (lastExecutedAttack == null)
            {
                ChangeState(WildRobotState.Positioning);
                return;
            }
            
            float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);
            
            // Si el jugador se alejó, perseguir
            if (distanceToPlayer > EffectiveAttackRange * 1.5f)
            {
                ChangeState(WildRobotState.Chase);
                return;
            }
            
            // Comportamiento basado en si el ataque impactó
            if (lastAttackHit)
            {
                // El ataque impactó, verificar si queremos encadenar
                if (lastExecutedAttack.chainOnHitChance > 0f && 
                    lastExecutedAttack.chainableAttacks != null && 
                    lastExecutedAttack.chainableAttacks.Length > 0)
                {
                    if (Random.value < lastExecutedAttack.chainOnHitChance)
                    {
                        // Seleccionar un ataque encadenable
                        SelectChainAttack();
                        ChangeState(WildRobotState.Attack);
                        return;
                    }
                }
            }
            
            // Aplicar comportamiento de recovery configurado
            switch (lastExecutedAttack.recoveryBehavior)
            {
                case AttackRecoveryBehavior.Hold:
                    // Mantener posición, preparar siguiente ataque
                    ChangeState(WildRobotState.Positioning);
                    break;
                    
                case AttackRecoveryBehavior.Retreat:
                    // Ya retrocedimos durante el recovery, ahora acechar
                    ChangeState(WildRobotState.Stalk);
                    break;
                    
                case AttackRecoveryBehavior.Chain:
                    // Intentar otro ataque inmediatamente
                    ChangeState(WildRobotState.Positioning);
                    break;
                    
                case AttackRecoveryBehavior.Reposition:
                    // Cambiar de posición completamente
                    strafeDirection *= -1;
                    ChangeState(WildRobotState.Circle);
                    break;
                    
                case AttackRecoveryBehavior.Stalk:
                    // Volver a acechar
                    ChangeState(WildRobotState.Stalk);
                    break;
                    
                default:
                    ChangeState(WildRobotState.Positioning);
                    break;
            }
        }
        
        /// <summary>
        /// Selecciona un ataque de la lista de ataques encadenables.
        /// </summary>
        private void SelectChainAttack()
        {
            if (lastExecutedAttack?.chainableAttacks == null || lastExecutedAttack.chainableAttacks.Length == 0)
                return;
            
            // Seleccionar uno aleatorio de los ataques encadenables
            int index = Random.Range(0, lastExecutedAttack.chainableAttacks.Length);
            AttackData chainAttack = lastExecutedAttack.chainableAttacks[index];
            
            if (chainAttack != null && selectedAttackPart != null)
            {
                // Verificar si la parte puede ejecutar este ataque
                foreach (var attack in selectedAttackPart.AvailableAttacks)
                {
                    if (attack == chainAttack)
                    {
                        selectedAttack = chainAttack;
                        selectedZone = selectedAttackPart.GetAttackZone(chainAttack.zoneId);
                        Debug.Log($"[WildRobot] Encadenando a: '{chainAttack.attackName}'");
                        return;
                    }
                }
            }
        }
        
        /// <summary>
        /// Ejecuta movimiento de retroceso.
        /// </summary>
        private void PerformRetreatMovement(float distance)
        {
            if (targetPlayer == null) return;
            
            float currentDistance = Vector3.Distance(transform.position, targetPlayer.position);
            float targetDistance = selectedAttack?.idealDistance ?? (EffectiveAttackRange * 0.8f);
            
            // Si ya estamos a la distancia deseada o más lejos, parar
            if (currentDistance >= targetDistance + distance)
            {
                StopMoving();
                return;
            }
            
            // Retroceder
            Vector3 retreatDir = (transform.position - targetPlayer.position).normalized;
            movement.SetMoveDirection(retreatDir * 0.5f); // Velocidad reducida
            LookAtTarget(targetPlayer.position);
        }

        private void UpdateFlee()
        {
            if (targetPlayer == null)
            {
                ChangeState(WildRobotState.Idle);
                return;
            }
            
            float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);
            
            // Si está lo suficientemente lejos, dejar de huir
            if (distanceToPlayer > wildData.detectionRadius * 2f)
            {
                ChangeState(WildRobotState.Idle);
                return;
            }
            
            // Calcular dirección de huida (opuesta al jugador)
            Vector3 fleeDirection = (transform.position - targetPlayer.position).normalized;
            Vector3 fleeTarget = transform.position + fleeDirection * 10f;
            
            MoveTowards(fleeTarget);
        }
        
        #endregion
        
        #region Detection
        
        private bool CanSeePlayer()
        {
            if (targetPlayer == null)
            {
                FindPlayer();
            }
            
            if (targetPlayer == null) return false;
            
            // Si está domesticado, no "ver" al dueño como amenaza
            if (isTamed && owner != null)
            {
                // Verificar si el jugador detectado es el dueño
                RobotCore playerCore = targetPlayer.GetComponentInParent<RobotCore>();
                if (playerCore == null)
                {
                    Robot playerRobot = targetPlayer.GetComponentInParent<Robot>();
                    if (playerRobot != null)
                    {
                        playerCore = playerRobot.Core;
                    }
                }
                
                if (playerCore == owner)
                {
                    return false; // No atacar al dueño
                }
            }
            
            // Usar VisionDetector para verificar campo de visión
            if (visionDetector != null)
            {
                return visionDetector.CanSeeTarget(targetPlayer);
            }
            
            // Fallback: detección por radio si no hay VisionDetector
            float distance = Vector3.Distance(transform.position, targetPlayer.position);
            return distance <= wildData.detectionRadius;
        }
        
        private void HandlePlayerDetected()
        {
            switch (wildData.behavior)
            {
                case WildRobotBehavior.Passive:
                    ChangeState(WildRobotState.Flee);
                    break;
                case WildRobotBehavior.Neutral:
                    ChangeState(WildRobotState.Alert);
                    break;
                case WildRobotBehavior.Aggressive:
                    ChangeState(WildRobotState.Chase);
                    break;
                case WildRobotBehavior.Territorial:
                    float distanceFromSpawn = Vector3.Distance(targetPlayer.position, spawnPosition);
                    if (distanceFromSpawn <= wildData.territoryRadius)
                    {
                        ChangeState(WildRobotState.Chase);
                    }
                    else
                    {
                        ChangeState(WildRobotState.Alert);
                    }
                    break;
            }
        }
        
        #endregion
        
        #region Movement
        
        private void MoveTowards(Vector3 target)
        {
            // No moverse si el ataque actual lo bloquea
            if (combatController != null && !combatController.CanMove)
            {
                movement.SetMoveDirection(Vector3.zero);
                return;
            }
            
            Vector3 direction = (target - transform.position);
            direction.y = 0; // Mantener en el plano horizontal
            
            if (direction.magnitude > 0.1f)
            {
                movement.SetMoveDirection(direction.normalized);
            }
            else
            {
                movement.SetMoveDirection(Vector3.zero);
            }
        }
        
        private void StopMoving()
        {
            movement.SetMoveDirection(Vector3.zero);
        }
        
        private void LookAtTarget(Vector3 target)
        {
            LookAtTarget(target, 1f);
        }
        
        private void LookAtTarget(Vector3 target, float speedMultiplier)
        {
            // Congelar rotación durante los estados de ataque y recuperación
            // Esto evita que el robot desalinee su hitbox
            if (currentState == WildRobotState.Attack || currentState == WildRobotState.Recovery)
            {
                return; // Congelado completamente
            }
            
            // También congelar si el CombatController está ejecutando un ataque
            if (combatController != null && combatController.IsAttacking)
            {
                return;
            }
            
            // Calcular dirección hacia el objetivo
            Vector3 direction = target - transform.position;
            direction.y = 0;
            
            if (direction.sqrMagnitude < 0.001f) return;
            
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            
            // Aplicar rotación con velocidad modificada
            float rotationSpeed = (movement != null ? 5f : 5f) * speedMultiplier;
            transform.rotation = Quaternion.Slerp(
                transform.rotation, 
                targetRotation, 
                rotationSpeed * Time.deltaTime
            );
        }
        
        #endregion
        
        #region Tamed States
        
        /// <summary>
        /// Obtiene la posición del dueño.
        /// </summary>
        private Vector3? GetOwnerPosition()
        {
            if (owner == null) return null;
            
            // Si el dueño está montado en este mecha, no seguirlo
            if (isBeingControlled) return null;
            
            // El dueño puede estar en su propio robot o montado en otro
            if (owner.CurrentRobot != null)
            {
                return owner.CurrentRobot.transform.position;
            }
            
            return null;
        }
        
        /// <summary>
        /// Estado: Siguiendo al dueño.
        /// </summary>
        private void UpdateTamedFollow()
        {
            Vector3? ownerPos = GetOwnerPosition();
            
            if (ownerPos == null)
            {
                // No hay dueño o está montado en nosotros, quedarse quieto
                StopMoving();
                return;
            }
            
            float distance = Vector3.Distance(transform.position, ownerPos.Value);
            
            if (distance > followMinDistance)
            {
                // Moverse hacia el dueño
                MoveTowards(ownerPos.Value);
                LookAtTarget(ownerPos.Value);
            }
            else
            {
                // Ya está cerca, detenerse
                StopMoving();
                LookAtTarget(ownerPos.Value);
            }
        }
        
        /// <summary>
        /// Estado: Quedarse quieto esperando órdenes.
        /// </summary>
        private void UpdateTamedStay()
        {
            // Simplemente quedarse quieto
            StopMoving();
            
            // Opcionalmente mirar al dueño si está cerca
            Vector3? ownerPos = GetOwnerPosition();
            if (ownerPos != null)
            {
                float distance = Vector3.Distance(transform.position, ownerPos.Value);
                if (distance < followStartDistance * 2f)
                {
                    LookAtTarget(ownerPos.Value);
                }
            }
        }
        
        /// <summary>
        /// Recibe una señal/silbido del dueño.
        /// Comportamiento contextual:
        /// - Si está lejos → viene hacia el dueño (TamedFollow)
        /// - Si está cerca y siguiendo → se queda (TamedStay)
        /// - Si está quieto → empieza a seguir (TamedFollow)
        /// </summary>
        public void ReceiveSignal()
        {
            if (!isTamed)
            {
                Debug.Log($"WildRobot '{wildData?.speciesName}': Señal ignorada (no domesticado)");
                return;
            }
            
            if (isBeingControlled)
            {
                Debug.Log($"WildRobot '{wildData?.speciesName}': Señal ignorada (siendo controlado)");
                return;
            }
            
            Vector3? ownerPos = GetOwnerPosition();
            if (ownerPos == null)
            {
                Debug.Log($"WildRobot '{wildData?.speciesName}': Señal ignorada (no hay dueño)");
                return;
            }
            
            float distance = Vector3.Distance(transform.position, ownerPos.Value);
            
            if (currentState == WildRobotState.TamedStay)
            {
                // Estaba quieto → empezar a seguir
                ChangeState(WildRobotState.TamedFollow);
                Debug.Log($"WildRobot '{wildData?.speciesName}': Señal recibida → Siguiendo");
            }
            else if (currentState == WildRobotState.TamedFollow && distance <= followStopDistance)
            {
                // Está cerca y siguiendo → quedarse
                ChangeState(WildRobotState.TamedStay);
                Debug.Log($"WildRobot '{wildData?.speciesName}': Señal recibida → Quieto");
            }
            else
            {
                // Está lejos o en otro estado → venir
                ChangeState(WildRobotState.TamedFollow);
                Debug.Log($"WildRobot '{wildData?.speciesName}': Señal recibida → Viniendo (dist: {distance:F1}m)");
            }
        }
        
        #endregion
        
        #region External Control (Mount System)
        
        /// <summary>
        /// Toma control del robot (para sistema de montura).
        /// Desactiva la IA y permite control manual.
        /// </summary>
        public void TakeControl()
        {
            isBeingControlled = true;
            StopMoving();
            ChangeState(WildRobotState.Idle);
            
            Debug.Log($"WildRobot '{wildData?.speciesName}': Control tomado por jugador");
        }
        
        /// <summary>
        /// Libera el control del robot.
        /// Reactiva la IA. Si está domesticado, entra en modo seguir.
        /// </summary>
        public void ReleaseControl()
        {
            isBeingControlled = false;
            StopMoving();
            
            // Si está domesticado, empezar a seguir al dueño
            if (isTamed)
            {
                ChangeState(WildRobotState.TamedFollow);
            }
            
            Debug.Log($"WildRobot '{wildData?.speciesName}': Control liberado");
        }
        
        #endregion
        
        #region Taming
        
        /// <summary>
        /// Domestica este robot, asignándolo a un jugador.
        /// </summary>
        public void Tame(RobotCore newOwner)
        {
            if (newOwner == null)
            {
                Debug.LogWarning("WildRobot: No se puede domesticar sin un dueño válido");
                return;
            }
            
            isTamed = true;
            owner = newOwner;
            
            // Al domesticar, detener cualquier comportamiento agresivo
            StopMoving();
            ChangeState(WildRobotState.Idle);
            
            Debug.Log($"WildRobot '{wildData?.speciesName}': Domesticado por {newOwner.name}");
        }
        
        /// <summary>
        /// Libera el robot (lo vuelve salvaje).
        /// </summary>
        public void Release()
        {
            if (!isTamed)
            {
                Debug.LogWarning("WildRobot: El robot ya es salvaje");
                return;
            }
            
            RobotCore previousOwner = owner;
            isTamed = false;
            owner = null;
            
            Debug.Log($"WildRobot '{wildData?.speciesName}': Liberado (era de {previousOwner?.name})");
        }
        
        #endregion
        
        #region Combat
        
        /// <summary>
        /// Intenta ejecutar el mejor ataque disponible basado en la situación.
        /// Prioriza ataques cuya zona de ataque contiene al jugador.
        /// </summary>
        private void TryPerformBestAttack(float distanceToTarget)
        {
            if (combatController == null || !combatController.CanAttack)
            {
                return;
            }
            
            // Obtener ataques que están listos (no en cooldown)
            var readyAttacks = combatController.GetReadyAttacks();
            
            if (readyAttacks.Count == 0)
            {
                // No hay ataques disponibles, usar fallback con wildData.attackSpeed
                if (Time.time - lastAttackTime >= 1f / wildData.attackSpeed)
                {
                    // Intentar ejecutar cualquier ataque (ignorando cooldown del CombatController)
                    var allAttacks = combatController.GetAllAvailableAttacks();
                    if (allAttacks.Count > 0)
                    {
                        var (part, attack) = SelectBestAttack(allAttacks, distanceToTarget);
                        if (part != null && attack != null)
                        {
                            combatController.TryExecuteAttack(part, attack);
                            lastAttackTime = Time.time;
                        }
                    }
                    else
                    {
                        // Sin CombatParts, usar sistema legacy
                        PerformLegacyAttack();
                        lastAttackTime = Time.time;
                    }
                }
                return;
            }
            
            // Filtrar por ataques viables (jugador en zona o no requiere zona)
            var viableAttacks = FilterViableAttacks(readyAttacks);
            
            if (viableAttacks.Count > 0)
            {
                // Hay ataques viables, seleccionar el mejor
                var (bestPart, bestAttack) = SelectBestAttack(viableAttacks, distanceToTarget);
                
                if (bestPart != null && bestAttack != null)
                {
                    combatController.TryExecuteAttack(bestPart, bestAttack);
                }
            }
            else
            {
                // No hay ataques viables - el jugador no está en ninguna zona
                // Reposicionarse girando hacia el jugador para poner zonas frontales en posición
                if (targetPlayer != null)
                {
                    LookAtTarget(targetPlayer.position);
                }
            }
        }
        
        /// <summary>
        /// Filtra ataques para quedarse solo con los viables.
        /// Un ataque es viable si:
        /// - No requiere zona (zoneId vacío)
        /// - O el jugador está dentro de su zona de ataque
        /// </summary>
        private System.Collections.Generic.List<(CombatPart part, AttackData attack)> FilterViableAttacks(
            System.Collections.Generic.List<(CombatPart part, AttackData attack)> attacks)
        {
            var viable = new System.Collections.Generic.List<(CombatPart part, AttackData attack)>();
            
            Debug.Log($"[WildRobot] FilterViableAttacks: Evaluando {attacks.Count} ataques");
            
            foreach (var (part, attack) in attacks)
            {
                if (part == null || attack == null) continue;
                
                bool isViable = part.IsAttackViable(attack);
                Debug.Log($"[WildRobot] - Ataque '{attack.attackName}' (zoneId: '{attack.zoneId}'): Viable = {isViable}");
                
                if (isViable)
                {
                    viable.Add((part, attack));
                }
            }
            
            Debug.Log($"[WildRobot] FilterViableAttacks: {viable.Count} ataques viables de {attacks.Count}");
            
            return viable;
        }
        
        /// <summary>
        /// Selecciona el mejor ataque basado en la situación actual.
        /// </summary>
        private (CombatPart part, AttackData attack) SelectBestAttack(
            System.Collections.Generic.List<(CombatPart part, AttackData attack)> attacks, 
            float distanceToTarget)
        {
            if (attacks == null || attacks.Count == 0)
            {
                return (null, null);
            }
            
            // Si solo hay un ataque, usarlo
            if (attacks.Count == 1)
            {
                return attacks[0];
            }
            
            // Evaluar cada ataque y seleccionar el mejor
            (CombatPart part, AttackData attack) bestAttack = (null, null);
            float bestScore = float.MinValue;
            
            foreach (var (part, attack) in attacks)
            {
                float score = EvaluateAttack(part, attack, distanceToTarget);
                
                if (score > bestScore)
                {
                    bestScore = score;
                    bestAttack = (part, attack);
                }
            }
            
            return bestAttack;
        }
        
        /// <summary>
        /// Evalúa qué tan bueno es un ataque para la situación actual.
        /// Mayor score = mejor opción.
        /// </summary>
        private float EvaluateAttack(CombatPart part, AttackData attack, float distanceToTarget)
        {
            float score = 0f;
            
            // Factor 1: Daño (más daño = mejor)
            float damage = part.CalculateDamage(attack);
            score += damage * 1f;
            
            // Factor 2: Cooldown corto es mejor para DPS sostenido
            // Ataques con cooldown muy largo penalizados ligeramente
            if (attack.cooldownTime > 0)
            {
                score -= attack.cooldownTime * 0.5f;
            }
            
            // Factor 3: Ataques de área son mejores si hay múltiples enemigos (futuro)
            if (attack.isAreaDamage)
            {
                score += 5f;
            }
            
            // Factor 4: Multiplicador de daño alto = ataque poderoso
            score += (attack.damageMultiplier - 1f) * 10f;
            
            // Factor 5: Algo de aleatoriedad para variedad
            score += Random.Range(-2f, 2f);
            
            return score;
        }
        
        /// <summary>
        /// Ataque legacy para robots sin CombatParts configurados.
        /// </summary>
        private void PerformLegacyAttack()
        {
            Debug.Log($"[WildRobot] '{wildData.speciesName}' ataca (legacy) por {wildData.attackDamage} de daño!");
            
            // TODO: Implementar daño directo si no hay sistema de hitbox
        }
        
        #region Positioning System
        
        /// <summary>
        /// Selecciona un ataque para el sistema de posicionamiento.
        /// Prioriza ataques listos con zonas definidas.
        /// </summary>
        private void SelectAttackForPositioning()
        {
            selectedAttackPart = null;
            selectedAttack = null;
            selectedZone = null;
            
            if (combatController == null) return;
            
            // Obtener ataques disponibles (no en cooldown)
            var readyAttacks = combatController.GetReadyAttacks();
            
            // Si no hay ataques listos, intentar con todos
            if (readyAttacks.Count == 0)
            {
                readyAttacks = combatController.GetAllAvailableAttacks();
            }
            
            if (readyAttacks.Count == 0) return;
            
            // Clasificar ataques
            var attacksWithZones = new System.Collections.Generic.List<(CombatPart part, AttackData attack, AttackZone zone)>();
            var attacksWithoutZones = new System.Collections.Generic.List<(CombatPart part, AttackData attack)>();
            
            foreach (var (part, attack) in readyAttacks)
            {
                if (part == null || attack == null) continue;
                
                AttackZone zone = null;
                
                // Primero: si la parte tiene zonas vinculadas, SIEMPRE usar una
                if (part.LinkedAttackZones != null && part.LinkedAttackZones.Count > 0)
                {
                    // Si el ataque especifica una zona específica, buscarla
                    if (attack.RequiresZone)
                    {
                        zone = part.GetAttackZone(attack.zoneId);
                    }
                    
                    // Si no encontró zona específica, usar la primera disponible
                    if (zone == null)
                    {
                        zone = part.LinkedAttackZones[0];
                    }
                }
                
                if (zone != null)
                {
                    attacksWithZones.Add((part, attack, zone));
                }
                else
                {
                    attacksWithoutZones.Add((part, attack));
                }
            }
            
            // Primero intentar ataques con zonas (podemos posicionarnos)
            if (attacksWithZones.Count > 0)
            {
                float bestScore = float.MinValue;
                float distance = targetPlayer != null ? Vector3.Distance(transform.position, targetPlayer.position) : 0f;
                
                foreach (var (part, attack, zone) in attacksWithZones)
                {
                    float score = EvaluateAttack(part, attack, distance);
                    
                    // Bonus si el target ya está en la zona - ALTA PRIORIDAD
                    if (zone.IsTargetInZone)
                    {
                        score += 100f;
                    }
                    
                    if (score > bestScore)
                    {
                        bestScore = score;
                        selectedAttackPart = part;
                        selectedAttack = attack;
                        selectedZone = zone;
                    }
                }
                
                if (selectedZone != null)
                {
                    DetermineStrafeDirection();
                }
            }
            // Si no hay ataques con zonas, usar uno sin zona
            else if (attacksWithoutZones.Count > 0)
            {
                float distance = targetPlayer != null ? Vector3.Distance(transform.position, targetPlayer.position) : 0f;
                var (part, attack) = SelectBestAttack(attacksWithoutZones, distance);
                selectedAttackPart = part;
                selectedAttack = attack;
                selectedZone = null;
            }
            
            if (selectedAttack != null)
            {
                Debug.Log($"[WildRobot] Ataque seleccionado: '{selectedAttack.attackName}' " +
                         $"(zona: {(selectedZone != null ? selectedZone.ZoneId : "NINGUNA")}, " +
                         $"parte: {selectedAttackPart?.name}, " +
                         $"zonas en parte: {selectedAttackPart?.LinkedAttackZones?.Count ?? 0})");
            }
            else
            {
                Debug.LogWarning($"[WildRobot] No se pudo seleccionar ningún ataque!");
            }
        }
        
        /// <summary>
        /// Determina la dirección de strafe de forma aleatoria.
        /// </summary>
        private void DetermineStrafeDirection()
        {
            // Dirección aleatoria: 1 = derecha (horario), -1 = izquierda (antihorario)
            strafeDirection = Random.value > 0.5f ? 1 : -1;
        }
        
        /// <summary>
        /// Verifica si un ataque sigue siendo válido (la parte existe y funciona).
        /// </summary>
        private bool IsAttackStillValid(CombatPart part, AttackData attack)
        {
            if (part == null || attack == null) return false;
            if (combatController == null) return false;
            
            // Verificar que la parte sigue existiendo en el CombatController
            bool partExists = false;
            foreach (var p in combatController.CombatParts)
            {
                if (p == part)
                {
                    partExists = true;
                    break;
                }
            }
            
            if (!partExists) return false;
            
            // Verificar que el ataque sigue disponible en la parte
            foreach (var a in part.AvailableAttacks)
            {
                if (a == attack) return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Verifica si estamos en posición válida para atacar según el AttackData seleccionado.
        /// Si hay zona configurada y el target está en ella, la zona tiene prioridad.
        /// Si no hay zona, usa la distancia ideal del AttackData.
        /// </summary>
        private bool IsInAttackPosition(float currentDistance)
        {
            if (selectedAttack == null) return false;
            
            // Si hay zona configurada, la zona tiene prioridad sobre la distancia
            bool hasZone = selectedZone != null;
            bool zoneReady = hasZone && selectedZone.IsTargetInZone;
            
            if (hasZone)
            {
                // Con zona: solo verificar que el target esté en la zona
                if (!zoneReady)
                {
                    return false;
                }
                // Target está en zona, continuar con otras verificaciones
            }
            else
            {
                // Sin zona: usar verificación de distancia
                if (!selectedAttack.IsDistanceValid(currentDistance))
                {
                    return false;
                }
            }
            
            // Verificar ángulo si es requerido
            if (selectedAttack.checkAngle && targetPlayer != null)
            {
                float angle = CalculateAngleToTarget();
                if (!selectedAttack.IsAngleValid(angle))
                    return false;
            }
            
            // Verificar si el target está mirando para ataques sorpresa
            if (selectedAttack.requiresTargetLookingAway && targetPlayer != null)
            {
                if (!selectedAttack.IsTargetLookingAway(transform, targetPlayer))
                    return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Calcula el ángulo relativo al objetivo (0 = frente del objetivo, 180 = espalda).
        /// </summary>
        private float CalculateAngleToTarget()
        {
            if (targetPlayer == null) return 0f;
            
            Vector3 toAttacker = (transform.position - targetPlayer.position).normalized;
            Vector3 targetForward = targetPlayer.forward;
            
            float dot = Vector3.Dot(targetForward, toAttacker);
            return Mathf.Acos(dot) * Mathf.Rad2Deg;
        }
        
        #region Approach Behaviors
        
        /// <summary>
        /// Aproximación directa: ir recto hacia el objetivo.
        /// </summary>
        private void PerformDirectApproach(float distanceToTarget)
        {
            if (targetPlayer == null) return;
            
            // No moverse si está atacando
            if (combatController != null && !combatController.CanMove)
            {
                StopMoving();
                LookAtTarget(targetPlayer.position);
                return;
            }
            
            float idealDist = selectedAttack?.idealDistance ?? EffectiveAttackRange * 0.8f;
            
            LookAtTarget(targetPlayer.position);
            
            if (distanceToTarget > idealDist + 0.5f)
            {
                MoveTowardsWithSpeed(targetPlayer.position, currentApproachSpeedMultiplier);
            }
            else if (distanceToTarget < idealDist - 0.5f)
            {
                // Muy cerca, retroceder un poco
                Vector3 retreatDir = (transform.position - targetPlayer.position).normalized;
                movement.SetMoveDirection(retreatDir * 0.5f);
            }
            else
            {
                StopMoving();
            }
        }
        
        /// <summary>
        /// Aproximación con strafe: rodear mientras se acerca.
        /// </summary>
        private void PerformStrafeApproach(float distanceToTarget)
        {
            if (targetPlayer == null) return;
            
            // No moverse si está atacando
            if (combatController != null && !combatController.CanMove)
            {
                StopMoving();
                LookAtTarget(targetPlayer.position);
                return;
            }
            
            float idealDist = selectedAttack?.idealDistance ?? EffectiveAttackRange * 0.8f;
            
            Vector3 toTarget = (targetPlayer.position - transform.position).normalized;
            toTarget.y = 0;
            
            Vector3 strafeDir = Vector3.Cross(Vector3.up, toTarget) * strafeDirection;
            
            Vector3 moveDirection;
            
            if (distanceToTarget > idealDist + 1f)
            {
                // Lejos: más hacia adelante
                moveDirection = (toTarget * 0.7f + strafeDir * 0.3f).normalized;
            }
            else if (distanceToTarget < idealDist - 1f)
            {
                // Cerca: retroceder con strafe
                moveDirection = (-toTarget * 0.3f + strafeDir * 0.7f).normalized;
            }
            else
            {
                // Distancia ideal: strafe puro
                moveDirection = strafeDir;
            }
            
            LookAtTarget(targetPlayer.position);
            movement.SetMoveDirection(moveDirection * currentApproachSpeedMultiplier);
        }
        
        /// <summary>
        /// Aproximación de acecho: lento y amenazante.
        /// </summary>
        private void PerformStalkApproach(float distanceToTarget)
        {
            if (targetPlayer == null) return;
            
            float idealDist = selectedAttack?.idealDistance ?? EffectiveAttackRange;
            
            LookAtTarget(targetPlayer.position);
            
            if (distanceToTarget > idealDist)
            {
                // Acercarse lentamente
                MoveTowardsWithSpeed(targetPlayer.position, currentApproachSpeedMultiplier * 0.5f);
            }
            else
            {
                // En posición, detenerse y observar
                StopMoving();
            }
        }
        
        /// <summary>
        /// Aproximación circular: rodear al objetivo manteniendo distancia.
        /// </summary>
        private void PerformCircleApproach(float distanceToTarget)
        {
            if (targetPlayer == null) return;
            
            // No moverse si está atacando
            if (combatController != null && !combatController.CanMove)
            {
                StopMoving();
                LookAtTarget(targetPlayer.position);
                return;
            }
            
            float idealDist = selectedAttack?.idealDistance ?? EffectiveAttackRange;
            
            Vector3 toTarget = (targetPlayer.position - transform.position).normalized;
            toTarget.y = 0;
            
            Vector3 strafeDir = Vector3.Cross(Vector3.up, toTarget) * strafeDirection;
            
            // Ajustar para mantener distancia constante
            Vector3 moveDirection;
            
            if (distanceToTarget > idealDist + 0.5f)
            {
                // Un poco lejos: acercarse mientras rodea
                moveDirection = (toTarget * 0.3f + strafeDir * 0.7f).normalized;
            }
            else if (distanceToTarget < idealDist - 0.5f)
            {
                // Un poco cerca: alejarse mientras rodea
                moveDirection = (-toTarget * 0.3f + strafeDir * 0.7f).normalized;
            }
            else
            {
                // Perfecto: strafe puro
                moveDirection = strafeDir;
            }
            
            LookAtTarget(targetPlayer.position);
            movement.SetMoveDirection(moveDirection * currentApproachSpeedMultiplier);
        }
        
        /// <summary>
        /// Aproximación de emboscada: quedarse quieto esperando.
        /// </summary>
        private void PerformAmbushApproach(float distanceToTarget)
        {
            if (targetPlayer == null) return;
            
            float idealDist = selectedAttack?.idealDistance ?? EffectiveAttackRange;
            
            // Quedarse quieto y observar
            StopMoving();
            LookAtTarget(targetPlayer.position);
            
            // Si el objetivo se acerca lo suficiente, el ataque se ejecutará
        }
        
        /// <summary>
        /// Aproximación de embestida: correr directo al objetivo.
        /// </summary>
        private void PerformRushApproach(float distanceToTarget)
        {
            if (targetPlayer == null) return;
            
            float idealDist = selectedAttack?.idealDistance ?? 2f;
            
            LookAtTarget(targetPlayer.position);
            
            if (distanceToTarget > idealDist)
            {
                // Correr hacia el objetivo a máxima velocidad
                MoveTowardsWithSpeed(targetPlayer.position, currentApproachSpeedMultiplier * 1.5f);
            }
            else
            {
                StopMoving();
            }
        }
        
        /// <summary>
        /// Aproximación de retroceso: alejarse del objetivo (para ataques a distancia).
        /// </summary>
        private void PerformRetreatApproach(float distanceToTarget)
        {
            if (targetPlayer == null) return;
            
            // No moverse si está atacando
            if (combatController != null && !combatController.CanMove)
            {
                StopMoving();
                LookAtTarget(targetPlayer.position);
                return;
            }
            
            float idealDist = selectedAttack?.idealDistance ?? EffectiveAttackRange * 1.5f;
            
            LookAtTarget(targetPlayer.position);
            
            if (distanceToTarget < idealDist)
            {
                // Alejarse del objetivo
                Vector3 retreatDir = (transform.position - targetPlayer.position).normalized;
                movement.SetMoveDirection(retreatDir * currentApproachSpeedMultiplier);
            }
            else
            {
                StopMoving();
            }
        }
        
        /// <summary>
        /// Mueve hacia un punto con velocidad modificada.
        /// </summary>
        private void MoveTowardsWithSpeed(Vector3 target, float speedMultiplier)
        {
            if (combatController != null && !combatController.CanMove)
            {
                movement.SetMoveDirection(Vector3.zero);
                return;
            }
            
            Vector3 direction = (target - transform.position);
            direction.y = 0;
            
            if (direction.magnitude > 0.1f)
            {
                movement.SetMoveDirection(direction.normalized * speedMultiplier);
            }
            else
            {
                movement.SetMoveDirection(Vector3.zero);
            }
        }
        
        #endregion
        
        /// <summary>
        /// Ejecuta movimiento de strafe para posicionar la zona sobre el objetivo.
        /// [LEGACY - usar PerformStrafeApproach]
        /// </summary>
        private void PerformPositioningStrafe(float distanceToTarget)
        {
            PerformStrafeApproach(distanceToTarget);
        }
        
        /// <summary>
        /// Ejecuta el ataque seleccionado previamente.
        /// </summary>
        private void ExecuteSelectedAttack()
        {
            if (selectedAttackPart == null || selectedAttack == null) return;
            if (combatController == null || !combatController.CanAttack) return;
            
            Debug.Log($"[WildRobot] Ejecutando ataque: '{selectedAttack.attackName}'");
            
            combatController.TryExecuteAttack(selectedAttackPart, selectedAttack);
            lastAttackTime = Time.time;
        }
        
        /// <summary>
        /// Registra si el último ataque impactó al objetivo.
        /// Llamar desde el sistema de daño cuando se confirma un hit.
        /// </summary>
        public void RegisterAttackHit()
        {
            lastAttackHit = true;
            Debug.Log($"[WildRobot] Ataque impactó!");
        }
        
        /// <summary>
        /// Registra que el último ataque falló.
        /// </summary>
        public void RegisterAttackMiss()
        {
            lastAttackHit = false;
            Debug.Log($"[WildRobot] Ataque falló!");
        }
        
        #endregion
        
        /// <summary>
        /// Recibe daño.
        /// </summary>
        public void TakeDamage(float damage)
        {
            if (!IsAlive) return;
            
            currentHealth -= damage;
            
            Debug.Log($"WildRobot '{wildData.speciesName}' recibió {damage} de daño. Salud: {currentHealth}/{MaxHealth}");
            
            if (currentHealth <= 0)
            {
                currentHealth = 0;
                ChangeState(WildRobotState.Dead);
            }
            else if (wildData.behavior == WildRobotBehavior.Neutral && currentState == WildRobotState.Idle)
            {
                // Neutral se vuelve agresivo si es atacado
                ChangeState(WildRobotState.Chase);
            }
        }
        
        private void OnDeath()
        {
            Debug.Log($"WildRobot '{wildData.speciesName}' ha sido derrotado!");
            
            // TODO: Generar loot
            // TODO: Efectos de muerte
            // TODO: Destruir o desactivar después de un tiempo
        }
        
        #endregion
        
        #region Debug
        
        private void OnDrawGizmosSelected()
        {
            if (!showGizmos || wildData == null) return;
            
            // Radio de detección (amarillo)
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, wildData.detectionRadius);
            
            // Radio de ataque efectivo (rojo) - usa CombatReach si está disponible
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            float attackRange = Application.isPlaying ? EffectiveAttackRange : wildData.attackRange;
            Gizmos.DrawWireSphere(transform.position, attackRange);
            
            // Territorio (azul) - solo si es territorial
            if (wildData.behavior == WildRobotBehavior.Territorial)
            {
                Gizmos.color = new Color(0f, 0.5f, 1f, 0.2f);
                Vector3 center = Application.isPlaying ? spawnPosition : transform.position;
                Gizmos.DrawWireSphere(center, wildData.territoryRadius);
            }
        }
        
        #endregion
    }
}
