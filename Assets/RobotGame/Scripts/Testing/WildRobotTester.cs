using UnityEngine;
using RobotGame.Data;
using RobotGame.AI;
using RobotGame.Enums;

namespace RobotGame.Testing
{
    /// <summary>
    /// Script de prueba para spawnear robots salvajes.
    /// Añádelo a un GameObject vacío en la escena.
    /// 
    /// USO:
    /// 1. Asigna un WildRobotData en el inspector
    /// 2. Presiona 'G' para spawnear un robot salvaje
    /// 3. El robot aparecerá frente al jugador
    /// </summary>
    public class WildRobotTester : MonoBehaviour
    {
        [Header("Robot a Spawnear")]
        [Tooltip("Datos del robot salvaje a spawnear")]
        [SerializeField] private WildRobotData wildRobotData;
        
        [Header("Spawn")]
        [Tooltip("Distancia frente al jugador donde aparece")]
        [SerializeField] private float spawnDistance = 5f;
        
        [Tooltip("Tecla para spawnear")]
        [SerializeField] private KeyCode spawnKey = KeyCode.G;
        
        [Header("Debug")]
        [Tooltip("Mostrar información en pantalla")]
        [SerializeField] private bool showDebugInfo = true;
        
        // Referencia al último robot spawneado
        private WildRobot lastSpawnedRobot;
        
        private void Update()
        {
            // Spawnear con tecla G
            if (Input.GetKeyDown(spawnKey))
            {
                SpawnWildRobot();
            }
            
            // Tecla H para hacer daño al último robot spawneado
            if (Input.GetKeyDown(KeyCode.H) && lastSpawnedRobot != null)
            {
                lastSpawnedRobot.TakeDamage(20f);
            }
            
            // Tecla J para matar al último robot
            if (Input.GetKeyDown(KeyCode.J) && lastSpawnedRobot != null)
            {
                lastSpawnedRobot.TakeDamage(float.MaxValue);
            }
        }
        
        private void SpawnWildRobot()
        {
            if (wildRobotData == null)
            {
                Debug.LogError("WildRobotTester: No hay WildRobotData asignado!");
                return;
            }
            
            // Validar configuración
            if (!wildRobotData.ValidateConfiguration(out var errors))
            {
                Debug.LogError($"WildRobotTester: Configuración inválida:\n{string.Join("\n", errors)}");
                return;
            }
            
            // Encontrar posición de spawn (frente al jugador o en el centro)
            Vector3 spawnPos = GetSpawnPosition();
            
            // Crear el robot
            WildRobot robot = CreateWildRobot(wildRobotData, spawnPos);
            
            if (robot != null)
            {
                lastSpawnedRobot = robot;
                Debug.Log($"WildRobotTester: Robot '{wildRobotData.speciesName}' spawneado en {spawnPos}");
            }
        }
        
        private Vector3 GetSpawnPosition()
        {
            // Intentar encontrar al jugador
            var playerCore = FindObjectOfType<RobotGame.Components.RobotCore>();
            
            if (playerCore != null && playerCore.IsPlayerCore && playerCore.CurrentRobot != null)
            {
                Transform playerTransform = playerCore.CurrentRobot.transform;
                return playerTransform.position + playerTransform.forward * spawnDistance;
            }
            
            // Si no hay jugador, usar la posición del tester
            return transform.position + transform.forward * spawnDistance;
        }
        
        private WildRobot CreateWildRobot(WildRobotData data, Vector3 position)
        {
            // Crear GameObject raíz
            GameObject robotGO = new GameObject($"WildRobot_{data.speciesName}");
            robotGO.transform.position = position;
            
            // Añadir componente Robot
            var robot = robotGO.AddComponent<RobotGame.Components.Robot>();
            robot.Initialize(null, data.speciesName, data.tier);
            
            // Crear estructura
            if (!CreateRobotStructure(robot, data))
            {
                Destroy(robotGO);
                return null;
            }
            
            // Añadir componente WildRobot
            WildRobot wildRobot = robotGO.AddComponent<WildRobot>();
            wildRobot.Initialize(data);
            
            return wildRobot;
        }
        
        private bool CreateRobotStructure(RobotGame.Components.Robot robot, WildRobotData data)
        {
            // Crear Hips
            if (data.hips == null || data.hips.prefab == null)
            {
                Debug.LogError("WildRobotTester: Hips no tiene prefab asignado");
                return false;
            }
            
            var hips = CreateStructuralPart(data.hips, robot.HipsSocket.transform);
            if (hips == null) return false;
            
            robot.AttachHips(hips);
            
            // Colocar armadura en Hips
            PlaceArmorPieces(hips, data.hipsArmorPieces);
            
            // Crear piezas conectadas
            CreateAttachedParts(hips, data.attachedParts);
            
            return true;
        }
        
        private RobotGame.Components.StructuralPart CreateStructuralPart(StructuralPartData data, Transform parent)
        {
            if (data == null || data.prefab == null) return null;
            
            GameObject partGO = Instantiate(data.prefab, parent);
            partGO.name = $"Part_{data.displayName}";
            
            var part = partGO.GetComponent<RobotGame.Components.StructuralPart>();
            if (part == null)
            {
                part = partGO.AddComponent<RobotGame.Components.StructuralPart>();
            }
            
            part.Initialize(data);
            return part;
        }
        
        private void CreateAttachedParts(RobotGame.Components.StructuralPart parent, System.Collections.Generic.List<AttachedStructuralPiece> pieces)
        {
            if (pieces == null) return;
            
            foreach (var piece in pieces)
            {
                if (piece.partData == null) continue;
                
                var socket = parent.GetSocket(piece.attachedToSocket);
                if (socket == null)
                {
                    Debug.LogWarning($"Socket {piece.attachedToSocket} no encontrado en {parent.PartData.displayName}");
                    continue;
                }
                
                var part = CreateStructuralPart(piece.partData, parent.transform);
                if (part == null) continue;
                
                if (!socket.TryAttach(part))
                {
                    Debug.LogWarning($"No se pudo conectar {piece.partData.displayName} a {piece.attachedToSocket}");
                    Destroy(part.gameObject);
                    continue;
                }
                
                PlaceArmorPieces(part, piece.armorPieces);
                CreateAttachedParts(part, piece.childParts);
            }
        }
        
        private void PlaceArmorPieces(RobotGame.Components.StructuralPart part, System.Collections.Generic.List<PlacedArmorPiece> armorPieces)
        {
            if (armorPieces == null) return;
            
            foreach (var armor in armorPieces)
            {
                if (armor.armorData == null || armor.armorData.prefab == null) continue;
                
                var grid = part.GetArmorGrid(armor.targetGridName);
                if (grid == null)
                {
                    Debug.LogWarning($"Grilla '{armor.targetGridName}' no encontrada en {part.PartData.displayName}");
                    continue;
                }
                
                GameObject armorGO = Instantiate(armor.armorData.prefab);
                armorGO.name = $"Armor_{armor.armorData.displayName}";
                
                var armorPart = armorGO.GetComponent<RobotGame.Components.ArmorPart>();
                if (armorPart == null)
                {
                    armorPart = armorGO.AddComponent<RobotGame.Components.ArmorPart>();
                }
                
                armorPart.Initialize(armor.armorData);
                
                if (!grid.TryPlace(armorPart, armor.gridPositionX, armor.gridPositionY))
                {
                    Debug.LogWarning($"No se pudo colocar {armor.armorData.displayName} en grilla {armor.targetGridName}");
                    Destroy(armorGO);
                }
            }
        }
        
        private void OnGUI()
        {
            if (!showDebugInfo) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.BeginVertical("box");
            
            GUILayout.Label("=== WILD ROBOT TESTER ===");
            GUILayout.Label($"Robot: {(wildRobotData != null ? wildRobotData.speciesName : "NINGUNO")}");
            GUILayout.Label($"[{spawnKey}] Spawnear robot");
            GUILayout.Label("[H] Hacer 20 de daño");
            GUILayout.Label("[J] Matar robot");
            
            if (lastSpawnedRobot != null && lastSpawnedRobot.IsAlive)
            {
                GUILayout.Space(10);
                GUILayout.Label($"--- Último Robot ---");
                GUILayout.Label($"Estado: {lastSpawnedRobot.CurrentState}");
                GUILayout.Label($"Salud: {lastSpawnedRobot.CurrentHealth:F0}/{lastSpawnedRobot.MaxHealth:F0}");
            }
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
}
