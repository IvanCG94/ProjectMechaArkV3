using UnityEngine;

namespace RobotGame.Combat
{
    /// <summary>
    /// Define un TIPO de ataque (Zarpazo, Estocada, Combo, etc.)
    /// 
    /// NO contiene daño - el daño viene de la parte que ejecuta el ataque.
    /// Contiene: timings (porcentuales), multiplicadores, animaciones.
    /// 
    /// Daño Final = CombatPart.baseDamage × AttackData.damageMultiplier
    /// 
    /// SISTEMA DE TIMINGS:
    /// Los tiempos se definen como porcentajes de la duración de la animación.
    /// - windupPercent: Preparación antes del golpe (tell)
    /// - activePercent: Duración de la hitbox activa
    /// - recoveryPercent: Se calcula automáticamente (1 - windup - active)
    /// </summary>
    [CreateAssetMenu(fileName = "NewAttack", menuName = "RobotGame/Combat/Attack Data")]
    public class AttackData : ScriptableObject
    {
        [Header("Identificación")]
        [Tooltip("Nombre del ataque para mostrar en UI")]
        public string attackName = "New Attack";
        
        [Tooltip("Descripción del ataque")]
        [TextArea(2, 4)]
        public string description;
        
        [Header("Daño")]
        [Tooltip("Multiplicador de daño (1.0 = daño base, 0.8 = rápido/débil, 1.5 = lento/fuerte)")]
        [Range(0.1f, 3f)]
        public float damageMultiplier = 1f;
        
        [Tooltip("Si es true, afecta múltiples partes por robot. Si es false, solo la primera parte golpeada.")]
        public bool isAreaDamage = false;
        
        [Header("Duración de Animación (Fallback)")]
        [Tooltip("Duración en segundos. Se usa SOLO si no se puede detectar automáticamente del Animator.")]
        [Min(0.1f)]
        public float animationDuration = 1f;
        
        [Header("Timings (Porcentaje de la animación)")]
        [Tooltip("Preparación antes del golpe (0.0 = inmediato, 0.3 = 30% de la animación)")]
        [Range(0f, 0.9f)]
        public float windupPercent = 0.2f;
        
        [Tooltip("Duración de la hitbox activa (0.5 = 50% de la animación)")]
        [Range(0.05f, 1f)]
        public float activePercent = 0.5f;
        
        [Header("Animación")]
        [Tooltip("Nombre del trigger/estado de animación para la parte que ataca")]
        public string mainAnimationTrigger = "Attack";
        
        [Tooltip("Nombre de la animación de acompañamiento para otras partes")]
        public string accompanimentAnimation = "AttackAccompany";
        
        #region Calculated Properties
        
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
        
        #endregion
        
        #region Editor Helpers
        
        private void OnValidate()
        {
            // Asegurar que windup + active no excedan 100%
            if (windupPercent + activePercent > 1f)
            {
                activePercent = 1f - windupPercent;
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
        
        #endregion
    }
}
