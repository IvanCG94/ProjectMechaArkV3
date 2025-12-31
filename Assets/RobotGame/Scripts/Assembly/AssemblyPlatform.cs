using UnityEngine;
using RobotGame.Components;

namespace RobotGame.Assembly
{
    /// <summary>
    /// Plataforma individual dentro de una estación de ensamblaje.
    /// Detecta EN TIEMPO REAL qué robot está encima.
    /// 
    /// SETUP REQUERIDO:
    /// - Collider (Box o cualquier otro) para definir el área de detección
    /// - RobotAnchor como hijo para posicionar robots
    /// 
    /// SIMPLIFICADO:
    /// - No usa triggers ni callbacks
    /// - Detecta robots en cada consulta via Physics.OverlapBox
    /// - Más robusto ante activaciones/desactivaciones de robots
    /// </summary>
    public class AssemblyPlatform : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Transform donde se posiciona el robot (crear hijo vacío llamado RobotAnchor)")]
        [SerializeField] private Transform robotAnchor;
        
        [Header("Detección")]
        [Tooltip("Tamaño del área de detección")]
        [SerializeField] private Vector3 detectionSize = new Vector3(2f, 2f, 2f);
        
        [Tooltip("Offset del área de detección desde el centro")]
        [SerializeField] private Vector3 detectionOffset = new Vector3(0f, 1f, 0f);
        
        [Header("Debug")]
        [SerializeField] private bool showDetectionArea = true;
        
        // Referencia a la estación padre
        private AssemblyStation parentStation;
        
        // Cache del robot detectado (para cascarones que no se mueven)
        private Robot cachedShellRobot;
        
        #region Properties
        
        /// <summary>
        /// Robot actualmente en la plataforma (detectado en tiempo real o cascarón cacheado).
        /// </summary>
        public Robot CurrentRobot
        {
            get
            {
                // Si hay un cascarón cacheado, verificar que siga ahí
                if (cachedShellRobot != null)
                {
                    if (cachedShellRobot.gameObject.activeInHierarchy && cachedShellRobot.Core == null)
                    {
                        return cachedShellRobot;
                    }
                    else
                    {
                        // El cascarón ya no es válido
                        cachedShellRobot = null;
                    }
                }
                
                // Detectar robot en tiempo real
                return DetectRobotInArea();
            }
        }
        
        /// <summary>
        /// Si la plataforma tiene el robot del jugador (con Core del jugador).
        /// </summary>
        public bool HasPlayerRobot
        {
            get
            {
                Robot robot = CurrentRobot;
                if (robot == null) return false;
                return robot.Core != null && robot.Core.IsPlayerCore;
            }
        }
        
        /// <summary>
        /// Si la plataforma tiene un cascarón (robot sin Core).
        /// </summary>
        public bool HasShellRobot
        {
            get
            {
                Robot robot = CurrentRobot;
                if (robot == null) return false;
                return robot.Core == null;
            }
        }
        
        /// <summary>
        /// Si la plataforma está vacía.
        /// </summary>
        public bool IsEmpty => CurrentRobot == null;
        
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
        
        #endregion
        
        #region Detection
        
        /// <summary>
        /// Detecta qué robot está dentro del área de la plataforma.
        /// </summary>
        private Robot DetectRobotInArea()
        {
            Vector3 center = transform.position + transform.TransformDirection(detectionOffset);
            Vector3 halfExtents = detectionSize * 0.5f;
            
            Collider[] colliders = Physics.OverlapBox(center, halfExtents, transform.rotation);
            
            foreach (var col in colliders)
            {
                Robot robot = col.GetComponentInParent<Robot>();
                if (robot != null && robot.gameObject.activeInHierarchy)
                {
                    return robot;
                }
            }
            
            return null;
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Coloca un robot en esta plataforma como cascarón.
        /// </summary>
        public bool PlaceShell(Robot robot)
        {
            if (robot == null) return false;
            
            // No permitir si ya hay un robot del jugador
            if (HasPlayerRobot)
            {
                Debug.LogWarning("AssemblyPlatform: La plataforma tiene al jugador");
                return false;
            }
            
            // Cachear el cascarón
            cachedShellRobot = robot;
            
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
            if (!HasShellRobot)
            {
                return null;
            }
            
            Robot shell = CurrentRobot;
            
            // Desparentar
            if (shell != null)
            {
                shell.transform.SetParent(null);
            }
            
            // Limpiar cache
            cachedShellRobot = null;
            
            Debug.Log($"AssemblyPlatform: Cascarón removido de {gameObject.name}");
            return shell;
        }
        
        /// <summary>
        /// Convierte el robot actual en cascarón (parenteándolo al anchor).
        /// </summary>
        public void ConvertToShell()
        {
            Robot robot = CurrentRobot;
            if (robot == null) return;
            
            // Cachear como cascarón
            cachedShellRobot = robot;
            
            // Parentear al anchor para que permanezca en la plataforma
            robot.transform.SetParent(robotAnchor);
            
            Debug.Log($"AssemblyPlatform: Robot anclado como cascarón en {gameObject.name}");
        }
        
        /// <summary>
        /// Libera el robot para que pueda moverse (desparentea del anchor).
        /// </summary>
        public void ReleaseRobot()
        {
            Robot robot = CurrentRobot;
            if (robot == null) return;
            
            // Desparentar para que pueda moverse
            robot.transform.SetParent(null);
            
            // Limpiar cache si era cascarón
            if (cachedShellRobot == robot)
            {
                cachedShellRobot = null;
            }
            
            Debug.Log($"AssemblyPlatform: Robot liberado en {gameObject.name}");
        }
        
        /// <summary>
        /// Limpia la plataforma (para casos especiales).
        /// </summary>
        public void Clear()
        {
            cachedShellRobot = null;
        }
        
        /// <summary>
        /// Fuerza re-detección (para compatibilidad, ahora no hace nada especial).
        /// </summary>
        public void ForceRedetect()
        {
            // La detección ya es en tiempo real, solo limpiamos el cache
            cachedShellRobot = null;
        }
        
        #endregion
        
        #region Debug
        
        private void OnDrawGizmos()
        {
            if (!showDetectionArea) return;
            
            Vector3 center = transform.position + transform.TransformDirection(detectionOffset);
            
            // Color según estado
            if (Application.isPlaying)
            {
                Gizmos.color = IsEmpty ? Color.green : (HasPlayerRobot ? Color.blue : Color.yellow);
            }
            else
            {
                Gizmos.color = Color.green;
            }
            
            // Dibujar área de detección
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, detectionSize);
            Gizmos.matrix = oldMatrix;
            
            // Dibujar anchor
            if (robotAnchor != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(robotAnchor.position, 0.2f);
            }
        }
        
        #endregion
    }
}
