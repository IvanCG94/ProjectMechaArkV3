using UnityEngine;
using RobotGame.Data;
using RobotGame.Components;
using RobotGame.Systems;

namespace RobotGame
{
    /// <summary>
    /// Script de inicialización del juego.
    /// Crea el robot inicial del jugador a partir de una configuración.
    /// </summary>
    public class GameInitializer : MonoBehaviour
    {
        [Header("Configuración Inicial")]
        [Tooltip("Configuración del robot inicial del jugador")]
        [SerializeField] private RobotConfiguration initialRobotConfig;
        
        [Header("Spawn")]
        [Tooltip("Punto donde aparece el robot")]
        [SerializeField] private Transform spawnPoint;
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = true;
        
        [Header("Referencias (Auto-asignadas)")]
        [SerializeField] private Robot playerRobot;
        
        /// <summary>
        /// Robot actual del jugador.
        /// </summary>
        public Robot PlayerRobot => playerRobot;
        
        private void Start()
        {
            InitializeGame();
        }
        
        /// <summary>
        /// Inicializa el juego creando el robot del jugador.
        /// </summary>
        public void InitializeGame()
        {
            if (initialRobotConfig == null)
            {
                Debug.LogError("GameInitializer: No hay configuración de robot inicial asignada.");
                return;
            }
            
            // Configurar el spawn point del factory
            RobotFactory factory = RobotFactory.Instance;
            
            if (spawnPoint != null)
            {
                // El factory usará este punto si está configurado en él
            }
            
            // Crear el robot del jugador
            playerRobot = factory.CreateRobot(initialRobotConfig, insertCore: true);
            
            if (playerRobot != null)
            {
                // Posicionar en el spawn point
                if (spawnPoint != null)
                {
                    playerRobot.transform.position = spawnPoint.position;
                    playerRobot.transform.rotation = spawnPoint.rotation;
                }
                
                if (debugMode)
                {
                    PrintRobotInfo(playerRobot);
                }
            }
            else
            {
                Debug.LogError("GameInitializer: No se pudo crear el robot del jugador.");
            }
        }
        
        /// <summary>
        /// Imprime información de debug sobre el robot.
        /// </summary>
        private void PrintRobotInfo(Robot robot)
        {
            Debug.Log("=== ROBOT INFO ===");
            Debug.Log($"Nombre: {robot.RobotName}");
            Debug.Log($"ID: {robot.RobotId}");
            Debug.Log($"Tier: {robot.CurrentTier}");
            Debug.Log($"Operacional: {robot.IsOperational}");
            
            if (robot.Core != null)
            {
                Debug.Log($"Core: {robot.Core.CoreData.displayName}");
                Debug.Log($"Energía: {robot.Core.CurrentEnergy}/{robot.Core.MaxEnergy}");
            }
            
            Debug.Log("--- Piezas Estructurales ---");
            foreach (var part in robot.GetAllStructuralParts())
            {
                Debug.Log($"  - {part.PartData.displayName} ({part.PartData.partType})");
                Debug.Log($"    Sockets: {part.ChildSockets.Count}, Grillas: {part.ArmorGrids.Count}");
            }
            
            Debug.Log("--- Piezas de Armadura ---");
            foreach (var armor in robot.GetAllArmorParts())
            {
                Debug.Log($"  - {armor.ArmorData.displayName}");
                Debug.Log($"    Tamaño: {armor.ArmorData.Size}, Surrounding: {armor.ArmorData.Surrounding}");
            }
            
            Debug.Log($"Peso Total: {robot.CalculateTotalWeight()}");
            Debug.Log($"Armadura Total: {robot.CalculateTotalArmor()}");
            Debug.Log("==================");
        }
        
        /// <summary>
        /// Método de prueba para transferir el core a otro robot.
        /// </summary>
        public void TransferCoreTo(Robot targetRobot)
        {
            if (playerRobot == null || playerRobot.Core == null)
            {
                Debug.LogWarning("No hay core para transferir.");
                return;
            }
            
            if (targetRobot == null)
            {
                Debug.LogWarning("Robot destino es null.");
                return;
            }
            
            // Extraer el core del robot actual
            RobotCore core = playerRobot.Core;
            core.Extract();
            
            // Insertar en el nuevo robot
            if (core.InsertInto(targetRobot))
            {
                playerRobot = targetRobot;
                Debug.Log($"Core transferido a {targetRobot.RobotName}");
            }
            else
            {
                // Si falla, reinsertar en el original
                core.InsertInto(playerRobot);
                Debug.LogWarning("Falló la transferencia del core.");
            }
        }
    }
}
