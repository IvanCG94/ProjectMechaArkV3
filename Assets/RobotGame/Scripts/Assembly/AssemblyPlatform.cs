using UnityEngine;
using RobotGame.Components;

namespace RobotGame.Assembly
{
    /// <summary>
    /// Plataforma individual dentro de una estación de ensamblaje.
    /// Detecta cuando un robot está encima y puede mantener cascarones.
    /// 
    /// SETUP REQUERIDO:
    /// - Collider marcado como Trigger para detectar robots
    /// - RobotAnchor como hijo para posicionar robots
    /// 
    /// IMPORTANTE:
    /// Las propiedades HasPlayerRobot y HasShellRobot verifican EN TIEMPO REAL
    /// si el robot tiene Core o no, para manejar correctamente las transferencias.
    /// </summary>
    public class AssemblyPlatform : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Transform donde se posiciona el robot (crear hijo vacío llamado RobotAnchor)")]
        [SerializeField] private Transform robotAnchor;
        
        [Header("Estado (Solo lectura)")]
        [SerializeField] private Robot currentRobot;
        
        // Referencia a la estación padre
        private AssemblyStation parentStation;
        
        #region Properties
        
        /// <summary>
        /// Robot actualmente en la plataforma (puede ser jugador o cascarón).
        /// </summary>
        public Robot CurrentRobot => currentRobot;
        
        /// <summary>
        /// Si la plataforma tiene el robot del jugador (con Core).
        /// Verifica EN TIEMPO REAL si el robot tiene Core insertado.
        /// </summary>
        public bool HasPlayerRobot
        {
            get
            {
                if (currentRobot == null) return false;
                return currentRobot.Core != null && currentRobot.Core.IsPlayerCore;
            }
        }
        
        /// <summary>
        /// Si la plataforma tiene un cascarón (robot sin Core).
        /// Verifica EN TIEMPO REAL si el robot NO tiene Core.
        /// </summary>
        public bool HasShellRobot
        {
            get
            {
                if (currentRobot == null) return false;
                return currentRobot.Core == null;
            }
        }
        
        /// <summary>
        /// Si la plataforma está vacía.
        /// </summary>
        public bool IsEmpty => currentRobot == null;
        
        /// <summary>
        /// Si la plataforma está disponible (vacía o tiene cascarón).
        /// </summary>
        public bool IsAvailable => IsEmpty || HasShellRobot;
        
        /// <summary>
        /// Transform donde se posiciona el robot.
        /// </summary>
        public Transform RobotAnchor => robotAnchor;
        
        /// <summary>
        /// Estación padre a la que pertenece esta plataforma.
        /// </summary>
        public AssemblyStation ParentStation => parentStation;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            // Buscar estación padre
            parentStation = GetComponentInParent<AssemblyStation>();
            
            // Auto-crear RobotAnchor si no existe
            if (robotAnchor == null)
            {
                GameObject anchorGO = new GameObject("RobotAnchor");
                robotAnchor = anchorGO.transform;
                robotAnchor.SetParent(transform);
                robotAnchor.localPosition = Vector3.zero;
                robotAnchor.localRotation = Quaternion.identity;
            }
        }
        
        private void OnTriggerEnter(Collider other)
        {
            // Intentar obtener el Robot del objeto que entró
            Robot robot = other.GetComponentInParent<Robot>();
            
            if (robot != null && currentRobot == null)
            {
                OnRobotEntered(robot);
            }
        }
        
        private void OnTriggerExit(Collider other)
        {
            Robot robot = other.GetComponentInParent<Robot>();
            
            if (robot != null && robot == currentRobot)
            {
                // Solo permitir salir si el robot tiene Core (es el jugador)
                // Los cascarones no deberían salir caminando
                if (HasPlayerRobot)
                {
                    OnRobotExited(robot);
                }
            }
        }
        
        #endregion
        
        #region Robot Detection
        
        private void OnRobotEntered(Robot robot)
        {
            currentRobot = robot;
            
            // Verificar si tiene Core (es el jugador) - usando la propiedad que verifica en tiempo real
            if (HasPlayerRobot)
            {
                // Notificar a la estación
                parentStation?.OnPlayerEnteredPlatform(this);
                
                Debug.Log($"AssemblyPlatform: Robot del jugador entró en {gameObject.name}");
            }
            else
            {
                Debug.Log($"AssemblyPlatform: Cascarón detectado en {gameObject.name}");
            }
        }
        
        private void OnRobotExited(Robot robot)
        {
            if (robot == currentRobot)
            {
                currentRobot = null;
                
                // Notificar a la estación
                parentStation?.OnPlayerExitedPlatform(this);
                
                Debug.Log($"AssemblyPlatform: Robot del jugador salió de {gameObject.name}");
            }
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Coloca un robot en esta plataforma como cascarón.
        /// </summary>
        public bool PlaceShell(Robot robot)
        {
            if (robot == null) return false;
            
            // No permitir si ya hay algo
            if (currentRobot != null)
            {
                Debug.LogWarning("AssemblyPlatform: La plataforma ya tiene un robot");
                return false;
            }
            
            currentRobot = robot;
            
            // Posicionar el robot en el anchor
            robot.transform.SetParent(robotAnchor);
            robot.transform.localPosition = Vector3.zero;
            robot.transform.localRotation = Quaternion.identity;
            
            Debug.Log($"AssemblyPlatform: Robot colocado en {gameObject.name}");
            return true;
        }
        
        /// <summary>
        /// Remueve el cascarón de la plataforma y lo retorna.
        /// </summary>
        public Robot RemoveShell()
        {
            if (!HasShellRobot || currentRobot == null)
            {
                return null;
            }
            
            Robot shell = currentRobot;
            
            // Desparentar
            shell.transform.SetParent(null);
            
            currentRobot = null;
            
            Debug.Log($"AssemblyPlatform: Cascarón removido de {gameObject.name}");
            return shell;
        }
        
        /// <summary>
        /// Convierte el robot en cascarón (parenteándolo al anchor para que no se mueva).
        /// Nota: La verificación de si es cascarón o jugador ahora es automática via Core.
        /// </summary>
        public void ConvertToShell()
        {
            if (currentRobot == null) return;
            
            // Parentear al anchor para que permanezca en la plataforma
            currentRobot.transform.SetParent(robotAnchor);
            
            Debug.Log($"AssemblyPlatform: Robot anclado como cascarón en {gameObject.name}");
        }
        
        /// <summary>
        /// Libera el robot para que pueda moverse (desparentea del anchor).
        /// Nota: La verificación de si es jugador ahora es automática via Core.
        /// </summary>
        public void ReleaseRobot()
        {
            if (currentRobot == null) return;
            
            // Desparentar para que pueda moverse
            currentRobot.transform.SetParent(null);
            
            Debug.Log($"AssemblyPlatform: Robot liberado en {gameObject.name}");
        }
        
        /// <summary>
        /// Limpia la plataforma (para casos especiales).
        /// </summary>
        public void Clear()
        {
            currentRobot = null;
        }
        
        #endregion
        
        #region Debug
        
        private void OnDrawGizmos()
        {
            // Visualizar el área de la plataforma
            Gizmos.color = IsEmpty ? Color.green : (HasPlayerRobot ? Color.blue : Color.yellow);
            
            if (robotAnchor != null)
            {
                Gizmos.DrawWireCube(robotAnchor.position, new Vector3(1f, 0.1f, 1f));
            }
            else
            {
                Gizmos.DrawWireCube(transform.position, new Vector3(1f, 0.1f, 1f));
            }
        }
        
        #endregion
    }
}
