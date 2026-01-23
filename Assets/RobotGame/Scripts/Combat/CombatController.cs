using System.Collections.Generic;
using UnityEngine;
using RobotGame.Components;
using RobotGame.Assembly;

namespace RobotGame.Combat
{
    /// <summary>
    /// Estado actual del sistema de combate.
    /// </summary>
    public enum CombatState
    {
        Idle,       // Listo para atacar
        WindUp,     // Preparando ataque (tell)
        Active,     // Hitbox activa, puede hacer daño
        Recovery    // Recuperándose, vulnerable
    }
    
    /// <summary>
    /// Controlador central de combate para un robot.
    /// 
    /// Funcionalidad:
    /// - Escanea el robot para encontrar CombatParts
    /// - Gestiona la ejecución de ataques (uno a la vez)
    /// - Controla timings (windup, active, recovery)
    /// - Activa/desactiva hitboxes de las armas
    /// 
    /// Los hitboxes están definidos en los prefabs de las armas como
    /// GameObjects con Collider (isTrigger=true) y componente WeaponHitbox.
    /// </summary>
    [RequireComponent(typeof(Robot))]
    public class CombatController : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private Robot robot;
        
        [Header("Modo de Hitbox")]
        [Tooltip("Si es true, usa Animation Events. Si es false, usa el sistema de porcentajes.")]
        [SerializeField] private bool useAnimationEvents = true;
        
        [Tooltip("Tiempo máximo de espera para Animation Events antes de usar fallback (segundos)")]
        [SerializeField] private float animationEventTimeout = 3f;
        
        [Header("Estado (Solo lectura)")]
        [SerializeField] private CombatState currentState = CombatState.Idle;
        [SerializeField] private float stateTimer = 0f;
        [SerializeField] private CombatPart currentAttackingPart;
        [SerializeField] private AttackData currentAttack;
        [SerializeField] private bool hitboxActivatedByEvent = false;
        
        [Header("Partes de Combate Detectadas")]
        [SerializeField] private List<CombatPart> combatParts = new List<CombatPart>();
        
        [Header("Debug")]
        [SerializeField] private bool logStateChanges = true;
        [SerializeField] private bool showAttackGizmos = true;
        [SerializeField] private bool trackReachDistance = true;
        
        // Sistema de ID de ataques para evitar daño múltiple
        private static int globalAttackIdCounter = 0;
        private int currentAttackId = -1;
        
        // Tracking de distancia máxima
        private float maxReachThisAttack = 0f;
        
        // Duración de la animación actual (detectada o fallback)
        private float currentAnimationDuration = 1f;
        
        // Control de Animation Events
        private bool waitingForHitboxStart = false;
        private bool waitingForHitboxEnd = false;
        private bool attackEndedByEvent = false;
        
        #region Properties
        
        public CombatState CurrentState => currentState;
        public bool IsAttacking => currentState != CombatState.Idle;
        public bool CanAttack => currentState == CombatState.Idle;
        public CombatPart CurrentAttackingPart => currentAttackingPart;
        public AttackData CurrentAttack => currentAttack;
        public IReadOnlyList<CombatPart> CombatParts => combatParts;
        
        /// <summary>
        /// Progreso del ataque actual (0-1). 0 si no está atacando.
        /// </summary>
        public float AttackProgress
        {
            get
            {
                if (currentAttack == null || currentState == CombatState.Idle)
                    return 0f;
                
                return Mathf.Clamp01(stateTimer / currentAnimationDuration);
            }
        }
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Se dispara cuando comienza un ataque. Params: parte, ataque
        /// </summary>
        public System.Action<CombatPart, AttackData> OnAttackStarted;
        
        /// <summary>
        /// Se dispara cuando la hitbox se activa (momento del impacto).
        /// </summary>
        public System.Action<CombatPart, AttackData> OnAttackActive;
        
        /// <summary>
        /// Se dispara cuando el ataque termina completamente.
        /// </summary>
        public System.Action<CombatPart, AttackData> OnAttackEnded;
        
        /// <summary>
        /// Se dispara cuando se golpea a un Damageable (DummyTarget, etc). Params: objetivo, daño
        /// </summary>
        public System.Action<Damageable, float> OnHitDamageable;
        
        /// <summary>
        /// Se dispara cuando se golpea una parte de robot. Params: parte, daño
        /// </summary>
        public System.Action<PartHealth, float> OnHitPart;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            if (robot == null)
            {
                robot = GetComponent<Robot>();
            }
        }
        
        private void Start()
        {
            // Escanear partes de combate iniciales
            RefreshCombatParts();
            
            // Suscribirse a eventos de estaciones de ensamblaje
            SubscribeToAssemblyStations();
        }
        
        private void Update()
        {
            if (currentState == CombatState.Idle) return;
            
            stateTimer += Time.deltaTime;
            
            switch (currentState)
            {
                case CombatState.WindUp:
                    UpdateWindUp();
                    break;
                    
                case CombatState.Active:
                    UpdateActive();
                    break;
                    
                case CombatState.Recovery:
                    UpdateRecovery();
                    break;
            }
        }
        
        private void OnDestroy()
        {
            // Desuscribirse de eventos de estaciones
            UnsubscribeFromAssemblyStations();
            
            // Desuscribirse de eventos de hitbox
            if (currentAttackingPart != null)
            {
                currentAttackingPart.UnsubscribeFromHitEvents(HandleHitPart, HandleHitDamageable);
            }
        }
        
        #endregion
        
        #region Assembly Station Events
        
        private void SubscribeToAssemblyStations()
        {
            var stations = FindObjectsOfType<UnifiedAssemblyStation>();
            foreach (var station in stations)
            {
                station.OnEditModeEnded += OnAssemblyEditEnded;
            }
        }
        
        private void UnsubscribeFromAssemblyStations()
        {
            var stations = FindObjectsOfType<UnifiedAssemblyStation>();
            foreach (var station in stations)
            {
                station.OnEditModeEnded -= OnAssemblyEditEnded;
            }
        }
        
        private void OnAssemblyEditEnded(UnifiedAssemblyStation station)
        {
            // Refrescar partes de combate cuando termina la edición
            Invoke(nameof(RefreshCombatParts), 0.1f);
            
            Debug.Log("[CombatController] Modo edición terminado - Refrescando partes de combate...");
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Escanea el robot buscando todas las CombatParts.
        /// Llamar después de modificar la estructura del robot.
        /// </summary>
        public void RefreshCombatParts()
        {
            combatParts.Clear();
            
            if (robot == null) return;
            
            // Buscar en todas las partes estructurales
            var structuralParts = robot.GetAllStructuralParts();
            
            foreach (var part in structuralParts)
            {
                CombatPart combatPart = part.GetComponent<CombatPart>();
                if (combatPart != null && combatPart.CanAttack)
                {
                    combatParts.Add(combatPart);
                    
                    // Asegurar que los hitboxes estén detectados
                    if (!combatPart.HasHitboxes)
                    {
                        combatPart.DetectHitboxes();
                    }
                    
                    // Asegurar que AttackAnimationEvents esté configurado
                    combatPart.SetupAnimationEvents();
                }
            }
            
            Debug.Log($"[CombatController] Refresh: Encontradas {combatParts.Count} partes de combate");
        }
        
        /// <summary>
        /// Intenta ejecutar un ataque con una parte y ataque específicos.
        /// </summary>
        public bool TryExecuteAttack(CombatPart part, AttackData attack)
        {
            if (!CanAttack)
            {
                Debug.LogWarning($"[CombatController] No puede atacar - Estado actual: {currentState}");
                return false;
            }
            
            if (part == null || attack == null)
            {
                Debug.LogWarning("[CombatController] Parte o ataque null");
                return false;
            }
            
            if (!part.CanExecuteAttack(attack))
            {
                Debug.LogWarning($"[CombatController] La parte {part.name} no puede ejecutar {attack.attackName}");
                return false;
            }
            
            if (!part.HasHitboxes)
            {
                Debug.LogWarning($"[CombatController] La parte {part.name} no tiene hitboxes configurados");
                return false;
            }
            
            StartAttack(part, attack);
            return true;
        }
        
        /// <summary>
        /// Intenta ejecutar el ataque por defecto de una parte.
        /// </summary>
        public bool TryExecuteDefaultAttack(CombatPart part)
        {
            if (part == null) return false;
            
            AttackData defaultAttack = part.GetDefaultAttack();
            return TryExecuteAttack(part, defaultAttack);
        }
        
        /// <summary>
        /// Ejecuta el primer ataque disponible de la primera parte disponible.
        /// </summary>
        public bool TryExecuteAnyAttack()
        {
            foreach (var part in combatParts)
            {
                if (part != null && part.CanAttack && part.HasHitboxes)
                {
                    return TryExecuteDefaultAttack(part);
                }
            }
            
            Debug.LogWarning("[CombatController] No hay partes de combate con hitboxes disponibles");
            return false;
        }
        
        /// <summary>
        /// Cancela el ataque actual inmediatamente.
        /// </summary>
        public void CancelAttack()
        {
            if (currentState == CombatState.Idle) return;
            
            if (logStateChanges)
            {
                Debug.Log("[CombatController] Ataque cancelado");
            }
            
            EndAttack();
        }
        
        /// <summary>
        /// Obtiene todos los ataques disponibles de todas las partes.
        /// </summary>
        public List<(CombatPart part, AttackData attack)> GetAllAvailableAttacks()
        {
            var allAttacks = new List<(CombatPart, AttackData)>();
            
            foreach (var part in combatParts)
            {
                if (part == null || !part.HasHitboxes) continue;
                
                foreach (var attack in part.AvailableAttacks)
                {
                    if (attack != null)
                    {
                        allAttacks.Add((part, attack));
                    }
                }
            }
            
            return allAttacks;
        }
        
        #endregion
        
        #region Private Methods - Attack Flow
        
        private void StartAttack(CombatPart part, AttackData attack)
        {
            currentAttackingPart = part;
            currentAttack = attack;
            stateTimer = 0f;
            
            // Generar nuevo ID de ataque
            currentAttackId = ++globalAttackIdCounter;
            
            // Resetear tracking de distancia
            maxReachThisAttack = 0f;
            
            // Inicializar flags de Animation Events
            waitingForHitboxStart = useAnimationEvents;
            waitingForHitboxEnd = false;
            attackEndedByEvent = false;
            hitboxActivatedByEvent = false;
            
            // Detectar duración de la animación
            currentAnimationDuration = DetectAnimationDuration(part, attack);
            
            // Suscribirse a eventos de hit
            part.SubscribeToHitEvents(HandleHitPart, HandleHitDamageable);
            
            ChangeState(CombatState.WindUp);
            
            OnAttackStarted?.Invoke(part, attack);
            
            // Disparar animación
            TriggerAttackAnimation(part, attack);
            
            if (logStateChanges)
            {
                float damage = part.CalculateDamage(attack);
                string modeStr = useAnimationEvents ? "Animation Events" : "Porcentual";
                
                Debug.Log($"[CombatController] Ataque iniciado: {attack.attackName} con {part.name} " +
                         $"(Daño: {damage:F1}, Duración: {currentAnimationDuration:F2}s, Modo: {modeStr}, AttackId: {currentAttackId})");
                
                if (!useAnimationEvents)
                {
                    float windupTime = currentAnimationDuration * attack.windupPercent;
                    float activeTime = currentAnimationDuration * attack.activePercent;
                    float recoveryTime = currentAnimationDuration * attack.RecoveryPercent;
                    
                    Debug.Log($"[CombatController] Timings: WindUp={windupTime:F2}s ({attack.windupPercent*100:F0}%), " +
                             $"Active={activeTime:F2}s ({attack.activePercent*100:F0}%), " +
                             $"Recovery={recoveryTime:F2}s ({attack.RecoveryPercent*100:F0}%)");
                }
            }
        }
        
        /// <summary>
        /// Detecta la duración de la animación de ataque desde el Animator.
        /// </summary>
        private float DetectAnimationDuration(CombatPart part, AttackData attack)
        {
            Animator animator = null;
            
            // Buscar Animator en StructuralPart
            if (part.StructuralPart != null && part.StructuralPart.Animator != null)
            {
                animator = part.StructuralPart.Animator;
            }
            else
            {
                // Buscar en padres
                animator = part.GetComponentInParent<Animator>();
            }
            
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                Debug.LogWarning($"[CombatController] No se encontró Animator, usando duración fallback: {attack.animationDuration}s");
                return attack.animationDuration;
            }
            
            // Buscar el clip de animación por nombre del trigger
            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            
            foreach (var clip in clips)
            {
                // Buscar clip que contenga el nombre del trigger (flexible)
                if (clip.name.Contains(attack.mainAnimationTrigger) || 
                    attack.mainAnimationTrigger.Contains(clip.name) ||
                    clip.name.ToLower().Contains("attack"))
                {
                    Debug.Log($"[CombatController] Duración detectada del clip '{clip.name}': {clip.length}s");
                    return clip.length;
                }
            }
            
            // Si no encontramos el clip específico, buscar cualquier clip con "attack" en el nombre
            foreach (var clip in clips)
            {
                if (clip.name.ToLower().Contains("attack"))
                {
                    Debug.Log($"[CombatController] Duración detectada (fallback) del clip '{clip.name}': {clip.length}s");
                    return clip.length;
                }
            }
            
            Debug.LogWarning($"[CombatController] No se encontró clip de ataque, usando duración fallback: {attack.animationDuration}s");
            return attack.animationDuration;
        }
        
        private void TriggerAttackAnimation(CombatPart part, AttackData attack)
        {
            bool animationTriggered = false;
            
            // Intentar disparar animación en la StructuralPart
            if (part.StructuralPart != null && part.StructuralPart.Animator != null)
            {
                part.StructuralPart.Animator.SetTrigger(attack.mainAnimationTrigger);
                animationTriggered = true;
                Debug.Log($"[CombatController] Animación disparada en StructuralPart: {part.StructuralPart.name}");
            }
            else
            {
                // Buscar Animator en los padres
                Animator parentAnimator = part.GetComponentInParent<Animator>();
                if (parentAnimator != null)
                {
                    parentAnimator.SetTrigger(attack.mainAnimationTrigger);
                    animationTriggered = true;
                    Debug.Log($"[CombatController] Animación disparada en padre: {parentAnimator.gameObject.name}");
                }
            }
            
            if (!animationTriggered)
            {
                Debug.LogWarning($"[CombatController] NO SE ENCONTRÓ ANIMATOR para {part.name}");
            }
        }
        
        private void UpdateWindUp()
        {
            // Trackear distancia durante toda la animación
            if (trackReachDistance && currentAttackingPart != null)
            {
                TrackMaxReach();
            }
            
            if (useAnimationEvents)
            {
                // Modo Animation Events: esperar OnAnimationHitboxStart()
                // Timeout de seguridad por si el evento no llega
                if (stateTimer >= animationEventTimeout)
                {
                    Debug.LogWarning($"[CombatController] Timeout esperando HitboxStart event. Usando fallback.");
                    waitingForHitboxStart = false;
                    ActivateHitboxes();
                    ChangeState(CombatState.Active);
                }
                // El cambio de estado ocurre en OnAnimationHitboxStart()
            }
            else
            {
                // Modo porcentual (fallback)
                float windupTime = currentAnimationDuration * currentAttack.windupPercent;
                if (stateTimer >= windupTime)
                {
                    ActivateHitboxes();
                    ChangeState(CombatState.Active);
                }
            }
        }
        
        private void UpdateActive()
        {
            // Trackear distancia máxima durante el ataque
            if (trackReachDistance && currentAttackingPart != null)
            {
                TrackMaxReach();
            }
            
            // Los hitboxes detectan colisiones automáticamente via OnTriggerEnter
            
            if (useAnimationEvents)
            {
                // Modo Animation Events: esperar OnAnimationHitboxEnd()
                // Timeout de seguridad
                if (stateTimer >= animationEventTimeout)
                {
                    Debug.LogWarning($"[CombatController] Timeout esperando HitboxEnd event. Usando fallback.");
                    waitingForHitboxEnd = false;
                    DeactivateHitboxes();
                    ChangeState(CombatState.Recovery);
                }
                // El cambio de estado ocurre en OnAnimationHitboxEnd()
            }
            else
            {
                // Modo porcentual (fallback)
                float windupTime = currentAnimationDuration * currentAttack.windupPercent;
                float activeTime = currentAnimationDuration * currentAttack.activePercent;
                float activeEndTime = windupTime + activeTime;
                
                if (stateTimer >= activeEndTime)
                {
                    DeactivateHitboxes();
                    ChangeState(CombatState.Recovery);
                }
            }
        }
        
        private void UpdateRecovery()
        {
            // Seguir trackeando por si la animación continúa
            if (trackReachDistance && currentAttackingPart != null)
            {
                TrackMaxReach();
            }
            
            if (useAnimationEvents)
            {
                // Modo Animation Events: esperar OnAnimationAttackEnd() o timeout
                // Si el evento ya lo terminó, no hacemos nada (EndAttack ya fue llamado)
                if (attackEndedByEvent)
                {
                    return;
                }
                
                // Timeout de seguridad - usar duración detectada de la animación
                if (stateTimer >= currentAnimationDuration + 0.1f)
                {
                    if (logStateChanges)
                    {
                        Debug.Log($"[CombatController] Animación completada (duración: {currentAnimationDuration}s). Finalizando ataque.");
                    }
                    EndAttack();
                }
            }
            else
            {
                // Modo porcentual (fallback)
                if (stateTimer >= currentAnimationDuration)
                {
                    EndAttack();
                }
            }
        }
        
        private void EndAttack()
        {
            var endedPart = currentAttackingPart;
            var endedAttack = currentAttack;
            
            // Log de distancia máxima alcanzada
            if (trackReachDistance && endedAttack != null)
            {
                Debug.Log($"<color=cyan>[REACH TRACKING] Ataque '{endedAttack.attackName}' → Distancia máxima: {maxReachThisAttack:F2}m</color>");
            }
            
            // Desactivar hitboxes por si acaso
            DeactivateHitboxes();
            
            // Desuscribirse de eventos de hit
            if (endedPart != null)
            {
                endedPart.UnsubscribeFromHitEvents(HandleHitPart, HandleHitDamageable);
            }
            
            currentAttackingPart = null;
            currentAttack = null;
            stateTimer = 0f;
            currentAttackId = -1;
            
            // Resetear flags de Animation Events
            waitingForHitboxStart = false;
            waitingForHitboxEnd = false;
            attackEndedByEvent = false;
            hitboxActivatedByEvent = false;
            
            ChangeState(CombatState.Idle);
            
            if (endedAttack != null)
            {
                OnAttackEnded?.Invoke(endedPart, endedAttack);
            }
        }
        
        #endregion
        
        #region Animation Event Receivers
        
        /// <summary>
        /// Llamado por AttackAnimationEvents cuando la animación indica que la hitbox debe activarse.
        /// </summary>
        public void OnAnimationHitboxStart()
        {
            if (currentState != CombatState.WindUp && currentState != CombatState.Idle)
            {
                if (logStateChanges)
                {
                    Debug.LogWarning($"[CombatController] OnAnimationHitboxStart ignorado - Estado actual: {currentState}");
                }
                return;
            }
            
            if (!waitingForHitboxStart && currentState == CombatState.Idle)
            {
                if (logStateChanges)
                {
                    Debug.LogWarning("[CombatController] OnAnimationHitboxStart ignorado - No hay ataque en curso");
                }
                return;
            }
            
            if (logStateChanges)
            {
                Debug.Log("<color=green>[CombatController] Animation Event: HitboxStart recibido</color>");
            }
            
            waitingForHitboxStart = false;
            waitingForHitboxEnd = true;
            hitboxActivatedByEvent = true;
            
            ActivateHitboxes();
            ChangeState(CombatState.Active);
        }
        
        /// <summary>
        /// Llamado por AttackAnimationEvents cuando la animación indica que la hitbox debe desactivarse.
        /// </summary>
        public void OnAnimationHitboxEnd()
        {
            if (currentState != CombatState.Active)
            {
                if (logStateChanges)
                {
                    Debug.LogWarning($"[CombatController] OnAnimationHitboxEnd ignorado - Estado actual: {currentState}");
                }
                return;
            }
            
            if (logStateChanges)
            {
                Debug.Log("<color=yellow>[CombatController] Animation Event: HitboxEnd recibido</color>");
            }
            
            waitingForHitboxEnd = false;
            
            DeactivateHitboxes();
            ChangeState(CombatState.Recovery);
        }
        
        /// <summary>
        /// Llamado por AttackAnimationEvents cuando la animación indica que el ataque terminó.
        /// </summary>
        public void OnAnimationAttackEnd()
        {
            if (currentState == CombatState.Idle)
            {
                return;
            }
            
            if (logStateChanges)
            {
                Debug.Log("<color=red>[CombatController] Animation Event: AttackEnd recibido</color>");
            }
            
            attackEndedByEvent = true;
            EndAttack();
        }
        
        #endregion
        
        #region Private Methods - State Machine
        
        private void ChangeState(CombatState newState)
        {
            if (logStateChanges && currentState != newState)
            {
                Debug.Log($"[CombatController] Estado: {currentState} → {newState}");
            }
            
            currentState = newState;
        }
        
        /// <summary>
        /// Trackea la distancia máxima desde el centro del robot hasta los hitboxes.
        /// Funciona incluso si los hitboxes están desactivados.
        /// </summary>
        private void TrackMaxReach()
        {
            if (robot == null || currentAttackingPart == null) return;
            
            Vector3 robotCenter = robot.transform.position;
            
            // Trackear posición de cada hitbox (incluso si está desactivado)
            foreach (var hitbox in currentAttackingPart.WeaponHitboxes)
            {
                if (hitbox == null) continue;
                
                float distance = Vector3.Distance(robotCenter, hitbox.transform.position);
                
                if (distance > maxReachThisAttack)
                {
                    maxReachThisAttack = distance;
                }
            }
            
            // Si no hay hitboxes, usar la posición del CombatPart
            if (currentAttackingPart.WeaponHitboxes.Count == 0)
            {
                float distance = Vector3.Distance(robotCenter, currentAttackingPart.transform.position);
                if (distance > maxReachThisAttack)
                {
                    maxReachThisAttack = distance;
                }
            }
        }
        
        #endregion
        
        #region Private Methods - Hitbox Control
        
        private void ActivateHitboxes()
        {
            if (currentAttackingPart == null || currentAttack == null)
            {
                Debug.LogError("[CombatController] ActivateHitboxes: currentAttackingPart o currentAttack es null!");
                return;
            }
            
            float damage = currentAttackingPart.CalculateDamage(currentAttack);
            
            currentAttackingPart.ActivateHitboxes(damage, robot.gameObject, currentAttackId);
            
            OnAttackActive?.Invoke(currentAttackingPart, currentAttack);
            
            if (logStateChanges)
            {
                Debug.Log($"[CombatController] Hitboxes ACTIVADOS - Daño: {damage}, AttackId: {currentAttackId}");
            }
        }
        
        private void DeactivateHitboxes()
        {
            if (currentAttackingPart != null)
            {
                currentAttackingPart.DeactivateHitboxes();
                
                if (logStateChanges)
                {
                    Debug.Log("[CombatController] Hitboxes DESACTIVADOS");
                }
            }
        }
        
        #endregion
        
        #region Event Handlers
        
        private void HandleHitPart(PartHealth part, float damage)
        {
            OnHitPart?.Invoke(part, damage);
            
            if (logStateChanges)
            {
                Debug.Log($"[CombatController] HIT: {part.gameObject.name} por {damage:F1} daño");
            }
        }
        
        private void HandleHitDamageable(Damageable target, float damage)
        {
            OnHitDamageable?.Invoke(target, damage);
            
            if (logStateChanges)
            {
                Debug.Log($"[CombatController] HIT: {target.gameObject.name} por {damage:F1} daño");
            }
        }
        
        #endregion
        
        #region Debug
        
        private void OnDrawGizmos()
        {
            if (!showAttackGizmos) return;
            
            // Mostrar estado durante ataque
            if (currentState != CombatState.Idle && currentAttackingPart != null)
            {
                switch (currentState)
                {
                    case CombatState.WindUp:
                        Gizmos.color = Color.yellow;
                        break;
                    case CombatState.Active:
                        Gizmos.color = Color.red;
                        break;
                    case CombatState.Recovery:
                        Gizmos.color = Color.blue;
                        break;
                }
                
                // Dibujar indicador sobre la parte atacante
                Vector3 pos = currentAttackingPart.transform.position + Vector3.up * 0.5f;
                Gizmos.DrawWireSphere(pos, 0.1f);
            }
        }
        
        #endregion
    }
}
