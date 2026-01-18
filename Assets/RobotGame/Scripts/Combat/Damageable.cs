using System;
using UnityEngine;

namespace RobotGame.Combat
{
    /// <summary>
    /// Componente para cualquier objeto que pueda recibir daño.
    /// 
    /// Usar en: Robots enemigos, robot del jugador, objetos destructibles.
    /// </summary>
    public class Damageable : MonoBehaviour
    {
        [Header("Salud")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth;
        
        [Header("Estado")]
        [SerializeField] private bool isInvulnerable = false;
        [SerializeField] private bool isDead = false;
        
        [Header("Debug")]
        [SerializeField] private bool logDamage = true;
        
        #region Events
        
        /// <summary>
        /// Se dispara cuando recibe daño. Params: daño recibido, salud restante, atacante (puede ser null)
        /// </summary>
        public event Action<float, float, GameObject> OnDamageReceived;
        
        /// <summary>
        /// Se dispara cuando la salud llega a 0.
        /// </summary>
        public event Action OnDeath;
        
        /// <summary>
        /// Se dispara cuando se cura. Params: cantidad curada, salud actual
        /// </summary>
        public event Action<float, float> OnHealed;
        
        #endregion
        
        #region Properties
        
        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float HealthPercent => maxHealth > 0 ? currentHealth / maxHealth : 0f;
        public bool IsInvulnerable => isInvulnerable;
        public bool IsDead => isDead;
        public bool IsAlive => !isDead && currentHealth > 0;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            currentHealth = maxHealth;
        }
        
        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Aplica daño a este objeto.
        /// </summary>
        /// <param name="damage">Cantidad de daño</param>
        /// <param name="attacker">GameObject que causó el daño (opcional)</param>
        /// <returns>True si el daño fue aplicado</returns>
        public bool TakeDamage(float damage, GameObject attacker = null)
        {
            if (isDead || isInvulnerable || damage <= 0)
            {
                return false;
            }
            
            float previousHealth = currentHealth;
            currentHealth = Mathf.Max(0f, currentHealth - damage);
            
            if (logDamage)
            {
                string attackerName = attacker != null ? attacker.name : "Unknown";
                Debug.Log($"[Damageable] {gameObject.name} recibió {damage:F1} daño de {attackerName}. " +
                         $"Salud: {previousHealth:F1} → {currentHealth:F1}");
            }
            
            OnDamageReceived?.Invoke(damage, currentHealth, attacker);
            
            if (currentHealth <= 0 && !isDead)
            {
                Die();
            }
            
            return true;
        }
        
        /// <summary>
        /// Cura este objeto.
        /// </summary>
        /// <param name="amount">Cantidad a curar</param>
        /// <returns>Cantidad realmente curada</returns>
        public float Heal(float amount)
        {
            if (isDead || amount <= 0)
            {
                return 0f;
            }
            
            float previousHealth = currentHealth;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            float actualHeal = currentHealth - previousHealth;
            
            if (actualHeal > 0)
            {
                OnHealed?.Invoke(actualHeal, currentHealth);
            }
            
            return actualHeal;
        }
        
        /// <summary>
        /// Establece invulnerabilidad.
        /// </summary>
        public void SetInvulnerable(bool invulnerable)
        {
            isInvulnerable = invulnerable;
        }
        
        /// <summary>
        /// Reinicia la salud al máximo y revive si estaba muerto.
        /// </summary>
        public void ResetHealth()
        {
            currentHealth = maxHealth;
            isDead = false;
        }
        
        /// <summary>
        /// Establece la salud máxima (y ajusta la actual proporcionalmente).
        /// </summary>
        public void SetMaxHealth(float newMax, bool adjustCurrent = true)
        {
            float percent = HealthPercent;
            maxHealth = Mathf.Max(1f, newMax);
            
            if (adjustCurrent)
            {
                currentHealth = maxHealth * percent;
            }
            else
            {
                currentHealth = Mathf.Min(currentHealth, maxHealth);
            }
        }
        
        #endregion
        
        #region Private Methods
        
        private void Die()
        {
            isDead = true;
            
            if (logDamage)
            {
                Debug.Log($"[Damageable] {gameObject.name} ha muerto!");
            }
            
            OnDeath?.Invoke();
        }
        
        #endregion
    }
}
