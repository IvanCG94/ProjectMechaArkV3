using UnityEngine;
using RobotGame.Data;
using RobotGame.Enums;

namespace RobotGame.Components
{
    /// <summary>
    /// Componente runtime que representa un socket estructural en el robot.
    /// Se adjunta a un Transform hijo del prefab de una pieza estructural.
    /// </summary>
    public class StructuralSocket : MonoBehaviour
    {
        [Header("Configuración")]
        [SerializeField] private StructuralSocketType socketType;
        [SerializeField] private bool isRequired = false;
        
        [Header("Estado")]
        [SerializeField] private bool isOccupied = false;
        [SerializeField] private StructuralPart attachedPart;
        
        /// <summary>
        /// Tipo de socket (Torso, Head, ArmLeft, etc.)
        /// </summary>
        public StructuralSocketType SocketType => socketType;
        
        /// <summary>
        /// Si es true, el robot no está completo sin una pieza en este socket.
        /// </summary>
        public bool IsRequired => isRequired;
        
        /// <summary>
        /// Si hay una pieza conectada a este socket.
        /// </summary>
        public bool IsOccupied => isOccupied;
        
        /// <summary>
        /// La pieza estructural conectada a este socket (null si está vacío).
        /// </summary>
        public StructuralPart AttachedPart => attachedPart;
        
        /// <summary>
        /// Inicializa el socket desde una definición de datos.
        /// </summary>
        public void Initialize(StructuralSocketDefinition definition)
        {
            socketType = definition.socketType;
            isRequired = definition.isRequired;
            isOccupied = false;
            attachedPart = null;
            
            // Crear collider para raycast detection
            EnsureCollider();
        }
        
        /// <summary>
        /// Asegura que el socket tenga un Collider configurado correctamente.
        /// </summary>
        public void EnsureCollider()
        {
            Collider collider = GetComponent<Collider>();
            if (collider == null)
            {
                SphereCollider sphereCollider = gameObject.AddComponent<SphereCollider>();
                sphereCollider.radius = 0.1f;
                sphereCollider.isTrigger = true;
            }
        }
        
        /// <summary>
        /// Intenta conectar una pieza estructural a este socket.
        /// </summary>
        /// <param name="part">La pieza a conectar</param>
        /// <returns>True si se conectó exitosamente</returns>
        public bool TryAttach(StructuralPart part)
        {
            if (isOccupied)
            {
                Debug.LogWarning($"Socket {socketType} ya está ocupado.");
                return false;
            }
            
            if (part == null)
            {
                Debug.LogWarning("Intentando conectar una pieza null.");
                return false;
            }
            
            // Verificar que el tipo de pieza coincida con el socket
            if (part.PartData.partType != socketType)
            {
                Debug.LogWarning($"Tipo de pieza {part.PartData.partType} no coincide con socket {socketType}.");
                return false;
            }
            
            // Conectar la pieza
            attachedPart = part;
            isOccupied = true;
            
            // Posicionar la pieza en el socket
            part.transform.SetParent(transform);
            part.transform.localPosition = Vector3.zero;
            part.transform.localRotation = Quaternion.identity;
            
            part.OnAttachedToSocket(this);
            
            return true;
        }
        
        /// <summary>
        /// Desconecta la pieza actual del socket.
        /// </summary>
        /// <returns>La pieza desconectada, o null si estaba vacío</returns>
        public StructuralPart Detach()
        {
            if (!isOccupied || attachedPart == null)
            {
                return null;
            }
            
            StructuralPart part = attachedPart;
            part.OnDetachedFromSocket(this);
            part.transform.SetParent(null);
            
            attachedPart = null;
            isOccupied = false;
            
            return part;
        }
        
        private void OnDrawGizmos()
        {
            // Visualizar el socket en el editor
            Gizmos.color = isOccupied ? Color.green : (isRequired ? Color.red : Color.yellow);
            Gizmos.DrawWireSphere(transform.position, 0.05f);
        }
        
        private void OnDrawGizmosSelected()
        {
            // Mostrar más detalles cuando está seleccionado
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.08f);
            
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.1f, socketType.ToString());
            #endif
        }
    }
}
