using System.Collections.Generic;
using UnityEngine;
using RobotGame.Components;
using RobotGame.Data;
using RobotGame.Enums;
using RobotGame.Systems;

namespace RobotGame.Data
{
    /// <summary>
    /// Snapshot del estado de una pieza de armadura en runtime.
    /// </summary>
    [System.Serializable]
    public class ArmorSnapshot
    {
        public ArmorPartData armorData;
        public string gridTransformName;
        public int positionX;
        public int positionY;
        public int rotation; // 0, 90, 180, 270
    }
    
    /// <summary>
    /// Snapshot del estado de una pieza estructural en runtime.
    /// </summary>
    [System.Serializable]
    public class StructuralSnapshot
    {
        public StructuralPartData partData;
        public StructuralSocketType socketType;
        public List<StructuralSnapshot> children = new List<StructuralSnapshot>();
        public List<ArmorSnapshot> armors = new List<ArmorSnapshot>();
    }
    
    /// <summary>
    /// Snapshot completo del estado de un robot en runtime.
    /// Se usa para guardar/restaurar configuraciones durante la edición.
    /// </summary>
    [System.Serializable]
    public class RobotSnapshot
    {
        public string robotName;
        public CoreData coreData;
        public bool coreWasInserted;
        public StructuralPartData hipsData;
        public List<ArmorSnapshot> hipsArmors = new List<ArmorSnapshot>();
        public List<StructuralSnapshot> attachedParts = new List<StructuralSnapshot>();
        
        /// <summary>
        /// Captura el estado actual de un robot.
        /// </summary>
        public static RobotSnapshot Capture(Robot robot)
        {
            if (robot == null) return null;
            
            RobotSnapshot snapshot = new RobotSnapshot();
            snapshot.robotName = robot.RobotName;
            
            // Capturar Core
            if (robot.Core != null)
            {
                snapshot.coreData = robot.Core.CoreData;
                snapshot.coreWasInserted = robot.Core.IsActive;
            }
            
            // Capturar Hips
            StructuralPart hips = robot.Hips;
            if (hips != null)
            {
                snapshot.hipsData = hips.PartData;
                
                // Capturar armaduras de las Hips
                foreach (var grid in hips.ArmorGrids)
                {
                    CaptureGridArmors(grid, snapshot.hipsArmors);
                }
                
                // Capturar piezas estructurales conectadas a las Hips
                foreach (var socket in hips.ChildSockets)
                {
                    if (socket.IsOccupied && socket.AttachedPart != null)
                    {
                        var childSnapshot = CaptureStructuralPart(socket.AttachedPart, socket.SocketType);
                        snapshot.attachedParts.Add(childSnapshot);
                    }
                }
            }
            
            return snapshot;
        }
        
        private static StructuralSnapshot CaptureStructuralPart(StructuralPart part, StructuralSocketType socketType)
        {
            StructuralSnapshot snapshot = new StructuralSnapshot();
            snapshot.partData = part.PartData;
            snapshot.socketType = socketType;
            
            // Capturar armaduras
            foreach (var grid in part.ArmorGrids)
            {
                CaptureGridArmors(grid, snapshot.armors);
            }
            
            // Capturar hijos recursivamente
            foreach (var childSocket in part.ChildSockets)
            {
                if (childSocket.IsOccupied && childSocket.AttachedPart != null)
                {
                    var childSnapshot = CaptureStructuralPart(childSocket.AttachedPart, childSocket.SocketType);
                    snapshot.children.Add(childSnapshot);
                }
            }
            
            return snapshot;
        }
        
        private static void CaptureGridArmors(GridHead grid, List<ArmorSnapshot> armorList)
        {
            foreach (var armor in grid.PlacedParts)
            {
                ArmorSnapshot armorSnapshot = new ArmorSnapshot();
                armorSnapshot.armorData = armor.ArmorData;
                armorSnapshot.gridTransformName = grid.name;
                armorSnapshot.positionX = armor.GridPositionX;
                armorSnapshot.positionY = armor.GridPositionY;
                armorSnapshot.rotation = (int)armor.CurrentRotation;
                armorList.Add(armorSnapshot);
            }
        }
        
        /// <summary>
        /// Restaura un robot desde este snapshot.
        /// Destruye el robot actual y crea uno nuevo.
        /// </summary>
        public Robot Restore(Robot currentRobot, RobotCore playerCore)
        {
            if (hipsData == null)
            {
                Debug.LogWarning("RobotSnapshot: No hay datos de Hips para restaurar.");
                return null;
            }
            
            Vector3 position = currentRobot != null ? currentRobot.transform.position : Vector3.zero;
            Quaternion rotation = currentRobot != null ? currentRobot.transform.rotation : Quaternion.identity;
            
            // Destruir el robot actual (pero no el Core del jugador)
            if (currentRobot != null)
            {
                // Extraer el Core antes de destruir
                if (currentRobot.Core != null && currentRobot.Core == playerCore)
                {
                    playerCore.Extract();
                }
                
                Object.Destroy(currentRobot.gameObject);
            }
            
            // Crear nuevo robot desde el snapshot
            RobotFactory factory = RobotFactory.Instance;
            
            // Crear el robot base
            Robot newRobot = factory.CreateEmptyRobot(robotName);
            newRobot.transform.position = position;
            newRobot.transform.rotation = rotation;
            
            // Crear y conectar las Hips
            StructuralPart hips = factory.CreateStructuralPart(hipsData);
            newRobot.SetHips(hips);
            
            // Restaurar armaduras de las Hips
            RestoreArmorsToStructural(hips, hipsArmors);
            
            // Restaurar piezas estructurales recursivamente
            RestoreStructuralParts(hips, attachedParts);
            
            // Reinsertar el Core del jugador si estaba insertado
            if (playerCore != null && coreWasInserted)
            {
                playerCore.InsertInto(newRobot);
            }
            
            return newRobot;
        }
        
        private void RestoreStructuralParts(StructuralPart parent, List<StructuralSnapshot> snapshots)
        {
            RobotFactory factory = RobotFactory.Instance;
            
            foreach (var snapshot in snapshots)
            {
                if (snapshot.partData == null) continue;
                
                // Buscar el socket correspondiente
                StructuralSocket socket = parent.GetSocket(snapshot.socketType);
                if (socket == null) continue;
                
                // Crear la pieza
                StructuralPart part = factory.CreateStructuralPart(snapshot.partData);
                
                // Conectar al socket
                socket.TryAttach(part);
                
                // Restaurar armaduras
                RestoreArmorsToStructural(part, snapshot.armors);
                
                // Restaurar hijos recursivamente
                RestoreStructuralParts(part, snapshot.children);
            }
        }
        
        private void RestoreArmorsToStructural(StructuralPart structuralPart, List<ArmorSnapshot> armors)
        {
            RobotFactory factory = RobotFactory.Instance;
            
            foreach (var armorSnapshot in armors)
            {
                if (armorSnapshot.armorData == null) continue;
                
                // Buscar la grilla por nombre
                GridHead targetGrid = null;
                foreach (var grid in structuralPart.ArmorGrids)
                {
                    if (grid.name == armorSnapshot.gridTransformName)
                    {
                        targetGrid = grid;
                        break;
                    }
                }
                
                if (targetGrid == null) continue;
                
                // Crear y colocar la armadura
                ArmorPart armor = factory.CreateArmorPart(armorSnapshot.armorData);
                
                // Aplicar rotación
                // TODO: Implementar rotación en ArmorPart si es necesario
                
                // Colocar en la grilla
                targetGrid.TryPlace(armor, armorSnapshot.positionX, armorSnapshot.positionY);
            }
        }
        
        /// <summary>
        /// Valida si el snapshot representa una configuración válida.
        /// Requiere: Hips, Torso, Head, Core insertado.
        /// </summary>
        public bool IsValid(out List<string> errors)
        {
            errors = new List<string>();
            
            // Verificar Core
            if (coreData == null || !coreWasInserted)
            {
                errors.Add("El robot necesita un Core insertado");
            }
            
            // Verificar Hips
            if (hipsData == null)
            {
                errors.Add("El robot necesita Hips");
            }
            
            // Verificar Torso
            bool hasTorso = false;
            StructuralSnapshot torsoSnapshot = null;
            foreach (var part in attachedParts)
            {
                if (part.socketType == StructuralSocketType.Torso && part.partData != null)
                {
                    hasTorso = true;
                    torsoSnapshot = part;
                    break;
                }
            }
            
            if (!hasTorso)
            {
                errors.Add("El robot necesita un Torso");
            }
            
            // Verificar Head (debe estar conectado al Torso)
            bool hasHead = false;
            if (torsoSnapshot != null)
            {
                foreach (var child in torsoSnapshot.children)
                {
                    if (child.socketType == StructuralSocketType.Head && child.partData != null)
                    {
                        hasHead = true;
                        break;
                    }
                }
            }
            
            if (!hasHead)
            {
                errors.Add("El robot necesita una Cabeza (Head)");
            }
            
            return errors.Count == 0;
        }
    }
}
