using UnityEngine;
using RobotGame.Assembly;

namespace RobotGame.Combat
{
    /// <summary>
    /// Maneja el input de combate para el jugador.
    /// 
    /// Por ahora es simple:
    /// - Click izquierdo = Atacar con la primera parte disponible
    /// - Scroll = Cambiar ataque/parte (futuro)
    /// 
    /// Este script va en el robot del jugador o en un manager de input.
    /// </summary>
    public class CombatInputHandler : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("El CombatController del robot del jugador")]
        [SerializeField] private CombatController combatController;
        
        [Header("Configuración")]
        [Tooltip("Botón para atacar")]
        [SerializeField] private KeyCode attackKey = KeyCode.Mouse0;
        
        [Tooltip("Botón alternativo para atacar")]
        [SerializeField] private KeyCode attackKeyAlt = KeyCode.J;
        
        [Header("Estado (Solo lectura)")]
        [SerializeField] private int selectedPartIndex = 0;
        [SerializeField] private int selectedAttackIndex = 0;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugUI = true;
        
        #region Unity Lifecycle
        
        private void Start()
        {
            // Buscar CombatController si no está asignado
            if (combatController == null)
            {
                combatController = GetComponent<CombatController>();
            }
            
            if (combatController == null)
            {
                combatController = GetComponentInParent<CombatController>();
            }
            
            // Último recurso: buscar en la escena
            if (combatController == null)
            {
                combatController = FindObjectOfType<CombatController>();
            }
            
            if (combatController == null)
            {
                Debug.LogError("[CombatInputHandler] No se encontró CombatController!");
            }
        }
        
        private void Update()
        {
            if (combatController == null) return;
            
            // No procesar input de combate si está en modo edición
            if (IsInEditMode()) return;
            
            // Input para atacar
            if (Input.GetKeyDown(attackKey) || Input.GetKeyDown(attackKeyAlt))
            {
                TryAttack();
            }
            
            // Scroll para cambiar parte/ataque (solo si no está atacando)
            if (combatController.CanAttack)
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (scroll > 0.1f)
                {
                    CycleAttack(1);
                }
                else if (scroll < -0.1f)
                {
                    CycleAttack(-1);
                }
            }
            
            // Teclas numéricas para seleccionar ataque rápido
            for (int i = 0; i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    SelectAttackByIndex(i);
                }
            }
        }
        
        /// <summary>
        /// Verifica si hay alguna estación de ensamblaje en modo edición.
        /// </summary>
        private bool IsInEditMode()
        {
            var stations = FindObjectsOfType<UnifiedAssemblyStation>();
            foreach (var station in stations)
            {
                if (station.IsEditing)
                {
                    return true;
                }
            }
            return false;
        }
        
        #endregion
        
        #region Attack Methods
        
        /// <summary>
        /// Asigna el CombatController manualmente.
        /// </summary>
        public void SetCombatController(CombatController controller)
        {
            combatController = controller;
        }
        
        private void TryAttack()
        {
            var allAttacks = combatController.GetAllAvailableAttacks();
            
            if (allAttacks.Count == 0)
            {
                Debug.LogWarning("[CombatInputHandler] No hay ataques disponibles");
                return;
            }
            
            // Asegurar que el índice es válido
            int index = Mathf.Clamp(selectedPartIndex, 0, allAttacks.Count - 1);
            var (part, attack) = allAttacks[index];
            
            combatController.TryExecuteAttack(part, attack);
        }
        
        private void CycleAttack(int direction)
        {
            var allAttacks = combatController.GetAllAvailableAttacks();
            if (allAttacks.Count == 0) return;
            
            selectedPartIndex += direction;
            
            // Wrap around
            if (selectedPartIndex < 0)
            {
                selectedPartIndex = allAttacks.Count - 1;
            }
            else if (selectedPartIndex >= allAttacks.Count)
            {
                selectedPartIndex = 0;
            }
            
            var (part, attack) = allAttacks[selectedPartIndex];
            Debug.Log($"[CombatInputHandler] Seleccionado: {attack.attackName} ({part.name})");
        }
        
        private void SelectAttackByIndex(int index)
        {
            var allAttacks = combatController.GetAllAvailableAttacks();
            
            if (index >= 0 && index < allAttacks.Count)
            {
                selectedPartIndex = index;
                var (part, attack) = allAttacks[index];
                Debug.Log($"[CombatInputHandler] Seleccionado: {attack.attackName} ({part.name})");
            }
        }
        
        #endregion
        
        #region Debug UI
        
        private void OnGUI()
        {
            if (!showDebugUI || combatController == null) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 300, 400));
            GUILayout.BeginVertical("box");
            
            GUILayout.Label("=== COMBAT DEBUG ===");
            GUILayout.Space(5);
            
            // Estado actual
            GUILayout.Label($"Estado: {combatController.CurrentState}");
            
            if (combatController.IsAttacking)
            {
                GUILayout.Label($"Atacando: {combatController.CurrentAttack?.attackName}");
                GUILayout.Label($"Progreso: {combatController.AttackProgress * 100:F0}%");
            }
            
            GUILayout.Space(10);
            
            // Ataques disponibles
            var allAttacks = combatController.GetAllAvailableAttacks();
            
            GUILayout.Label($"Ataques disponibles: {allAttacks.Count}");
            GUILayout.Space(5);
            
            for (int i = 0; i < allAttacks.Count; i++)
            {
                var (part, attack) = allAttacks[i];
                float damage = part.CalculateDamage(attack);
                
                string prefix = (i == selectedPartIndex) ? "►" : "  ";
                string keyHint = (i < 9) ? $"[{i + 1}]" : "";
                
                GUILayout.Label($"{prefix} {keyHint} {attack.attackName} ({part.name}) - {damage:F0} dmg");
            }
            
            GUILayout.Space(10);
            GUILayout.Label("[Click/J] Atacar");
            GUILayout.Label("[Scroll] Cambiar ataque");
            GUILayout.Label("[1-9] Selección rápida");
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        
        #endregion
    }
}
