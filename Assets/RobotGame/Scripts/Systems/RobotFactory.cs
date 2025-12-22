using System.Collections.Generic;
using UnityEngine;
using RobotGame.Data;
using RobotGame.Components;
using RobotGame.Enums;

namespace RobotGame.Systems
{
    /// <summary>
    /// Fábrica que ensambla robots a partir de configuraciones (RobotConfiguration).
    /// </summary>
    public class RobotFactory : MonoBehaviour
    {
        [Header("Configuración")]
        [SerializeField] private Transform spawnPoint;
        
        private static RobotFactory instance;
        
        /// <summary>
        /// Instancia singleton del factory.
        /// </summary>
        public static RobotFactory Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<RobotFactory>();
                    
                    if (instance == null)
                    {
                        GameObject go = new GameObject("RobotFactory");
                        instance = go.AddComponent<RobotFactory>();
                    }
                }
                
                return instance;
            }
        }
        
        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }
        
        /// <summary>
        /// Crea un robot completo a partir de una configuración.
        /// </summary>
        /// <param name="config">Configuración del robot</param>
        /// <param name="insertCore">Si debe insertar el core automáticamente</param>
        /// <returns>El robot creado</returns>
        public Robot CreateRobot(RobotConfiguration config, bool insertCore = true)
        {
            if (config == null)
            {
                Debug.LogError("RobotFactory: Configuración es null.");
                return null;
            }
            
            // Validar configuración
            if (!config.ValidateConfiguration(out List<string> errors))
            {
                Debug.LogError($"RobotFactory: Configuración inválida:\n{string.Join("\n", errors)}");
                return null;
            }
            
            // Crear el GameObject raíz del robot
            GameObject robotGO = new GameObject($"Robot_{config.robotName}");
            robotGO.transform.position = spawnPoint != null ? spawnPoint.position : Vector3.zero;
            
            // Agregar componente Robot
            Robot robot = robotGO.AddComponent<Robot>();
            
            // Crear las Hips (raíz estructural)
            StructuralPart hips = CreateStructuralPart(config.hips, robotGO.transform);
            
            if (hips == null)
            {
                Debug.LogError("RobotFactory: No se pudieron crear las Hips.");
                Destroy(robotGO);
                return null;
            }
            
            // Colocar armadura en las Hips
            PlaceArmorPieces(hips, config.hipsArmorPieces);
            
            // Crear piezas estructurales conectadas ANTES de inicializar
            CreateAttachedParts(hips, config.attachedParts);
            
            // Inicializar el robot DESPUÉS de crear todas las piezas
            robot.Initialize(null, config.robotName, hips, config.core.tier);
            
            // Crear e insertar el core si se requiere
            if (insertCore && config.core != null)
            {
                RobotCore core = CreateCore(config.core);
                if (core != null)
                {
                    core.InsertInto(robot);
                }
            }
            
            Debug.Log($"RobotFactory: Robot '{config.robotName}' creado exitosamente.");
            
            return robot;
        }
        
        /// <summary>
        /// Crea un robot vacío (solo estructura, sin core).
        /// </summary>
        public Robot CreateEmptyRobot(StructuralPartData hipsData, RobotTier tier, string name = "Empty Robot")
        {
            if (hipsData == null || !hipsData.canBeRoot)
            {
                Debug.LogError("RobotFactory: HipsData es null o no puede ser raíz.");
                return null;
            }
            
            GameObject robotGO = new GameObject($"Robot_{name}");
            robotGO.transform.position = spawnPoint != null ? spawnPoint.position : Vector3.zero;
            
            Robot robot = robotGO.AddComponent<Robot>();
            StructuralPart hips = CreateStructuralPart(hipsData, robotGO.transform);
            
            robot.Initialize(null, name, hips, tier);
            
            return robot;
        }
        
        /// <summary>
        /// Crea una pieza estructural e inicializa sus componentes.
        /// </summary>
        public StructuralPart CreateStructuralPart(StructuralPartData data, Transform parent)
        {
            if (data == null || data.prefab == null)
            {
                Debug.LogError("RobotFactory: StructuralPartData o prefab es null.");
                return null;
            }
            
            // Instanciar el prefab
            GameObject partGO = Instantiate(data.prefab, parent);
            partGO.name = $"Part_{data.displayName}";
            
            // Agregar e inicializar el componente
            StructuralPart part = partGO.GetComponent<StructuralPart>();
            if (part == null)
            {
                part = partGO.AddComponent<StructuralPart>();
            }
            
            part.Initialize(data);
            
            return part;
        }
        
        /// <summary>
        /// Crea una pieza de armadura.
        /// </summary>
        public ArmorPart CreateArmorPart(ArmorPartData data)
        {
            if (data == null || data.prefab == null)
            {
                Debug.LogError("RobotFactory: ArmorPartData o prefab es null.");
                return null;
            }
            
            // Instanciar el prefab
            GameObject armorGO = Instantiate(data.prefab);
            armorGO.name = $"Armor_{data.displayName}";
            
            // Agregar e inicializar el componente
            ArmorPart armor = armorGO.GetComponent<ArmorPart>();
            if (armor == null)
            {
                armor = armorGO.AddComponent<ArmorPart>();
            }
            
            armor.Initialize(data);
            
            return armor;
        }
        
        /// <summary>
        /// Crea un core.
        /// </summary>
        public RobotCore CreateCore(CoreData data)
        {
            if (data == null)
            {
                Debug.LogError("RobotFactory: CoreData es null.");
                return null;
            }
            
            GameObject coreGO;
            
            if (data.prefab != null)
            {
                coreGO = Instantiate(data.prefab);
            }
            else
            {
                // Crear un GameObject simple si no hay prefab
                coreGO = new GameObject($"Core_{data.displayName}");
            }
            
            coreGO.name = $"Core_{data.displayName}";
            
            RobotCore core = coreGO.GetComponent<RobotCore>();
            if (core == null)
            {
                core = coreGO.AddComponent<RobotCore>();
            }
            
            core.Initialize(data);
            
            return core;
        }
        
        /// <summary>
        /// Crea las piezas estructurales conectadas recursivamente.
        /// </summary>
        private void CreateAttachedParts(StructuralPart parent, List<AttachedStructuralPiece> attachedPieces)
        {
            if (attachedPieces == null) return;
            
            foreach (var attachedPiece in attachedPieces)
            {
                if (attachedPiece.partData == null) continue;
                
                // Buscar el socket correspondiente
                StructuralSocket socket = parent.GetSocket(attachedPiece.attachedToSocket);
                
                if (socket == null)
                {
                    Debug.LogWarning($"RobotFactory: Socket {attachedPiece.attachedToSocket} no encontrado en {parent.PartData.displayName}.");
                    continue;
                }
                
                // Crear la pieza
                StructuralPart part = CreateStructuralPart(attachedPiece.partData, parent.transform);
                
                if (part == null) continue;
                
                // Conectar al socket
                if (!socket.TryAttach(part))
                {
                    Debug.LogWarning($"RobotFactory: No se pudo conectar {part.PartData.displayName} a {socket.SocketType}.");
                    Destroy(part.gameObject);
                    continue;
                }
                
                // Colocar armadura en esta pieza
                PlaceArmorPieces(part, attachedPiece.armorPieces);
                
                // Crear piezas hijas recursivamente
                CreateAttachedParts(part, attachedPiece.childParts);
            }
        }
        
        /// <summary>
        /// Coloca piezas de armadura en las grillas de una pieza estructural.
        /// </summary>
        private void PlaceArmorPieces(StructuralPart structuralPart, List<PlacedArmorPiece> armorPieces)
        {
            if (armorPieces == null) return;
            
            foreach (var placedArmor in armorPieces)
            {
                if (placedArmor.armorData == null) continue;
                
                // Buscar la grilla
                GridHead grid = structuralPart.GetArmorGrid(placedArmor.targetGridName);
                
                if (grid == null)
                {
                    Debug.LogWarning($"RobotFactory: Grilla '{placedArmor.targetGridName}' no encontrada en {structuralPart.PartData.displayName}.");
                    continue;
                }
                
                // Crear la pieza de armadura
                ArmorPart armor = CreateArmorPart(placedArmor.armorData);
                
                if (armor == null) continue;
                
                // Colocar en la grilla
                if (!grid.TryPlace(armor, placedArmor.gridPositionX, placedArmor.gridPositionY))
                {
                    Debug.LogWarning($"RobotFactory: No se pudo colocar {armor.ArmorData.displayName} en grilla '{placedArmor.targetGridName}' posición ({placedArmor.gridPositionX}, {placedArmor.gridPositionY}).");
                    Destroy(armor.gameObject);
                }
            }
        }
    }
}
