using UnityEngine;
using RobotGame.Components;
using RobotGame.Control;
using RobotGame.AI;
using RobotGame.Enums;

namespace RobotGame.Assembly
{
    /// <summary>
    /// Estación de ensamblaje para robots de Tier 2 o superior (Mecha Fauna).
    /// 
    /// DIFERENCIAS CON AssemblyStation (Tier 1):
    /// - Una plataforma principal para el MECHA (robot a editar)
    /// - Una plataforma secundaria para el JUGADOR (habilita la edición)
    /// - El jugador NO puede estar montado en el mecha que edita
    /// - El mecha debe estar DOMESTICADO y pertenecer al jugador
    /// - El tier del mecha debe coincidir con el tier de la estación
    /// 
    /// FLUJO:
    /// 1. Jugador lleva su mecha domesticado a la plataforma principal
    /// 2. Jugador se para en la plataforma secundaria
    /// 3. Presiona P para abrir menú de edición
    /// 4. Edita el mecha (estructural/armadura)
    /// 5. Presiona ESC para salir
    /// </summary>
    public class MechaAssemblyStation : MonoBehaviour
    {
        [Header("Configuración de Tier")]
        [Tooltip("Tier de robots que esta estación puede editar")]
        [SerializeField] private int stationTier = 2;
        
        [Header("Plataformas")]
        [Tooltip("Plataforma principal donde va el MECHA")]
        [SerializeField] private AssemblyPlatform mechaPlatform;
        
        [Tooltip("Plataforma secundaria donde va el JUGADOR")]
        [SerializeField] private AssemblyPlatform playerPlatform;
        
        [Header("Controles")]
        [SerializeField] private KeyCode activateKey = KeyCode.P;
        [SerializeField] private KeyCode cancelKey = KeyCode.Escape;
        
        [Header("Estado (Solo lectura)")]
        [SerializeField] private bool isInEditMode = false;
        [SerializeField] private WildRobot currentMecha;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugUI = true;
        
        // Referencias
        private RobotCore playerCore;
        
        // Eventos
        public event System.Action<MechaAssemblyStation> OnEditModeStarted;
        public event System.Action<MechaAssemblyStation> OnEditModeEnded;
        
        #region Properties
        
        /// <summary>
        /// Tier de robots que esta estación puede editar.
        /// </summary>
        public int StationTier => stationTier;
        
        /// <summary>
        /// Si está en modo edición.
        /// </summary>
        public bool IsInEditMode => isInEditMode;
        
        /// <summary>
        /// El mecha actualmente en edición.
        /// </summary>
        public WildRobot CurrentMecha => currentMecha;
        
        /// <summary>
        /// Si el jugador está en la plataforma de jugador.
        /// </summary>
        public bool PlayerInStation => playerPlatform != null && playerPlatform.HasPlayerRobot;
        
        /// <summary>
        /// Si hay un mecha válido en la plataforma principal.
        /// </summary>
        public bool HasValidMecha
        {
            get
            {
                WildRobot mecha = GetMechaOnPlatform();
                return mecha != null && ValidateMecha(mecha) == MechaValidationResult.Valid;
            }
        }
        
        /// <summary>
        /// Plataforma principal (mecha).
        /// </summary>
        public AssemblyPlatform MechaPlatform => mechaPlatform;
        
        /// <summary>
        /// Plataforma del jugador.
        /// </summary>
        public AssemblyPlatform PlayerPlatform => playerPlatform;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Start()
        {
            // Buscar el PlayerCore
            playerCore = FindPlayerCore();
        }
        
        private void Update()
        {
            // Solo procesar input si el jugador está en su plataforma
            if (!PlayerInStation) return;
            
            if (isInEditMode)
            {
                // En modo edición, ESC para salir
                if (Input.GetKeyDown(cancelKey))
                {
                    ExitEditMode();
                }
            }
            else
            {
                // No en modo edición, P para entrar
                if (Input.GetKeyDown(activateKey))
                {
                    TryEnterEditMode();
                }
            }
        }
        
        #endregion
        
        #region Validation
        
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
        /// Obtiene el WildRobot en la plataforma principal.
        /// </summary>
        public WildRobot GetMechaOnPlatform()
        {
            if (mechaPlatform == null) return null;
            
            Robot robot = mechaPlatform.CurrentRobot;
            if (robot == null) return null;
            
            return robot.GetComponent<WildRobot>();
        }
        
        /// <summary>
        /// Valida si un mecha puede ser editado en esta estación.
        /// </summary>
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
            if (playerCore == null || !mecha.BelongsTo(playerCore))
                return MechaValidationResult.NotOwned;
            
            // Verificar que el jugador no esté montado
            if (mecha.IsBeingControlled)
                return MechaValidationResult.PlayerMounted;
            
            return MechaValidationResult.Valid;
        }
        
        /// <summary>
        /// Obtiene el tier principal del mecha.
        /// </summary>
        private int GetMechaTier(WildRobot mecha)
        {
            if (mecha.Robot == null) return 0;
            
            RobotTier tier = mecha.Robot.CurrentTier;
            // Extraer el tier principal (1, 2, 3, 4)
            return tier.GetMainTier();
        }
        
        /// <summary>
        /// Obtiene el mensaje de error para un resultado de validación.
        /// </summary>
        public string GetValidationMessage(MechaValidationResult result)
        {
            switch (result)
            {
                case MechaValidationResult.Valid:
                    return "Mecha válido";
                case MechaValidationResult.NoMecha:
                    return "No hay ningún robot en la plataforma";
                case MechaValidationResult.WrongTier:
                    return $"Esta estación es para Tier {stationTier}";
                case MechaValidationResult.NotTamed:
                    return "El robot debe estar domesticado";
                case MechaValidationResult.NotOwned:
                    return "El robot no te pertenece";
                case MechaValidationResult.PlayerMounted:
                    return "Desmonta del robot primero";
                case MechaValidationResult.MultipleMechas:
                    return "Solo puede haber un robot en la plataforma";
                default:
                    return "Error desconocido";
            }
        }
        
        private RobotCore FindPlayerCore()
        {
            var cores = FindObjectsOfType<RobotCore>();
            foreach (var core in cores)
            {
                if (core.IsPlayerCore)
                {
                    return core;
                }
            }
            return null;
        }
        
        #endregion
        
        #region Edit Mode
        
        /// <summary>
        /// Intenta entrar en modo edición.
        /// </summary>
        public void TryEnterEditMode()
        {
            // Buscar PlayerCore si no lo tenemos
            if (playerCore == null)
            {
                playerCore = FindPlayerCore();
            }
            
            // Obtener y validar el mecha
            WildRobot mecha = GetMechaOnPlatform();
            MechaValidationResult result = ValidateMecha(mecha);
            
            if (result != MechaValidationResult.Valid)
            {
                Debug.LogWarning($"MechaAssemblyStation: {GetValidationMessage(result)}");
                return;
            }
            
            // Entrar en modo edición
            EnterEditMode(mecha);
        }
        
        /// <summary>
        /// Entra en modo edición para un mecha.
        /// </summary>
        private void EnterEditMode(WildRobot mecha)
        {
            currentMecha = mecha;
            isInEditMode = true;
            
            // Desactivar movimiento del jugador
            var movement = FindObjectOfType<PlayerMovement>();
            if (movement != null)
            {
                movement.Disable();
            }
            
            // Notificar
            OnEditModeStarted?.Invoke(this);
            
            Debug.Log($"MechaAssemblyStation: Modo edición iniciado para '{mecha.WildData?.speciesName}'");
        }
        
        /// <summary>
        /// Sale del modo edición.
        /// </summary>
        public void ExitEditMode()
        {
            if (!isInEditMode) return;
            
            // Reactivar movimiento del jugador
            var movement = FindObjectOfType<PlayerMovement>();
            if (movement != null)
            {
                movement.Enable();
            }
            
            // Notificar
            OnEditModeEnded?.Invoke(this);
            
            Debug.Log($"MechaAssemblyStation: Modo edición terminado");
            
            currentMecha = null;
            isInEditMode = false;
        }
        
        #endregion
        
        #region Debug UI
        
        private void OnGUI()
        {
            if (!showDebugUI) return;
            
            GUILayout.BeginArea(new Rect(10, Screen.height - 200, 350, 190));
            GUILayout.BeginVertical("box");
            
            GUILayout.Label($"=== MECHA ASSEMBLY (Tier {stationTier}) ===");
            
            // Estado del jugador
            string playerStatus = PlayerInStation ? "EN PLATAFORMA" : "Fuera";
            GUILayout.Label($"Jugador: {playerStatus}");
            
            // Estado del mecha
            WildRobot mecha = GetMechaOnPlatform();
            if (mecha != null)
            {
                MechaValidationResult result = ValidateMecha(mecha);
                string mechaName = mecha.WildData?.speciesName ?? "Desconocido";
                int mechaTier = GetMechaTier(mecha);
                string tamedStatus = mecha.IsTamed ? "Domesticado" : "SALVAJE";
                
                GUILayout.Label($"Mecha: {mechaName} (Tier {mechaTier})");
                GUILayout.Label($"Estado: {tamedStatus}");
                
                if (result == MechaValidationResult.Valid)
                {
                    GUILayout.Label("<color=green>✓ Listo para editar</color>");
                }
                else
                {
                    GUILayout.Label($"<color=red>✗ {GetValidationMessage(result)}</color>");
                }
            }
            else
            {
                GUILayout.Label("Mecha: Ninguno");
            }
            
            GUILayout.Space(5);
            
            // Controles
            if (isInEditMode)
            {
                GUILayout.Label($"[{cancelKey}] Salir del modo edición");
            }
            else if (PlayerInStation && HasValidMecha)
            {
                GUILayout.Label($"[{activateKey}] Editar mecha");
            }
            else if (PlayerInStation)
            {
                GUILayout.Label("Coloca un mecha válido en la plataforma");
            }
            else
            {
                GUILayout.Label("Párate en la plataforma de jugador");
            }
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        
        private void OnDrawGizmos()
        {
            // Dibujar conexión entre plataformas
            if (mechaPlatform != null && playerPlatform != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(mechaPlatform.transform.position, playerPlatform.transform.position);
            }
            
            // Etiqueta de tier
            Gizmos.color = Color.white;
            Vector3 labelPos = transform.position + Vector3.up * 3f;
            
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(labelPos, $"Tier {stationTier} Assembly");
            #endif
        }
        
        #endregion
    }
}
