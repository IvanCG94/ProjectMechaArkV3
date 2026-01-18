using UnityEngine;
using RobotGame.Data;
using RobotGame.Enums;
using RobotGame.Control;

namespace RobotGame.Components
{
    /// <summary>
    /// Componente runtime que representa el Core del robot.
    /// El Core es el "alma" que se puede transferir entre cuerpos de robot.
    /// 
    /// NOTA: El movimiento está delegado a PlayerController (script independiente).
    /// Este script solo notifica cuando el Core se inserta/extrae.
    /// </summary>
    public class RobotCore : MonoBehaviour
    {
        [Header("Datos")]
        [SerializeField] private CoreData coreData;
        [SerializeField] private string instanceId;
        
        [Header("Jugador")]
        [SerializeField] private bool isPlayerCore = false;
        
        [Header("Estado")]
        [SerializeField] private float currentEnergy;
        [SerializeField] private bool isActive = false;
        
        [Header("Conexión")]
        [SerializeField] private Robot currentRobot;
        
        // Evento para notificar cambios de robot (usado por PlayerController y PlayerCamera)
        public static event System.Action<RobotCore, Robot> OnPlayerRobotChanged;
        
        #region Properties
        
        /// <summary>
        /// Datos del core (ScriptableObject).
        /// </summary>
        public CoreData CoreData => coreData;
        
        /// <summary>
        /// ID único de esta instancia.
        /// </summary>
        public string InstanceId => instanceId;
        
        /// <summary>
        /// Si este es el core del jugador.
        /// </summary>
        public bool IsPlayerCore => isPlayerCore;
        
        /// <summary>
        /// Tier del core.
        /// </summary>
        public TierInfo Tier => coreData != null ? coreData.tier : TierInfo.Tier1_1;
        
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
        
        #endregion
        
        #region Initialization
        
        /// <summary>
        /// Marca este core como el core del jugador.
        /// </summary>
        public void SetAsPlayerCore(bool isPlayer)
        {
            isPlayerCore = isPlayer;
        }
        
        /// <summary>
        /// Inicializa el core con sus datos.
        /// </summary>
        public void Initialize(CoreData data, string id = null, bool playerCore = false)
        {
            coreData = data;
            instanceId = id ?? System.Guid.NewGuid().ToString();
            currentEnergy = data != null ? data.maxEnergy : 100f;
            isActive = false;
            currentRobot = null;
            isPlayerCore = playerCore;
        }
        
        #endregion
        
        #region Core Operations
        
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
                Debug.LogWarning("RobotCore: Intentando insertar en un robot null.");
                return false;
            }
            
            if (isActive && currentRobot != null)
            {
                Debug.LogWarning("RobotCore: Ya está insertado en otro robot. Extráelo primero.");
                return false;
            }
            
            // Buscar el socket del core en el robot
            Transform coreSocketTransform = robot.FindCoreSocket();
            
            if (coreSocketTransform == null)
            {
                Debug.LogWarning("RobotCore: El robot no tiene CoreSocket disponible.");
                return false;
            }
            
            currentRobot = robot;
            isActive = true;
            
            // Posicionar el core en el socket
            transform.SetParent(coreSocketTransform);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            
            // Mostrar el Core (por si estaba oculto)
            SetCoreVisible(true);
            
            robot.OnCoreInserted(this);
            
            // Notificar cambio de robot (PlayerController y PlayerCamera escuchan esto)
            if (isPlayerCore)
            {
                OnPlayerRobotChanged?.Invoke(this, robot);
            }
            
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
            
            // Ocultar el Core cuando está extraído (va al "inventario")
            SetCoreVisible(false);
            
            // Notificar que ya no hay robot
            if (isPlayerCore)
            {
                OnPlayerRobotChanged?.Invoke(this, null);
            }
            
            return previousRobot;
        }
        
        /// <summary>
        /// Muestra u oculta el Core visualmente.
        /// </summary>
        private void SetCoreVisible(bool visible)
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = visible;
            }
            
            foreach (var collider in GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = visible;
            }
        }
        
        #endregion
        
        #region Energy
        
        /// <summary>
        /// Consume energía del core.
        /// </summary>
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
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Update()
        {
            // Solo regenerar energía
            if (isActive && coreData != null)
            {
                float regenAmount = coreData.energyRegenRate * Time.deltaTime;
                RechargeEnergy(regenAmount);
            }
        }
        
        #endregion
        
        #region Movement Control (Legacy - para compatibilidad con AssemblyTester)
        
        // Estos métodos son llamados por AssemblyTester para deshabilitar movimiento en modo edición.
        // Ahora delegan a PlayerController si existe.
        
        /// <summary>
        /// Deshabilita el movimiento (llamado al entrar en modo edición).
        /// </summary>
        public void DisableMovement()
        {
            var movement = FindObjectOfType<PlayerController>();
            if (movement != null)
            {
                movement.EnterEditModeState();
            }
        }
        
        /// <summary>
        /// Habilita el movimiento (llamado al salir de modo edición).
        /// </summary>
        public void EnableMovement()
        {
            var movement = FindObjectOfType<PlayerController>();
            if (movement != null)
            {
                movement.ExitEditModeState();
            }
        }
        
        #endregion
    }
}
