using UnityEngine;
using RobotGame.Components;
using RobotGame.Control;
using RobotGame.AI;
using RobotGame.Enums;

namespace RobotGame.Assembly
{
    /// <summary>
    /// Tipo de estación de ensamblaje.
    /// </summary>
    public enum AssemblyStationType
    {
        /// <summary>
        /// Estación para el jugador (Tier 1).
        /// - Dos plataformas: una para el jugador, otra para cascarones
        /// - Permite editar robot propio (P) o crear cascarones (O)
        /// </summary>
        Player,
        
        /// <summary>
        /// Estación para mechas domesticados (Tier 2+).
        /// - Dos plataformas: una para el mecha, otra para el jugador
        /// - Solo permite editar el mecha (P)
        /// - Requiere que el mecha esté domesticado y pertenezca al jugador
        /// </summary>
        Mecha
    }
    
    /// <summary>
    /// Modo de edición actual.
    /// </summary>
    public enum AssemblyEditMode
    {
        None,
        EditOwnRobot,       // Editando robot propio (con snapshot)
        EditShell,          // Editando cascarón vacío (sin snapshot)
        EditMecha           // Editando mecha domesticado
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
    /// CONFIGURACIÓN:
    /// - StationType.Player: Para robots del jugador, permite editar propio o crear cascarones
    /// - StationType.Mecha: Para mechas domesticados, solo edición
    /// 
    /// SETUP:
    /// 1. Crear GameObject con este componente
    /// 2. Crear dos plataformas hijas con AssemblyPlatform
    /// 3. Asignar platformA (jugador/mecha) y platformB (cascarón/jugador)
    /// 4. Agregar RobotAssemblyController para la lógica de edición
    /// </summary>
    public class UnifiedAssemblyStation : MonoBehaviour
    {
        #region Serialized Fields
        
        [Header("Tipo de Estación")]
        [SerializeField] private AssemblyStationType stationType = AssemblyStationType.Player;
        
        [Header("Configuración de Tier")]
        [Tooltip("Para Player: tier del jugador. Para Mecha: tier de mechas que acepta")]
        [SerializeField] private int stationTier = 1;
        
        [Header("Plataformas")]
        [Tooltip("Player: plataforma del jugador. Mecha: plataforma del mecha")]
        [SerializeField] private AssemblyPlatform platformA;
        
        [Tooltip("Player: plataforma del cascarón. Mecha: plataforma del jugador")]
        [SerializeField] private AssemblyPlatform platformB;
        
        [Header("Controles")]
        [SerializeField] private KeyCode editOwnKey = KeyCode.P;
        [SerializeField] private KeyCode editOtherKey = KeyCode.O;
        [SerializeField] private KeyCode cancelKey = KeyCode.Escape;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugUI = true;
        
        #endregion
        
        #region Private Fields
        
        private AssemblyEditMode currentEditMode = AssemblyEditMode.None;
        private Robot targetRobot;
        private WildRobot targetMecha;
        private bool showingMenu;
        
        #endregion
        
        #region Events
        
        public event System.Action<UnifiedAssemblyStation, AssemblyEditMode, Robot> OnEditModeStarted;
        public event System.Action<UnifiedAssemblyStation> OnEditModeEnded;
        
        #endregion
        
        #region Properties
        
        public AssemblyStationType StationType => stationType;
        public int StationTier => stationTier;
        public AssemblyEditMode CurrentEditMode => currentEditMode;
        public bool IsEditing => currentEditMode != AssemblyEditMode.None;
        public bool ShowingMenu => showingMenu;
        public Robot TargetRobot => targetRobot;
        public WildRobot TargetMecha => targetMecha;
        
        /// <summary>
        /// Si el jugador está en posición válida para editar.
        /// Player: en platformA. Mecha: en platformB.
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
        /// Plataforma del objetivo a editar.
        /// Player: platformB (cascarón). Mecha: platformA (mecha).
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
        private RobotCore PlayerCore
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
        
        private void Update()
        {
            if (!PlayerInStation) return;
            
            if (showingMenu)
            {
                HandleMenuInput();
            }
            else if (IsEditing)
            {
                if (Input.GetKeyDown(cancelKey))
                {
                    ExitEditMode();
                }
            }
            else
            {
                if (Input.GetKeyDown(editOwnKey))
                {
                    if (stationType == AssemblyStationType.Player)
                        ShowSelectionMenu();
                    else
                        TryEnterMechaEditMode();
                }
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
                else
                {
                    Debug.LogError("UnifiedAssemblyStation: No se encontraron plataformas");
                }
            }
        }
        
        #endregion
        
        #region Player Station Logic (Tier 1)
        
        private void ShowSelectionMenu()
        {
            showingMenu = true;
            Debug.Log("UnifiedAssemblyStation: Menú de selección abierto");
        }
        
        private void HandleMenuInput()
        {
            // P - Editar robot propio
            if (Input.GetKeyDown(editOwnKey))
            {
                showingMenu = false;
                TryEnterOwnRobotEditMode();
            }
            // O - Editar cascarón (solo para Player)
            else if (Input.GetKeyDown(editOtherKey) && stationType == AssemblyStationType.Player)
            {
                showingMenu = false;
                TryEnterShellEditMode();
            }
            // ESC - Cerrar menú
            else if (Input.GetKeyDown(cancelKey))
            {
                showingMenu = false;
                Debug.Log("UnifiedAssemblyStation: Menú cerrado");
            }
        }
        
        private void TryEnterOwnRobotEditMode()
        {
            Robot playerRobot = PlayerPlatform?.CurrentRobot;
            
            if (playerRobot == null)
            {
                Debug.LogWarning("UnifiedAssemblyStation: No hay robot del jugador en la plataforma");
                return;
            }
            
            targetRobot = playerRobot;
            targetMecha = null;
            currentEditMode = AssemblyEditMode.EditOwnRobot;
            
            Debug.Log($"UnifiedAssemblyStation: Editando robot propio '{targetRobot.name}'");
            OnEditModeStarted?.Invoke(this, currentEditMode, targetRobot);
        }
        
        private void TryEnterShellEditMode()
        {
            Robot shellRobot = TargetPlatform?.CurrentRobot;
            
            if (shellRobot == null)
            {
                Debug.LogWarning("UnifiedAssemblyStation: No hay cascarón en la otra plataforma");
                return;
            }
            
            // Verificar que no tenga Core (es un cascarón)
            if (shellRobot.Core != null)
            {
                Debug.LogWarning("UnifiedAssemblyStation: El robot tiene Core, no es un cascarón");
                return;
            }
            
            targetRobot = shellRobot;
            targetMecha = null;
            currentEditMode = AssemblyEditMode.EditShell;
            
            Debug.Log($"UnifiedAssemblyStation: Editando cascarón '{targetRobot.name}'");
            OnEditModeStarted?.Invoke(this, currentEditMode, targetRobot);
        }
        
        #endregion
        
        #region Mecha Station Logic (Tier 2+)
        
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
            
            Debug.Log($"UnifiedAssemblyStation: Editando mecha '{mecha.WildData?.speciesName}'");
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
            
            // Verificar tier
            if (mecha.Robot == null || GetMechaTier(mecha) != stationTier)
                return MechaValidationResult.WrongTier;
            
            // Verificar domesticación
            if (!mecha.IsTamed)
                return MechaValidationResult.NotTamed;
            
            // Verificar propiedad
            if (PlayerCore == null || !mecha.BelongsTo(PlayerCore))
                return MechaValidationResult.NotOwned;
            
            // Verificar que no esté montado
            if (mecha.IsBeingControlled)
                return MechaValidationResult.PlayerMounted;
            
            return MechaValidationResult.Valid;
        }
        
        private int GetMechaTier(WildRobot mecha)
        {
            if (mecha.Robot == null) return 0;
            return mecha.Robot.CurrentTier.GetMainTier();
        }
        
        private string GetValidationMessage(MechaValidationResult result)
        {
            switch (result)
            {
                case MechaValidationResult.NoMecha:
                    return "No hay mecha en la plataforma";
                case MechaValidationResult.WrongTier:
                    return $"El mecha debe ser Tier {stationTier}";
                case MechaValidationResult.NotTamed:
                    return "El mecha no está domesticado";
                case MechaValidationResult.NotOwned:
                    return "El mecha no te pertenece";
                case MechaValidationResult.PlayerMounted:
                    return "Desmonta del mecha primero";
                case MechaValidationResult.MultipleMechas:
                    return "Hay más de un mecha en la plataforma";
                default:
                    return "Error desconocido";
            }
        }
        
        #endregion
        
        #region Exit Edit Mode
        
        public void ExitEditMode()
        {
            if (!IsEditing) return;
            
            Debug.Log($"UnifiedAssemblyStation: Saliendo del modo {currentEditMode}");
            
            currentEditMode = AssemblyEditMode.None;
            targetRobot = null;
            targetMecha = null;
            
            OnEditModeEnded?.Invoke(this);
        }
        
        #endregion
        
        #region Debug UI
        
        private void DrawDebugUI()
        {
            string stationTypeStr = stationType == AssemblyStationType.Player ? "JUGADOR" : $"MECHA (Tier {stationTier})";
            
            GUILayout.BeginArea(new Rect(10, 450, 300, 200));
            GUILayout.BeginVertical("box");
            
            GUILayout.Label($"=== ASSEMBLY STATION ({stationTypeStr}) ===");
            GUILayout.Label($"Jugador en estación: {(PlayerInStation ? "SÍ" : "NO")}");
            
            if (stationType == AssemblyStationType.Player)
            {
                Robot playerRobot = PlayerPlatform?.CurrentRobot;
                Robot shellRobot = TargetPlatform?.CurrentRobot;
                
                GUILayout.Label($"Robot jugador: {(playerRobot != null ? playerRobot.name : "Ninguno")}");
                GUILayout.Label($"Cascarón: {(shellRobot != null ? shellRobot.name : "Ninguno")}");
            }
            else
            {
                WildRobot mecha = GetMechaOnPlatform();
                MechaValidationResult validation = ValidateMecha(mecha);
                
                GUILayout.Label($"Mecha: {(mecha != null ? mecha.WildData?.speciesName : "Ninguno")}");
                GUILayout.Label($"Estado: {(validation == MechaValidationResult.Valid ? "✓ Listo" : GetValidationMessage(validation))}");
            }
            
            GUILayout.Label($"Modo: {currentEditMode}");
            
            if (PlayerInStation && !IsEditing)
            {
                if (stationType == AssemblyStationType.Player)
                    GUILayout.Label("[P] Menú de edición");
                else
                    GUILayout.Label("[P] Editar mecha");
            }
            
            if (showingMenu)
            {
                GUILayout.Label("[P] Editar robot propio");
                if (stationType == AssemblyStationType.Player)
                    GUILayout.Label("[O] Editar cascarón");
                GUILayout.Label("[Escape] Cerrar menú");
            }
            
            if (IsEditing)
            {
                GUILayout.Label("[Escape] Salir del modo edición");
            }
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        
        #endregion
    }
}
