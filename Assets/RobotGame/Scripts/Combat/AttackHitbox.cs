using System.Collections.Generic;
using UnityEngine;
using RobotGame.Components;

namespace RobotGame.Combat
{
    /// <summary>
    /// Maneja la detección de colisiones durante un ataque.
    /// Usa el sistema de attackId para evitar daño múltiple por robot.
    /// 
    /// Detecta objetos en los layers configurados (por defecto RobotHitbox y Default).
    /// </summary>
    public class AttackHitbox : MonoBehaviour
    {
        [Header("Configuración")]
        [Tooltip("Layers que puede golpear este ataque. Por defecto incluye RobotHitbox (11) y Default (0).")]
        [SerializeField] private LayerMask targetLayers = (1 << 0) | (1 << 11); // Default + RobotHitbox
        
        [Header("Estado (Solo lectura)")]
        [SerializeField] private bool isActive = false;
        [SerializeField] private float currentRadius = 0.5f;
        [SerializeField] private float currentDamage = 0f;
        [SerializeField] private int currentAttackId = -1;
        
        [Header("Debug")]
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private Color activeColor = new Color(1f, 0f, 0f, 0.5f);
        
        private HashSet<PartHealth> hitParts = new HashSet<PartHealth>();
        private HashSet<Damageable> hitDamageables = new HashSet<Damageable>();
        private GameObject attackerRoot;
        private Collider[] hitBuffer = new Collider[50];
        
        #region Properties
        
        public bool IsActive => isActive;
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Activa la hitbox.
        /// </summary>
        public void Activate(float radius, float damage, GameObject attacker, bool areaAttack = false, int attackId = -1)
        {
            isActive = true;
            currentRadius = radius;
            currentDamage = damage;
            attackerRoot = attacker;
            currentAttackId = areaAttack ? -1 : attackId; // Área = -1 (sin filtro)
            
            hitParts.Clear();
            hitDamageables.Clear();
            
            string attackType = areaAttack ? "ÁREA" : "NORMAL";
            Debug.Log($"[AttackHitbox] Activada ({attackType}) - Radio: {radius:F2}, Daño: {damage:F1}, AttackId: {currentAttackId}");
        }
        
        /// <summary>
        /// Desactiva la hitbox.
        /// </summary>
        public void Deactivate()
        {
            if (isActive)
            {
                Debug.Log($"[AttackHitbox] Desactivada - Partes: {hitParts.Count}, Otros: {hitDamageables.Count}");
            }
            
            isActive = false;
            hitParts.Clear();
            hitDamageables.Clear();
        }
        
        /// <summary>
        /// Verifica colisiones y aplica daño.
        /// </summary>
        public int CheckHits()
        {
            if (!isActive) return 0;
            
            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                currentRadius,
                hitBuffer,
                targetLayers
            );
            
            int newHits = 0;
            
            for (int i = 0; i < hitCount; i++)
            {
                Collider col = hitBuffer[i];
                
                // Ignorar si es parte del atacante
                if (attackerRoot != null && col.transform.IsChildOf(attackerRoot.transform))
                {
                    continue;
                }
                
                // Intentar golpear como parte de robot
                if (TryHitAsPart(col, ref newHits))
                {
                    continue;
                }
                
                // Intentar golpear como Damageable genérico
                TryHitAsDamageable(col, ref newHits);
            }
            
            return newHits;
        }
        
        #endregion
        
        #region Private Methods
        
        private bool TryHitAsPart(Collider col, ref int newHits)
        {
            PartHealth partHealth = col.GetComponent<PartHealth>();
            if (partHealth == null)
            {
                partHealth = col.GetComponentInParent<PartHealth>();
            }
            
            if (partHealth == null || !partHealth.IsAlive)
            {
                return false;
            }
            
            // Ya golpeamos esta parte específica
            if (hitParts.Contains(partHealth))
            {
                return true;
            }
            
            // Aplicar daño - PartHealth verifica con Robot si puede recibir
            if (partHealth.TakeDamage(currentDamage, attackerRoot, currentAttackId))
            {
                hitParts.Add(partHealth);
                newHits++;
                
                string criticalTag = partHealth.IsCriticalPart ? " [CRÍTICA]" : "";
                string robotName = partHealth.ParentRobot != null ? partHealth.ParentRobot.RobotName : "SIN ROBOT";
                Debug.Log($"[AttackHitbox] ¡GOLPE! {partHealth.gameObject.name}{criticalTag} de {robotName} - {currentDamage:F1} daño");
            }
            
            return true;
        }
        
        private bool TryHitAsDamageable(Collider col, ref int newHits)
        {
            Damageable damageable = col.GetComponent<Damageable>();
            if (damageable == null)
            {
                damageable = col.GetComponentInParent<Damageable>();
            }
            
            if (damageable == null || !damageable.IsAlive)
            {
                return false;
            }
            
            // No procesar si es parte de robot
            if (damageable.GetComponent<PartHealth>() != null)
            {
                return false;
            }
            
            if (hitDamageables.Contains(damageable))
            {
                return true;
            }
            
            if (damageable.TakeDamage(currentDamage, attackerRoot))
            {
                hitDamageables.Add(damageable);
                newHits++;
                Debug.Log($"[AttackHitbox] ¡GOLPE! {damageable.gameObject.name} - {currentDamage:F1} daño");
            }
            
            return true;
        }
        
        #endregion
        
        #region Debug
        
        private void OnDrawGizmos()
        {
            if (!showGizmos || !isActive) return;
            
            Gizmos.color = activeColor;
            Gizmos.DrawWireSphere(transform.position, currentRadius);
        }
        
        #endregion
    }
}
