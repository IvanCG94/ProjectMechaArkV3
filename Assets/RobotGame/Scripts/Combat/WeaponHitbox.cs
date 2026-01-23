using UnityEngine;
using RobotGame.Components;

namespace RobotGame.Combat
{
    /// <summary>
    /// Componente que se coloca en cada hitbox de un arma.
    /// Detecta colisiones mediante OnTriggerEnter cuando está activo.
    /// 
    /// Configuración en prefab:
    /// 1. Crear GameObject hijo con Collider (isTrigger = true)
    /// 2. Agregar este componente
    /// 3. El GameObject padre debe tener CombatPart
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class WeaponHitbox : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = false;
        
        // Referencias (se configuran al activar)
        private CombatPart ownerPart;
        private float currentDamage;
        private int currentAttackId;
        private GameObject attackerRoot;
        private bool isActive = false;
        
        // Tracking de hits para evitar duplicados
        private System.Collections.Generic.HashSet<GameObject> hitThisAttack = new System.Collections.Generic.HashSet<GameObject>();
        
        // Eventos
        public System.Action<PartHealth, float> OnHitPart;
        public System.Action<Damageable, float> OnHitDamageable;
        
        #region Properties
        
        public bool IsActive => isActive;
        public CombatPart OwnerPart => ownerPart;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            // Asegurar que el collider sea trigger
            Collider col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
            {
                col.isTrigger = true;
                Debug.LogWarning($"[WeaponHitbox] {gameObject.name}: Collider cambiado a isTrigger=true");
            }
            
            // Buscar CombatPart en padres
            ownerPart = GetComponentInParent<CombatPart>();
            if (ownerPart == null)
            {
                Debug.LogError($"[WeaponHitbox] {gameObject.name}: No se encontró CombatPart en padres!");
            }
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (!isActive) return;
            
            // Ignorar si es parte del atacante
            if (attackerRoot != null && other.transform.IsChildOf(attackerRoot.transform))
            {
                return;
            }
            
            // Ignorar si ya golpeamos este objeto en este ataque
            if (hitThisAttack.Contains(other.gameObject))
            {
                return;
            }
            
            // Intentar hacer daño
            bool hitSomething = TryDamage(other.gameObject);
            
            if (hitSomething)
            {
                hitThisAttack.Add(other.gameObject);
            }
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Activa el hitbox para detectar colisiones.
        /// </summary>
        public void Activate(float damage, GameObject attacker, int attackId)
        {
            currentDamage = damage;
            attackerRoot = attacker;
            currentAttackId = attackId;
            isActive = true;
            hitThisAttack.Clear();
            
            if (showDebugLogs)
            {
                Debug.Log($"[WeaponHitbox] {gameObject.name} ACTIVADO - Daño: {damage}, AttackId: {attackId}");
            }
        }
        
        /// <summary>
        /// Desactiva el hitbox.
        /// </summary>
        public void Deactivate()
        {
            isActive = false;
            hitThisAttack.Clear();
            
            if (showDebugLogs)
            {
                Debug.Log($"[WeaponHitbox] {gameObject.name} DESACTIVADO");
            }
        }
        
        #endregion
        
        #region Private Methods
        
        private bool TryDamage(GameObject target)
        {
            bool hitSomething = false;
            
            // Intentar PartHealth (partes de robot)
            PartHealth partHealth = target.GetComponent<PartHealth>();
            if (partHealth != null)
            {
                // PartHealth maneja internamente la verificación de attackId
                // No verificamos aquí para evitar duplicación
                bool damaged = partHealth.TakeDamage(currentDamage, attackerRoot, currentAttackId);
                
                if (damaged)
                {
                    hitSomething = true;
                    OnHitPart?.Invoke(partHealth, currentDamage);
                    
                    if (showDebugLogs)
                    {
                        Debug.Log($"[WeaponHitbox] HIT PartHealth: {target.name} por {currentDamage} daño");
                    }
                }
                
                return hitSomething;
            }
            
            // Intentar Damageable (objetos genéricos como DummyTarget)
            Damageable damageable = target.GetComponent<Damageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(currentDamage);
                hitSomething = true;
                
                OnHitDamageable?.Invoke(damageable, currentDamage);
                
                if (showDebugLogs)
                {
                    Debug.Log($"[WeaponHitbox] HIT Damageable: {target.name} por {currentDamage} daño");
                }
                
                return hitSomething;
            }
            
            return hitSomething;
        }
        
        #endregion
        
        #region Debug
        
        private void OnDrawGizmos()
        {
            if (!isActive) return;
            
            // Mostrar el collider activo en rojo
            Collider col = GetComponent<Collider>();
            if (col == null) return;
            
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            
            if (col is BoxCollider box)
            {
                Matrix4x4 oldMatrix = Gizmos.matrix;
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.matrix = oldMatrix;
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawSphere(transform.TransformPoint(sphere.center), sphere.radius);
                Gizmos.DrawWireSphere(transform.TransformPoint(sphere.center), sphere.radius);
            }
            else if (col is CapsuleCollider capsule)
            {
                // Simplificado - solo dibuja esfera en el centro
                Gizmos.DrawSphere(transform.TransformPoint(capsule.center), capsule.radius);
            }
        }
        
        #endregion
    }
}
