using UnityEngine;
using RobotGame.Components;
using RobotGame.Control;
using RobotGame.AI;

namespace RobotGame.Systems
{
    /// <summary>
    /// Sistema de montura que permite al jugador montar robots salvajes.
    /// 
    /// Funcionalidad:
    /// - Detecta robots salvajes cercanos
    /// - Tecla F (configurable) para montar/desmontar
    /// - Al montar: robot del jugador se desactiva, jugador controla el mecha
    /// - Al desmontar: robot del jugador reaparece debajo del mecha
    /// 
    /// SETUP:
    /// - Añadir a un GameObject en la escena (puede ser el mismo que tiene PlayerMovement)
    /// - Asignar referencias en el inspector o dejar que se auto-detecten
    /// </summary>
    public class MountSystem : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("El Core del jugador (se auto-detecta si no se asigna)")]
        [SerializeField] private RobotCore playerCore;
        
        [Tooltip("El componente de movimiento del jugador")]
        [SerializeField] private PlayerMovement playerMovement;
        
        [Tooltip("La cámara del jugador")]
        [SerializeField] private PlayerCamera playerCamera;
        
        [Header("Configuración")]
        [Tooltip("Tecla para montar/desmontar")]
        [SerializeField] private KeyCode mountKey = KeyCode.F;
        
        [Tooltip("Distancia máxima para montar un mecha")]
        [SerializeField] private float mountRange = 3f;
        
        [Tooltip("Layers que contienen robots montables")]
        [SerializeField] private LayerMask mountableLayers = ~0;
        
        [Header("Estado (Solo lectura)")]
        [SerializeField] private bool isMounted = false;
        [SerializeField] private WildRobot currentMount;
        [SerializeField] private WildRobot nearbyMount;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private bool showGizmos = true;
        
        // Estado interno
        private Robot playerRobot;
        private Vector3 dismountOffset = new Vector3(0f, 0f, -2f); // Ya no se usa, pero lo dejamos por si acaso
        
        #region Properties
        
        /// <summary>
        /// Si el jugador está montado en un mecha.
        /// </summary>
        public bool IsMounted => isMounted;
        
        /// <summary>
        /// El mecha actualmente montado (null si no está montado).
        /// </summary>
        public WildRobot CurrentMount => currentMount;
        
        /// <summary>
        /// El mecha cercano que se puede montar (null si no hay ninguno).
        /// </summary>
        public WildRobot NearbyMount => nearbyMount;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Start()
        {
            FindReferences();
        }
        
        private void Update()
        {
            // Si no tenemos referencias, intentar buscarlas
            if (playerCore == null || playerRobot == null)
            {
                FindReferences();
                if (playerCore == null) return; // Sin jugador, no hacer nada
            }
            
            // Actualizar referencia al robot del jugador si cambió
            if (playerCore != null && playerRobot != playerCore.CurrentRobot)
            {
                playerRobot = playerCore.CurrentRobot;
                
                // Actualizar referencia en PlayerMovement también
                if (playerMovement != null && playerRobot != null && !isMounted)
                {
                    playerMovement.SetTarget(playerRobot.transform);
                }
            }
            
            if (isMounted)
            {
                // Mientras está montado, procesar input para el mecha
                HandleMountedInput();
                
                // Verificar si quiere desmontar
                if (Input.GetKeyDown(mountKey))
                {
                    Dismount();
                }
            }
            else
            {
                // Buscar mechas cercanos para montar
                FindNearbyMount();
                
                // Verificar si quiere montar
                if (Input.GetKeyDown(mountKey) && nearbyMount != null)
                {
                    Mount(nearbyMount);
                }
            }
        }
        
        #endregion
        
        #region Reference Detection
        
        private void FindReferences()
        {
            // Buscar PlayerCore
            if (playerCore == null)
            {
                playerCore = FindPlayerCore();
                
                if (playerCore != null)
                {
                    playerRobot = playerCore.CurrentRobot;
                    Debug.Log($"MountSystem: PlayerCore encontrado - Robot: {playerRobot?.name}");
                }
            }
            
            // Buscar PlayerMovement
            if (playerMovement == null)
            {
                playerMovement = FindObjectOfType<PlayerMovement>();
                
                if (playerMovement != null)
                {
                    Debug.Log("MountSystem: PlayerMovement encontrado");
                }
            }
            
            // Buscar PlayerCamera
            if (playerCamera == null)
            {
                playerCamera = FindObjectOfType<PlayerCamera>();
                
                if (playerCamera != null)
                {
                    Debug.Log("MountSystem: PlayerCamera encontrado");
                }
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
        
        private void FindNearbyMount()
        {
            nearbyMount = null;
            
            if (playerRobot == null) return;
            
            Vector3 playerPos = playerRobot.transform.position;
            float closestDistance = mountRange;
            
            // Buscar todos los WildRobots en la escena
            var wildRobots = FindObjectsOfType<WildRobot>();
            
            foreach (var wildRobot in wildRobots)
            {
                // Ignorar robots muertos o ya controlados
                if (!wildRobot.IsAlive || wildRobot.IsBeingControlled) continue;
                
                float distance = Vector3.Distance(playerPos, wildRobot.transform.position);
                
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    nearbyMount = wildRobot;
                }
            }
        }
        
        #endregion
        
        #region Mount/Dismount
        
        /// <summary>
        /// Monta al jugador en un robot salvaje.
        /// Por ahora, montar también domestica al robot automáticamente.
        /// </summary>
        public void Mount(WildRobot target)
        {
            if (target == null || isMounted) return;
            if (!target.IsAlive) return;
            
            Debug.Log($"MountSystem: Montando en '{target.WildData?.speciesName}'");
            
            // Domesticar automáticamente si no está domesticado (comportamiento temporal)
            if (!target.IsTamed && playerCore != null)
            {
                target.Tame(playerCore);
                Debug.Log($"MountSystem: Robot domesticado automáticamente");
            }
            
            // Guardar referencia
            currentMount = target;
            isMounted = true;
            
            // Desactivar el robot del jugador
            if (playerRobot != null)
            {
                playerRobot.gameObject.SetActive(false);
            }
            
            // Desactivar el movimiento del jugador
            if (playerMovement != null)
            {
                playerMovement.Disable();
            }
            
            // Tomar control del mecha
            currentMount.TakeControl();
            
            // Cambiar el objetivo de la cámara al mecha
            SetCameraTarget(currentMount.transform);
        }
        
        /// <summary>
        /// Desmonta al jugador del mecha actual.
        /// </summary>
        public void Dismount()
        {
            if (!isMounted || currentMount == null) return;
            
            Debug.Log($"MountSystem: Desmontando de '{currentMount.WildData?.speciesName}'");
            
            // Calcular posición de desmonte (debajo del mecha)
            Vector3 dismountPos = currentMount.transform.position;
            Quaternion dismountRot = currentMount.transform.rotation;
            
            // Liberar control del mecha ANTES de reactivar al jugador
            currentMount.ReleaseControl();
            
            // Reactivar el robot del jugador DEBAJO DEL MECHA
            if (playerRobot != null)
            {
                playerRobot.transform.position = dismountPos;
                playerRobot.transform.rotation = dismountRot;
                playerRobot.gameObject.SetActive(true);
            }
            
            // Cambiar el objetivo de la cámara al jugador ANTES de habilitar movimiento
            if (playerRobot != null)
            {
                SetCameraTarget(playerRobot.transform);
            }
            
            // Reactivar el movimiento del jugador
            if (playerMovement != null && playerRobot != null)
            {
                playerMovement.SetTarget(playerRobot.transform);
                playerMovement.Enable();
            }
            
            // Limpiar estado
            currentMount = null;
            isMounted = false;
            
            Debug.Log($"MountSystem: Desmontado. Jugador en {dismountPos}");
        }
        
        /// <summary>
        /// Cambia el target de la cámara.
        /// </summary>
        private void SetCameraTarget(Transform newTarget)
        {
            if (playerCamera != null)
            {
                playerCamera.SetTarget(newTarget, true);
                Debug.Log($"MountSystem: Camera target -> {newTarget.name}");
            }
        }
        
        private Vector3 CalculateDismountPosition()
        {
            if (currentMount == null) return Vector3.zero;
            
            // Intentar desmontar detrás del mecha
            Vector3 behindMecha = currentMount.transform.position + 
                                  currentMount.transform.TransformDirection(dismountOffset);
            
            // Verificar si hay espacio (raycast hacia abajo para encontrar suelo)
            if (Physics.Raycast(behindMecha + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f, mountableLayers))
            {
                return hit.point + Vector3.up * 0.1f;
            }
            
            // Si no hay suelo detrás, intentar a los lados o debajo
            Vector3 belowMecha = currentMount.transform.position;
            if (Physics.Raycast(belowMecha + Vector3.up * 2f, Vector3.down, out hit, 5f, mountableLayers))
            {
                return hit.point + Vector3.up * 0.1f;
            }
            
            // Fallback: misma posición del mecha
            return currentMount.transform.position;
        }
        
        #endregion
        
        #region Mounted Input
        
        private void HandleMountedInput()
        {
            if (currentMount == null || currentMount.Movement == null) return;
            
            // Leer input de movimiento
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            
            // Calcular dirección relativa a la cámara
            Vector3 moveDirection = Vector3.zero;
            
            if (playerCamera != null)
            {
                Transform camTransform = playerCamera.transform;
                Vector3 forward = camTransform.forward;
                Vector3 right = camTransform.right;
                
                forward.y = 0f;
                right.y = 0f;
                forward.Normalize();
                right.Normalize();
                
                moveDirection = (forward * vertical + right * horizontal);
            }
            else
            {
                moveDirection = new Vector3(horizontal, 0f, vertical);
            }
            
            // Aplicar movimiento al mecha
            currentMount.Movement.SetMoveDirection(moveDirection);
            
            // Sprint
            currentMount.Movement.SetSprinting(Input.GetKey(KeyCode.LeftShift));
            
            // Salto
            if (Input.GetKeyDown(KeyCode.Space))
            {
                currentMount.Movement.Jump();
            }
        }
        
        #endregion
        
        #region Debug
        
        private void OnGUI()
        {
            if (!showDebugInfo) return;
            
            GUILayout.BeginArea(new Rect(Screen.width - 310, 10, 300, 200));
            GUILayout.BeginVertical("box");
            
            GUILayout.Label("=== MOUNT SYSTEM ===");
            
            // Mostrar estado de referencias
            string coreStatus = playerCore != null ? "OK" : "NO";
            string robotStatus = playerRobot != null ? playerRobot.name : "NO";
            string movementStatus = playerMovement != null ? "OK" : "NO";
            string cameraStatus = playerCamera != null ? "OK" : "NO";
            
            GUILayout.Label($"Core: {coreStatus} | Robot: {robotStatus}");
            GUILayout.Label($"Movement: {movementStatus} | Camera: {cameraStatus}");
            
            GUILayout.Space(5);
            
            if (isMounted && currentMount != null)
            {
                GUILayout.Label($"MONTADO en: {currentMount.WildData?.speciesName}");
                GUILayout.Label($"[{mountKey}] Desmontar");
                GUILayout.Label($"[WASD] Mover | [Shift] Correr");
                GUILayout.Label($"[Space] Saltar");
            }
            else if (playerRobot != null)
            {
                if (nearbyMount != null)
                {
                    float dist = Vector3.Distance(playerRobot.transform.position, nearbyMount.transform.position);
                    GUILayout.Label($"Mecha cercano: {nearbyMount.WildData?.speciesName}");
                    GUILayout.Label($"Distancia: {dist:F1}m");
                    GUILayout.Label($"[{mountKey}] Montar");
                }
                else
                {
                    GUILayout.Label("No hay mecha cercano");
                    GUILayout.Label($"(Rango: {mountRange}m)");
                }
            }
            else
            {
                GUILayout.Label("Esperando referencias...");
            }
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        
        private void OnDrawGizmos()
        {
            if (!showGizmos) return;
            
            // Dibujar rango de montura alrededor del jugador
            if (playerRobot != null && !isMounted)
            {
                Gizmos.color = nearbyMount != null ? Color.green : Color.yellow;
                Gizmos.DrawWireSphere(playerRobot.transform.position, mountRange);
                
                // Línea hacia el mecha cercano
                if (nearbyMount != null)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(playerRobot.transform.position, nearbyMount.transform.position);
                }
            }
            
            // Dibujar posición de desmonte
            if (isMounted && currentMount != null)
            {
                Gizmos.color = Color.cyan;
                Vector3 dismountPos = currentMount.transform.position + 
                                      currentMount.transform.TransformDirection(dismountOffset);
                Gizmos.DrawWireSphere(dismountPos, 0.5f);
            }
        }
        
        #endregion
    }
}
