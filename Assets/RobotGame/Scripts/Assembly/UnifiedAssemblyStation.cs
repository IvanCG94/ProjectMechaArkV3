using UnityEngine;
using RobotGame.Components;
using RobotGame.Control;
using RobotGame.AI;
using RobotGame.Enums;
using RobotGame.Data;

namespace RobotGame.Assembly
{
    /// <summary>
    /// Tipo de estación de ensamblaje.
    /// </summary>
    public enum AssemblyStationType
    {
        Player,     // Para el jugador (Tier 1) - permite P y O
        Mecha       // Para mechas domesticados (Tier 2+) - solo P
    }
    
    /// <summary>
    /// Modo de edición actual.
    /// </summary>
    public enum AssemblyEditMode
    {
        None,
        EditOwnRobot,       // Editando robot propio (con snapshot)
        EditShell,          // Editando cascarón vacío (sin snapshot)
        EditMecha           // Editando mecha domesticado (con snapshot)
    }
    
    /// <summary>
    /// Resultado de validación de mecha.
    /// </summary>
    public enum MechaValidationResult
    {
        Valid,
        NoMecha,
        WrongTier,
        NotTamed,
        NotOwned,
        PlayerMounted,
        MultipleMechas
    }
    
    /// <summary>
    /// Estación de ensamblaje unificada.
    /// Funciona tanto para el jugador (Tier 1) como para mechas domesticados (Tier 2+).
    /// 
    /// PARA JUGADOR (StationType.Player):
    /// - [P] Editar robot propio (con snapshot, valida al salir)
    /// - [O] Editar/crear cascarón (sin snapshot, puede quedar incompleto)
    /// - [C] Extraer/insertar Core
    /// 
    /// PARA MECHA (StationType.Mecha):
    /// - [P] Editar mecha domesticado (con snapshot)
    /// - Requiere que el mecha esté domesticado y pertenezca al jugador
    /// </summary>
    public class UnifiedAssemblyStation : MonoBehaviour
    {
        #region Serialized Fields
        
        [Header("Tipo de Estación")]
        [SerializeField] private AssemblyStationType stationType = AssemblyStationType.Player;
        
        [Header("Configuración de Tier")]
        [Tooltip("Tier de la estación. Determina qué piezas pueden usarse.\n" +
                 "Solo piezas del MISMO tier principal y subtier IGUAL o INFERIOR son compatibles.\n" +
                 "Ejemplo: Estación Tier 2.2 acepta piezas 2.1 y 2.2, pero NO 2.3 ni 1.x")]
        [SerializeField] private TierInfo stationTier = TierInfo.Tier1_1;
        
        [Header("Plataformas")]
        [Tooltip("Player: plataforma del jugador. Mecha: plataforma del mecha")]
        [SerializeField] private AssemblyPlatform platformA;
        
        [Tooltip("Player: plataforma del cascarón. Mecha: plataforma del jugador")]
        [SerializeField] private AssemblyPlatform platformB;
        
        [Header("Controles")]
        [SerializeField] private KeyCode editOwnKey = KeyCode.P;
        [SerializeField] private KeyCode editShellKey = KeyCode.O;
        [SerializeField] private KeyCode coreKey = KeyCode.C;
        [SerializeField] private KeyCode cancelKey = KeyCode.Escape;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugUI = true;
        
        #endregion
        
        #region Private Fields
        
        private AssemblyEditMode currentEditMode = AssemblyEditMode.None;
        private Robot targetRobot;
        private WildRobot targetMecha;
        private bool showingMenu;
        
        // Referencias
        private PlayerController playerController;
        
        #endregion
        
        #region Events
        
        public event System.Action<UnifiedAssemblyStation, AssemblyEditMode, Robot> OnEditModeStarted;
        public event System.Action<UnifiedAssemblyStation> OnEditModeEnded;
        
        #endregion
        
        #region Properties
        
        public AssemblyStationType StationType => stationType;
        public TierInfo StationTier => stationTier;
        
        /// <summary>
        /// Tier principal de la estación (1, 2, 3, etc.)
        /// </summary>
        public int StationMainTier => stationTier.MainTier;
        
        /// <summary>
        /// Subtier de la estación (1, 2, 3, etc.)
        /// </summary>
        public int StationSubTier => stationTier.SubTier;
        
        public AssemblyEditMode CurrentEditMode => currentEditMode;
        public bool IsEditing => currentEditMode != AssemblyEditMode.None;
        public bool ShowingMenu => showingMenu;
        public Robot TargetRobot => targetRobot;
        public WildRobot TargetMecha => targetMecha;
        
        /// <summary>
        /// Si el jugador está en posición válida para editar.
        /// </summary>
        public bool PlayerInStation
        {
            get
            {
                if (stationType == AssemblyStationType.Player)
                    return platformA != null && platformA.HasPlayerRobot;
                else
                    return platformB != null && platformB.HasPlayerRobot;
            }
        }
        
        /// <summary>
        /// Plataforma donde está el jugador.
        /// </summary>
        public AssemblyPlatform PlayerPlatform
        {
            get
            {
                if (stationType == AssemblyStationType.Player)
                    return platformA;
                else
                    return platformB;
            }
        }
        
        /// <summary>
        /// Plataforma del objetivo (cascarón o mecha).
        /// </summary>
        public AssemblyPlatform TargetPlatform
        {
            get
            {
                if (stationType == AssemblyStationType.Player)
                    return platformB;
                else
                    return platformA;
            }
        }
        
        /// <summary>
        /// Obtiene el PlayerCore actual (búsqueda dinámica).
        /// </summary>
        public RobotCore PlayerCore
        {
            get
            {
                var cores = FindObjectsOfType<RobotCore>();
                foreach (var core in cores)
                {
                    if (core.IsPlayerCore)
                        return core;
                }
                return null;
            }
        }
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            AutoDetectPlatforms();
        }
        
        private void Start()
        {
            playerController = FindObjectOfType<PlayerController>();
        }
        
        private void Update()
        {
            // Si estamos editando, SIEMPRE procesar inputs (aunque el Core esté fuera)
            if (IsEditing)
            {
                // En modo edición, ESC para salir
                if (Input.GetKeyDown(cancelKey))
                {
                    RequestExitEditMode();
                }
                
                // C para extraer/insertar Core (solo en Player station, solo en modo edición)
                if (stationType == AssemblyStationType.Player && Input.GetKeyDown(coreKey))
                {
                    HandleCoreAction();
                }
                
                return; // No procesar más mientras editamos
            }
            
            // Fuera de modo edición, requerir que el jugador esté en la estación
            if (!PlayerInStation) return;
            
            if (showingMenu)
            {
                HandleMenuInput();
            }
            else
            {
                // No estamos editando - solo P para entrar al menú
                if (Input.GetKeyDown(editOwnKey))
                {
                    if (stationType == AssemblyStationType.Player)
                        ShowSelectionMenu();
                    else
                        TryEnterMechaEditMode();
                }
                
                // C NO funciona fuera del modo edición
            }
        }
        
        private void OnGUI()
        {
            if (!showDebugUI) return;
            DrawDebugUI();
        }
        
        #endregion
        
        #region Setup
        
        private void AutoDetectPlatforms()
        {
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
                    Debug.LogWarning("UnifiedAssemblyStation: Solo se encontró una plataforma");
                }
            }
        }
        
        #endregion
        
        #region Menu (Player Station)
        
        private void ShowSelectionMenu()
        {
            showingMenu = true;
            
            // Deshabilitar movimiento
            if (playerController != null)
            {
                playerController.Disable();
            }
            
            Debug.Log("=== MENÚ DE ENSAMBLAJE ===");
            Debug.Log("[P] Editar tu robot (con snapshot)");
            Debug.Log("[O] Editar/crear cascarón (sin snapshot)");
            Debug.Log("[ESC] Cancelar");
        }
        
        private void HideSelectionMenu()
        {
            showingMenu = false;
            
            if (currentEditMode == AssemblyEditMode.None && playerController != null)
            {
                playerController.Enable();
            }
        }
        
        private void HandleMenuInput()
        {
            if (Input.GetKeyDown(editOwnKey))
            {
                showingMenu = false;
                TryEnterOwnRobotEditMode();
            }
            else if (Input.GetKeyDown(editShellKey))
            {
                showingMenu = false;
                TryEnterShellEditMode();
            }
            else if (Input.GetKeyDown(cancelKey))
            {
                HideSelectionMenu();
            }
        }
        
        #endregion
        
        #region Player Station - Edit Modes
        
        private void TryEnterOwnRobotEditMode()
        {
            Robot playerRobot = PlayerPlatform?.CurrentRobot;
            
            if (playerRobot == null)
            {
                Debug.LogWarning("UnifiedAssemblyStation: No hay robot del jugador en la plataforma");
                HideSelectionMenu();
                return;
            }
            
            // Verificar tier
            if (playerRobot.CurrentTier > stationTier)
            {
                Debug.LogWarning($"UnifiedAssemblyStation: Robot requiere estación de tier superior");
                HideSelectionMenu();
                return;
            }
            
            targetRobot = playerRobot;
            targetMecha = null;
            currentEditMode = AssemblyEditMode.EditOwnRobot;
            
            // Deshabilitar movimiento
            if (playerController != null)
            {
                playerController.EnterEditModeState();
            }
            
            Debug.Log($"UnifiedAssemblyStation: Editando robot propio '{targetRobot.name}' (CON snapshot)");
            OnEditModeStarted?.Invoke(this, currentEditMode, targetRobot);
        }
        
        private void TryEnterShellEditMode()
        {
            // Si la otra plataforma está vacía, crear cascarón
            if (TargetPlatform.IsEmpty || TargetPlatform.CurrentRobot == null)
            {
                Robot newShell = CreateEmptyShell();
                if (newShell == null)
                {
                    Debug.LogWarning("UnifiedAssemblyStation: No se pudo crear el cascarón");
                    HideSelectionMenu();
                    return;
                }
            }
            
            Robot shellRobot = TargetPlatform.CurrentRobot;
            
            if (shellRobot == null)
            {
                Debug.LogWarning("UnifiedAssemblyStation: No hay cascarón en la otra plataforma");
                HideSelectionMenu();
                return;
            }
            
            // Verificar que no tenga Core (es un cascarón)
            if (shellRobot.Core != null)
            {
                Debug.LogWarning("UnifiedAssemblyStation: El robot tiene Core, no es un cascarón");
                HideSelectionMenu();
                return;
            }
            
            targetRobot = shellRobot;
            targetMecha = null;
            currentEditMode = AssemblyEditMode.EditShell;
            
            // Deshabilitar movimiento
            if (playerController != null)
            {
                playerController.EnterEditModeState();
            }
            
            Debug.Log($"UnifiedAssemblyStation: Editando cascarón '{targetRobot.name}' (SIN snapshot)");
            OnEditModeStarted?.Invoke(this, currentEditMode, targetRobot);
        }
        
        /// <summary>
        /// Crea un robot vacío (cascarón) en la plataforma disponible.
        /// </summary>
        public Robot CreateEmptyShell()
        {
            if (TargetPlatform == null || !TargetPlatform.IsAvailable)
            {
                Debug.LogWarning("UnifiedAssemblyStation: No hay plataforma disponible");
                return null;
            }
            
            // Crear GameObject para el nuevo robot
            GameObject shellGO = new GameObject("Robot_Shell");
            shellGO.transform.position = TargetPlatform.transform.position;
            
            Robot shell = shellGO.AddComponent<Robot>();
            shell.Initialize(null, "Cascarón Vacío", stationTier);
            
            // Colocar en la plataforma
            TargetPlatform.PlaceShell(shell);
            
            Debug.Log("UnifiedAssemblyStation: Cascarón vacío creado");
            return shell;
        }
        
        #endregion
        
        #region Mecha Station - Edit Mode
        
        private void TryEnterMechaEditMode()
        {
            WildRobot mecha = GetMechaOnPlatform();
            MechaValidationResult result = ValidateMecha(mecha);
            
            if (result != MechaValidationResult.Valid)
            {
                Debug.LogWarning($"UnifiedAssemblyStation: {GetValidationMessage(result)}");
                return;
            }
            
            targetMecha = mecha;
            targetRobot = mecha.Robot;
            currentEditMode = AssemblyEditMode.EditMecha;
            
            // Deshabilitar movimiento
            if (playerController != null)
            {
                playerController.EnterEditModeState();
            }
            
            Debug.Log($"UnifiedAssemblyStation: Editando mecha '{mecha.WildData?.speciesName}' (CON snapshot)");
            OnEditModeStarted?.Invoke(this, currentEditMode, targetRobot);
        }
        
        private WildRobot GetMechaOnPlatform()
        {
            if (platformA == null) return null;
            
            Robot robot = platformA.CurrentRobot;
            if (robot == null) return null;
            
            return robot.GetComponent<WildRobot>();
        }
        
        public MechaValidationResult ValidateMecha(WildRobot mecha)
        {
            if (mecha == null)
                return MechaValidationResult.NoMecha;
            
            if (mecha.Robot == null || GetMechaTier(mecha) != StationMainTier)
                return MechaValidationResult.WrongTier;
            
            if (!mecha.IsTamed)
                return MechaValidationResult.NotTamed;
            
            if (PlayerCore == null || !mecha.BelongsTo(PlayerCore))
                return MechaValidationResult.NotOwned;
            
            if (mecha.IsBeingControlled)
                return MechaValidationResult.PlayerMounted;
            
            return MechaValidationResult.Valid;
        }
        
        private int GetMechaTier(WildRobot mecha)
        {
            if (mecha.Robot == null) return 0;
            return mecha.Robot.CurrentTier.MainTier;
        }
        
        private string GetValidationMessage(MechaValidationResult result)
        {
            switch (result)
            {
                case MechaValidationResult.NoMecha:
                    return "No hay mecha en la plataforma";
                case MechaValidationResult.WrongTier:
                    return $"El mecha debe ser Tier {StationMainTier}";
                case MechaValidationResult.NotTamed:
                    return "El mecha no está domesticado";
                case MechaValidationResult.NotOwned:
                    return "El mecha no te pertenece";
                case MechaValidationResult.PlayerMounted:
                    return "Desmonta del mecha primero";
                default:
                    return "Error desconocido";
            }
        }
        
        #endregion
        
        #region Core Management
        
        private void HandleCoreAction()
        {
            RobotCore core = PlayerCore;
            
            if (core == null)
            {
                Debug.LogWarning("UnifiedAssemblyStation: No hay PlayerCore");
                return;
            }
            
            Debug.Log($"UnifiedAssemblyStation: HandleCoreAction - Core en robot: {core.CurrentRobot != null}");
            
            // Si el Core está en un robot, extraerlo
            if (core.CurrentRobot != null)
            {
                Robot robotWithCore = core.CurrentRobot;
                
                // Extraer Core
                Robot extracted = core.Extract();
                if (extracted != null)
                {
                    Debug.Log($"UnifiedAssemblyStation: Core extraído de '{extracted.name}'");
                    
                    // Forzar re-detección de plataformas
                    platformA?.ForceRedetect();
                    platformB?.ForceRedetect();
                }
                else
                {
                    Debug.LogWarning("UnifiedAssemblyStation: No se pudo extraer el Core");
                }
            }
            else
            {
                // Core está libre, intentar insertar
                // Buscar el robot objetivo actual (el que estamos editando)
                Robot robotToInsert = targetRobot;
                
                // Si no hay targetRobot, buscar en las plataformas
                if (robotToInsert == null || robotToInsert.Core != null)
                {
                    // Buscar un robot sin Core
                    Robot platformARobot = platformA?.CurrentRobot;
                    Robot platformBRobot = platformB?.CurrentRobot;
                    
                    if (platformARobot != null && platformARobot.Core == null)
                    {
                        robotToInsert = platformARobot;
                    }
                    else if (platformBRobot != null && platformBRobot.Core == null)
                    {
                        robotToInsert = platformBRobot;
                    }
                }
                
                if (robotToInsert != null && robotToInsert.Core == null)
                {
                    Debug.Log($"UnifiedAssemblyStation: Intentando insertar Core en '{robotToInsert.name}'");
                    
                    if (core.InsertInto(robotToInsert))
                    {
                        Debug.Log($"UnifiedAssemblyStation: Core insertado exitosamente en '{robotToInsert.name}'");
                        
                        // Forzar re-detección
                        platformA?.ForceRedetect();
                        platformB?.ForceRedetect();
                    }
                    else
                    {
                        Debug.LogWarning("UnifiedAssemblyStation: InsertInto retornó false");
                    }
                }
                else
                {
                    Debug.LogWarning($"UnifiedAssemblyStation: No hay robot disponible para insertar. targetRobot={targetRobot?.name}, Core={targetRobot?.Core}");
                }
            }
        }
        
        #endregion
        
        #region Exit Edit Mode
        
        /// <summary>
        /// Solicita salir del modo edición.
        /// El RobotAssemblyController validará antes de permitir la salida.
        /// </summary>
        public void RequestExitEditMode()
        {
            if (!IsEditing) return;
            
            Debug.Log($"UnifiedAssemblyStation: Solicitando salir del modo {currentEditMode}");
            
            // El evento notifica al controller para que valide
            OnEditModeEnded?.Invoke(this);
        }
        
        /// <summary>
        /// Completa la salida del modo edición (llamado por el controller después de validar).
        /// </summary>
        public void CompleteExitEditMode()
        {
            Debug.Log($"UnifiedAssemblyStation: Saliendo del modo {currentEditMode}");
            
            currentEditMode = AssemblyEditMode.None;
            targetRobot = null;
            targetMecha = null;
            
            // Rehabilitar movimiento
            if (playerController != null)
            {
                playerController.ExitEditModeState();
            }
        }
        
        /// <summary>
        /// Fuerza la salida sin validación (para casos especiales).
        /// </summary>
        public void ForceExitEditMode()
        {
            currentEditMode = AssemblyEditMode.None;
            targetRobot = null;
            targetMecha = null;
            
            if (playerController != null)
            {
                playerController.ExitEditModeState();
            }
        }
        
        #endregion
        
        #region Debug UI
        
        private void DrawDebugUI()
        {
            string stationTypeStr = stationType == AssemblyStationType.Player 
                ? $"JUGADOR (Tier {stationTier})" 
                : $"MECHA (Tier {stationTier})";
            
            GUILayout.BeginArea(new Rect(10, 450, 350, 250));
            GUILayout.BeginVertical("box");
            
            GUILayout.Label($"=== ASSEMBLY STATION ({stationTypeStr}) ===");
            GUILayout.Label($"Jugador en estación: {(PlayerInStation ? "SÍ" : "NO")}");
            
            if (stationType == AssemblyStationType.Player)
            {
                Robot playerRobot = PlayerPlatform?.CurrentRobot;
                Robot shellRobot = TargetPlatform?.CurrentRobot;
                
                GUILayout.Label($"Tu robot: {(playerRobot != null ? playerRobot.name : "Ninguno")}");
                GUILayout.Label($"Cascarón: {(shellRobot != null ? shellRobot.name : "Ninguno")}");
                
                RobotCore core = PlayerCore;
                if (core != null)
                {
                    string coreStatus = core.CurrentRobot != null 
                        ? $"En {core.CurrentRobot.name}" 
                        : "Libre";
                    GUILayout.Label($"Core: {coreStatus}");
                }
            }
            else
            {
                WildRobot mecha = GetMechaOnPlatform();
                MechaValidationResult validation = ValidateMecha(mecha);
                
                GUILayout.Label($"Mecha: {(mecha != null ? mecha.WildData?.speciesName : "Ninguno")}");
                GUILayout.Label($"Estado: {(validation == MechaValidationResult.Valid ? "✓ Listo" : GetValidationMessage(validation))}");
            }
            
            GUILayout.Label($"Modo: {currentEditMode}");
            
            if (PlayerInStation && !IsEditing && !showingMenu)
            {
                if (stationType == AssemblyStationType.Player)
                {
                    GUILayout.Label("[P] Menú de edición");
                }
                else
                {
                    GUILayout.Label("[P] Editar mecha");
                }
            }
            
            if (showingMenu)
            {
                GUILayout.Label("--- MENÚ ---");
                GUILayout.Label("[P] Editar tu robot");
                GUILayout.Label("[O] Editar/crear cascarón");
                GUILayout.Label("[ESC] Cancelar");
            }
            
            if (IsEditing)
            {
                GUILayout.Label("[ESC] Salir del modo edición");
                if (stationType == AssemblyStationType.Player)
                {
                    GUILayout.Label("[C] Extraer/Insertar Core");
                }
            }
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        
        #endregion
    }
}
