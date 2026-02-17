using UnityEngine;
using RobotGame.Components;
using System.Collections;
using System.Collections.Generic;

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
        
        [Header("Feedback Visual")]
        [Tooltip("Duración del flash rojo cuando recibe daño")]
        [SerializeField] private float damageFlashDuration = 0.2f;
        [Tooltip("Color del flash de daño")]
        [SerializeField] private Color damageFlashColor = Color.red;
        [Tooltip("Activar efecto visual de daño")]
        [SerializeField] private bool enableDamageFlash = true;
        
        [Header("Debug")]
        [SerializeField] private bool logDamage = true;
        
        // Referencias cacheadas
        private StructuralPart structuralPart;
        private Robot parentRobot;
        
        // Para el efecto de flash
        private List<Renderer> partRenderers = new List<Renderer>();
        private Dictionary<Renderer, Color[]> originalColors = new Dictionary<Renderer, Color[]>();
        private Coroutine flashCoroutine;
        
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
            
            // Cachear renderers para el efecto de flash
            CacheRenderers();
        }
        
        /// <summary>
        /// Cachea todos los renderers de esta parte y sus colores originales.
        /// </summary>
        private void CacheRenderers()
        {
            partRenderers.Clear();
            originalColors.Clear();
            
            // Obtener todos los renderers de esta parte y sus hijos
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            
            foreach (var renderer in renderers)
            {
                // Ignorar renderers sin materiales
                if (renderer.materials == null || renderer.materials.Length == 0) continue;
                
                partRenderers.Add(renderer);
                
                // Guardar colores originales
                Color[] colors = new Color[renderer.materials.Length];
                for (int i = 0; i < renderer.materials.Length; i++)
                {
                    if (renderer.materials[i].HasProperty("_Color"))
                    {
                        colors[i] = renderer.materials[i].color;
                    }
                    else
                    {
                        colors[i] = Color.white;
                    }
                }
                originalColors[renderer] = colors;
            }
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
        /// Sistema de daño penetrante: si el daño supera la vida de la parte,
        /// el exceso puede dañar otras partes del mismo robot.
        /// </summary>
        /// <param name="damage">Cantidad de daño original del ataque</param>
        /// <param name="attacker">GameObject que causó el daño (opcional)</param>
        /// <param name="attackId">ID único del ataque para el sistema penetrante. -1 para ataques de área.</param>
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
            
            // Obtener el daño disponible para esta parte (sistema penetrante)
            float availableDamage = damage;
            
            if (parentRobot != null && attackId >= 0)
            {
                availableDamage = parentRobot.GetRemainingDamageFromAttack(attackId, damage);
                
                // Si no queda daño disponible, rechazar
                if (availableDamage <= 0)
                {
                    if (logDamage)
                    {
                        Debug.Log($"[PartHealth] {gameObject.name} - Sin daño restante del ataque #{attackId}");
                    }
                    return false;
                }
            }
            
            // Calcular cuánto daño absorbe esta parte
            // (el mínimo entre el daño disponible y la vida actual)
            float damageAbsorbed = Mathf.Min(availableDamage, currentHealth);
            
            // Aplicar daño
            float previousHealth = currentHealth;
            currentHealth = Mathf.Max(0f, currentHealth - availableDamage);
            
            // Registrar el daño absorbido en el Robot (para que las siguientes partes reciban menos)
            if (parentRobot != null && attackId >= 0)
            {
                parentRobot.RegisterDamageAbsorbed(attackId, damageAbsorbed);
            }
            
            if (logDamage)
            {
                string attackerName = attacker != null ? attacker.name : "Unknown";
                string criticalTag = IsCriticalPart ? " [CRÍTICA]" : "";
                string robotName = parentRobot != null ? parentRobot.RobotName : "SIN ROBOT";
                Debug.Log($"[PartHealth] {gameObject.name}{criticalTag} de '{robotName}' recibió {availableDamage:F1} daño (absorbió {damageAbsorbed:F1}). " +
                         $"Salud: {previousHealth:F1} → {currentHealth:F1}");
            }
            
            // Efecto visual de flash rojo
            if (enableDamageFlash)
            {
                TriggerDamageFlash();
            }
            
            OnDamageReceived?.Invoke(availableDamage, currentHealth, attacker);
            
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
            
            // Detener flash si está activo
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
                RestoreOriginalColors();
            }
            
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
        
        #region Damage Flash
        
        /// <summary>
        /// Activa el efecto de flash rojo.
        /// </summary>
        private void TriggerDamageFlash()
        {
            // Si ya hay un flash activo, reiniciarlo
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
            }
            
            flashCoroutine = StartCoroutine(DamageFlashCoroutine());
        }
        
        /// <summary>
        /// Coroutine que cambia el color a rojo y luego lo restaura.
        /// </summary>
        private IEnumerator DamageFlashCoroutine()
        {
            // Cambiar a color de daño
            SetRenderersColor(damageFlashColor);
            
            // Esperar
            yield return new WaitForSeconds(damageFlashDuration);
            
            // Restaurar colores originales
            RestoreOriginalColors();
            
            flashCoroutine = null;
        }
        
        /// <summary>
        /// Cambia el color de todos los renderers.
        /// </summary>
        private void SetRenderersColor(Color color)
        {
            foreach (var renderer in partRenderers)
            {
                if (renderer == null) continue;
                
                foreach (var material in renderer.materials)
                {
                    if (material != null && material.HasProperty("_Color"))
                    {
                        material.color = color;
                    }
                }
            }
        }
        
        /// <summary>
        /// Restaura los colores originales de todos los renderers.
        /// </summary>
        private void RestoreOriginalColors()
        {
            foreach (var renderer in partRenderers)
            {
                if (renderer == null) continue;
                
                if (originalColors.TryGetValue(renderer, out Color[] colors))
                {
                    for (int i = 0; i < renderer.materials.Length && i < colors.Length; i++)
                    {
                        if (renderer.materials[i] != null && renderer.materials[i].HasProperty("_Color"))
                        {
                            renderer.materials[i].color = colors[i];
                        }
                    }
                }
            }
        }
        
        #endregion
    }
}
