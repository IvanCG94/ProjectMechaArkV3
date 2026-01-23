using UnityEngine;

namespace RobotGame.Combat
{
    /// <summary>
    /// Componente que recibe Animation Events y los comunica al CombatController.
    /// 
    /// COLOCAR EN: El mismo GameObject que tiene el Animator de la parte que ataca.
    /// 
    /// ANIMATION EVENTS A CREAR:
    /// - OnHitboxStart() → Activa la hitbox (momento del impacto)
    /// - OnHitboxEnd()   → Desactiva la hitbox
    /// - OnAttackEnd()   → (Opcional) Termina el ataque antes del fin de la animación
    /// </summary>
    public class AttackAnimationEvents : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;
        
        // Referencia al CombatController (se busca automáticamente)
        private CombatController combatController;
        
        // Estado
        private bool hitboxActive = false;
        
        #region Properties
        
        public bool HitboxActive => hitboxActive;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            // Buscar CombatController en padres
            combatController = GetComponentInParent<CombatController>();
            
            if (combatController == null)
            {
                Debug.LogWarning($"[AttackAnimationEvents] {gameObject.name}: No se encontró CombatController en padres. " +
                    "Se buscará cuando se reciba un evento.");
            }
        }
        
        #endregion
        
        #region Animation Events - Llamados desde la animación
        
        /// <summary>
        /// ANIMATION EVENT: Llamar cuando la hitbox debe activarse.
        /// Coloca este evento en el frame donde el arma empieza a hacer daño.
        /// </summary>
        public void OnHitboxStart()
        {
            if (showDebugLogs)
            {
                Debug.Log($"<color=green>[AnimEvent] {gameObject.name}: OnHitboxStart()</color>");
            }
            
            hitboxActive = true;
            
            // Buscar CombatController si no lo tenemos
            if (combatController == null)
            {
                combatController = GetComponentInParent<CombatController>();
            }
            
            if (combatController != null)
            {
                combatController.OnAnimationHitboxStart();
            }
            else
            {
                Debug.LogError($"[AttackAnimationEvents] {gameObject.name}: No se encontró CombatController!");
            }
        }
        
        /// <summary>
        /// ANIMATION EVENT: Llamar cuando la hitbox debe desactivarse.
        /// Coloca este evento en el frame donde el arma deja de hacer daño.
        /// </summary>
        public void OnHitboxEnd()
        {
            if (showDebugLogs)
            {
                Debug.Log($"<color=yellow>[AnimEvent] {gameObject.name}: OnHitboxEnd()</color>");
            }
            
            hitboxActive = false;
            
            if (combatController != null)
            {
                combatController.OnAnimationHitboxEnd();
            }
        }
        
        /// <summary>
        /// ANIMATION EVENT: (Opcional) Llamar para terminar el ataque.
        /// Útil si quieres que el ataque termine antes de que la animación termine.
        /// Si no usas este evento, el ataque termina cuando la animación termina.
        /// </summary>
        public void OnAttackEnd()
        {
            if (showDebugLogs)
            {
                Debug.Log($"<color=red>[AnimEvent] {gameObject.name}: OnAttackEnd()</color>");
            }
            
            hitboxActive = false;
            
            if (combatController != null)
            {
                combatController.OnAnimationAttackEnd();
            }
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Asigna el CombatController manualmente.
        /// </summary>
        public void SetCombatController(CombatController controller)
        {
            combatController = controller;
        }
        
        /// <summary>
        /// Resetea el estado (llamar si se cancela un ataque).
        /// </summary>
        public void ResetState()
        {
            hitboxActive = false;
        }
        
        #endregion
    }
}
