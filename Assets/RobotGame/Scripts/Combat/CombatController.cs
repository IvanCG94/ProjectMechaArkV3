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
    /// - Activa/desactiva hitboxes
    /// - Notifica a otras partes para animaciones de acompañamiento
    /// 
    /// Uso:
    /// - Para jugador: Input llama a TryExecuteAttack()
    /// - Para IA: WildRobot decide y llama a TryExecuteAttack()
    /// </summary>
    [RequireComponent(typeof(Robot))]
    public class CombatController : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private Robot robot;
        
        [Header("Configuración")]
        [Tooltip("Layer de objetivos que pueden recibir daño")]
        [SerializeField] private LayerMask targetLayers = ~0;
        
        [Header("Estado (Solo lectura)")]
        [SerializeField] private CombatState currentState = CombatState.Idle;
        [SerializeField] private float stateTimer = 0f;
        [SerializeField] private CombatPart currentAttackingPart;
        [SerializeField] private AttackData currentAttack;
        
        [Header("Partes de Combate Detectadas")]
        [SerializeField] private List<CombatPart> combatParts = new List<CombatPart>();
        
        [Header("Debug")]
        [SerializeField] private bool logStateChanges = true;
        [SerializeField] private bool showAttackGizmos = true;
        [SerializeField] private bool showHitboxInGame = true;
        
        // Hitbox temporal para ataques
        private AttackHitbox activeHitbox;
        private GameObject hitboxObject;
        private GameObject hitboxVisual;
        
        // Sistema de ID de ataques para evitar daño múltiple
        private static int globalAttackIdCounter = 0;
        private int currentAttackId = -1;
        
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
                
                float elapsed = GetElapsedTimeInCurrentAttack();
                return Mathf.Clamp01(elapsed / currentAttack.TotalDuration);
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
            
            CreateHitboxObject();
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
            if (hitboxObject != null)
            {
                Destroy(hitboxObject);
            }
            
            if (hitboxVisual != null)
            {
                Destroy(hitboxVisual);
            }
            
            // Desuscribirse de eventos de estaciones
            UnsubscribeFromAssemblyStations();
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
            // Usar un pequeño delay para asegurar que las partes estén actualizadas
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
                }
            }
            
            Debug.Log($"[CombatController] Refresh: Encontradas {combatParts.Count} partes de combate");
        }
        
        /// <summary>
        /// Intenta ejecutar un ataque con una parte y ataque específicos.
        /// </summary>
        /// <returns>True si el ataque comenzó</returns>
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
        /// Útil para testing o input simple.
        /// </summary>
        public bool TryExecuteAnyAttack()
        {
            if (!CanAttack || combatParts.Count == 0)
            {
                return false;
            }
            
            // Buscar la primera parte con ataques
            foreach (var part in combatParts)
            {
                AttackData attack = part.GetDefaultAttack();
                if (attack != null)
                {
                    return TryExecuteAttack(part, attack);
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Obtiene todos los ataques disponibles de todas las partes.
        /// </summary>
        public List<(CombatPart part, AttackData attack)> GetAllAvailableAttacks()
        {
            var result = new List<(CombatPart, AttackData)>();
            
            foreach (var part in combatParts)
            {
                foreach (var attack in part.AvailableAttacks)
                {
                    if (attack != null)
                    {
                        result.Add((part, attack));
                    }
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Cancela el ataque actual (si es posible).
        /// Por ahora solo funciona durante WindUp.
        /// </summary>
        public bool TryCancelAttack()
        {
            if (currentState == CombatState.WindUp)
            {
                EndAttack();
                Debug.Log("[CombatController] Ataque cancelado durante WindUp");
                return true;
            }
            
            return false;
        }
        
        #endregion
        
        #region Private Methods - State Machine
        
        private void StartAttack(CombatPart part, AttackData attack)
        {
            currentAttackingPart = part;
            currentAttack = attack;
            stateTimer = 0f;
            
            // Generar ID único para este ataque (para evitar daño múltiple por robot)
            // Si es ataque de área, usar -1 (permitir múltiples golpes)
            if (attack.isAreaDamage)
            {
                currentAttackId = -1;
            }
            else
            {
                globalAttackIdCounter++;
                currentAttackId = globalAttackIdCounter;
            }
            
            ChangeState(CombatState.WindUp);
            
            OnAttackStarted?.Invoke(part, attack);
            
            // Disparar animación de ataque
            bool animationTriggered = false;
            
            if (part.StructuralPart != null && part.StructuralPart.Animator != null)
            {
                part.StructuralPart.Animator.SetTrigger(attack.mainAnimationTrigger);
                animationTriggered = true;
                Debug.Log($"[CombatController] Animación disparada en StructuralPart: {part.StructuralPart.name}");
            }
            else
            {
                // Buscar Animator en los padres (por si está en el brazo y la garra es hija)
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
                Debug.LogWarning($"  - StructuralPart: {(part.StructuralPart != null ? part.StructuralPart.name : "NULL")}");
                Debug.LogWarning($"  - StructuralPart.Animator: {(part.StructuralPart?.Animator != null ? "EXISTS" : "NULL")}");
                Debug.LogWarning($"  - Trigger buscado: '{attack.mainAnimationTrigger}'");
            }
            
            if (logStateChanges)
            {
                float damage = part.CalculateDamage(attack);
                Debug.Log($"[CombatController] Ataque iniciado: {attack.attackName} con {part.name} " +
                         $"(Daño: {damage:F1}, Duración: {attack.TotalDuration:F2}s)");
            }
        }
        
        private void UpdateWindUp()
        {
            if (stateTimer >= currentAttack.windupTime)
            {
                ActivateHitbox();
                ChangeState(CombatState.Active);
            }
        }
        
        private void UpdateActive()
        {
            // Actualizar posición de hitbox para que siga al arma
            if (hitboxObject != null && currentAttackingPart != null)
            {
                hitboxObject.transform.position = currentAttackingPart.GetHitboxWorldPosition();
            }
            
            // Verificar hits cada frame
            if (activeHitbox != null)
            {
                activeHitbox.CheckHits();
            }
            
            // Verificar si terminó la fase activa
            float activeEndTime = currentAttack.windupTime + currentAttack.activeTime;
            if (stateTimer >= activeEndTime)
            {
                DeactivateHitbox();
                ChangeState(CombatState.Recovery);
            }
        }
        
        private void UpdateRecovery()
        {
            if (stateTimer >= currentAttack.TotalDuration)
            {
                EndAttack();
            }
        }
        
        private void EndAttack()
        {
            var endedPart = currentAttackingPart;
            var endedAttack = currentAttack;
            
            DeactivateHitbox();
            
            currentAttackingPart = null;
            currentAttack = null;
            stateTimer = 0f;
            
            ChangeState(CombatState.Idle);
            
            if (endedAttack != null)
            {
                OnAttackEnded?.Invoke(endedPart, endedAttack);
            }
        }
        
        private void ChangeState(CombatState newState)
        {
            if (logStateChanges && currentState != newState)
            {
                Debug.Log($"[CombatController] Estado: {currentState} → {newState}");
            }
            
            currentState = newState;
        }
        
        #endregion
        
        #region Private Methods - Hitbox
        
        private void CreateHitboxObject()
        {
            hitboxObject = new GameObject("AttackHitbox");
            hitboxObject.transform.SetParent(transform);
            hitboxObject.SetActive(false);
            
            activeHitbox = hitboxObject.AddComponent<AttackHitbox>();
            
            // Crear visualización del hitbox (esfera semi-transparente)
            if (showHitboxInGame)
            {
                hitboxVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                hitboxVisual.name = "HitboxVisual";
                hitboxVisual.transform.SetParent(hitboxObject.transform);
                hitboxVisual.transform.localPosition = Vector3.zero;
                
                // Remover el collider de la esfera visual
                Collider visualCollider = hitboxVisual.GetComponent<Collider>();
                if (visualCollider != null)
                {
                    Destroy(visualCollider);
                }
                
                // Material semi-transparente rojo
                Renderer renderer = hitboxVisual.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Material mat = new Material(Shader.Find("Standard"));
                    mat.color = new Color(1f, 0f, 0f, 0.3f);
                    mat.SetFloat("_Mode", 3); // Transparent
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = 3000;
                    renderer.material = mat;
                }
            }
        }
        
        private void ActivateHitbox()
        {
            if (currentAttackingPart == null || currentAttack == null)
            {
                Debug.LogError("[CombatController] ActivateHitbox: currentAttackingPart o currentAttack es null!");
                return;
            }
            
            hitboxObject.SetActive(true);
            
            Vector3 hitboxPos = currentAttackingPart.GetHitboxWorldPosition();
            float damage = currentAttackingPart.CalculateDamage(currentAttack);
            float radius = currentAttackingPart.HitboxRadius;
            
            hitboxObject.transform.position = hitboxPos;
            
            // Escalar visualización
            if (hitboxVisual != null)
            {
                hitboxVisual.transform.localScale = Vector3.one * radius * 2f;
            }
            
            activeHitbox.Activate(radius, damage, robot.gameObject, currentAttack.isAreaDamage, currentAttackId);
            
            OnAttackActive?.Invoke(currentAttackingPart, currentAttack);
        }
        
        private void DeactivateHitbox()
        {
            if (activeHitbox != null)
            {
                activeHitbox.Deactivate();
            }
            
            if (hitboxObject != null)
            {
                hitboxObject.SetActive(false);
            }
        }
        
        #endregion
        
        #region Helper Methods
        
        private float GetElapsedTimeInCurrentAttack()
        {
            return stateTimer;
        }
        
        #endregion
        
        #region Debug
        
        private void OnDrawGizmos()
        {
            if (!showAttackGizmos) return;
            
            // Mostrar estado
            if (currentState != CombatState.Idle && currentAttackingPart != null)
            {
                Vector3 origin = currentAttackingPart.GetHitboxWorldPosition();
                
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
                
                Gizmos.DrawWireSphere(origin, 0.2f);
            }
        }
        
        #endregion
    }
}
