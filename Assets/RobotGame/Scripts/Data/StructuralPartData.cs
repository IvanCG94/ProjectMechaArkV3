using System;
using System.Collections.Generic;
using UnityEngine;
using RobotGame.Enums;

namespace RobotGame.Data
{
    /// <summary>
    /// Definición de un socket estructural en una pieza.
    /// </summary>
    [Serializable]
    public class StructuralSocketDefinition
    {
        [Tooltip("Tipo de socket")]
        public StructuralSocketType socketType;
        
        [Tooltip("Nombre del transform hijo en el prefab que representa este socket")]
        public string transformName;
        
        [Tooltip("Es un socket requerido (debe tener una pieza) u opcional")]
        public bool isRequired = false;
        
        [Tooltip("Posición offset local del socket")]
        public Vector3 localPosition;
        
        [Tooltip("Rotación offset local del socket")]
        public Vector3 localRotation;
    }
    
    /// <summary>
    /// ScriptableObject para piezas estructurales (huesos del robot).
    /// </summary>
    [CreateAssetMenu(fileName = "NewStructuralPart", menuName = "RobotGame/Parts/Structural Part")]
    public class StructuralPartData : PartDataBase
    {
        [Header("Configuración Estructural")]
        [Tooltip("Tipo de pieza estructural que representa")]
        public StructuralSocketType partType;
        
        [Tooltip("Si es true, esta pieza puede ser la raíz de un robot (ej: Hips)")]
        public bool canBeRoot = false;
        
        [Header("Sockets Estructurales")]
        [Tooltip("Sockets donde se pueden conectar otras piezas estructurales")]
        public List<StructuralSocketDefinition> structuralSockets = new List<StructuralSocketDefinition>();
        
        [Header("Grillas para Armadura")]
        [Tooltip("Grillas Head donde se pueden insertar piezas de armadura/decorativas")]
        public List<HeadGridDefinition> armorGrids = new List<HeadGridDefinition>();
        
        [Header("Animación")]
        [Tooltip("Controller de animación para esta pieza")]
        public RuntimeAnimatorController animatorController;
        
        [Tooltip("Avatar para el animator (si usa humanoid/generic rig)")]
        public Avatar animatorAvatar;
        
        /// <summary>
        /// Busca un socket por tipo.
        /// </summary>
        public StructuralSocketDefinition GetSocket(StructuralSocketType type)
        {
            return structuralSockets.Find(s => s.socketType == type);
        }
        
        /// <summary>
        /// Verifica si esta pieza tiene un socket del tipo especificado.
        /// </summary>
        public bool HasSocket(StructuralSocketType type)
        {
            return structuralSockets.Exists(s => s.socketType == type);
        }
        
        /// <summary>
        /// Obtiene todos los sockets requeridos.
        /// </summary>
        public List<StructuralSocketDefinition> GetRequiredSockets()
        {
            return structuralSockets.FindAll(s => s.isRequired);
        }
        
        /// <summary>
        /// Obtiene todos los sockets opcionales.
        /// </summary>
        public List<StructuralSocketDefinition> GetOptionalSockets()
        {
            return structuralSockets.FindAll(s => !s.isRequired);
        }
        
        private void OnValidate()
        {
            // Asegurar que la categoría sea Structural
            category = PartCategory.Structural;
        }
    }
}
