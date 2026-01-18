using UnityEngine;

namespace RobotGame.Combat
{
    /// <summary>
    /// DEBUG: Mide y muestra la velocidad de un arma durante animaciones.
    /// 
    /// Uso:
    /// 1. Poner este script en la punta del arma (donde estaría el HitboxOrigin)
    /// 2. Reproducir animación de ataque
    /// 3. Ver en consola las velocidades alcanzadas
    /// 
    /// Esto nos ayuda a determinar:
    /// - Qué velocidad mínima activa el hitbox
    /// - Qué velocidad es el "punto dulce" (100% daño)
    /// - Si el sistema basado en velocidad es viable
    /// </summary>
    public class WeaponVelocityDebugger : MonoBehaviour
    {
        [Header("Configuración")]
        [Tooltip("Activar/desactivar el logging")]
        [SerializeField] private bool enableLogging = true;
        
        [Tooltip("Solo loguear cuando la velocidad supere este valor")]
        [SerializeField] private float velocityLogThreshold = 0.1f;
        
        [Tooltip("Intervalo mínimo entre logs (para no spamear consola)")]
        [SerializeField] private float logInterval = 0.05f;
        
        [Header("Umbrales de Prueba")]
        [Tooltip("Velocidad mínima para considerar que el arma está 'activa'")]
        [SerializeField] private float minActiveVelocity = 1f;
        
        [Tooltip("Velocidad para daño máximo (100%)")]
        [SerializeField] private float maxDamageVelocity = 5f;
        
        [Header("Estado Actual (Solo Lectura)")]
        [SerializeField] private Vector3 currentVelocity;
        [SerializeField] private float currentSpeed;
        [SerializeField] private float currentDamagePercent;
        [SerializeField] private float maxSpeedRecorded;
        
        [Header("Visualización")]
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private float gizmoScale = 0.1f;
        
        // Estado interno
        private Vector3 previousPosition;
        private float lastLogTime;
        private bool wasAboveThreshold;
        
        #region Unity Lifecycle
        
        private void Start()
        {
            previousPosition = transform.position;
            maxSpeedRecorded = 0f;
        }
        
        private void LateUpdate()
        {
            // Calcular velocidad
            Vector3 currentPosition = transform.position;
            currentVelocity = (currentPosition - previousPosition) / Time.deltaTime;
            currentSpeed = currentVelocity.magnitude;
            
            // Calcular porcentaje de daño teórico
            if (currentSpeed < minActiveVelocity)
            {
                currentDamagePercent = 0f;
            }
            else if (currentSpeed >= maxDamageVelocity)
            {
                currentDamagePercent = 100f;
            }
            else
            {
                // Interpolación lineal entre min y max
                float t = (currentSpeed - minActiveVelocity) / (maxDamageVelocity - minActiveVelocity);
                currentDamagePercent = t * 100f;
            }
            
            // Registrar máximo
            if (currentSpeed > maxSpeedRecorded)
            {
                maxSpeedRecorded = currentSpeed;
            }
            
            // Logging
            if (enableLogging)
            {
                LogVelocity();
            }
            
            // Guardar posición para siguiente frame
            previousPosition = currentPosition;
        }
        
        #endregion
        
        #region Logging
        
        private void LogVelocity()
        {
            bool isAboveThreshold = currentSpeed > velocityLogThreshold;
            float timeSinceLastLog = Time.time - lastLogTime;
            
            // Log cuando cruza el umbral (empieza o termina movimiento)
            if (isAboveThreshold != wasAboveThreshold)
            {
                if (isAboveThreshold)
                {
                    Debug.Log($"[VelocityDebug] ══════ MOVIMIENTO INICIADO ══════");
                }
                else
                {
                    Debug.Log($"[VelocityDebug] ══════ MOVIMIENTO TERMINADO ══════");
                    Debug.Log($"[VelocityDebug] Velocidad máxima alcanzada: {maxSpeedRecorded:F2} m/s");
                    maxSpeedRecorded = 0f; // Reset para siguiente ataque
                }
                
                wasAboveThreshold = isAboveThreshold;
                lastLogTime = Time.time;
            }
            // Log periódico mientras está en movimiento
            else if (isAboveThreshold && timeSinceLastLog >= logInterval)
            {
                string damageStatus = GetDamageStatus();
                Debug.Log($"[VelocityDebug] Velocidad: {currentSpeed:F2} m/s | Daño: {currentDamagePercent:F0}% | {damageStatus}");
                lastLogTime = Time.time;
            }
        }
        
        private string GetDamageStatus()
        {
            if (currentSpeed < minActiveVelocity)
            {
                return "INACTIVO (muy lento)";
            }
            else if (currentSpeed >= maxDamageVelocity)
            {
                return "¡MÁXIMO DAÑO!";
            }
            else if (currentDamagePercent >= 75f)
            {
                return "Daño alto";
            }
            else if (currentDamagePercent >= 50f)
            {
                return "Daño medio";
            }
            else
            {
                return "Daño bajo";
            }
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Reinicia el tracking de velocidad máxima.
        /// Llamar al inicio de cada ataque.
        /// </summary>
        public void ResetMaxSpeed()
        {
            maxSpeedRecorded = 0f;
        }
        
        /// <summary>
        /// Obtiene el porcentaje de daño actual basado en velocidad.
        /// </summary>
        public float GetCurrentDamagePercent()
        {
            return currentDamagePercent;
        }
        
        /// <summary>
        /// Verifica si el arma está lo suficientemente rápida para hacer daño.
        /// </summary>
        public bool IsActiveSpeed()
        {
            return currentSpeed >= minActiveVelocity;
        }
        
        #endregion
        
        #region Debug Gizmos
        
        private void OnDrawGizmos()
        {
            if (!showGizmos || !Application.isPlaying) return;
            
            // Color basado en velocidad
            if (currentSpeed < minActiveVelocity)
            {
                Gizmos.color = Color.gray; // Inactivo
            }
            else if (currentSpeed >= maxDamageVelocity)
            {
                Gizmos.color = Color.red; // Máximo daño
            }
            else
            {
                // Gradiente de amarillo a rojo
                float t = currentDamagePercent / 100f;
                Gizmos.color = Color.Lerp(Color.yellow, Color.red, t);
            }
            
            // Dibujar esfera en posición actual
            float sphereSize = 0.05f + (currentSpeed * gizmoScale * 0.02f);
            Gizmos.DrawSphere(transform.position, sphereSize);
            
            // Dibujar línea de velocidad
            if (currentSpeed > velocityLogThreshold)
            {
                Gizmos.DrawLine(transform.position, transform.position + currentVelocity * gizmoScale);
            }
        }
        
        #endregion
    }
}
