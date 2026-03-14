using System.Collections.Generic;
using UnityEngine;
using RobotGame.Data;
using RobotGame.Utils;
using RobotGame.Enums;

namespace RobotGame.Components
{
    /// <summary>
    /// Componente de grilla receptora basado en posiciones físicas de studs.
    /// 
    /// USO:
    /// 1. En Blender, crea Empties hijos con nombres:
    ///    Head_T1-2_Stud1, Head_T1-2_Stud2, etc.
    /// 2. Posiciona los Empties con el espaciado correcto para el tier
    /// 3. Agrega este componente al objeto padre
    /// 4. Click "Detectar Studs" en el Inspector
    /// 
    /// ESPACIADO POR TIER:
    /// - T1-1: 0.10    T1-2: 0.125   T1-3: 0.15
    /// - T2-1: 0.25    T2-2: 0.30    T2-3: 0.35
    /// - etc.
    /// </summary>
    public class StudGridHead : MonoBehaviour
    {
        [Header("Studs Detectados")]
        [SerializeField] private List<StudPoint> headStuds = new List<StudPoint>();
        
        [Header("Estado de Ocupación")]
        [SerializeField] private List<int> occupiedIndices = new List<int>();
        
        [Header("Configuración de Validación")]
        [Tooltip("Tolerancia para considerar que dos studs coinciden en posición (en metros)")]
        [SerializeField] private float positionTolerance = 0.05f;
        
        [Header("Visualización")]
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private float gizmoRadius = 0.03f;
        [SerializeField] private Color freeStudColor = Color.green;
        [SerializeField] private Color occupiedStudColor = Color.red;
        
        #region Properties
        
        public IReadOnlyList<StudPoint> Studs => headStuds;
        public int StudCount => headStuds.Count;
        public int FreeStudCount => headStuds.Count - occupiedIndices.Count;
        public int AvailableStudCount => FreeStudCount;
        public float Tolerance => positionTolerance;
        
        /// <summary>
        /// Lista de piezas de armadura colocadas en esta grilla.
        /// </summary>
        public IReadOnlyList<ArmorPart> PlacedParts => placedParts;
        private List<ArmorPart> placedParts = new List<ArmorPart>();
        
        #region Propiedades de Compatibilidad
        
        /// <summary>
        /// Tier de la grilla (compatibilidad legacy).
        /// </summary>
        public int Tier
        {
            get
            {
                if (headStuds != null && headStuds.Count > 0)
                    return headStuds[0].tierInfo.MainTier;
                return 1;
            }
        }
        
        /// <summary>
        /// TierInfo de la grilla.
        /// </summary>
        public TierInfo TierInfo
        {
            get
            {
                if (headStuds != null && headStuds.Count > 0)
                    return headStuds[0].tierInfo;
                return TierInfo.Default;
            }
        }
        
        /// <summary>
        /// Tamaño de celda (espaciado por tier).
        /// </summary>
        public float CellSize
        {
            get
            {
                var tier = TierInfo;
                switch (tier.MainTier)
                {
                    case 1:
                        return tier.SubTier switch { 1 => 0.10f, 2 => 0.125f, _ => 0.15f };
                    case 2:
                        return tier.SubTier switch { 1 => 0.25f, 2 => 0.30f, _ => 0.35f };
                    case 3:
                        return tier.SubTier switch { 1 => 0.40f, 2 => 0.45f, _ => 0.50f };
                    default:
                        return 0.125f;
                }
            }
        }
        
        /// <summary>
        /// GridInfo de compatibilidad.
        /// </summary>
        public GridInfoCompat GridInfo => new GridInfoCompat(TierInfo);
        
        /// <summary>
        /// Índice del stud actualmente seleccionado (para snap).
        /// </summary>
        public int CurrentHoveredStudIndex { get; set; } = -1;
        
        /// <summary>
        /// Encuentra el stud más cercano a un punto en espacio mundial.
        /// Retorna el índice del stud o -1 si no hay studs.
        /// </summary>
        public int FindClosestStud(Vector3 worldPoint)
        {
            if (headStuds == null || headStuds.Count == 0)
            {
                // Debug.LogWarning($"FindClosestStud: No hay studs en {gameObject.name}");
                return -1;
            }
            
            int closestIndex = -1;
            float closestDistance = float.MaxValue;
            
            for (int i = 0; i < headStuds.Count; i++)
            {
                // Usar GetStudWorldPosition que maneja sourceTransform para huesos animados
                Vector3 studWorldPos = GetStudWorldPosition(i);
                float distance = Vector3.Distance(worldPoint, studWorldPos);
                
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestIndex = i;
                }
            }
            
            if (closestIndex >= 0)
            {
                // Debug.Log($"FindClosestStud: Stud más cercano [{closestIndex}] '{headStuds[closestIndex].name}' a dist:{closestDistance:F3}");
            }
            
            return closestIndex;
        }
        
        /// <summary>
        /// Obtiene la posición mundial de un stud por índice.
        /// Usa el transform original del stud si está disponible (para soportar huesos animados).
        /// </summary>
        public Vector3 GetStudWorldPosition(int studIndex)
        {
            if (studIndex < 0 || studIndex >= headStuds.Count)
                return transform.position;
            
            var stud = headStuds[studIndex];
            
            // Si tenemos referencia al transform original, usarlo directamente
            // Esto es importante para studs que son hijos de huesos animados
            if (stud.sourceTransform != null)
            {
                return stud.sourceTransform.position;
            }
            
            // Intentar reconectar el transform si está perdido
            if (!string.IsNullOrEmpty(stud.transformName))
            {
                Transform found = FindChildRecursive(transform, stud.transformName);
                if (found != null)
                {
                    stud.sourceTransform = found;
                    return found.position;
                }
            }
            
            // Fallback: usar posición local guardada
            return transform.TransformPoint(stud.localPosition);
        }
        
        /// <summary>
        /// Obtiene la posición mundial del stud actualmente hovereado.
        /// </summary>
        public Vector3 GetCurrentHoveredStudPosition()
        {
            return GetStudWorldPosition(CurrentHoveredStudIndex);
        }
        
        /// <summary>
        /// Verifica si el stud actual está libre (no ocupado).
        /// </summary>
        public bool IsCurrentStudFree()
        {
            return CurrentHoveredStudIndex >= 0 && !IsStudOccupied(CurrentHoveredStudIndex);
        }
        
        #endregion
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Detecta todos los studs Head en los hijos de este objeto.
        /// </summary>
        public void DetectStuds()
        {
            headStuds = StudDetector.DetectHeadStuds(transform);
            occupiedIndices.Clear();
            
            // Debug.Log($"StudGridHead: Detectados {headStuds.Count} studs Head");
            
            foreach (var stud in headStuds)
            {
                // Debug.Log($"  - {stud.name} (T{stud.tierInfo}) @ {stud.localPosition}");
            }
        }
        
        /// <summary>
        /// Asigna directamente una lista de studs ya detectados.
        /// Útil cuando los studs fueron detectados desde otro transform (ej: StructuralPart).
        /// </summary>
        public void SetStuds(List<StudPoint> studs)
        {
            headStuds = studs ?? new List<StudPoint>();
            occupiedIndices.Clear();
            
            // Debug.Log($"StudGridHead.SetStuds: Asignados {headStuds.Count} studs Head en {gameObject.name}");
            
            foreach (var stud in headStuds)
            {
                // Debug.Log($"  - {stud.name} (T{stud.tierInfo}) @ local:{stud.localPosition}, group:'{stud.groupId}'");
            }
        }
        
        /// <summary>
        /// Reconecta los sourceTransforms de los studs buscándolos por nombre.
        /// Útil si los transforms se perdieron por serialización.
        /// </summary>
        public void ReconnectStudTransforms()
        {
            if (headStuds == null || headStuds.Count == 0) return;
            
            int reconnected = 0;
            foreach (var stud in headStuds)
            {
                if (stud.sourceTransform == null && !string.IsNullOrEmpty(stud.transformName))
                {
                    // Buscar el transform por nombre en toda la jerarquía
                    Transform found = FindChildRecursive(transform, stud.transformName);
                    if (found != null)
                    {
                        stud.sourceTransform = found;
                        reconnected++;
                    }
                }
            }
            
            if (reconnected > 0)
            {
                // Debug.Log($"StudGridHead.ReconnectStudTransforms: Reconectados {reconnected} transforms en {gameObject.name}");
            }
        }
        
        private Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                    return child;
                
                Transform found = FindChildRecursive(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }
        
        /// <summary>
        /// Verifica si un stud específico está ocupado.
        /// </summary>
        public bool IsStudOccupied(int index)
        {
            return occupiedIndices.Contains(index);
        }
        
        /// <summary>
        /// Verifica si un stud específico está ocupado.
        /// </summary>
        public bool IsStudOccupied(StudPoint stud)
        {
            int index = headStuds.IndexOf(stud);
            return index >= 0 && occupiedIndices.Contains(index);
        }
        
        /// <summary>
        /// Busca el stud más cercano a una posición mundial dentro de una tolerancia.
        /// Retorna -1 si no encuentra ninguno dentro de la tolerancia.
        /// </summary>
        public int FindClosestStudIndex(Vector3 worldPosition, float tolerance = 0.05f)
        {
            if (headStuds == null || headStuds.Count == 0) return -1;
            
            int closestIndex = -1;
            float closestDist = float.MaxValue;
            
            for (int i = 0; i < headStuds.Count; i++)
            {
                Vector3 studWorld = GetStudWorldPosition(i);
                float dist = Vector3.Distance(worldPosition, studWorld);
                if (dist < closestDist && dist <= tolerance)
                {
                    closestDist = dist;
                    closestIndex = i;
                }
            }
            
            return closestIndex;
        }
        
        /// <summary>
        /// Obtiene el nombre de la pieza que ocupa un stud específico.
        /// </summary>
        public string GetOccupantName(int studIndex)
        {
            if (!occupiedIndices.Contains(studIndex)) return null;
            
            // Buscar en las piezas colocadas cuál ocupa este stud
            foreach (var part in placedParts)
            {
                if (part == null) continue;
                // No podemos saber exactamente qué stud ocupa cada parte sin más info
                // Retornamos el nombre de alguna pieza colocada como aproximación
                return part.gameObject.name;
            }
            
            return "Desconocido";
        }
        
        /// <summary>
        /// Ya no crea colliders automáticos.
        /// Los colliders para detección de raycast deben crearse manualmente como Box_ en Blender.
        /// </summary>
        public void EnsureCollider()
        {
            // NO crear collider automático
            // Los Box_ manuales se usan para detección de raycast
        }
        
        /// <summary>
        /// Obtiene el índice del stud Head que coincide con la posición dada.
        /// Retorna -1 si no hay coincidencia.
        /// </summary>
        public int FindStudAtPosition(Vector3 localPosition, TierInfo tierInfo)
        {
            for (int i = 0; i < headStuds.Count; i++)
            {
                var stud = headStuds[i];
                
                // Verificar tier exacto
                if (stud.tierInfo != tierInfo)
                    continue;
                
                // Verificar posición
                if (stud.MatchesPosition(localPosition, positionTolerance))
                {
                    return i;
                }
            }
            
            return -1;
        }
        
        /// <summary>
        /// Verifica si todos los studs Tail pueden colocarse en este Head.
        /// </summary>
        /// <param name="tailStuds">Studs del Tail en posiciones locales</param>
        /// <param name="offset">Offset a aplicar a las posiciones del Tail</param>
        public bool CanPlace(List<StudPoint> tailStuds, Vector3 offset)
        {
            if (tailStuds == null || tailStuds.Count == 0)
                return false;
            
            foreach (var tailStud in tailStuds)
            {
                // Calcular posición con offset
                Vector3 targetPos = tailStud.localPosition + offset;
                
                // Buscar stud Head en esa posición
                int headIndex = FindStudAtPosition(targetPos, tailStud.tierInfo);
                
                if (headIndex < 0)
                {
                    // No hay stud Head en esa posición
                    return false;
                }
                
                if (IsStudOccupied(headIndex))
                {
                    // El stud está ocupado
                    return false;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Coloca una pieza (marca studs como ocupados).
        /// </summary>
        public bool Place(List<StudPoint> tailStuds, Vector3 offset)
        {
            if (!CanPlace(tailStuds, offset))
                return false;
            
            // Marcar studs como ocupados
            foreach (var tailStud in tailStuds)
            {
                Vector3 targetPos = tailStud.localPosition + offset;
                int headIndex = FindStudAtPosition(targetPos, tailStud.tierInfo);
                
                if (headIndex >= 0 && !occupiedIndices.Contains(headIndex))
                {
                    occupiedIndices.Add(headIndex);
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Libera studs ocupados por una pieza.
        /// </summary>
        public void Free(List<StudPoint> tailStuds, Vector3 offset)
        {
            foreach (var tailStud in tailStuds)
            {
                Vector3 targetPos = tailStud.localPosition + offset;
                int headIndex = FindStudAtPosition(targetPos, tailStud.tierInfo);
                
                if (headIndex >= 0)
                {
                    occupiedIndices.Remove(headIndex);
                }
            }
        }
        
        /// <summary>
        /// Limpia todos los studs ocupados.
        /// </summary>
        public void ClearOccupied()
        {
            occupiedIndices.Clear();
        }
        
        /// <summary>
        /// Limpia todos los studs ocupados y la lista de piezas colocadas.
        /// </summary>
        public void ClearAllOccupation()
        {
            occupiedIndices.Clear();
            placedParts.Clear();
        }
        
        /// <summary>
        /// Intenta colocar una pieza de armadura en esta grilla.
        /// </summary>
        /// <param name="armorPart">Pieza de armadura ya instanciada</param>
        /// <returns>True si se colocó exitosamente</returns>
        public bool TryPlace(ArmorPart armorPart)
        {
            if (armorPart == null) return false;
            
            var tailGrid = armorPart.TailGrid;
            if (tailGrid == null || tailGrid.StudCount == 0)
            {
                // Debug.LogWarning($"StudGridHead.TryPlace: La pieza no tiene StudGridTail o studs");
                return false;
            }
            
            // Calcular offset basado en posición relativa
            Vector3 offset = transform.InverseTransformPoint(armorPart.transform.position);
            
            // Intentar colocar
            if (Place(tailGrid.Studs as List<StudPoint> ?? new List<StudPoint>(tailGrid.Studs), offset))
            {
                // Registrar la pieza
                if (!placedParts.Contains(armorPart))
                {
                    placedParts.Add(armorPart);
                }
                armorPart.OnPlaced(this);
                return true;
            }
            
            return false;
        }
        
        #region Métodos de Compatibilidad
        
        /// <summary>
        /// Intenta colocar una pieza en el stud actualmente seleccionado.
        /// Marca el stud como ocupado y registra la pieza.
        /// </summary>
        public bool TryPlaceAtCurrentStud(ArmorPart armorPart)
        {
            if (armorPart == null) return false;
            if (CurrentHoveredStudIndex < 0 || CurrentHoveredStudIndex >= headStuds.Count)
            {
                // Debug.LogWarning("StudGridHead.TryPlaceAtCurrentStud: No hay stud seleccionado");
                return false;
            }
            
            if (IsStudOccupied(CurrentHoveredStudIndex))
            {
                // Debug.LogWarning("StudGridHead.TryPlaceAtCurrentStud: El stud ya está ocupado");
                return false;
            }
            
            // Marcar el stud como ocupado
            occupiedIndices.Add(CurrentHoveredStudIndex);
            
            // Registrar la pieza
            if (!placedParts.Contains(armorPart))
            {
                placedParts.Add(armorPart);
            }
            
            // Notificar a la pieza
            armorPart.OnPlaced(this);
            
            // Debug.Log($"StudGridHead: Pieza colocada en stud {CurrentHoveredStudIndex} ({headStuds[CurrentHoveredStudIndex].name})");
            
            return true;
        }
        
        /// <summary>
        /// Obtiene el Transform del hueso/objeto donde está el stud Head.
        /// Esto es importante para hacer parent de las piezas de armadura al hueso correcto.
        /// </summary>
        public Transform GetStudParentTransform(int studIndex)
        {
            if (studIndex < 0 || studIndex >= headStuds.Count)
                return transform;
            
            // El sourceTransform es el Empty del stud, queremos su padre (el hueso)
            var stud = headStuds[studIndex];
            if (stud.sourceTransform != null && stud.sourceTransform.parent != null)
            {
                return stud.sourceTransform.parent;
            }
            
            return stud.sourceTransform ?? transform;
        }
        
        /// <summary>
        /// Obtiene el Transform del hueso para el stud actualmente hovereado.
        /// </summary>
        public Transform GetCurrentStudParentTransform()
        {
            return GetStudParentTransform(CurrentHoveredStudIndex);
        }
        
        // Offset de rotación para alinear correctamente las piezas con los studs
        private static readonly Quaternion StudRotationOffset = Quaternion.Euler(0f, 90f, 0f);
        
        /// <summary>
        /// Obtiene la rotación mundial del stud actual con offset aplicado.
        /// Esta es la orientación base que tendrá la armadura al hacer snap.
        /// </summary>
        public Quaternion GetCurrentStudRotation()
        {
            if (CurrentHoveredStudIndex < 0 || CurrentHoveredStudIndex >= headStuds.Count)
                return transform.rotation * StudRotationOffset;
            
            StudPoint stud = headStuds[CurrentHoveredStudIndex];
            
            // Usar la rotación mundial del Empty del stud + offset
            if (stud.sourceTransform != null)
            {
                return stud.sourceTransform.rotation * StudRotationOffset;
            }
            
            // Fallback: usar rotación del grid * rotación local del stud + offset
            return transform.rotation * stud.localRotation * StudRotationOffset;
        }
        
        /// <summary>
        /// Obtiene la rotación mundial de un stud específico por índice con offset aplicado.
        /// </summary>
        public Quaternion GetStudRotation(int index)
        {
            if (index < 0 || index >= headStuds.Count)
                return transform.rotation * StudRotationOffset;
            
            StudPoint stud = headStuds[index];
            
            if (stud.sourceTransform != null)
            {
                return stud.sourceTransform.rotation * StudRotationOffset;
            }
            
            return transform.rotation * stud.localRotation * StudRotationOffset;
        }
        
        /// <summary>
        /// Verifica si TODOS los Tails de una armadura pueden colocarse.
        /// Cada Tail debe coincidir con un Head libre en la MISMA POSICIÓN MUNDIAL.
        /// </summary>
        /// <param name="tailGrid">El StudGridTail de la armadura</param>
        /// <param name="anchorHeadIndex">El índice del Head donde se "ancla" el primer Tail</param>
        /// <returns>True si todos los Tails tienen un Head libre correspondiente</returns>
        public bool CanPlaceAllTails(StudGridTail tailGrid, int anchorHeadIndex)
        {
            return CanPlaceAllTails(tailGrid, anchorHeadIndex, Quaternion.identity);
        }
        
        /// <summary>
        /// Verifica si TODOS los Tails de una armadura pueden colocarse con una rotación específica.
        /// Cada Tail debe coincidir con un Head libre en la MISMA POSICIÓN MUNDIAL.
        /// </summary>
        /// <param name="tailGrid">El StudGridTail de la armadura</param>
        /// <param name="anchorHeadIndex">El índice del Head donde se "ancla" el primer Tail</param>
        /// <param name="rotation">Rotación a aplicar a los offsets de los Tails</param>
        /// <returns>True si todos los Tails tienen un Head libre correspondiente</returns>
        public bool CanPlaceAllTails(StudGridTail tailGrid, int anchorHeadIndex, Quaternion rotation)
        {
            if (tailGrid == null || tailGrid.StudCount == 0)
            {
                // Debug.Log("CanPlaceAllTails: tailGrid es null o vacío");
                return false;
            }
            
            if (anchorHeadIndex < 0 || anchorHeadIndex >= headStuds.Count)
            {
                // Debug.Log($"CanPlaceAllTails: anchorHeadIndex {anchorHeadIndex} inválido (headStuds.Count={headStuds.Count})");
                return false;
            }
            
            var tails = tailGrid.Studs;
            if (tails.Count == 0)
                return false;
            
            // Posición mundial del Head anchor
            Vector3 anchorHeadWorldPos = GetStudWorldPosition(anchorHeadIndex);
            var anchorHead = headStuds[anchorHeadIndex];
            
            // GroupId requerido - todos los Heads deben pertenecer al mismo grupo (mismo hueso)
            string requiredGroupId = anchorHead.groupId;
            
            // Posición local del primer Tail (en el prefab de armadura)
            Vector3 firstTailLocalPos = tails[0].localPosition;
            
            // Debug.Log($"CanPlaceAllTails: Anchor Head[{anchorHeadIndex}] en {anchorHeadWorldPos}, Group:'{requiredGroupId}', Rotation:{rotation.eulerAngles}");
            // Debug.Log($"CanPlaceAllTails: Verificando {tails.Count} Tails...");
            
            // Para cada Tail, calcular dónde debería estar en espacio mundial
            // y buscar un Head libre en esa posición
            List<int> matchedHeadIndices = new List<int>();
            
            for (int i = 0; i < tails.Count; i++)
            {
                var tail = tails[i];
                
                // Verificar compatibilidad de tier
                if (!tail.tierInfo.IsCompatibleWith(anchorHead.tierInfo))
                {
                    // Debug.Log($"CanPlaceAllTails: Tail[{i}] tier {tail.tierInfo} incompatible con Head tier {anchorHead.tierInfo}");
                    return false;
                }
                
                // Calcular offset del Tail respecto al primer Tail (en espacio local del prefab)
                Vector3 tailOffsetLocal = tail.localPosition - firstTailLocalPos;
                
                // APLICAR LA ROTACIÓN al offset
                Vector3 rotatedOffset = rotation * tailOffsetLocal;
                
                // La posición mundial donde debería estar este Tail
                // = posición del Head anchor + offset rotado
                Vector3 expectedWorldPos = anchorHeadWorldPos + rotatedOffset;
                
                // Debug.Log($"  Tail[{i}] '{tail.name}': localOffset={tailOffsetLocal}, rotatedOffset={rotatedOffset}, expectedWorld={expectedWorldPos}");
                
                // Buscar un Head libre en esa posición mundial QUE PERTENEZCA AL MISMO GRUPO
                int headIndex = FindHeadAtWorldPosition(expectedWorldPos, tail.tierInfo, requiredGroupId);
                
                if (headIndex < 0)
                {
                    // Debug.Log($"CanPlaceAllTails: FALLO - No hay Head (grupo '{requiredGroupId}') para Tail[{i}] '{tail.name}' en posición mundial {expectedWorldPos}");
                    return false;
                }
                
                if (IsStudOccupied(headIndex))
                {
                    // Debug.Log($"CanPlaceAllTails: FALLO - Head[{headIndex}] para Tail[{i}] está ocupado");
                    return false;
                }
                
                // Verificar que no estamos usando el mismo Head dos veces
                if (matchedHeadIndices.Contains(headIndex))
                {
                    // Debug.Log($"CanPlaceAllTails: FALLO - Head[{headIndex}] ya está asignado a otro Tail");
                    return false;
                }
                
                // Debug.Log($"  Tail[{i}] → Head[{headIndex}] (grupo '{headStuds[headIndex].groupId}') OK");
                matchedHeadIndices.Add(headIndex);
            }
            
            // Debug.Log($"CanPlaceAllTails: ÉXITO - {tails.Count} Tails coinciden con Heads del grupo '{requiredGroupId}': [{string.Join(", ", matchedHeadIndices)}]");
            return true;
        }
        
        /// <summary>
        /// Busca un Head libre en una posición mundial específica.
        /// </summary>
        /// <param name="worldPosition">Posición mundial a buscar</param>
        /// <param name="tierInfo">Tier requerido</param>
        /// <returns>Índice del Head o -1 si no se encuentra</returns>
        public int FindHeadAtWorldPosition(Vector3 worldPosition, TierInfo tierInfo)
        {
            return FindHeadAtWorldPosition(worldPosition, tierInfo, null);
        }
        
        /// <summary>
        /// Busca un Head libre en una posición mundial específica, filtrando por groupId.
        /// </summary>
        /// <param name="worldPosition">Posición mundial a buscar</param>
        /// <param name="tierInfo">Tier requerido</param>
        /// <param name="requiredGroupId">GroupId requerido (null para ignorar)</param>
        /// <returns>Índice del Head o -1 si no se encuentra</returns>
        public int FindHeadAtWorldPosition(Vector3 worldPosition, TierInfo tierInfo, string requiredGroupId)
        {
            float tolerance = positionTolerance;
            float closestDistance = float.MaxValue;
            int closestIndex = -1;
            string closestGroup = "";
            
            for (int i = 0; i < headStuds.Count; i++)
            {
                var head = headStuds[i];
                
                // Verificar tier compatible
                if (!head.tierInfo.IsCompatibleWith(tierInfo))
                    continue;
                
                // Verificar groupId si se especificó
                if (!string.IsNullOrEmpty(requiredGroupId) && head.groupId != requiredGroupId)
                    continue;
                
                // Obtener posición mundial del Head
                Vector3 headWorldPos = GetStudWorldPosition(i);
                
                // Verificar distancia
                float distance = Vector3.Distance(worldPosition, headWorldPos);
                
                // Trackear el más cercano para debug
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestIndex = i;
                    closestGroup = head.groupId;
                }
                
                if (distance <= tolerance)
                {
                    return i;
                }
            }
            
            // Log del más cercano que no cumplió
            if (closestIndex >= 0)
            {
                // Debug.Log($"FindHeadAtWorldPosition: No encontró Head (grupo:'{requiredGroupId}') en {worldPosition}. Más cercano: [{closestIndex}] grupo:'{closestGroup}' a dist:{closestDistance:F4}m (tolerancia:{tolerance}m)");
            }
            else if (!string.IsNullOrEmpty(requiredGroupId))
            {
                // Debug.Log($"FindHeadAtWorldPosition: No hay Heads del grupo '{requiredGroupId}' cerca de {worldPosition}");
            }
            
            return -1;
        }
        
        /// <summary>
        /// Coloca una armadura ocupando TODOS los Heads que coinciden con sus Tails.
        /// </summary>
        public bool PlaceArmorWithAllTails(ArmorPart armorPart, int anchorHeadIndex)
        {
            return PlaceArmorWithAllTails(armorPart, anchorHeadIndex, Quaternion.identity);
        }
        
        /// <summary>
        /// Coloca una armadura ocupando TODOS los Heads que coinciden con sus Tails, con rotación.
        /// </summary>
        public bool PlaceArmorWithAllTails(ArmorPart armorPart, int anchorHeadIndex, Quaternion rotation)
        {
            if (armorPart == null) return false;
            
            var tailGrid = armorPart.TailGrid;
            if (tailGrid == null || tailGrid.StudCount == 0)
            {
                // Debug.LogWarning("PlaceArmorWithAllTails: Armadura sin TailGrid");
                return false;
            }
            
            if (!CanPlaceAllTails(tailGrid, anchorHeadIndex, rotation))
            {
                // Debug.LogWarning("PlaceArmorWithAllTails: No se pueden colocar todos los Tails");
                return false;
            }
            
            var tails = tailGrid.Studs;
            Vector3 anchorHeadWorldPos = GetStudWorldPosition(anchorHeadIndex);
            Vector3 firstTailLocalPos = tails[0].localPosition;
            
            // GroupId requerido - todos los Heads deben pertenecer al mismo grupo
            string requiredGroupId = headStuds[anchorHeadIndex].groupId;
            
            // Marcar todos los Heads correspondientes como ocupados
            List<int> occupiedHeadIndices = new List<int>();
            for (int i = 0; i < tails.Count; i++)
            {
                var tail = tails[i];
                Vector3 tailOffsetLocal = tail.localPosition - firstTailLocalPos;
                Vector3 rotatedOffset = rotation * tailOffsetLocal;
                Vector3 expectedWorldPos = anchorHeadWorldPos + rotatedOffset;
                
                // Usar el mismo groupId para buscar
                int headIndex = FindHeadAtWorldPosition(expectedWorldPos, tail.tierInfo, requiredGroupId);
                
                if (headIndex >= 0 && !occupiedIndices.Contains(headIndex))
                {
                    occupiedIndices.Add(headIndex);
                    occupiedHeadIndices.Add(headIndex);
                }
            }
            
            // Registrar la pieza
            if (!placedParts.Contains(armorPart))
            {
                placedParts.Add(armorPart);
            }
            
            armorPart.OnPlaced(this);
            
            // Debug.Log($"PlaceArmorWithAllTails: Armadura colocada en grupo '{requiredGroupId}' ocupando {occupiedHeadIndices.Count} studs: [{string.Join(", ", occupiedHeadIndices)}]");
            return true;
        }
        
        /// <summary>
        /// Calcula la posición mundial donde debe estar el origen de la armadura
        /// para que su primer Tail quede en el Head seleccionado.
        /// </summary>
        public Vector3 CalculateArmorPosition(StudGridTail tailGrid, int anchorHeadIndex)
        {
            return CalculateArmorPosition(tailGrid, anchorHeadIndex, Quaternion.identity);
        }
        
        /// <summary>
        /// Calcula la posición mundial donde debe estar el origen de la armadura
        /// para que su primer Tail quede en el Head seleccionado, con rotación.
        /// </summary>
        public Vector3 CalculateArmorPosition(StudGridTail tailGrid, int anchorHeadIndex, Quaternion rotation)
        {
            if (tailGrid == null || tailGrid.StudCount == 0)
                return GetStudWorldPosition(anchorHeadIndex);
            
            if (anchorHeadIndex < 0 || anchorHeadIndex >= headStuds.Count)
                return transform.position;
            
            // Posición mundial del Head anchor
            Vector3 headWorldPos = GetStudWorldPosition(anchorHeadIndex);
            
            // Posición local del primer Tail en la armadura
            Vector3 firstTailLocalPos = tailGrid.Studs[0].localPosition;
            
            // Aplicar rotación al offset del primer Tail
            Vector3 rotatedFirstTailOffset = rotation * firstTailLocalPos;
            
            // La armadura debe posicionarse de modo que su primer Tail (rotado) quede en el Head
            return headWorldPos - rotatedFirstTailOffset;
        }
        
        /// <summary>
        /// Intenta colocar una pieza en la posición especificada (compatibilidad legacy).
        /// </summary>
        public bool TryPlace(ArmorPart armorPart, int posX, int posY)
        {
            return TryPlace(armorPart);
        }
        
        /// <summary>
        /// Intenta colocar una pieza con rotación (compatibilidad legacy).
        /// </summary>
        public bool TryPlace(ArmorPart armorPart, int posX, int posY, int rotation)
        {
            return TryPlace(armorPart);
        }
        
        /// <summary>
        /// Intenta colocar una pieza con rotación enum (compatibilidad legacy).
        /// </summary>
        public bool TryPlace(ArmorPart armorPart, int posX, int posY, RobotGame.Utils.GridRotation.Rotation rotation)
        {
            return TryPlace(armorPart);
        }
        
        /// <summary>
        /// Verifica si se puede colocar en la posición (compatibilidad legacy).
        /// Ahora también verifica si el stud actual está libre.
        /// </summary>
        public bool CanPlace(ArmorPartData armorData, int startX, int startY)
        {
            if (armorData == null) return false;
            if (headStuds == null || headStuds.Count == 0) return false;
            
            // Verificar que hay un stud seleccionado y está libre
            if (CurrentHoveredStudIndex < 0 || CurrentHoveredStudIndex >= headStuds.Count)
                return false;
            
            if (IsStudOccupied(CurrentHoveredStudIndex))
                return false;
            
            // Verificar compatibilidad de tier
            var studTier = headStuds[CurrentHoveredStudIndex].tierInfo;
            return studTier.IsCompatibleWith(armorData.tierInfo);
        }
        
        /// <summary>
        /// Verifica si se puede colocar con rotación (compatibilidad legacy).
        /// </summary>
        public bool CanPlace(ArmorPartData armorData, int startX, int startY, RobotGame.Utils.GridRotation.Rotation rotation)
        {
            return CanPlace(armorData, startX, startY);
        }
        
        /// <summary>
        /// Verifica si se puede colocar con rotación int (compatibilidad legacy).
        /// </summary>
        public bool CanPlace(ArmorPartData armorData, int startX, int startY, int rotation)
        {
            return CanPlace(armorData, startX, startY);
        }
        
        /// <summary>
        /// Convierte posición de celda a posición mundial (compatibilidad).
        /// Si hay un stud hovereado, retorna su posición en lugar de calcular por celda.
        /// </summary>
        public Vector3 CellToWorldPosition(int cellX, int cellY)
        {
            // Si hay un stud seleccionado, usar su posición
            if (CurrentHoveredStudIndex >= 0 && CurrentHoveredStudIndex < headStuds.Count)
            {
                return GetStudWorldPosition(CurrentHoveredStudIndex);
            }
            
            // Fallback: cálculo por celda (compatibilidad legacy)
            float cellSize = CellSize;
            Vector3 localPos = new Vector3(cellX * cellSize, cellY * cellSize, 0f);
            return transform.TransformPoint(localPos);
        }
        
        /// <summary>
        /// Obtiene la pieza que ocupa el stud actualmente seleccionado.
        /// </summary>
        public ArmorPart GetPartAtCurrentStud()
        {
            if (CurrentHoveredStudIndex < 0 || CurrentHoveredStudIndex >= headStuds.Count)
            {
                // Debug.Log($"GetPartAtCurrentStud: índice inválido {CurrentHoveredStudIndex}");
                return null;
            }
            
            if (!IsStudOccupied(CurrentHoveredStudIndex))
            {
                // Debug.Log($"GetPartAtCurrentStud: Head[{CurrentHoveredStudIndex}] no está ocupado");
                return null;
            }
            
            // Debug.Log($"GetPartAtCurrentStud: Buscando pieza en Head[{CurrentHoveredStudIndex}], placedParts.Count={placedParts.Count}");
            
            // Buscar qué pieza está cerca de este stud
            Vector3 studWorldPos = GetStudWorldPosition(CurrentHoveredStudIndex);
            
            foreach (var part in placedParts)
            {
                if (part == null) continue;
                
                // Verificar si alguno de los Tails de esta pieza coincide con el stud
                var tailGrid = part.TailGrid;
                if (tailGrid == null)
                {
                    // Debug.Log($"  Parte '{part.gameObject.name}' no tiene TailGrid");
                    continue;
                }
                
                foreach (var tail in tailGrid.Studs)
                {
                    Vector3 tailWorldPos = part.transform.TransformPoint(tail.localPosition);
                    float distance = Vector3.Distance(tailWorldPos, studWorldPos);
                    
                    if (distance <= positionTolerance * 2f) // Usar tolerancia más amplia
                    {
                        // Debug.Log($"  Encontrada: '{part.gameObject.name}' (tail '{tail.name}' dist={distance:F4}m)");
                        return part;
                    }
                }
            }
            
            // Debug.Log("GetPartAtCurrentStud: No se encontró ninguna pieza");
            return null;
        }
        
        /// <summary>
        /// Obtiene la pieza en una celda (compatibilidad).
        /// Ahora usa el stud actual si está disponible.
        /// </summary>
        public ArmorPart GetPartAtCell(int cellX, int cellY)
        {
            // Intentar usar el stud actual primero
            if (CurrentHoveredStudIndex >= 0)
            {
                return GetPartAtCurrentStud();
            }
            
            // Fallback: buscar por posición
            Vector3 cellWorldPos = CellToWorldPosition(cellX, cellY);
            
            foreach (var part in placedParts)
            {
                if (part == null) continue;
                
                float distance = Vector3.Distance(part.transform.position, cellWorldPos);
                if (distance < CellSize)
                {
                    return part;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Remueve una pieza por posición de celda (compatibilidad).
        /// </summary>
        public bool Remove(int cellX, int cellY)
        {
            var part = GetPartAtCell(cellX, cellY);
            if (part != null)
            {
                return RemovePart(part);
            }
            return false;
        }
        
        /// <summary>
        /// Remueve una pieza de la grilla (compatibilidad).
        /// </summary>
        public bool Remove(ArmorPart armorPart)
        {
            return RemovePart(armorPart);
        }
        
        #endregion
        
        /// <summary>
        /// Remueve una pieza de armadura de esta grilla.
        /// Libera todos los studs Head que ocupaban los Tails de la pieza.
        /// </summary>
        public bool RemovePart(ArmorPart armorPart)
        {
            if (armorPart == null)
            {
                // Debug.LogWarning("RemovePart: armorPart es null");
                return false;
            }
            
            if (!placedParts.Contains(armorPart))
            {
                // Debug.LogWarning($"RemovePart: '{armorPart.gameObject.name}' no está en placedParts (count={placedParts.Count})");
                return false;
            }
            
            // Debug.Log($"RemovePart: Removiendo '{armorPart.gameObject.name}', occupiedIndices antes: [{string.Join(", ", occupiedIndices)}]");
            
            var tailGrid = armorPart.TailGrid;
            if (tailGrid != null && tailGrid.StudCount > 0)
            {
                // Liberar studs usando posiciones mundiales
                var tails = tailGrid.Studs;
                int freedCount = 0;
                
                // Usar tolerancia más generosa para la liberación
                float removeTolerance = positionTolerance * 2f; // 0.1m
                
                foreach (var tail in tails)
                {
                    // Calcular posición mundial del Tail
                    Vector3 tailWorldPos = armorPart.transform.TransformPoint(tail.localPosition);
                    
                    // Debug.Log($"  Buscando Head para Tail '{tail.name}' en worldPos {tailWorldPos}");
                    
                    // Buscar qué Head ocupa este Tail
                    bool found = false;
                    for (int i = 0; i < headStuds.Count; i++)
                    {
                        if (!occupiedIndices.Contains(i))
                            continue;
                        
                        Vector3 headWorldPos = GetStudWorldPosition(i);
                        float distance = Vector3.Distance(tailWorldPos, headWorldPos);
                        
                        if (distance <= removeTolerance)
                        {
                            occupiedIndices.Remove(i);
                            freedCount++;
                            // Debug.Log($"    Liberado Head[{i}] '{headStuds[i].name}' (dist={distance:F4}m)");
                            found = true;
                            break;
                        }
                    }
                    
                    if (!found)
                    {
                        // Debug.LogWarning($"    No se encontró Head ocupado para Tail '{tail.name}'");
                    }
                }
                
                // Debug.Log($"RemovePart: Liberados {freedCount} de {tails.Count} studs");
            }
            else
            {
                // Debug.LogWarning($"RemovePart: '{armorPart.gameObject.name}' no tiene TailGrid");
            }
            
            placedParts.Remove(armorPart);
            armorPart.OnRemoved();
            
            // Debug.Log($"RemovePart: occupiedIndices después: [{string.Join(", ", occupiedIndices)}]");
            
            return true;
        }
        
        /// <summary>
        /// Convierte posición local a mundial.
        /// </summary>
        public Vector3 LocalToWorld(Vector3 localPos)
        {
            return transform.TransformPoint(localPos);
        }
        
        #endregion
        
        #region Gizmos
        
        private void OnDrawGizmos()
        {
            if (!showGizmos || headStuds == null)
                return;
            
            for (int i = 0; i < headStuds.Count; i++)
            {
                var stud = headStuds[i];
                // Usar GetStudWorldPosition para consistencia con el sistema de snap
                Vector3 worldPos = GetStudWorldPosition(i);
                bool isOccupied = occupiedIndices.Contains(i);
                
                // Esfera sólida
                Gizmos.color = isOccupied ? occupiedStudColor : freeStudColor;
                Gizmos.DrawSphere(worldPos, gizmoRadius);
                
                // Borde
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(worldPos, gizmoRadius);
                
                // Destacar el stud actualmente hovereado
                if (i == CurrentHoveredStudIndex)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(worldPos, gizmoRadius * 1.5f);
                }
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            if (headStuds == null || headStuds.Count == 0)
                return;
            
            // Dibujar etiquetas
            #if UNITY_EDITOR
            for (int i = 0; i < headStuds.Count; i++)
            {
                var stud = headStuds[i];
                Vector3 worldPos = LocalToWorld(stud.localPosition);
                UnityEditor.Handles.Label(worldPos + Vector3.up * 0.05f, $"{stud.name}\nT{stud.tierInfo}");
            }
            #endif
        }
        
        #endregion
    }
}
