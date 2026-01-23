using System.Collections.Generic;
using UnityEngine;
using RobotGame.Components;

namespace RobotGame.Combat
{
    /// <summary>
    /// Componente que convierte una StructuralPart en una parte capaz de atacar.
    /// 
    /// Configuración de Hitboxes:
    /// - Crear GameObjects hijos con Colliders (isTrigger = true)
    /// - Nombrarlos "Hitbox" o "Hitbox_1", "Hitbox_2", etc.
    /// - El sistema los detecta automáticamente
    /// - Deben estar DESACTIVADOS por defecto
    /// </summary>
    [RequireComponent(typeof(StructuralPart))]
    public class CombatPart : MonoBehaviour
    {
        [Header("Estadísticas de Combate")]
        [SerializeField] private float baseDamage = 20f;
        
        [Header("Ataques Disponibles")]
        [SerializeField] private List<AttackData> availableAttacks = new List<AttackData>();
        
        [Header("Hitboxes (Auto-detectados)")]
        [SerializeField] private List<WeaponHitbox> weaponHitboxes = new List<WeaponHitbox>();
        [SerializeField] private GameObject hitboxContainer;
        
        [Header("Debug")]
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private Color gizmoColor = new Color(1f, 0f, 0f, 0.3f);
        
        private StructuralPart structuralPart;
        
        #region Properties
        
        public float BaseDamage => baseDamage;
        public IReadOnlyList<AttackData> AvailableAttacks => availableAttacks;
        public IReadOnlyList<WeaponHitbox> WeaponHitboxes => weaponHitboxes;
        public StructuralPart StructuralPart => structuralPart;
        public bool CanAttack => availableAttacks != null && availableAttacks.Count > 0;
        public bool HasHitboxes => weaponHitboxes != null && weaponHitboxes.Count > 0;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            structuralPart = GetComponent<StructuralPart>();
            DetectHitboxes();
        }
        
        private void Start()
        {
            // Intentar configurar Animation Events en Start
            // (el Animator puede haberse agregado en Initialize después de Awake)
            EnsureAnimationEventsComponent();
        }
        
        private void OnValidate()
        {
            baseDamage = Mathf.Max(0f, baseDamage);
        }
        
        #endregion
        
        #region Animation Events Setup
        
        /// <summary>
        /// Asegura que exista el componente AttackAnimationEvents en el GameObject que tiene el Animator.
        /// </summary>
        private void EnsureAnimationEventsComponent()
        {
            // Buscar Animator en este objeto o en padres
            Animator animator = GetComponentInParent<Animator>();
            
            if (animator == null)
            {
                // También revisar en StructuralPart por si se agrega después
                if (structuralPart != null && structuralPart.Animator != null)
                {
                    animator = structuralPart.Animator;
                }
            }
            
            if (animator == null)
            {
                // No hay Animator, no es necesariamente un error
                // Puede que esta parte no tenga animaciones
                return;
            }
            
            // Verificar si ya tiene AttackAnimationEvents
            AttackAnimationEvents animEvents = animator.GetComponent<AttackAnimationEvents>();
            
            if (animEvents == null)
            {
                animEvents = animator.gameObject.AddComponent<AttackAnimationEvents>();
                Debug.Log($"[CombatPart] AttackAnimationEvents agregado automáticamente a '{animator.gameObject.name}'");
            }
        }
        
        /// <summary>
        /// Llamar después de que el Animator esté configurado (ej: después de Initialize de StructuralPart).
        /// </summary>
        public void SetupAnimationEvents()
        {
            EnsureAnimationEventsComponent();
        }
        
        #endregion
        
        #region Hitbox Detection
        
        /// <summary>
        /// Detecta automáticamente los hitboxes hijos.
        /// Busca GameObjects con nombre "Hitbox" o que empiecen con "Hitbox_".
        /// </summary>
        [ContextMenu("Detectar Hitboxes")]
        public void DetectHitboxes()
        {
            weaponHitboxes.Clear();
            hitboxContainer = null;
            
            // Buscar todos los colliders hijos que sean triggers
            Collider[] allColliders = GetComponentsInChildren<Collider>(true); // true = incluir inactivos
            
            foreach (Collider col in allColliders)
            {
                // Ignorar si es el mismo objeto
                if (col.gameObject == gameObject) continue;
                
                // Verificar si es un hitbox por nombre
                string name = col.gameObject.name;
                if (name == "Hitbox" || name.StartsWith("Hitbox_"))
                {
                    // Asegurar que sea trigger
                    if (!col.isTrigger)
                    {
                        col.isTrigger = true;
                    }
                    
                    // Agregar o obtener WeaponHitbox
                    WeaponHitbox weaponHitbox = col.GetComponent<WeaponHitbox>();
                    if (weaponHitbox == null)
                    {
                        weaponHitbox = col.gameObject.AddComponent<WeaponHitbox>();
                    }
                    
                    weaponHitboxes.Add(weaponHitbox);
                    
                    // Guardar referencia al contenedor (padre de los hitboxes)
                    if (hitboxContainer == null && col.transform.parent != transform)
                    {
                        hitboxContainer = col.transform.parent.gameObject;
                    }
                }
            }
            
            // Si no hay contenedor específico, buscar objeto llamado "Hitboxes"
            if (hitboxContainer == null)
            {
                Transform hitboxesTransform = transform.Find("Hitboxes");
                if (hitboxesTransform != null)
                {
                    hitboxContainer = hitboxesTransform.gameObject;
                }
            }
            
            // Si encontramos hitboxes pero no contenedor, el contenedor es el primer hitbox's parent
            if (hitboxContainer == null && weaponHitboxes.Count > 0)
            {
                hitboxContainer = weaponHitboxes[0].transform.parent.gameObject;
                if (hitboxContainer == gameObject)
                {
                    hitboxContainer = null; // Los hitboxes están directamente en este objeto
                }
            }
            
            if (weaponHitboxes.Count > 0)
            {
                Debug.Log($"[CombatPart] {gameObject.name}: Detectados {weaponHitboxes.Count} hitbox(es)");
            }
            else
            {
                Debug.LogWarning($"[CombatPart] {gameObject.name}: No se detectaron hitboxes. " +
                    "Crea GameObjects hijos con Collider y nombre 'Hitbox' o 'Hitbox_1', etc.");
            }
        }
        
        #endregion
        
        #region Hitbox Control
        
        /// <summary>
        /// Activa todos los hitboxes para detectar colisiones.
        /// </summary>
        public void ActivateHitboxes(float damage, GameObject attacker, int attackId)
        {
            // Activar el contenedor si existe
            if (hitboxContainer != null)
            {
                hitboxContainer.SetActive(true);
            }
            
            // Activar cada hitbox individualmente
            foreach (var hitbox in weaponHitboxes)
            {
                if (hitbox != null)
                {
                    hitbox.gameObject.SetActive(true);
                    hitbox.Activate(damage, attacker, attackId);
                }
            }
        }
        
        /// <summary>
        /// Desactiva todos los hitboxes.
        /// </summary>
        public void DeactivateHitboxes()
        {
            // Desactivar cada hitbox
            foreach (var hitbox in weaponHitboxes)
            {
                if (hitbox != null)
                {
                    hitbox.Deactivate();
                    hitbox.gameObject.SetActive(false);
                }
            }
            
            // Desactivar el contenedor si existe
            if (hitboxContainer != null)
            {
                hitboxContainer.SetActive(false);
            }
        }
        
        /// <summary>
        /// Suscribe callbacks a los eventos de hit de todos los hitboxes.
        /// </summary>
        public void SubscribeToHitEvents(System.Action<PartHealth, float> onHitPart, System.Action<Damageable, float> onHitDamageable)
        {
            foreach (var hitbox in weaponHitboxes)
            {
                if (hitbox != null)
                {
                    hitbox.OnHitPart += onHitPart;
                    hitbox.OnHitDamageable += onHitDamageable;
                }
            }
        }
        
        /// <summary>
        /// Desuscribe callbacks de los eventos de hit.
        /// </summary>
        public void UnsubscribeFromHitEvents(System.Action<PartHealth, float> onHitPart, System.Action<Damageable, float> onHitDamageable)
        {
            foreach (var hitbox in weaponHitboxes)
            {
                if (hitbox != null)
                {
                    hitbox.OnHitPart -= onHitPart;
                    hitbox.OnHitDamageable -= onHitDamageable;
                }
            }
        }
        
        #endregion
        
        #region Public Methods
        
        public bool CanExecuteAttack(AttackData attack)
        {
            return attack != null && availableAttacks.Contains(attack);
        }
        
        public float CalculateDamage(AttackData attack)
        {
            if (attack == null) return 0f;
            return baseDamage * attack.damageMultiplier;
        }
        
        public AttackData GetDefaultAttack()
        {
            return availableAttacks.Count > 0 ? availableAttacks[0] : null;
        }
        
        public AttackData GetAttackByName(string attackName)
        {
            return availableAttacks.Find(a => a.attackName == attackName);
        }
        
        #endregion
        
        #region Debug
        
        private void OnDrawGizmosSelected()
        {
            if (!showGizmos) return;
            
            Gizmos.color = gizmoColor;
            
            // Dibujar los hitboxes detectados
            foreach (var hitbox in weaponHitboxes)
            {
                if (hitbox == null) continue;
                
                Collider col = hitbox.GetComponent<Collider>();
                if (col == null) continue;
                
                if (col is BoxCollider box)
                {
                    Matrix4x4 oldMatrix = Gizmos.matrix;
                    Gizmos.matrix = hitbox.transform.localToWorldMatrix;
                    Gizmos.DrawWireCube(box.center, box.size);
                    Gizmos.matrix = oldMatrix;
                }
                else if (col is SphereCollider sphere)
                {
                    Gizmos.DrawWireSphere(hitbox.transform.TransformPoint(sphere.center), sphere.radius);
                }
                else if (col is CapsuleCollider capsule)
                {
                    Vector3 center = hitbox.transform.TransformPoint(capsule.center);
                    Gizmos.DrawWireSphere(center, capsule.radius);
                    // Línea indicando dirección
                    Vector3 dir = capsule.direction == 0 ? Vector3.right : 
                                  capsule.direction == 1 ? Vector3.up : Vector3.forward;
                    float halfHeight = (capsule.height / 2f) - capsule.radius;
                    Gizmos.DrawLine(center - hitbox.transform.TransformDirection(dir) * halfHeight,
                                   center + hitbox.transform.TransformDirection(dir) * halfHeight);
                }
            }
        }
        
        #endregion
    }
}
