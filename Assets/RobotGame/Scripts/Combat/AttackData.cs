using UnityEngine;

namespace RobotGame.Combat
{
    /// <summary>
    /// Comportamiento de aproximación que la IA debe seguir para este ataque.
    /// </summary>
    public enum AttackApproachBehavior
    {
        /// <summary>Ir directo al objetivo sin rodeos.</summary>
        Direct,
        
        /// <summary>Rodear al objetivo mientras se acerca (strafe).</summary>
        Strafe,
        
        /// <summary>Acercarse lentamente, acechar. Genera tensión.</summary>
        Stalk,
        
        /// <summary>Mantener distancia ideal, rodear buscando apertura.</summary>
        Circle,
        
        /// <summary>Quedarse quieto/oculto, esperar que el objetivo se acerque.</summary>
        Ambush,
        
        /// <summary>Embestida rápida, correr hacia el objetivo.</summary>
        Rush,
        
        /// <summary>Alejarse del objetivo (para ataques a distancia).</summary>
        Retreat
    }
    
    /// <summary>
    /// Comportamiento después de ejecutar el ataque.
    /// </summary>
    public enum AttackRecoveryBehavior
    {
        /// <summary>Mantener posición actual.</summary>
        Hold,
        
        /// <summary>Retroceder después del ataque.</summary>
        Retreat,
        
        /// <summary>Intentar encadenar otro ataque si es posible.</summary>
        Chain,
        
        /// <summary>Buscar nueva posición para el siguiente ataque.</summary>
        Reposition,
        
        /// <summary>Volver a acechar/observar al objetivo.</summary>
        Stalk
    }
    
    /// <summary>
    /// Define un TIPO de ataque con toda la información necesaria para que
    /// la IA pueda ejecutarlo de forma inteligente y orgánica.
    /// 
    /// Cada pieza de combate (brazo, cuello, cola, etc.) define sus propios
    /// ataques con comportamientos específicos. La IA central solo ejecuta
    /// las instrucciones que el ataque provee.
    /// 
    /// Ejemplo: Un cuello con "Mordisco Sorpresa" define:
    /// - Mantener distancia de 8m
    /// - Acechar lentamente
    /// - Esperar 2-5 segundos
    /// - Atacar de sorpresa
    /// - Retroceder después
    /// </summary>
    [CreateAssetMenu(fileName = "NewAttack", menuName = "RobotGame/Combat/Attack Data")]
    public class AttackData : ScriptableObject
    {
        #region Identification
        
        [Header("Identificación")]
        [Tooltip("Nombre del ataque para mostrar en UI")]
        public string attackName = "New Attack";
        
        [Tooltip("Descripción del ataque")]
        [TextArea(2, 4)]
        public string description;
        
        #endregion
        
        #region Damage
        
        [Header("Daño")]
        [Tooltip("Multiplicador de daño (1.0 = daño base, 0.8 = rápido/débil, 1.5 = lento/fuerte)")]
        [Range(0.1f, 3f)]
        public float damageMultiplier = 1f;
        
        [Tooltip("Si es true, afecta múltiples partes por robot. Si es false, solo la primera parte golpeada.")]
        public bool isAreaDamage = false;
        
        #endregion
        
        #region Cooldown
        
        [Header("Cooldown")]
        [Tooltip("Tiempo en segundos antes de poder usar este ataque de nuevo")]
        [Min(0f)]
        public float cooldownTime = 2f;
        
        #endregion
        
        #region Animation
        
        [Header("Animación")]
        [Tooltip("Duración en segundos. Se usa SOLO si no se puede detectar automáticamente del Animator.")]
        [Min(0.1f)]
        public float animationDuration = 1f;
        
        [Tooltip("Preparación antes del golpe (0.0 = inmediato, 0.3 = 30% de la animación)")]
        [Range(0f, 0.9f)]
        public float windupPercent = 0.2f;
        
        [Tooltip("Duración de la hitbox activa (0.5 = 50% de la animación)")]
        [Range(0.05f, 1f)]
        public float activePercent = 0.5f;
        
        [Tooltip("Nombre del trigger/estado de animación para la parte que ataca")]
        public string mainAnimationTrigger = "Attack";
        
        [Tooltip("Nombre de la animación de acompañamiento para otras partes")]
        public string accompanimentAnimation = "AttackAccompany";
        
        #endregion
        
        #region Attack Zone
        
        [Header("Zona de Ataque")]
        [Tooltip("ID de la zona de ataque requerida. Debe coincidir con el zoneId de un AttackZone.")]
        public string zoneId = "";
        
        #endregion
        
        #region Mobility During Attack
        
        [Header("Movilidad Durante Ataque")]
        [Tooltip("Si permite moverse mientras ataca (ej: disparos, embestidas)")]
        public bool allowMovement = false;
        
        [Tooltip("Si permite rotar hacia el objetivo mientras ataca")]
        public bool allowRotation = false;
        
        #endregion
        
        #region AI Approach Behavior
        
        [Header("Comportamiento de Aproximación (IA)")]
        [Tooltip("Cómo debe aproximarse la IA para ejecutar este ataque")]
        public AttackApproachBehavior approachBehavior = AttackApproachBehavior.Direct;
        
        [Tooltip("Distancia ideal para iniciar el ataque")]
        public float idealDistance = 3f;
        
        [Tooltip("Tolerancia de distancia (ataque válido si está dentro de idealDistance ± tolerance)")]
        public float distanceTolerance = 0.5f;
        
        [Tooltip("Multiplicador de velocidad durante aproximación (0.3 = sigiloso, 1 = normal, 2 = rush)")]
        [Range(0.1f, 2f)]
        public float approachSpeedMultiplier = 1f;
        
        #endregion
        
        #region AI Timing
        
        [Header("Timing de Ejecución (IA)")]
        [Tooltip("Tiempo mínimo en posición antes de atacar (para ataques sorpresa/tensión)")]
        public float minWaitBeforeAttack = 0f;
        
        [Tooltip("Tiempo máximo esperando en posición (0 = atacar inmediatamente)")]
        public float maxWaitBeforeAttack = 0f;
        
        [Tooltip("Ataque sorpresa - minimiza 'tells' de preparación")]
        public bool isSurpriseAttack = false;
        
        #endregion
        
        #region AI Recovery Behavior
        
        [Header("Comportamiento Post-Ataque (IA)")]
        [Tooltip("Qué hacer después de ejecutar el ataque")]
        public AttackRecoveryBehavior recoveryBehavior = AttackRecoveryBehavior.Hold;
        
        [Tooltip("Distancia a retroceder después del ataque (si recoveryBehavior = Retreat)")]
        public float retreatDistance = 0f;
        
        [Tooltip("Pausa después del ataque antes de decidir siguiente acción")]
        public float postAttackPause = 0.5f;
        
        [Tooltip("Probabilidad de encadenar otro ataque si el anterior impactó (0-1)")]
        [Range(0f, 1f)]
        public float chainOnHitChance = 0.3f;
        
        [Tooltip("Ataques que pueden encadenarse después de este (si chainOnHitChance tiene éxito)")]
        public AttackData[] chainableAttacks;
        
        #endregion
        
        #region AI Position Requirements
        
        [Header("Requerimientos de Posición (IA)")]
        [Tooltip("Requiere que el objetivo NO esté mirando al atacante")]
        public bool requiresTargetLookingAway = false;
        
        [Tooltip("Ángulo requerido respecto al objetivo (0=frente, 90=lado, 180=espalda)")]
        [Range(0f, 180f)]
        public float requiredAngle = 0f;
        
        [Tooltip("Tolerancia del ángulo requerido")]
        [Range(0f, 180f)]
        public float angleTolerance = 45f;
        
        [Tooltip("Si true, verifica el ángulo. Si false, ignora requiredAngle.")]
        public bool checkAngle = false;
        
        #endregion
        
        #region Properties
        
        /// <summary>
        /// Si este ataque requiere una zona específica para ser viable.
        /// </summary>
        public bool RequiresZone => !string.IsNullOrEmpty(zoneId);
        
        /// <summary>
        /// Porcentaje de recuperación (calculado automáticamente).
        /// </summary>
        public float RecoveryPercent => Mathf.Max(0f, 1f - windupPercent - activePercent);
        
        /// <summary>
        /// Tiempo de preparación en segundos.
        /// </summary>
        public float WindupTime => animationDuration * windupPercent;
        
        /// <summary>
        /// Tiempo de hitbox activa en segundos.
        /// </summary>
        public float ActiveTime => animationDuration * activePercent;
        
        /// <summary>
        /// Tiempo de recuperación en segundos.
        /// </summary>
        public float RecoveryTime => animationDuration * RecoveryPercent;
        
        /// <summary>
        /// Duración total del ataque (igual a animationDuration).
        /// </summary>
        public float TotalDuration => animationDuration;
        
        /// <summary>
        /// Tiempo desde el inicio hasta que la hitbox se activa.
        /// </summary>
        public float TimeToActive => WindupTime;
        
        /// <summary>
        /// Tiempo desde el inicio hasta que la hitbox se desactiva.
        /// </summary>
        public float TimeToInactive => WindupTime + ActiveTime;
        
        /// <summary>
        /// Si este ataque tiene tiempo de espera configurado.
        /// </summary>
        public bool HasWaitTime => maxWaitBeforeAttack > 0f;
        
        /// <summary>
        /// Genera un tiempo de espera aleatorio dentro del rango configurado.
        /// </summary>
        public float GetRandomWaitTime()
        {
            if (maxWaitBeforeAttack <= 0f) return 0f;
            return Random.Range(minWaitBeforeAttack, maxWaitBeforeAttack);
        }
        
        #endregion
        
        #region Validation Methods
        
        /// <summary>
        /// Verifica si la distancia actual es válida para este ataque.
        /// </summary>
        public bool IsDistanceValid(float currentDistance)
        {
            return Mathf.Abs(currentDistance - idealDistance) <= distanceTolerance;
        }
        
        /// <summary>
        /// Verifica si el ángulo actual es válido para este ataque.
        /// </summary>
        /// <param name="angleToTarget">Ángulo desde el atacante hacia el objetivo (0=frente del objetivo)</param>
        public bool IsAngleValid(float angleToTarget)
        {
            if (!checkAngle) return true;
            return Mathf.Abs(angleToTarget - requiredAngle) <= angleTolerance;
        }
        
        /// <summary>
        /// Verifica si el objetivo está mirando hacia otro lado (para ataques sorpresa).
        /// </summary>
        public bool IsTargetLookingAway(Transform attacker, Transform target)
        {
            if (!requiresTargetLookingAway) return true;
            
            Vector3 targetForward = target.forward;
            Vector3 toAttacker = (attacker.position - target.position).normalized;
            
            float dot = Vector3.Dot(targetForward, toAttacker);
            
            // dot < 0 significa que el target está mirando en dirección opuesta al atacante
            return dot < 0f;
        }
        
        /// <summary>
        /// Verifica todos los requerimientos de posición para este ataque.
        /// </summary>
        public bool ArePositionRequirementsMet(Transform attacker, Transform target, float distance, float angle)
        {
            if (!IsDistanceValid(distance)) return false;
            if (!IsAngleValid(angle)) return false;
            if (!IsTargetLookingAway(attacker, target)) return false;
            
            return true;
        }
        
        #endregion
        
        #region Editor
        
        private void OnValidate()
        {
            // Asegurar que windup + active no excedan 100%
            if (windupPercent + activePercent > 1f)
            {
                activePercent = 1f - windupPercent;
            }
            
            // Asegurar que min wait no sea mayor que max wait
            if (minWaitBeforeAttack > maxWaitBeforeAttack && maxWaitBeforeAttack > 0f)
            {
                minWaitBeforeAttack = maxWaitBeforeAttack;
            }
        }
        
        #endregion
        
        #region Debug
        
        /// <summary>
        /// Devuelve un string con los tiempos calculados para debug.
        /// </summary>
        public string GetTimingsSummary()
        {
            return $"Duración: {animationDuration:F2}s | " +
                   $"WindUp: {WindupTime:F2}s ({windupPercent * 100:F0}%) | " +
                   $"Active: {ActiveTime:F2}s ({activePercent * 100:F0}%) | " +
                   $"Recovery: {RecoveryTime:F2}s ({RecoveryPercent * 100:F0}%)";
        }
        
        /// <summary>
        /// Devuelve un string con el comportamiento de IA para debug.
        /// </summary>
        public string GetAIBehaviorSummary()
        {
            return $"Approach: {approachBehavior} @ {idealDistance:F1}m (±{distanceTolerance:F1}m) | " +
                   $"Speed: {approachSpeedMultiplier:F1}x | " +
                   $"Wait: {minWaitBeforeAttack:F1}-{maxWaitBeforeAttack:F1}s | " +
                   $"Recovery: {recoveryBehavior}";
        }
        
        #endregion
    }
}
