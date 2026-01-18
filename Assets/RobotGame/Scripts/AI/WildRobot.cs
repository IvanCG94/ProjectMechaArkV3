using UnityEngine;
using RobotGame.Components;
using RobotGame.Control;
using RobotGame.Data;
using RobotGame.Enums;

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
        Chase,          // Persiguiendo al jugador
        Attack,         // Atacando
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
        
        [Header("Estado (Solo lectura)")]
        [SerializeField] private WildRobotState currentState = WildRobotState.Idle;
        [SerializeField] private float currentHealth;
        [SerializeField] private Transform targetPlayer;
        
        [Header("Debug")]
        [SerializeField] private bool showGizmos = true;
        
        // Referencias
        private Robot robot;
        private RobotMovement movement;
        
        // Estado interno
        private Vector3 spawnPosition;
        private float lastAttackTime;
        private float stateTimer;
        
        // Control externo (para montura)
        private bool isBeingControlled = false;
        
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
            
            // Si no existe el componente de movimiento, crearlo
            if (movement == null)
            {
                movement = gameObject.AddComponent<RobotMovement>();
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
            }
            
            // Buscar al jugador
            FindPlayer();
        }
        
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
                case WildRobotState.Attack:
                    UpdateAttack();
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
            // Buscar el Core del jugador
            var playerCore = FindObjectOfType<RobotCore>();
            if (playerCore != null && playerCore.IsPlayerCore && playerCore.CurrentRobot != null)
            {
                targetPlayer = playerCore.CurrentRobot.transform;
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
                case WildRobotState.Alert:
                    // Podría reproducir sonido de alerta
                    break;
                case WildRobotState.Dead:
                    OnDeath();
                    break;
            }
        }
        
        private void OnExitState(WildRobotState state)
        {
            // Limpiar estado anterior si es necesario
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
            // Por ahora, solo verificar si ve al jugador
            if (CanSeePlayer())
            {
                HandlePlayerDetected();
            }
            
            // TODO: Implementar movimiento de patrulla
        }
        
        private void UpdateAlert()
        {
            // Detenerse y girar hacia el jugador
            StopMoving();
            
            if (targetPlayer != null)
            {
                LookAtTarget(targetPlayer.position);
            }
            
            // Después de un momento, decidir qué hacer
            if (stateTimer > 1f)
            {
                if (wildData.behavior == WildRobotBehavior.Passive)
                {
                    ChangeState(WildRobotState.Flee);
                }
                else if (CanSeePlayer())
                {
                    ChangeState(WildRobotState.Chase);
                }
                else
                {
                    ChangeState(WildRobotState.Idle);
                }
            }
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
            
            // Si está en rango de ataque
            if (distanceToPlayer <= wildData.attackRange)
            {
                ChangeState(WildRobotState.Attack);
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
        
        private void UpdateAttack()
        {
            if (targetPlayer == null)
            {
                ChangeState(WildRobotState.Idle);
                return;
            }
            
            float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);
            
            // Si el jugador se alejó, perseguir
            if (distanceToPlayer > wildData.attackRange * 1.2f)
            {
                ChangeState(WildRobotState.Chase);
                return;
            }
            
            // Detenerse y mirar al jugador
            StopMoving();
            LookAtTarget(targetPlayer.position);
            
            // Atacar si es momento
            if (Time.time - lastAttackTime >= 1f / wildData.attackSpeed)
            {
                PerformAttack();
                lastAttackTime = Time.time;
            }
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
            movement.RotateTowards(target);
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
        
        private void PerformAttack()
        {
            Debug.Log($"WildRobot '{wildData.speciesName}' ataca por {wildData.attackDamage} de daño!");
            
            // TODO: Implementar sistema de daño real
            // Por ahora solo es un placeholder
        }
        
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
            
            // Radio de ataque (rojo)
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, wildData.attackRange);
            
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
