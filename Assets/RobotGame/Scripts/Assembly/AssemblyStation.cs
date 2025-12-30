using UnityEngine;
using RobotGame.Components;
using RobotGame.Enums;

namespace RobotGame.Assembly
{
    /// <summary>
    /// Modo de edición en la estación.
    /// </summary>
    public enum StationEditMode
    {
        None,               // No está en modo edición
        EditCurrentRobot,   // Editando robot donde está el jugador (con Core)
        EditOtherPlatform   // Editando robot en la otra plataforma (cascarón)
    }
    
    /// <summary>
    /// Estación de ensamblaje con dos plataformas.
    /// Permite modificar el robot actual o crear nuevos robots.
    /// 
    /// SETUP:
    /// 1. Crear GameObject "AssemblyStation"
    /// 2. Agregar este componente
    /// 3. Crear dos hijos con AssemblyPlatform y colliders trigger
    /// 4. El robot del jugador necesita un Collider para ser detectado
    /// </summary>
    public class AssemblyStation : MonoBehaviour
    {
        [Header("Configuración")]
        [Tooltip("Tier de la estación (determina qué robots puede modificar)")]
        [SerializeField] private RobotTier stationTier = RobotTier.Tier1_1;
        
        [Header("Plataformas")]
        [SerializeField] private AssemblyPlatform platformA;
        [SerializeField] private AssemblyPlatform platformB;
        
        [Header("Input")]
        [SerializeField] private KeyCode activateKey = KeyCode.P;
        [SerializeField] private KeyCode cancelKey = KeyCode.Escape;
        
        [Header("Estado (Solo lectura)")]
        [SerializeField] private StationEditMode currentEditMode = StationEditMode.None;
        [SerializeField] private bool showingMenu;
        
        // Eventos
        public event System.Action<AssemblyStation, StationEditMode> OnEditModeStarted;
        public event System.Action<AssemblyStation> OnEditModeEnded;
        
        #region Properties
        
        /// <summary>
        /// Tier de esta estación.
        /// </summary>
        public RobotTier StationTier => stationTier;
        
        /// <summary>
        /// Si el jugador (robot con Core) está en alguna plataforma de esta estación.
        /// Verifica EN TIEMPO REAL.
        /// </summary>
        public bool PlayerInStation
        {
            get
            {
                return (platformA != null && platformA.HasPlayerRobot) ||
                       (platformB != null && platformB.HasPlayerRobot);
            }
        }
        
        /// <summary>
        /// Plataforma donde está el jugador (robot con Core).
        /// Verifica EN TIEMPO REAL.
        /// </summary>
        public AssemblyPlatform PlayerPlatform
        {
            get
            {
                if (platformA != null && platformA.HasPlayerRobot) return platformA;
                if (platformB != null && platformB.HasPlayerRobot) return platformB;
                return null;
            }
        }
        
        /// <summary>
        /// La otra plataforma (donde no está el jugador).
        /// </summary>
        public AssemblyPlatform OtherPlatform
        {
            get
            {
                var player = PlayerPlatform;
                if (player == null) return null;
                return player == platformA ? platformB : platformA;
            }
        }
        
        /// <summary>
        /// Modo de edición actual.
        /// </summary>
        public StationEditMode CurrentEditMode => currentEditMode;
        
        /// <summary>
        /// Si está mostrando el menú de selección.
        /// </summary>
        public bool ShowingMenu => showingMenu;
        
        /// <summary>
        /// Si está activamente en modo edición.
        /// </summary>
        public bool IsEditing => currentEditMode != StationEditMode.None;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            // Auto-detectar plataformas si no están asignadas
            if (platformA == null || platformB == null)
            {
                var platforms = GetComponentsInChildren<AssemblyPlatform>();
                if (platforms.Length >= 2)
                {
                    platformA = platforms[0];
                    platformB = platforms[1];
                }
                else if (platforms.Length == 1)
                {
                    platformA = platforms[0];
                    Debug.LogWarning("AssemblyStation: Solo se encontró una plataforma");
                }
                else
                {
                    Debug.LogError("AssemblyStation: No se encontraron plataformas hijas");
                }
            }
        }
        
        private void Update()
        {
            // Solo procesar input si el jugador (robot con Core) está en la estación
            // Usa la propiedad que verifica en tiempo real
            if (!PlayerInStation) return;
            
            if (showingMenu)
            {
                HandleMenuInput();
            }
            else if (currentEditMode != StationEditMode.None)
            {
                // Está en modo edición, ESC para salir
                if (Input.GetKeyDown(cancelKey))
                {
                    ExitEditMode();
                }
            }
            else
            {
                // Jugador en plataforma pero no en modo edición
                if (Input.GetKeyDown(activateKey))
                {
                    ShowSelectionMenu();
                }
            }
        }
        
        #endregion
        
        #region Platform Callbacks
        
        /// <summary>
        /// Llamado por AssemblyPlatform cuando el jugador entra.
        /// Nota: Ahora solo para debug, la detección real es via propiedades en tiempo real.
        /// </summary>
        public void OnPlayerEnteredPlatform(AssemblyPlatform platform)
        {
            Debug.Log($"AssemblyStation: Jugador entró a la estación en {platform.gameObject.name}");
            Debug.Log($"AssemblyStation: Presiona {activateKey} para entrar al modo de ensamblaje");
        }
        
        /// <summary>
        /// Llamado por AssemblyPlatform cuando el jugador sale.
        /// </summary>
        public void OnPlayerExitedPlatform(AssemblyPlatform platform)
        {
            // Si estaba en modo edición, manejar salida
            if (currentEditMode != StationEditMode.None)
            {
                Debug.Log("AssemblyStation: Jugador intentando salir durante modo edición");
                return;
            }
            
            showingMenu = false;
            
            Debug.Log("AssemblyStation: Jugador salió de la estación");
        }
        
        #endregion
        
        #region Menu
        
        private void ShowSelectionMenu()
        {
            showingMenu = true;
            
            // Pausar el juego o deshabilitar movimiento
            var movement = FindObjectOfType<RobotGame.Control.PlayerMovement>();
            if (movement != null)
            {
                movement.Disable();
            }
            
            Debug.Log("=== MENÚ DE ENSAMBLAJE ===");
            Debug.Log("[1] Editar robot actual (con Core)");
            Debug.Log($"[2] Otra plataforma: {GetOtherPlatformDescription()}");
            Debug.Log("[ESC] Cancelar");
        }
        
        private string GetOtherPlatformDescription()
        {
            if (OtherPlatform == null) return "No disponible";
            
            if (OtherPlatform.HasShellRobot)
            {
                return "Editar cascarón existente";
            }
            else
            {
                return "Crear nuevo robot";
            }
        }
        
        private void HideSelectionMenu()
        {
            showingMenu = false;
            
            // Solo rehabilitar movimiento si no entramos a modo edición
            if (currentEditMode == StationEditMode.None)
            {
                var movement = FindObjectOfType<RobotGame.Control.PlayerMovement>();
                if (movement != null)
                {
                    movement.Enable();
                }
            }
        }
        
        private void HandleMenuInput()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                SelectEditCurrentRobot();
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                SelectEditOtherPlatform();
            }
            else if (Input.GetKeyDown(cancelKey))
            {
                CancelMenu();
            }
        }
        
        /// <summary>
        /// Editar el robot actual (donde está el jugador, con Core).
        /// Siempre requiere snapshot.
        /// </summary>
        private void SelectEditCurrentRobot()
        {
            showingMenu = false;
            
            if (PlayerPlatform == null || PlayerPlatform.CurrentRobot == null)
            {
                Debug.LogWarning("AssemblyStation: No hay robot en tu plataforma");
                HideSelectionMenu();
                return;
            }
            
            // Verificar tier
            if (!CanModifyRobot(PlayerPlatform.CurrentRobot))
            {
                Debug.LogWarning("AssemblyStation: El robot requiere una estación de tier superior");
                HideSelectionMenu();
                return;
            }
            
            currentEditMode = StationEditMode.EditCurrentRobot;
            
            Debug.Log("AssemblyStation: Editando robot actual - Con snapshot");
            
            OnEditModeStarted?.Invoke(this, StationEditMode.EditCurrentRobot);
        }
        
        /// <summary>
        /// Editar el robot en la otra plataforma.
        /// Si está vacía, crea un cascarón nuevo.
        /// No requiere snapshot.
        /// </summary>
        private void SelectEditOtherPlatform()
        {
            showingMenu = false;
            
            // Si la otra plataforma está vacía, crear cascarón
            if (OtherPlatform.IsEmpty)
            {
                Robot newShell = CreateEmptyShell();
                if (newShell == null)
                {
                    Debug.LogWarning("AssemblyStation: No se pudo crear el cascarón");
                    HideSelectionMenu();
                    return;
                }
            }
            
            // Verificar que hay robot para editar
            if (OtherPlatform.CurrentRobot == null)
            {
                Debug.LogWarning("AssemblyStation: No hay robot en la otra plataforma");
                HideSelectionMenu();
                return;
            }
            
            currentEditMode = StationEditMode.EditOtherPlatform;
            
            Debug.Log("AssemblyStation: Editando otra plataforma - Sin snapshot");
            
            OnEditModeStarted?.Invoke(this, StationEditMode.EditOtherPlatform);
        }
        
        private void CancelMenu()
        {
            Debug.Log("AssemblyStation: Menú cancelado");
            HideSelectionMenu();
        }
        
        #endregion
        
        #region Edit Mode Control
        
        /// <summary>
        /// Sale del modo edición (llamado por ESC).
        /// </summary>
        public void ExitEditMode()
        {
            var previousMode = currentEditMode;
            currentEditMode = StationEditMode.None;
            
            // Rehabilitar movimiento
            var movement = FindObjectOfType<RobotGame.Control.PlayerMovement>();
            if (movement != null)
            {
                movement.ExitEditMode();
            }
            
            Debug.Log($"AssemblyStation: Saliendo de modo edición (era: {previousMode})");
            
            OnEditModeEnded?.Invoke(this);
        }
        
        /// <summary>
        /// Finaliza el modo de edición (llamado externamente, ej: por AssemblyTester).
        /// </summary>
        public void EndEditMode()
        {
            var previousMode = currentEditMode;
            currentEditMode = StationEditMode.None;
            
            Debug.Log($"AssemblyStation: Modo edición finalizado (era: {previousMode})");
            
            OnEditModeEnded?.Invoke(this);
        }
        
        /// <summary>
        /// Verifica si un robot puede ser modificado en esta estación (por tier).
        /// </summary>
        public bool CanModifyRobot(Robot robot)
        {
            if (robot == null) return false;
            
            // Comparar tiers (el robot debe ser igual o menor al tier de la estación)
            // Por ahora simplificado - asumiendo que los tiers son comparables numéricamente
            return robot.CurrentTier <= stationTier;
        }
        
        /// <summary>
        /// Crea un robot vacío (cascarón) en la plataforma disponible.
        /// </summary>
        public Robot CreateEmptyShell()
        {
            AssemblyPlatform targetPlatform = OtherPlatform;
            
            if (!targetPlatform.IsAvailable)
            {
                Debug.LogWarning("AssemblyStation: No hay plataforma disponible para crear cascarón");
                return null;
            }
            
            // Crear GameObject para el nuevo robot
            GameObject shellGO = new GameObject("Robot_Shell");
            Robot shell = shellGO.AddComponent<Robot>();
            shell.Initialize(null, "Cascarón Vacío", stationTier);
            
            // Colocar en la plataforma
            targetPlatform.PlaceShell(shell);
            
            Debug.Log("AssemblyStation: Cascarón vacío creado");
            return shell;
        }
        
        /// <summary>
        /// Transfiere el Core del jugador al cascarón de la otra plataforma.
        /// </summary>
        public bool TransferCoreToOtherPlatform()
        {
            var currentPlayerPlatform = PlayerPlatform;
            var otherPlatform = OtherPlatform;
            
            if (currentPlayerPlatform == null || !currentPlayerPlatform.HasPlayerRobot)
            {
                Debug.LogWarning("AssemblyStation: No hay robot del jugador para transferir Core");
                return false;
            }
            
            if (otherPlatform == null || !otherPlatform.HasShellRobot)
            {
                Debug.LogWarning("AssemblyStation: No hay cascarón en la otra plataforma");
                return false;
            }
            
            Robot currentRobot = currentPlayerPlatform.CurrentRobot;
            Robot targetShell = otherPlatform.CurrentRobot;
            
            if (currentRobot.Core == null)
            {
                Debug.LogWarning("AssemblyStation: El robot actual no tiene Core");
                return false;
            }
            
            // Extraer Core del robot actual
            RobotCore core = currentRobot.Core;
            core.Extract();
            
            // Anclar robot actual como cascarón
            currentPlayerPlatform.ConvertToShell();
            
            // Insertar Core en el nuevo robot
            core.InsertInto(targetShell);
            
            // Liberar el nuevo robot para que pueda moverse
            otherPlatform.ReleaseRobot();
            
            Debug.Log("AssemblyStation: Core transferido exitosamente");
            return true;
        }
        
        #endregion
        
        #region Debug GUI
        
        private void OnGUI()
        {
            if (!showingMenu) return;
            
            // Menú simple temporal
            float width = 320;
            float height = 160;
            float x = (Screen.width - width) / 2;
            float y = (Screen.height - height) / 2;
            
            GUI.Box(new Rect(x, y, width, height), "ESTACIÓN DE ENSAMBLAJE");
            
            // Opción 1: Robot actual (donde está el jugador)
            if (GUI.Button(new Rect(x + 20, y + 40, width - 40, 30), "[1] Editar robot actual"))
            {
                SelectEditCurrentRobot();
            }
            
            // Opción 2: Otra plataforma
            string label2 = $"[2] {GetOtherPlatformDescription()}";
            if (GUI.Button(new Rect(x + 20, y + 80, width - 40, 30), label2))
            {
                SelectEditOtherPlatform();
            }
            
            if (GUI.Button(new Rect(x + 20, y + 125, width - 40, 25), "[ESC] Cancelar"))
            {
                CancelMenu();
            }
        }
        
        private void OnDrawGizmos()
        {
            // Visualizar conexión entre plataformas
            if (platformA != null && platformB != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(platformA.transform.position, platformB.transform.position);
            }
            
            // Visualizar tier
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, new Vector3(0.5f, 0.5f, 0.5f));
        }
        
        #endregion
    }
}
