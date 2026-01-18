using UnityEngine;
using RobotGame.Components;

namespace RobotGame.Combat
{
    /// <summary>
    /// Componente de salud para partes del robot (estructurales y armadura).
    /// 
    /// Cada parte tiene su propia resistencia. Cuando llega a 0, la parte se destruye.
    /// Las partes críticas (Core, parte con CoreSocket) destruyen el robot al morir.
    /// Las partes no críticas simplemente desaparecen.
    /// </summary>
    public class PartHealth : MonoBehaviour
    {
        [Header("Salud")]
        [SerializeField] private float maxHealth = 50f;
        [SerializeField] private float currentHealth;
        
        [Header("Estado")]
        [SerializeField] private bool isDestroyed = false;
        
        [Header("Debug")]
        [SerializeField] private bool logDamage = true;
        
        // Referencias cacheadas
        private StructuralPart structuralPart;
        private Robot parentRobot;
        
        #region Properties
        
        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float HealthPercent => maxHealth > 0 ? currentHealth / maxHealth : 0f;
        public bool IsDestroyed => isDestroyed;
        public bool IsAlive => !isDestroyed && currentHealth > 0;
        
        /// <summary>
        /// El robot al que pertenece esta parte.
        /// </summary>
        public Robot ParentRobot => parentRobot;
        
        /// <summary>
        /// Si esta parte tiene el StructuralPart component.
        /// </summary>
        public StructuralPart StructuralPart => structuralPart;
        
        /// <summary>
        /// Si destruir esta parte mata al robot.
        /// True si: es el Core, o contiene el Core insertado.
        /// </summary>
        public bool IsCriticalPart
        {
            get
            {
                // ¿Es el Core mismo?
                if (GetComponent<RobotCore>() != null) return true;
                
                // ¿Contiene el Core insertado? (el Core es hijo de esta parte)
                RobotCore coreInChildren = GetComponentInChildren<RobotCore>();
                if (coreInChildren != null && coreInChildren.IsActive)
                {
                    return true;
                }
                
                return false;
            }
        }
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Se dispara cuando la parte recibe daño. Params: daño, salud restante, atacante
        /// </summary>
        public event System.Action<float, float, GameObject> OnDamageReceived;
        
        /// <summary>
        /// Se dispara cuando la parte es destruida.
        /// </summary>
        public event System.Action<PartHealth> OnPartDestroyed;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            currentHealth = maxHealth;
            structuralPart = GetComponent<StructuralPart>();
            
            // Buscar el robot padre
            parentRobot = GetComponentInParent<Robot>();
        }
        
        private void Start()
        {
            // Registrarse con el robot
            if (parentRobot != null)
            {
                parentRobot.RegisterPartHealth(this);
            }
        }
        
        private void OnDestroy()
        {
            // Desregistrarse del robot
            if (parentRobot != null)
            {
                parentRobot.UnregisterPartHealth(this);
            }
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Aplica daño a esta parte.
        /// </summary>
        /// <param name="damage">Cantidad de daño</param>
        /// <param name="attacker">GameObject que causó el daño (opcional)</param>
        /// <param name="attackId">ID único del ataque para evitar daño múltiple. -1 para ataques de área.</param>
        /// <returns>True si el daño fue aplicado</returns>
        public bool TakeDamage(float damage, GameObject attacker = null, int attackId = -1)
        {
            if (isDestroyed || damage <= 0)
            {
                return false;
            }
            
            // Buscar Robot padre si no lo tenemos cacheado
            if (parentRobot == null)
            {
                parentRobot = GetComponentInParent<Robot>();
            }
            
            // DEBUG: Mostrar estado
            Debug.Log($"[PartHealth DEBUG] {gameObject.name} - AttackId: {attackId}, ParentRobot: {(parentRobot != null ? parentRobot.RobotName : "NULL")}");
            
            // Verificar con el Robot si puede recibir daño de este ataque
            if (parentRobot != null && attackId >= 0)
            {
                bool canReceive = parentRobot.CanReceiveDamageFromAttack(attackId);
                Debug.Log($"[PartHealth DEBUG] Robot.CanReceiveDamageFromAttack({attackId}) = {canReceive}");
                
                if (!canReceive)
                {
                    Debug.Log($"[PartHealth] {gameObject.name} RECHAZADO - Robot '{parentRobot.RobotName}' ya golpeado por ataque #{attackId}");
                    return false;
                }
            }
            else
            {
                Debug.Log($"[PartHealth DEBUG] Saltando verificación: parentRobot={parentRobot != null}, attackId={attackId}");
            }
            
            // Aplicar daño
            float previousHealth = currentHealth;
            currentHealth = Mathf.Max(0f, currentHealth - damage);
            
            // Registrar el hit en el Robot
            if (parentRobot != null && attackId >= 0)
            {
                parentRobot.RegisterAttackHit(attackId);
                Debug.Log($"[PartHealth DEBUG] Registrado hit en Robot para attackId #{attackId}");
            }
            
            if (logDamage)
            {
                string attackerName = attacker != null ? attacker.name : "Unknown";
                string criticalTag = IsCriticalPart ? " [CRÍTICA]" : "";
                string robotName = parentRobot != null ? parentRobot.RobotName : "SIN ROBOT";
                Debug.Log($"[PartHealth] {gameObject.name}{criticalTag} de '{robotName}' recibió {damage:F1} daño. " +
                         $"Salud: {previousHealth:F1} → {currentHealth:F1}");
            }
            
            OnDamageReceived?.Invoke(damage, currentHealth, attacker);
            
            if (currentHealth <= 0 && !isDestroyed)
            {
                DestroyPart();
            }
            
            return true;
        }
        
        /// <summary>
        /// Versión simplificada sin attackId (para compatibilidad).
        /// </summary>
        public bool TakeDamageSimple(float damage, GameObject attacker = null)
        {
            return TakeDamage(damage, attacker, -1);
        }
        
        /// <summary>
        /// Cura esta parte.
        /// </summary>
        public float Heal(float amount)
        {
            if (isDestroyed || amount <= 0) return 0f;
            
            float previousHealth = currentHealth;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            return currentHealth - previousHealth;
        }
        
        /// <summary>
        /// Resetea la salud al máximo.
        /// </summary>
        public void ResetHealth()
        {
            currentHealth = maxHealth;
            isDestroyed = false;
        }
        
        /// <summary>
        /// Establece la salud máxima.
        /// </summary>
        public void SetMaxHealth(float newMax, bool healToFull = false)
        {
            maxHealth = Mathf.Max(1f, newMax);
            if (healToFull)
            {
                currentHealth = maxHealth;
            }
            else
            {
                currentHealth = Mathf.Min(currentHealth, maxHealth);
            }
        }
        
        #endregion
        
        #region Private Methods
        
        private void DestroyPart()
        {
            isDestroyed = true;
            
            if (logDamage)
            {
                string criticalTag = IsCriticalPart ? " [PARTE CRÍTICA]" : "";
                Debug.Log($"[PartHealth] {gameObject.name}{criticalTag} ha sido DESTRUIDA!");
            }
            
            // Notificar al robot antes de destruir
            OnPartDestroyed?.Invoke(this);
            
            // Si es parte crítica, notificar al robot para que maneje la muerte
            if (IsCriticalPart && parentRobot != null)
            {
                parentRobot.OnCriticalPartDestroyed(this);
            }
            
            // Desactivar/destruir el GameObject
            // Usamos Destroy con delay pequeño para que los eventos se procesen
            gameObject.SetActive(false);
            Destroy(gameObject, 0.1f);
        }
        
        #endregion
    }
}
