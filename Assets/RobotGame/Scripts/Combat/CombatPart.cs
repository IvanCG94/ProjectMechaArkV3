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
        
        // Zonas de ataque vinculadas (encontradas en el brazo padre)
        private List<AttackZone> linkedAttackZones = new List<AttackZone>();
        
        #region Properties
        
        public float BaseDamage => baseDamage;
        public IReadOnlyList<AttackData> AvailableAttacks => availableAttacks;
        public IReadOnlyList<WeaponHitbox> WeaponHitboxes => weaponHitboxes;
        public StructuralPart StructuralPart => structuralPart;
        public bool CanAttack => availableAttacks != null && availableAttacks.Count > 0;
        public bool HasHitboxes => weaponHitboxes != null && weaponHitboxes.Count > 0;
        
        /// <summary>
        /// Zonas de ataque vinculadas a esta parte.
        /// </summary>
        public IReadOnlyList<AttackZone> LinkedAttackZones => linkedAttackZones;
        
        /// <summary>
        /// Obtiene una zona de ataque específica por su ID.
        /// </summary>
        public AttackZone GetAttackZone(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId)) return null;
            
            foreach (var zone in linkedAttackZones)
            {
                if (zone != null && zone.ZoneId == zoneId)
                {
                    return zone;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Alcance de ataque melee desde el centro del robot.
        /// Obtiene el valor del StructuralPart padre (brazo, cola, cabeza, etc.)
        /// </summary>
        public float CombatReach
        {
            get
            {
                // Buscar en este objeto primero
                if (structuralPart != null && structuralPart.PartData != null)
                {
                    if (structuralPart.PartData.combatReach > 0f)
                    {
                        return structuralPart.PartData.combatReach;
                    }
                }
                
                // Buscar en padres (por si CombatPart está en un hijo del brazo)
                StructuralPart parentStructural = GetComponentInParent<StructuralPart>();
                while (parentStructural != null)
                {
                    if (parentStructural.PartData != null && parentStructural.PartData.combatReach > 0f)
                    {
                        return parentStructural.PartData.combatReach;
                    }
                    
                    // Buscar más arriba
                    if (parentStructural.transform.parent != null)
                    {
                        parentStructural = parentStructural.transform.parent.GetComponentInParent<StructuralPart>();
                    }
                    else
                    {
                        break;
                    }
                }
                
                return 0f;
            }
        }
        
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
            
            // Buscar y vincular zonas de ataque en el brazo padre
            LinkAttackZones();
        }
        
        private void OnDestroy()
        {
            // Desvincular todas las zonas al destruirse
            UnlinkAllAttackZones();
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
        
        #region Attack Zones
        
        /// <summary>
        /// Busca AttackZones en el brazo padre y las vincula a los ataques disponibles.
        /// </summary>
        public void LinkAttackZones()
        {
            UnlinkAllAttackZones();
            
            Debug.Log($"[CombatPart] {name}: Buscando AttackZones para vincular...");
            
            // Lista para almacenar todas las zonas encontradas
            List<AttackZone> foundZones = new List<AttackZone>();
            
            // Método 1: Buscar en padres directos (subiendo la jerarquía)
            Transform current = transform.parent;
            while (current != null)
            {
                // Buscar zonas en los hijos de este nivel
                foreach (Transform child in current)
                {
                    AttackZone zone = child.GetComponent<AttackZone>();
                    if (zone != null && !foundZones.Contains(zone))
                    {
                        foundZones.Add(zone);
                        Debug.Log($"[CombatPart] Encontrada zona '{zone.ZoneId}' en '{child.name}' (hijo de '{current.name}')");
                    }
                }
                
                // Buscar zonas en el padre mismo
                AttackZone parentZone = current.GetComponent<AttackZone>();
                if (parentZone != null && !foundZones.Contains(parentZone))
                {
                    foundZones.Add(parentZone);
                    Debug.Log($"[CombatPart] Encontrada zona '{parentZone.ZoneId}' en padre '{current.name}'");
                }
                
                current = current.parent;
            }
            
            // Método 2: Buscar en todo el robot (por si la estructura es diferente)
            Transform root = transform.root;
            AttackZone[] allZonesInRobot = root.GetComponentsInChildren<AttackZone>(true);
            foreach (var zone in allZonesInRobot)
            {
                if (!foundZones.Contains(zone))
                {
                    foundZones.Add(zone);
                    Debug.Log($"[CombatPart] Encontrada zona '{zone.ZoneId}' en '{zone.name}' (búsqueda global)");
                }
            }
            
            Debug.Log($"[CombatPart] {name}: Total de zonas encontradas: {foundZones.Count}");
            
            // Vincular zonas a ataques por zoneId
            foreach (var attack in availableAttacks)
            {
                if (attack == null) continue;
                
                Debug.Log($"[CombatPart] Procesando ataque '{attack.attackName}', zoneId: '{attack.zoneId}', RequiresZone: {attack.RequiresZone}");
                
                if (!attack.RequiresZone)
                {
                    Debug.Log($"[CombatPart] Ataque '{attack.attackName}' no requiere zona (zoneId vacío)");
                    continue;
                }
                
                // Buscar zona con el mismo zoneId
                AttackZone matchingZone = foundZones.Find(z => 
                    z.ZoneId.Equals(attack.zoneId, System.StringComparison.OrdinalIgnoreCase));
                
                if (matchingZone != null)
                {
                    matchingZone.Link(attack, this);
                    linkedAttackZones.Add(matchingZone);
                    Debug.Log($"[CombatPart] ✓ Zona '{matchingZone.ZoneId}' vinculada a ataque '{attack.attackName}'");
                }
                else
                {
                    Debug.LogWarning($"[CombatPart] ✗ No se encontró AttackZone con id '{attack.zoneId}' para ataque '{attack.attackName}'");
                    Debug.LogWarning($"[CombatPart] Zonas disponibles: {string.Join(", ", foundZones.ConvertAll(z => z.ZoneId))}");
                }
            }
            
            Debug.Log($"[CombatPart] {name}: Vinculación completada. Zonas vinculadas: {linkedAttackZones.Count}");
        }
        
        /// <summary>
        /// Desvincula todas las zonas de ataque.
        /// </summary>
        public void UnlinkAllAttackZones()
        {
            foreach (var zone in linkedAttackZones)
            {
                if (zone != null)
                {
                    zone.Unlink();
                }
            }
            linkedAttackZones.Clear();
        }
        
        /// <summary>
        /// Obtiene los ataques que actualmente tienen al jugador en su zona.
        /// Incluye también ataques que no requieren zona.
        /// </summary>
        public List<AttackData> GetViableAttacks()
        {
            List<AttackData> viable = new List<AttackData>();
            
            foreach (var attack in availableAttacks)
            {
                if (attack == null) continue;
                
                // Si el ataque no requiere zona, siempre es viable
                if (!attack.RequiresZone)
                {
                    viable.Add(attack);
                    continue;
                }
                
                // Buscar la zona vinculada a este ataque
                AttackZone linkedZone = linkedAttackZones.Find(z => z.LinkedAttack == attack);
                
                if (linkedZone != null && linkedZone.IsPlayerInZone)
                {
                    viable.Add(attack);
                }
            }
            
            return viable;
        }
        
        /// <summary>
        /// Verifica si un ataque específico es viable (jugador en zona o no requiere zona).
        /// </summary>
        public bool IsAttackViable(AttackData attack)
        {
            if (attack == null) return false;
            
            // Si no requiere zona, siempre es viable
            if (!attack.RequiresZone)
            {
                Debug.Log($"[CombatPart] IsAttackViable '{attack.attackName}': TRUE (no requiere zona)");
                return true;
            }
            
            // Buscar la zona vinculada
            AttackZone linkedZone = linkedAttackZones.Find(z => z.LinkedAttack == attack);
            
            if (linkedZone == null)
            {
                Debug.Log($"[CombatPart] IsAttackViable '{attack.attackName}': FALSE (zona no encontrada en linkedAttackZones, count: {linkedAttackZones.Count})");
                return false;
            }
            
            bool result = linkedZone.IsPlayerInZone;
            Debug.Log($"[CombatPart] IsAttackViable '{attack.attackName}': {result} (zona '{linkedZone.ZoneId}' PlayerInZone = {linkedZone.IsPlayerInZone})");
            
            return result;
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
