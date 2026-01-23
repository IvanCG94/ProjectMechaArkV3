using UnityEngine;
using RobotGame.Combat;

namespace RobotGame.Components
{
    /// <summary>
    /// Asigna los layers correctos al robot y sus partes.
    /// NO crea colliders - debes agregarlos manualmente en los prefabs.
    /// 
    /// Layers:
    /// - Robot raíz: RobotNavigation (9) - para el capsule de navegación
    /// - Partes con colliders físicos: RobotParts (10) - colisionan con jugador
    /// - Partes con PartHealth: RobotHitbox (11) - triggers para recibir daño
    /// </summary>
    public class RobotColliderSetup : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool logLayerAssignments = false;
        
        private Robot robot;
        
        /// <summary>
        /// Inicializa el sistema de layers. Llamar después de ensamblar el robot.
        /// </summary>
        public void Initialize(Robot robot)
        {
            this.robot = robot;
            
            // Asegurar que la matriz de colisiones esté configurada
            CollisionLayerInitializer.Initialize();
            
            // Asignar layers
            AssignLayers();
        }
        
        /// <summary>
        /// Asigna los layers correctos a todas las partes.
        /// Llamar después de agregar/quitar partes.
        /// </summary>
        public void AssignLayers()
        {
            // Robot raíz usa RobotNavigation (para el capsule)
            gameObject.layer = RobotLayers.ROBOT_NAVIGATION;
            
            if (logLayerAssignments)
            {
                Debug.Log($"[RobotColliderSetup] {gameObject.name} → Layer {RobotLayers.ROBOT_NAVIGATION} (RobotNavigation)");
            }
            
            // Asignar layers a las partes estructurales
            StructuralPart[] parts = GetComponentsInChildren<StructuralPart>();
            foreach (StructuralPart part in parts)
            {
                AssignPartLayer(part.gameObject);
            }
            
            // Asignar layers a partes con PartHealth que no son StructuralPart
            PartHealth[] healthParts = GetComponentsInChildren<PartHealth>();
            foreach (PartHealth health in healthParts)
            {
                if (health.GetComponent<StructuralPart>() == null)
                {
                    AssignPartLayer(health.gameObject);
                }
            }
        }
        
        /// <summary>
        /// Asigna el layer correcto a una parte basándose en sus colliders.
        /// </summary>
        private void AssignPartLayer(GameObject partGO)
        {
            // Verificar qué tipo de colliders tiene
            Collider[] colliders = partGO.GetComponents<Collider>();
            
            bool hasPhysicalCollider = false;
            bool hasTrigger = false;
            
            foreach (Collider col in colliders)
            {
                if (col.isTrigger)
                    hasTrigger = true;
                else
                    hasPhysicalCollider = true;
            }
            
            // Decidir el layer
            int targetLayer;
            string layerName;
            
            if (hasPhysicalCollider)
            {
                // Tiene collider físico → RobotParts (colisiona con jugador)
                targetLayer = RobotLayers.ROBOT_PARTS;
                layerName = "RobotParts";
            }
            else if (hasTrigger)
            {
                // Solo tiene triggers → RobotHitbox
                targetLayer = RobotLayers.ROBOT_HITBOX;
                layerName = "RobotHitbox";
            }
            else
            {
                // No tiene colliders → RobotHitbox por defecto (no afecta física)
                targetLayer = RobotLayers.ROBOT_HITBOX;
                layerName = "RobotHitbox (sin collider)";
            }
            
            partGO.layer = targetLayer;
            
            if (logLayerAssignments)
            {
                Debug.Log($"[RobotColliderSetup] {partGO.name} → Layer {targetLayer} ({layerName})");
            }
        }
    }
}
