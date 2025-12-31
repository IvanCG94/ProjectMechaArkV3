using System.Collections.Generic;
using UnityEngine;
using RobotGame.Components;
using RobotGame.Data;
using RobotGame.Enums;

namespace RobotGame.AI
{
    /// <summary>
    /// Spawner que crea robots salvajes a partir de WildRobotData.
    /// Puede spawnear robots individuales o múltiples de forma aleatoria.
    /// </summary>
    public class WildRobotSpawner : MonoBehaviour
    {
        [Header("Robots a Spawnear")]
        [Tooltip("Lista de robots que pueden aparecer en este spawner")]
        [SerializeField] private List<WildRobotData> possibleRobots = new List<WildRobotData>();
        
        [Header("Configuración de Spawn")]
        [Tooltip("Cantidad máxima de robots activos de este spawner")]
        [SerializeField] private int maxActiveRobots = 3;
        
        [Tooltip("Tiempo entre spawns (segundos)")]
        [SerializeField] private float spawnInterval = 30f;
        
        [Tooltip("Radio de spawn alrededor del spawner")]
        [SerializeField] private float spawnRadius = 5f;
        
        [Tooltip("Spawnear automáticamente al iniciar")]
        [SerializeField] private bool spawnOnStart = true;
        
        [Tooltip("Spawnear automáticamente con el tiempo")]
        [SerializeField] private bool autoRespawn = true;
        
        [Header("Debug")]
        [SerializeField] private bool showGizmos = true;
        
        // Estado interno
        private List<WildRobot> activeRobots = new List<WildRobot>();
        private float lastSpawnTime;
        
        #region Properties
        
        /// <summary>
        /// Cantidad de robots activos actualmente.
        /// </summary>
        public int ActiveRobotCount => activeRobots.Count;
        
        /// <summary>
        /// Si puede spawnear más robots.
        /// </summary>
        public bool CanSpawn => activeRobots.Count < maxActiveRobots && possibleRobots.Count > 0;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Start()
        {
            if (spawnOnStart)
            {
                // Spawnear la cantidad inicial
                for (int i = 0; i < maxActiveRobots; i++)
                {
                    SpawnRandomRobot();
                }
            }
        }
        
        private void Update()
        {
            // Limpiar robots destruidos de la lista
            activeRobots.RemoveAll(r => r == null || !r.IsAlive);
            
            // Auto-respawn
            if (autoRespawn && CanSpawn)
            {
                if (Time.time - lastSpawnTime >= spawnInterval)
                {
                    SpawnRandomRobot();
                    lastSpawnTime = Time.time;
                }
            }
        }
        
        #endregion
        
        #region Spawning
        
        /// <summary>
        /// Spawnea un robot aleatorio de la lista.
        /// </summary>
        public WildRobot SpawnRandomRobot()
        {
            if (!CanSpawn) return null;
            
            // Seleccionar robot basado en peso
            WildRobotData selectedData = SelectRandomRobot();
            
            if (selectedData == null)
            {
                Debug.LogWarning("WildRobotSpawner: No se pudo seleccionar un robot para spawnear");
                return null;
            }
            
            return SpawnRobot(selectedData);
        }
        
        /// <summary>
        /// Spawnea un robot específico.
        /// </summary>
        public WildRobot SpawnRobot(WildRobotData data)
        {
            if (data == null)
            {
                Debug.LogError("WildRobotSpawner: WildRobotData es null");
                return null;
            }
            
            // Validar configuración
            List<string> errors;
            if (!data.ValidateConfiguration(out errors))
            {
                Debug.LogError($"WildRobotSpawner: Configuración inválida para '{data.speciesName}':\n{string.Join("\n", errors)}");
                return null;
            }
            
            // Calcular posición de spawn
            Vector3 spawnPos = GetRandomSpawnPosition();
            
            // Crear el robot usando RobotFactory
            Robot robot = CreateRobotFromData(data, spawnPos);
            
            if (robot == null)
            {
                Debug.LogError($"WildRobotSpawner: Error al crear robot '{data.speciesName}'");
                return null;
            }
            
            // Añadir componente WildRobot
            WildRobot wildRobot = robot.gameObject.AddComponent<WildRobot>();
            wildRobot.Initialize(data);
            
            // Registrar
            activeRobots.Add(wildRobot);
            lastSpawnTime = Time.time;
            
            Debug.Log($"WildRobotSpawner: Spawneado '{data.speciesName}' en {spawnPos}");
            
            return wildRobot;
        }
        
        /// <summary>
        /// Crea un robot desde WildRobotData (sin Core).
        /// </summary>
        private Robot CreateRobotFromData(WildRobotData data, Vector3 position)
        {
            // Crear el GameObject raíz
            GameObject robotGO = new GameObject($"WildRobot_{data.speciesName}");
            robotGO.transform.position = position;
            robotGO.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            
            // Añadir componente Robot
            Robot robot = robotGO.AddComponent<Robot>();
            robot.Initialize(null, data.speciesName, data.tier);
            
            // Crear las Hips
            if (data.hips == null)
            {
                Debug.LogError("WildRobotSpawner: WildRobotData no tiene Hips asignadas");
                Destroy(robotGO);
                return null;
            }
            
            StructuralPart hips = CreateStructuralPart(data.hips, robot.HipsSocket.transform);
            if (hips == null)
            {
                Destroy(robotGO);
                return null;
            }
            
            robot.AttachHips(hips);
            
            // Colocar armadura en las Hips
            PlaceArmorPieces(hips, data.hipsArmorPieces);
            
            // Crear piezas estructurales conectadas
            CreateAttachedParts(hips, data.attachedParts);
            
            return robot;
        }
        
        private StructuralPart CreateStructuralPart(StructuralPartData data, Transform parent)
        {
            if (data == null || data.prefab == null)
            {
                return null;
            }
            
            GameObject partGO = Instantiate(data.prefab, parent);
            partGO.name = $"Part_{data.displayName}";
            
            StructuralPart part = partGO.GetComponent<StructuralPart>();
            if (part == null)
            {
                part = partGO.AddComponent<StructuralPart>();
            }
            
            part.Initialize(data);
            
            return part;
        }
        
        private void CreateAttachedParts(StructuralPart parent, List<AttachedStructuralPiece> attachedPieces)
        {
            if (attachedPieces == null) return;
            
            foreach (var attachedPiece in attachedPieces)
            {
                if (attachedPiece.partData == null) continue;
                
                StructuralSocket socket = parent.GetSocket(attachedPiece.attachedToSocket);
                
                if (socket == null)
                {
                    Debug.LogWarning($"WildRobotSpawner: Socket {attachedPiece.attachedToSocket} no encontrado");
                    continue;
                }
                
                StructuralPart part = CreateStructuralPart(attachedPiece.partData, parent.transform);
                
                if (part == null) continue;
                
                if (!socket.TryAttach(part))
                {
                    Debug.LogWarning($"WildRobotSpawner: No se pudo conectar {part.PartData.displayName}");
                    Destroy(part.gameObject);
                    continue;
                }
                
                // Colocar armadura
                PlaceArmorPieces(part, attachedPiece.armorPieces);
                
                // Recursión
                CreateAttachedParts(part, attachedPiece.childParts);
            }
        }
        
        private void PlaceArmorPieces(StructuralPart structuralPart, List<PlacedArmorPiece> armorPieces)
        {
            if (armorPieces == null) return;
            
            foreach (var placedArmor in armorPieces)
            {
                if (placedArmor.armorData == null) continue;
                
                GridHead grid = structuralPart.GetArmorGrid(placedArmor.targetGridName);
                
                if (grid == null)
                {
                    Debug.LogWarning($"WildRobotSpawner: Grilla '{placedArmor.targetGridName}' no encontrada");
                    continue;
                }
                
                ArmorPart armor = CreateArmorPart(placedArmor.armorData);
                
                if (armor == null) continue;
                
                if (!grid.TryPlace(armor, placedArmor.gridPositionX, placedArmor.gridPositionY))
                {
                    Debug.LogWarning($"WildRobotSpawner: No se pudo colocar armadura");
                    Destroy(armor.gameObject);
                }
            }
        }
        
        private ArmorPart CreateArmorPart(ArmorPartData data)
        {
            if (data == null || data.prefab == null)
            {
                return null;
            }
            
            GameObject armorGO = Instantiate(data.prefab);
            armorGO.name = $"Armor_{data.displayName}";
            
            ArmorPart armor = armorGO.GetComponent<ArmorPart>();
            if (armor == null)
            {
                armor = armorGO.AddComponent<ArmorPart>();
            }
            
            armor.Initialize(data);
            
            return armor;
        }
        
        #endregion
        
        #region Selection
        
        private WildRobotData SelectRandomRobot()
        {
            if (possibleRobots.Count == 0) return null;
            if (possibleRobots.Count == 1) return possibleRobots[0];
            
            // Calcular peso total
            int totalWeight = 0;
            foreach (var robot in possibleRobots)
            {
                if (robot != null)
                {
                    totalWeight += robot.spawnWeight;
                }
            }
            
            if (totalWeight <= 0) return possibleRobots[0];
            
            // Seleccionar basado en peso
            int roll = Random.Range(0, totalWeight);
            int currentWeight = 0;
            
            foreach (var robot in possibleRobots)
            {
                if (robot == null) continue;
                
                currentWeight += robot.spawnWeight;
                
                if (roll < currentWeight)
                {
                    return robot;
                }
            }
            
            return possibleRobots[0];
        }
        
        private Vector3 GetRandomSpawnPosition()
        {
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            Vector3 offset = new Vector3(randomCircle.x, 0f, randomCircle.y);
            
            return transform.position + offset;
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Fuerza el spawn de un robot específico, ignorando límites.
        /// </summary>
        public WildRobot ForceSpawn(WildRobotData data)
        {
            return SpawnRobot(data);
        }
        
        /// <summary>
        /// Mata a todos los robots activos de este spawner.
        /// </summary>
        public void KillAll()
        {
            foreach (var robot in activeRobots)
            {
                if (robot != null)
                {
                    robot.TakeDamage(float.MaxValue);
                }
            }
            
            activeRobots.Clear();
        }
        
        /// <summary>
        /// Destruye todos los robots activos de este spawner.
        /// </summary>
        public void DestroyAll()
        {
            foreach (var robot in activeRobots)
            {
                if (robot != null)
                {
                    Destroy(robot.gameObject);
                }
            }
            
            activeRobots.Clear();
        }
        
        #endregion
        
        #region Debug
        
        private void OnDrawGizmos()
        {
            if (!showGizmos) return;
            
            // Área de spawn
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
            
            // Centro
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position, new Vector3(0.5f, 0.5f, 0.5f));
        }
        
        private void OnDrawGizmosSelected()
        {
            if (!showGizmos) return;
            
            // Área de spawn más visible
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.5f);
            Gizmos.DrawSphere(transform.position, 0.3f);
            
            // Mostrar info
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2f, 
                $"Spawner\nRobots: {possibleRobots.Count}\nMax: {maxActiveRobots}"
            );
            #endif
        }
        
        #endregion
    }
}
