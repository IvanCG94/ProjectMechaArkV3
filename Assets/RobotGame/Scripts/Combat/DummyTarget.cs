using UnityEngine;

namespace RobotGame.Combat
{
    /// <summary>
    /// Muñeco de prueba para testear el sistema de combate.
    /// 
    /// Características:
    /// - Recibe daño y muestra feedback visual
    /// - Muestra barra de vida en pantalla
    /// - Se puede resetear con una tecla
    /// - Cambia de color cuando recibe daño
    /// 
    /// Setup:
    /// 1. Crear un Cubo en la escena
    /// 2. Agregar este componente (agrega Damageable automáticamente)
    /// 3. Play y atacar
    /// </summary>
    [RequireComponent(typeof(Damageable))]
    [RequireComponent(typeof(Collider))]
    public class DummyTarget : MonoBehaviour
    {
        [Header("Configuración")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private KeyCode resetKey = KeyCode.R;
        
        [Header("Feedback Visual")]
        [SerializeField] private Color normalColor = Color.gray;
        [SerializeField] private Color hitColor = Color.red;
        [SerializeField] private Color deadColor = Color.black;
        [SerializeField] private float hitFlashDuration = 0.2f;
        
        [Header("UI")]
        [SerializeField] private bool showHealthBar = true;
        [SerializeField] private Vector3 healthBarOffset = new Vector3(0, 2f, 0);
        
        // Referencias
        private Damageable damageable;
        private Renderer meshRenderer;
        private Material material;
        
        // Estado
        private float hitFlashTimer = 0f;
        private bool isFlashing = false;
        private float lastDamageReceived = 0f;
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            // Obtener o configurar Damageable
            damageable = GetComponent<Damageable>();
            
            // Obtener renderer
            meshRenderer = GetComponent<Renderer>();
            if (meshRenderer == null)
            {
                meshRenderer = GetComponentInChildren<Renderer>();
            }
            
            if (meshRenderer != null)
            {
                // Crear instancia del material para no afectar otros objetos
                material = meshRenderer.material;
                material.color = normalColor;
            }
        }
        
        private void Start()
        {
            // Configurar salud inicial
            damageable.SetMaxHealth(maxHealth, true);
            damageable.ResetHealth();
            
            // Suscribirse a eventos
            damageable.OnDamageReceived += OnDamageReceived;
            damageable.OnDeath += OnDeath;
        }
        
        private void OnDestroy()
        {
            if (damageable != null)
            {
                damageable.OnDamageReceived -= OnDamageReceived;
                damageable.OnDeath -= OnDeath;
            }
        }
        
        private void Update()
        {
            // Reset con tecla
            if (Input.GetKeyDown(resetKey))
            {
                ResetDummy();
            }
            
            // Flash de daño
            if (isFlashing)
            {
                hitFlashTimer -= Time.deltaTime;
                if (hitFlashTimer <= 0f)
                {
                    isFlashing = false;
                    if (material != null && damageable.IsAlive)
                    {
                        material.color = normalColor;
                    }
                }
            }
        }
        
        #endregion
        
        #region Event Handlers
        
        private void OnDamageReceived(float damage, float currentHealth, GameObject attacker)
        {
            lastDamageReceived = damage;
            
            // Flash visual
            if (material != null)
            {
                material.color = hitColor;
                isFlashing = true;
                hitFlashTimer = hitFlashDuration;
            }
            
            // Efecto de escala (pequeño "golpe")
            StartCoroutine(HitScaleEffect());
            
            Debug.Log($"[DummyTarget] ¡Recibió {damage:F1} daño! Salud: {currentHealth:F1}/{damageable.MaxHealth:F1}");
        }
        
        private void OnDeath()
        {
            if (material != null)
            {
                material.color = deadColor;
            }
            
            isFlashing = false;
            
            Debug.Log($"[DummyTarget] ¡MUERTO! Presiona [{resetKey}] para resetear");
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Resetea el dummy a su estado inicial.
        /// </summary>
        public void ResetDummy()
        {
            damageable.ResetHealth();
            
            if (material != null)
            {
                material.color = normalColor;
            }
            
            isFlashing = false;
            transform.localScale = Vector3.one;
            
            Debug.Log("[DummyTarget] ¡Reseteado!");
        }
        
        #endregion
        
        #region Visual Effects
        
        private System.Collections.IEnumerator HitScaleEffect()
        {
            Vector3 originalScale = Vector3.one;
            Vector3 hitScale = originalScale * 0.9f;
            
            transform.localScale = hitScale;
            yield return new WaitForSeconds(0.05f);
            transform.localScale = originalScale;
        }
        
        #endregion
        
        #region UI
        
        private void OnGUI()
        {
            if (!showHealthBar || Camera.main == null) return;
            
            // Convertir posición del mundo a pantalla
            Vector3 worldPos = transform.position + healthBarOffset;
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
            
            // Si está detrás de la cámara, no mostrar
            if (screenPos.z < 0) return;
            
            // Invertir Y para GUI
            screenPos.y = Screen.height - screenPos.y;
            
            // Dimensiones de la barra
            float barWidth = 100f;
            float barHeight = 20f;
            float x = screenPos.x - barWidth / 2f;
            float y = screenPos.y - barHeight / 2f;
            
            // Fondo
            GUI.Box(new Rect(x - 2, y - 2, barWidth + 4, barHeight + 4), "");
            
            // Barra de fondo (rojo)
            GUI.color = Color.red;
            GUI.DrawTexture(new Rect(x, y, barWidth, barHeight), Texture2D.whiteTexture);
            
            // Barra de vida (verde)
            float healthPercent = damageable.HealthPercent;
            GUI.color = damageable.IsAlive ? Color.green : Color.black;
            GUI.DrawTexture(new Rect(x, y, barWidth * healthPercent, barHeight), Texture2D.whiteTexture);
            
            // Texto de salud
            GUI.color = Color.white;
            GUIStyle healthStyle = new GUIStyle(GUI.skin.label);
            healthStyle.alignment = TextAnchor.MiddleCenter;
            healthStyle.normal.textColor = Color.white;
            
            string healthText = $"{damageable.CurrentHealth:F0}/{damageable.MaxHealth:F0}";
            GUI.Label(new Rect(x, y, barWidth, barHeight), healthText, healthStyle);
            
            // Nombre
            GUI.Label(new Rect(x, y - 22, barWidth, 20), gameObject.name, healthStyle);
            
            // Último daño recibido
            if (lastDamageReceived > 0 && isFlashing)
            {
                GUIStyle damageStyle = new GUIStyle(GUI.skin.label);
                damageStyle.alignment = TextAnchor.MiddleLeft;
                damageStyle.normal.textColor = Color.yellow;
                damageStyle.fontStyle = FontStyle.Bold;
                
                GUI.Label(new Rect(x + barWidth + 5, y, 50, barHeight), $"-{lastDamageReceived:F0}", damageStyle);
            }
            
            // Instrucción de reset si está muerto
            if (!damageable.IsAlive)
            {
                GUI.Label(new Rect(x, y + barHeight + 5, barWidth, 20), $"[{resetKey}] Reset", healthStyle);
            }
        }
        
        #endregion
        
        #region Gizmos
        
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireCube(transform.position, Vector3.one);
        }
        
        #endregion
    }
}
