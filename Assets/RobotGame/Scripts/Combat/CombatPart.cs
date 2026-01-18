using System.Collections.Generic;
using UnityEngine;
using RobotGame.Components;

namespace RobotGame.Combat
{
    /// <summary>
    /// Componente que convierte una StructuralPart en una parte capaz de atacar.
    /// </summary>
    [RequireComponent(typeof(StructuralPart))]
    public class CombatPart : MonoBehaviour
    {
        [Header("Estadísticas de Combate")]
        [SerializeField] private float baseDamage = 20f;
        [SerializeField] private float partReach = 0.2f;
        
        [Header("Hitbox")]
        [SerializeField] private float hitboxRadius = 0.3f;
        [SerializeField] private bool autoCalculateHitbox = true;
        [SerializeField] private Transform hitboxOrigin;
        
        [Header("Ataques Disponibles")]
        [SerializeField] private List<AttackData> availableAttacks = new List<AttackData>();
        
        [Header("Debug")]
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private Color gizmoColor = Color.red;
        
        private StructuralPart structuralPart;
        
        #region Properties
        
        public float BaseDamage => baseDamage;
        public float PartReach => partReach;
        public float HitboxRadius => hitboxRadius;
        public Transform HitboxOrigin => hitboxOrigin;
        public IReadOnlyList<AttackData> AvailableAttacks => availableAttacks;
        public StructuralPart StructuralPart => structuralPart;
        public bool CanAttack => availableAttacks != null && availableAttacks.Count > 0;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            structuralPart = GetComponent<StructuralPart>();
            
            if (hitboxOrigin == null)
            {
                hitboxOrigin = transform;
            }
            
            if (autoCalculateHitbox)
            {
                CalculateHitboxFromMesh();
            }
        }
        
        private void OnValidate()
        {
            baseDamage = Mathf.Max(0f, baseDamage);
            partReach = Mathf.Max(0f, partReach);
            hitboxRadius = Mathf.Max(0.05f, hitboxRadius);
        }
        
        #endregion
        
        #region Hitbox Calculation
        
        public void CalculateHitboxFromMesh()
        {
            Bounds combinedBounds = new Bounds(transform.position, Vector3.zero);
            bool foundMesh = false;
            
            MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in renderers)
            {
                if (foundMesh)
                    combinedBounds.Encapsulate(renderer.bounds);
                else
                {
                    combinedBounds = renderer.bounds;
                    foundMesh = true;
                }
            }
            
            SkinnedMeshRenderer[] skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var renderer in skinnedRenderers)
            {
                if (foundMesh)
                    combinedBounds.Encapsulate(renderer.bounds);
                else
                {
                    combinedBounds = renderer.bounds;
                    foundMesh = true;
                }
            }
            
            if (!foundMesh)
            {
                hitboxRadius = 0.3f;
                return;
            }
            
            Vector3 size = combinedBounds.size;
            float avgAxis = (size.x + size.y + size.z) / 3f;
            hitboxRadius = avgAxis / 2f;
            hitboxRadius = Mathf.Max(0.1f, hitboxRadius);
            
            Debug.Log($"[CombatPart] {gameObject.name}: Radio calculado = {hitboxRadius:F2}m");
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
        
        public Vector3 GetHitboxWorldPosition()
        {
            return hitboxOrigin != null ? hitboxOrigin.position : transform.position;
        }
        
        #endregion
        
        #region Debug
        
        private void OnDrawGizmosSelected()
        {
            if (!showGizmos) return;
            
            Vector3 origin = hitboxOrigin != null ? hitboxOrigin.position : transform.position;
            
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(origin, hitboxRadius);
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(origin, 0.03f);
        }
        
        #endregion
    }
}
