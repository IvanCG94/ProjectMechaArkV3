using UnityEngine;
using RobotGame.Data;
using RobotGame.Enums;

namespace RobotGame.Components
{
    /// <summary>
    /// Componente runtime que representa el Core del jugador.
    /// El Core es el "alma" que se puede transferir entre cuerpos de robot.
    /// </summary>
    public class RobotCore : MonoBehaviour
    {
        [Header("Datos")]
        [SerializeField] private CoreData coreData;
        [SerializeField] private string instanceId;
        
        [Header("Estado")]
        [SerializeField] private float currentEnergy;
        [SerializeField] private bool isActive = false;
        
        [Header("Conexión")]
        [SerializeField] private Robot currentRobot;
        
        /// <summary>
        /// Datos del core (ScriptableObject).
        /// </summary>
        public CoreData CoreData => coreData;
        
        /// <summary>
        /// ID único de esta instancia.
        /// </summary>
        public string InstanceId => instanceId;
        
        /// <summary>
        /// Tier del core.
        /// </summary>
        public RobotTier Tier => coreData != null ? coreData.tier : RobotTier.Tier1_1;
        
        /// <summary>
        /// Energía actual.
        /// </summary>
        public float CurrentEnergy => currentEnergy;
        
        /// <summary>
        /// Energía máxima.
        /// </summary>
        public float MaxEnergy => coreData != null ? coreData.maxEnergy : 0f;
        
        /// <summary>
        /// Porcentaje de energía (0-1).
        /// </summary>
        public float EnergyPercent => MaxEnergy > 0 ? currentEnergy / MaxEnergy : 0f;
        
        /// <summary>
        /// Si el core está activo (insertado en un robot).
        /// </summary>
        public bool IsActive => isActive;
        
        /// <summary>
        /// Robot actual donde está insertado el core.
        /// </summary>
        public Robot CurrentRobot => currentRobot;
        
        /// <summary>
        /// Inicializa el core con sus datos.
        /// </summary>
        public void Initialize(CoreData data, string id = null)
        {
            coreData = data;
            instanceId = id ?? System.Guid.NewGuid().ToString();
            currentEnergy = data.maxEnergy;
            isActive = false;
            currentRobot = null;
        }
        
        /// <summary>
        /// Verifica si una pieza es compatible con este core.
        /// </summary>
        public bool CanUsePart(PartDataBase part)
        {
            return coreData != null && coreData.CanUsePart(part);
        }
        
        /// <summary>
        /// Inserta el core en un robot.
        /// </summary>
        public bool InsertInto(Robot robot)
        {
            if (robot == null)
            {
                Debug.LogWarning("Intentando insertar core en un robot null.");
                return false;
            }
            
            if (isActive && currentRobot != null)
            {
                Debug.LogWarning("El core ya está insertado en otro robot. Extráelo primero.");
                return false;
            }
            
            currentRobot = robot;
            isActive = true;
            
            // Posicionar el core en el socket correspondiente
            transform.SetParent(robot.CoreSocket);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            
            robot.OnCoreInserted(this);
            
            return true;
        }
        
        /// <summary>
        /// Extrae el core del robot actual.
        /// </summary>
        public Robot Extract()
        {
            if (!isActive || currentRobot == null)
            {
                return null;
            }
            
            Robot previousRobot = currentRobot;
            previousRobot.OnCoreExtracted(this);
            
            transform.SetParent(null);
            currentRobot = null;
            isActive = false;
            
            return previousRobot;
        }
        
        /// <summary>
        /// Consume energía del core.
        /// </summary>
        /// <param name="amount">Cantidad de energía a consumir</param>
        /// <returns>True si había suficiente energía</returns>
        public bool ConsumeEnergy(float amount)
        {
            if (currentEnergy >= amount)
            {
                currentEnergy -= amount;
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Recarga energía del core.
        /// </summary>
        public void RechargeEnergy(float amount)
        {
            currentEnergy = Mathf.Min(MaxEnergy, currentEnergy + amount);
        }
        
        private void Update()
        {
            if (isActive && coreData != null)
            {
                // Regenerar energía
                float regenAmount = coreData.energyRegenRate * Time.deltaTime;
                RechargeEnergy(regenAmount);
            }
        }
    }
}
