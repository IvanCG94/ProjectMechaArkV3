using UnityEngine;
using RobotGame.Data;
using RobotGame.Components;
using RobotGame.Systems;
using RobotGame.Control;

namespace RobotGame
{
    /// <summary>
    /// Script de inicialización del juego.
    /// Crea el robot inicial del jugador a partir de una configuración.
    /// Configura los sistemas de movimiento y cámara.
    /// </summary>
    public class GameInitializer : MonoBehaviour
    {
        [Header("Configuración Inicial")]
        [Tooltip("Configuración del robot inicial del jugador")]
        [SerializeField] private RobotConfiguration initialRobotConfig;
        
        [Header("Spawn")]
        [Tooltip("Punto donde aparece el robot")]
        [SerializeField] private Transform spawnPoint;
        
        [Header("Control")]
        [Tooltip("Cámara principal (si no se asigna, busca Camera.main)")]
        [SerializeField] private Camera mainCamera;
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = true;
        
        [Header("Referencias (Auto-asignadas)")]
        [SerializeField] private Robot playerRobot;
        [SerializeField] private RobotCore playerCore;
        [SerializeField] private PlayerMovement playerMovement;
        [SerializeField] private PlayerCamera playerCamera;
        
        /// <summary>
        /// Robot actual del jugador.
        /// </summary>
        public Robot PlayerRobot => playerRobot;
        
        /// <summary>
        /// Core del jugador.
        /// </summary>
        public RobotCore PlayerCore => playerCore;
        
        private void OnEnable()
        {
            RobotCore.OnPlayerRobotChanged += OnPlayerRobotChanged;
        }
        
        private void OnDisable()
        {
            RobotCore.OnPlayerRobotChanged -= OnPlayerRobotChanged;
        }
        
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
            
            // Crear el robot del jugador
            RobotFactory factory = RobotFactory.Instance;
            playerRobot = factory.CreateRobot(initialRobotConfig, insertCore: true);
            
            if (playerRobot == null)
            {
                Debug.LogError("GameInitializer: No se pudo crear el robot del jugador.");
                return;
            }
            
            // Posicionar en el spawn point
            if (spawnPoint != null)
            {
                playerRobot.transform.position = spawnPoint.position;
                playerRobot.transform.rotation = spawnPoint.rotation;
            }
            
            // Obtener el Core y marcarlo como del jugador
            playerCore = playerRobot.Core;
            if (playerCore != null)
            {
                playerCore.SetAsPlayerCore(true);
            }
            
            // Configurar sistemas de control
            SetupMovement();
            SetupCamera();
            
            if (debugMode)
            {
                Debug.Log($"Robot creado: {playerRobot.RobotName}");
                Debug.Log($"Core del jugador: {(playerCore != null ? "OK" : "NO ENCONTRADO")}");
                Debug.Log($"Controles: WASD=mover, P=modo edición");
            }
        }
        
        private void SetupMovement()
        {
            // Buscar o crear PlayerMovement
            playerMovement = FindObjectOfType<PlayerMovement>();
            if (playerMovement == null)
            {
                // Crear un GameObject para el sistema de movimiento
                GameObject movementGO = new GameObject("PlayerMovement");
                playerMovement = movementGO.AddComponent<PlayerMovement>();
            }
            
            playerMovement.SetTarget(playerRobot.transform);
            playerMovement.Enable();
        }
        
        private void SetupCamera()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
            
            if (mainCamera != null)
            {
                // Buscar PlayerCamera o CameraController (compatibilidad)
                playerCamera = mainCamera.GetComponent<PlayerCamera>();
                if (playerCamera == null)
                {
                    // Intentar con el nombre antiguo
                    var oldController = mainCamera.GetComponent<CameraController>();
                    if (oldController != null)
                    {
                        oldController.SetTarget(playerRobot.transform, true);
                        return;
                    }
                    
                    // Crear nuevo PlayerCamera
                    playerCamera = mainCamera.gameObject.AddComponent<PlayerCamera>();
                }
                
                playerCamera.SetTarget(playerRobot.transform, true);
            }
        }
        
        private void OnPlayerRobotChanged(RobotCore core, Robot newRobot)
        {
            if (core != playerCore) return;
            
            playerRobot = newRobot;
            
            // Actualizar movimiento
            if (playerMovement != null)
            {
                if (newRobot != null)
                {
                    playerMovement.SetTarget(newRobot.transform);
                    playerMovement.Enable();
                }
                else
                {
                    playerMovement.SetTarget(null);
                    playerMovement.Disable();
                }
            }
            
            // Actualizar cámara
            if (playerCamera != null && newRobot != null)
            {
                playerCamera.SetTarget(newRobot.transform, false);
            }
        }
        
        /// <summary>
        /// Método para transferir el core a otro robot.
        /// </summary>
        public void TransferCoreTo(Robot targetRobot)
        {
            if (playerCore == null)
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
            playerCore.Extract();
            
            // Insertar en el nuevo robot
            if (playerCore.InsertInto(targetRobot))
            {
                Debug.Log($"Core transferido a: {targetRobot.RobotName}");
            }
            else
            {
                Debug.LogWarning("Falló la transferencia del core.");
            }
        }
    }
}
