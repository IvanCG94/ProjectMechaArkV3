using UnityEngine;
using RobotGame.Enums;

namespace RobotGame.Data
{
    /// <summary>
    /// ScriptableObject para el Core del jugador.
    /// El Core define el tier del robot y es el "alma" que se puede transferir entre cuerpos.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCore", menuName = "RobotGame/Parts/Core")]
    public class CoreData : PartDataBase
    {
        [Header("Configuración del Core")]
        [Tooltip("Nivel de energía máxima del core")]
        public float maxEnergy = 100f;
        
        [Tooltip("Tasa de regeneración de energía por segundo")]
        public float energyRegenRate = 5f;
        
        [Tooltip("Multiplicador de velocidad de procesamiento (afecta cooldowns)")]
        public float processingSpeed = 1f;
        
        [Header("Capacidades")]
        [Tooltip("Número máximo de sistemas activos simultáneos")]
        public int maxActiveSystems = 3;
        
        [Tooltip("Rango de escaneo/detección")]
        public float scanRange = 20f;
        
        [Header("Visual")]
        [Tooltip("Color del core (para efectos visuales)")]
        public Color coreColor = Color.cyan;
        
        [Tooltip("Prefab de efectos cuando el core está activo")]
        public GameObject activeEffectPrefab;
        
        [Tooltip("Prefab de efectos al transferir el core")]
        public GameObject transferEffectPrefab;
        
        /// <summary>
        /// Verifica si una pieza es compatible con este core.
        /// </summary>
        public bool CanUsePart(PartDataBase part)
        {
            return part.IsCompatibleWith(this.tier);
        }
        
        /// <summary>
        /// Obtiene una lista descriptiva de las piezas compatibles.
        /// Por ejemplo, para Tier1_2: "Compatible con piezas Tier 1-1 y Tier 1-2"
        /// </summary>
        public string GetCompatibilityDescription()
        {
            int mainTier = MainTier;
            int variant = TierVariant;
            
            if (variant == 1)
            {
                return $"Compatible con piezas Tier {mainTier}-1";
            }
            else
            {
                return $"Compatible con piezas Tier {mainTier}-1 hasta Tier {mainTier}-{variant}";
            }
        }
        
        private void OnValidate()
        {
            // El Core no tiene categoría tradicional, pero lo marcamos como Structural
            // ya que es una pieza fundamental
            category = PartCategory.Structural;
        }
    }
}
