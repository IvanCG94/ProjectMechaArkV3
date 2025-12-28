using System;
using System.Collections.Generic;
using UnityEngine;
using RobotGame.Data;
using RobotGame.Enums;

namespace RobotGame.Utils
{
    /// <summary>
    /// Utilidad para auto-detectar sockets estructurales desde los Empties de un prefab.
    /// Busca objetos con nomenclatura Socket_TipoSocket (ej: Socket_Torso, Socket_ArmLeft, Socket_Core).
    /// </summary>
    public static class SocketAutoDetector
    {
        private const string SOCKET_PREFIX = "Socket_";
        
        /// <summary>
        /// Información parseada de un socket.
        /// </summary>
        public class SocketInfo
        {
            public StructuralSocketType socketType;
            public string transformName;
            public Vector3 localPosition;
            public Vector3 localRotation;
            public bool isValid;
        }
        
        /// <summary>
        /// Intenta parsear un nombre de transform para extraer información de socket.
        /// Formato: Socket_TipoSocket (ej: Socket_Torso, Socket_ArmLeft, Socket_Core)
        /// </summary>
        public static bool TryParse(string transformName, out SocketInfo socketInfo)
        {
            socketInfo = new SocketInfo();
            socketInfo.transformName = transformName;
            socketInfo.isValid = false;
            
            if (string.IsNullOrEmpty(transformName)) return false;
            
            // Verificar prefijo Socket_
            if (!transformName.StartsWith(SOCKET_PREFIX, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            
            // Extraer el tipo de socket
            string typePart = transformName.Substring(SOCKET_PREFIX.Length);
            
            // Intentar parsear como enum
            if (Enum.TryParse<StructuralSocketType>(typePart, true, out StructuralSocketType socketType))
            {
                socketInfo.socketType = socketType;
                socketInfo.isValid = true;
                return true;
            }
            
            // Si no se pudo parsear, intentar con variantes comunes
            socketType = TryMatchSocketType(typePart);
            if (socketType != StructuralSocketType.Custom || typePart.Equals("Custom", StringComparison.OrdinalIgnoreCase))
            {
                socketInfo.socketType = socketType;
                socketInfo.isValid = true;
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Intenta hacer match de nombres comunes a tipos de socket.
        /// </summary>
        private static StructuralSocketType TryMatchSocketType(string name)
        {
            string lower = name.ToLowerInvariant();
            
            // Variantes comunes
            switch (lower)
            {
                case "hips":
                case "hip":
                case "pelvis":
                    return StructuralSocketType.Hips;
                    
                case "torso":
                case "chest":
                case "body":
                    return StructuralSocketType.Torso;
                    
                case "head":
                case "cabeza":
                    return StructuralSocketType.Head;
                    
                case "armleft":
                case "arm_left":
                case "leftarm":
                case "left_arm":
                case "brazoi":
                case "brazoizq":
                    return StructuralSocketType.ArmLeft;
                    
                case "armright":
                case "arm_right":
                case "rightarm":
                case "right_arm":
                case "brazod":
                case "brazoder":
                    return StructuralSocketType.ArmRight;
                    
                case "legleft":
                case "leg_left":
                case "leftleg":
                case "left_leg":
                case "piernai":
                case "piernaizq":
                    return StructuralSocketType.LegLeft;
                    
                case "legright":
                case "leg_right":
                case "rightleg":
                case "right_leg":
                case "piernad":
                case "piernader":
                    return StructuralSocketType.LegRight;
                    
                case "tail":
                case "cola":
                    return StructuralSocketType.Tail;
                    
                case "wingleft":
                case "wing_left":
                case "leftwing":
                case "left_wing":
                case "alai":
                case "alaizq":
                    return StructuralSocketType.WingLeft;
                    
                case "wingright":
                case "wing_right":
                case "rightwing":
                case "right_wing":
                case "alad":
                case "alader":
                    return StructuralSocketType.WingRight;
                    
                case "core":
                case "nucleo":
                case "nucleus":
                    return StructuralSocketType.Core;
                    
                case "custom":
                    return StructuralSocketType.Custom;
                    
                default:
                    return StructuralSocketType.Custom;
            }
        }
        
        /// <summary>
        /// Detecta todos los sockets en un prefab.
        /// </summary>
        public static List<StructuralSocketDefinition> DetectSockets(GameObject prefab)
        {
            List<StructuralSocketDefinition> sockets = new List<StructuralSocketDefinition>();
            
            if (prefab == null) return sockets;
            
            // Verificar el objeto raíz
            if (TryParse(prefab.name, out SocketInfo rootInfo))
            {
                StructuralSocketDefinition def = new StructuralSocketDefinition
                {
                    socketType = rootInfo.socketType,
                    transformName = prefab.name,
                    localPosition = Vector3.zero,
                    localRotation = Vector3.zero,
                    isRequired = false
                };
                sockets.Add(def);
            }
            
            // Buscar recursivamente en hijos
            SearchForSockets(prefab.transform, prefab.transform, sockets);
            
            return sockets;
        }
        
        private static void SearchForSockets(Transform current, Transform root, List<StructuralSocketDefinition> sockets)
        {
            foreach (Transform child in current)
            {
                if (TryParse(child.name, out SocketInfo socketInfo))
                {
                    // Calcular posición y rotación relativas al root
                    Vector3 localPos = root.InverseTransformPoint(child.position);
                    Vector3 localRot = (Quaternion.Inverse(root.rotation) * child.rotation).eulerAngles;
                    
                    StructuralSocketDefinition def = new StructuralSocketDefinition
                    {
                        socketType = socketInfo.socketType,
                        transformName = child.name,
                        localPosition = localPos,
                        localRotation = localRot,
                        isRequired = false
                    };
                    sockets.Add(def);
                }
                
                // Buscar recursivamente
                SearchForSockets(child, root, sockets);
            }
        }
        
        /// <summary>
        /// Auto-configura los sockets estructurales de un StructuralPartData basándose en los Empties del prefab.
        /// </summary>
        public static void AutoConfigureStructuralPart(StructuralPartData partData)
        {
            if (partData == null || partData.prefab == null)
            {
                Debug.LogWarning("SocketAutoDetector: PartData o prefab es null.");
                return;
            }
            
            var detectedSockets = DetectSockets(partData.prefab);
            
            if (detectedSockets.Count > 0)
            {
                partData.structuralSockets.Clear();
                partData.structuralSockets.AddRange(detectedSockets);
                
                // Log detallado
                string socketList = "";
                foreach (var socket in detectedSockets)
                {
                    socketList += $"\n  - {socket.socketType} ({socket.transformName})";
                }
                Debug.Log($"SocketAutoDetector: Configurados {detectedSockets.Count} sockets en '{partData.displayName}':{socketList}");
            }
            else
            {
                Debug.Log($"SocketAutoDetector: No se encontraron sockets Socket_* en '{partData.displayName}'");
            }
        }
        
        /// <summary>
        /// Auto-configura AMBOS sockets Y grillas de un StructuralPartData.
        /// </summary>
        public static void AutoConfigureAll(StructuralPartData partData)
        {
            AutoConfigureStructuralPart(partData);
            GridAutoDetector.AutoConfigureStructuralPart(partData);
        }
    }
}
