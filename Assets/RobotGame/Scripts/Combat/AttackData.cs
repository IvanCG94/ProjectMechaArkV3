using UnityEngine;

namespace RobotGame.Combat
{
    /// <summary>
    /// Define un TIPO de ataque (Zarpazo, Estocada, Combo, etc.)
    /// 
    /// NO contiene daño - el daño viene de la parte que ejecuta el ataque.
    /// Contiene: timings, forma de hitbox, multiplicadores, animaciones.
    /// 
    /// Daño Final = CombatPart.baseDamage × AttackData.damageMultiplier
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
        
        [Header("Timings")]
        [Tooltip("Tiempo de preparación antes del golpe (tell para el jugador)")]
        [Range(0f, 2f)]
        public float windupTime = 0.3f;
        
        [Tooltip("Duración de la hitbox activa")]
        [Range(0.05f, 1f)]
        public float activeTime = 0.15f;
        
        [Tooltip("Tiempo de recuperación después del golpe (ventana de castigo)")]
        [Range(0f, 2f)]
        public float recoveryTime = 0.4f;
        
        [Header("Animación")]
        [Tooltip("Nombre del trigger/estado de animación para la parte que ataca")]
        public string mainAnimationTrigger = "Attack";
        
        [Tooltip("Nombre de la animación de acompañamiento para otras partes")]
        public string accompanimentAnimation = "AttackAccompany";
        
        /// <summary>
        /// Duración total del ataque (windup + active + recovery)
        /// </summary>
        public float TotalDuration => windupTime + activeTime + recoveryTime;
        
        /// <summary>
        /// Tiempo desde el inicio hasta que la hitbox se activa
        /// </summary>
        public float TimeToActive => windupTime;
        
        /// <summary>
        /// Tiempo desde el inicio hasta que la hitbox se desactiva
        /// </summary>
        public float TimeToInactive => windupTime + activeTime;
    }
}
