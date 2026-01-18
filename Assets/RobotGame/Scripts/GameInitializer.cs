using UnityEngine;
using RobotGame.Data;
using RobotGame.Components;
using RobotGame.Systems;
using RobotGame.Control;
using RobotGame.Combat;

using RobotGame.UI;

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
        [SerializeField] private PlayerController playerController;
        [SerializeField] private PlayerCamera playerCamera;
        [SerializeField] private CombatController combatController;
        [SerializeField] private CombatInputHandler combatInputHandler;
        
        /// <summary>
        /// Robot actual del jugador.
        /// </summary>
        public Robot PlayerRobot => playerRobot;
        
        /// <summary>
        /// Core del jugador.
        /// </summary>
        public RobotCore PlayerCore => playerCore;
        
        /// <summary>
        /// Controlador del jugador.
        /// </summary>
        public PlayerController PlayerController => playerController;
        
        /// <summary>
        /// Controlador de combate del jugador.
        /// </summary>
        public CombatController CombatController => combatController;
        
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

        private void OnGUI()
{
    GUILayout.BeginArea(new Rect(10, 200, 400, 300));
    GUILayout.Label($"InventoryPanel: {FindObjectOfType<InventoryPanelUI>()}");
    GUILayout.Label($"Canvas: {FindObjectOfType<Canvas>()}");
    GUILayout.Label($"EventSystem: {FindObjectOfType<UnityEngine.EventSystems.EventSystem>()}");
    GUILayout.EndArea();
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
            SetupPlayerController();
            SetupCamera();
            SetupCombat();
            
            if (debugMode)
            {
                Debug.Log($"Robot creado: {playerRobot.RobotName}");
                Debug.Log($"Core del jugador: {(playerCore != null ? "OK" : "NO ENCONTRADO")}");
                Debug.Log($"Controles: WASD=mover, Space=saltar, Shift=correr, Click/J=atacar");
            }
        }
        
        private void SetupPlayerController()
        {
            // Buscar o crear PlayerController
            playerController = FindObjectOfType<PlayerController>();
            if (playerController == null)
            {
                // Crear un GameObject para el controlador
                GameObject controllerGO = new GameObject("PlayerController");
                playerController = controllerGO.AddComponent<PlayerController>();
                
                // Agregar PlayerAnimator al mismo objeto
                controllerGO.AddComponent<PlayerAnimator>();
            }
            else
            {
                // Si ya existe PlayerController, asegurar que tenga PlayerAnimator
                if (playerController.GetComponent<PlayerAnimator>() == null)
                {
                    playerController.gameObject.AddComponent<PlayerAnimator>();
                }
            }
            
            playerController.SetTarget(playerRobot.transform);
            playerController.Enable();
        }
        
        private void SetupCamera()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
            
            if (mainCamera != null)
            {
                // Buscar PlayerCamera
                playerCamera = mainCamera.GetComponent<PlayerCamera>();
                if (playerCamera == null)
                {
                    // Crear nuevo PlayerCamera
                    playerCamera = mainCamera.gameObject.AddComponent<PlayerCamera>();
                }
                
                playerCamera.SetTarget(playerRobot.transform, true);
            }
        }
        
        private void SetupCombat()
        {
            if (playerRobot == null) return;
            
            // Agregar CombatController al robot del jugador
            combatController = playerRobot.GetComponent<CombatController>();
            if (combatController == null)
            {
                combatController = playerRobot.gameObject.AddComponent<CombatController>();
            }
            
            // Agregar CombatInputHandler junto al PlayerController
            if (playerController != null)
            {
                combatInputHandler = playerController.GetComponent<CombatInputHandler>();
                if (combatInputHandler == null)
                {
                    combatInputHandler = playerController.gameObject.AddComponent<CombatInputHandler>();
                }
                
                // Asignar la referencia al CombatController
                combatInputHandler.SetCombatController(combatController);
            }
            
            // Refrescar las partes de combate después de un pequeño delay
            // (para asegurar que todas las partes estén inicializadas)
            Invoke(nameof(RefreshCombatParts), 0.1f);
            
            if (debugMode)
            {
                Debug.Log("[GameInitializer] Sistema de combate configurado");
            }
        }
        
        private void RefreshCombatParts()
        {
            if (combatController != null)
            {
                combatController.RefreshCombatParts();
            }
        }
        
        private void OnPlayerRobotChanged(RobotCore core, Robot newRobot)
        {
            if (core != playerCore) return;
            
            playerRobot = newRobot;
            
            // Actualizar controlador
            if (playerController != null)
            {
                if (newRobot != null)
                {
                    playerController.SetTarget(newRobot.transform);
                    playerController.Enable();
                }
                else
                {
                    playerController.SetTarget(null);
                    playerController.Disable();
                }
            }
            
            // Actualizar cámara
            if (playerCamera != null && newRobot != null)
            {
                playerCamera.SetTarget(newRobot.transform, false);
            }
            
            // Actualizar combate - mover CombatController al nuevo robot
            if (newRobot != null)
            {
                // Remover del robot anterior si existe
                if (combatController != null && combatController.gameObject != newRobot.gameObject)
                {
                    Destroy(combatController);
                }
                
                // Agregar al nuevo robot
                combatController = newRobot.GetComponent<CombatController>();
                if (combatController == null)
                {
                    combatController = newRobot.gameObject.AddComponent<CombatController>();
                }
                
                // Refrescar partes de combate
                Invoke(nameof(RefreshCombatParts), 0.1f);
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
