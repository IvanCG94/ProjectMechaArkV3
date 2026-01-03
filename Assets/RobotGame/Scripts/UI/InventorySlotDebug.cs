using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RobotGame.UI
{
    /// <summary>
    /// Script de debug para verificar la configuración del InventorySlotUI.
    /// Agregar a cualquier InventorySlotUI para ver qué referencias están asignadas.
    /// </summary>
    [ExecuteInEditMode]
    public class InventorySlotDebug : MonoBehaviour
    {
        [Header("Estado de Referencias")]
        [SerializeField] private bool hasIconImage;
        [SerializeField] private bool hasBorderImage;
        [SerializeField] private bool hasBackgroundImage;
        [SerializeField] private bool hasQuantityText;
        [SerializeField] private bool hasSelectedIndicator;
        
        [Header("Auto-Detectar (Click para asignar)")]
        [SerializeField] private bool autoDetectReferences;
        
        private void OnValidate()
        {
            CheckReferences();
            
            if (autoDetectReferences)
            {
                autoDetectReferences = false;
                AutoAssignReferences();
            }
        }
        
        private void CheckReferences()
        {
            var slot = GetComponent<InventorySlotUI>();
            if (slot == null)
            {
                Debug.LogWarning("InventorySlotDebug: No hay InventorySlotUI en este GameObject");
                return;
            }
            
            // Usar reflexión para verificar campos privados
            var type = typeof(InventorySlotUI);
            var iconField = type.GetField("iconImage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var borderField = type.GetField("borderImage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var bgField = type.GetField("backgroundImage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var qtyField = type.GetField("quantityText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var selField = type.GetField("selectedIndicator", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            hasIconImage = iconField?.GetValue(slot) != null;
            hasBorderImage = borderField?.GetValue(slot) != null;
            hasBackgroundImage = bgField?.GetValue(slot) != null;
            hasQuantityText = qtyField?.GetValue(slot) != null;
            hasSelectedIndicator = selField?.GetValue(slot) != null;
        }
        
        private void AutoAssignReferences()
        {
            var slot = GetComponent<InventorySlotUI>();
            if (slot == null) return;
            
            var type = typeof(InventorySlotUI);
            
            // Buscar Icon
            Transform iconTrans = transform.Find("Icon");
            if (iconTrans != null)
            {
                var iconImg = iconTrans.GetComponent<Image>();
                if (iconImg != null)
                {
                    var iconField = type.GetField("iconImage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    iconField?.SetValue(slot, iconImg);
                    Debug.Log("InventorySlotDebug: Icon asignado");
                }
            }
            
            // Buscar Border
            Transform borderTrans = transform.Find("Border");
            if (borderTrans != null)
            {
                var borderImg = borderTrans.GetComponent<Image>();
                if (borderImg != null)
                {
                    var borderField = type.GetField("borderImage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    borderField?.SetValue(slot, borderImg);
                    Debug.Log("InventorySlotDebug: Border asignado");
                }
            }
            
            // Background es el Image del propio slot
            var bgImg = GetComponent<Image>();
            if (bgImg != null)
            {
                var bgField = type.GetField("backgroundImage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                bgField?.SetValue(slot, bgImg);
                Debug.Log("InventorySlotDebug: Background asignado");
            }
            
            // Buscar Quantity
            Transform qtyTrans = transform.Find("Quantity");
            if (qtyTrans != null)
            {
                var qtyText = qtyTrans.GetComponent<TextMeshProUGUI>();
                if (qtyText != null)
                {
                    var qtyField = type.GetField("quantityText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    qtyField?.SetValue(slot, qtyText);
                    Debug.Log("InventorySlotDebug: Quantity asignado");
                }
            }
            
            // Buscar Selected
            Transform selTrans = transform.Find("Selected");
            if (selTrans != null)
            {
                var selField = type.GetField("selectedIndicator", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                selField?.SetValue(slot, selTrans.gameObject);
                Debug.Log("InventorySlotDebug: Selected asignado");
            }
            
            CheckReferences();
            
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(slot);
#endif
        }
    }
}
