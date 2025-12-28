using UnityEngine;

namespace RobotGame.Control
{
    /// <summary>
    /// Sistema de movimiento del jugador.
    /// Este script es INDEPENDIENTE del sistema modular y puede ser reemplazado
    /// sin afectar la lógica de ensamblaje, Core, o edición.
    /// 
    /// INTEGRACIÓN:
    /// - Recibe el Transform a mover via SetTarget()
    /// - Se habilita/deshabilita via Enable()/Disable()
    /// - El sistema de Core llama a estos métodos cuando se inserta/extrae
    /// 
    /// PARA REEMPLAZAR:
    /// - Mantén la interfaz pública (SetTarget, Enable, Disable, IsEnabled, Target)
    /// - Implementa tu propio sistema de movimiento en Update()
    /// </summary>
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        
        [Header("Movimiento Básico")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float rotationSpeed = 120f;
        
        [Header("Estado")]
        [SerializeField] private bool isEnabled = true;
        
        /// <summary>
        /// Si el movimiento está habilitado.
        /// </summary>
        public bool IsEnabled => isEnabled && target != null;
        
        /// <summary>
        /// El Transform que se está moviendo.
        /// </summary>
        public Transform Target => target;
        
        /// <summary>
        /// Establece el objetivo a mover.
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }
        
        /// <summary>
        /// Habilita el movimiento.
        /// </summary>
        public void Enable()
        {
            isEnabled = true;
        }
        
        /// <summary>
        /// Deshabilita el movimiento.
        /// </summary>
        public void Disable()
        {
            isEnabled = false;
        }
        
        private void Update()
        {
            if (!IsEnabled) return;
            
            HandleMovement();
        }
        
        /// <summary>
        /// Lógica de movimiento. REEMPLAZA ESTE MÉTODO con tu implementación.
        /// </summary>
        protected virtual void HandleMovement()
        {
            // === MOVIMIENTO BÁSICO ===
            // Este es un placeholder simple. Reemplázalo con tu sistema kinematic.
            
            float vertical = 0f;
            float horizontal = 0f;
            
            if (Input.GetKey(KeyCode.W)) vertical = 1f;
            else if (Input.GetKey(KeyCode.S)) vertical = -1f;
            
            if (Input.GetKey(KeyCode.A)) horizontal = -1f;
            else if (Input.GetKey(KeyCode.D)) horizontal = 1f;
            
            if (vertical != 0f || horizontal != 0f)
            {
                // Movimiento relativo a la orientación del robot
                Vector3 movement = (target.forward * vertical + target.right * horizontal).normalized;
                target.position += movement * moveSpeed * Time.deltaTime;
            }
        }
    }
}
