using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace RobotGame.UI
{
    /// <summary>
    /// Slot individual en el grid de inventario.
    /// Muestra ícono, cantidad, borde de rareza y maneja clicks.
    /// </summary>
    public class InventorySlotUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Referencias UI")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Image borderImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI quantityText;
        [SerializeField] private GameObject selectedIndicator;
        
        [Header("Colores de Rareza")]
        [SerializeField] private Color commonColor = new Color(0.6f, 0.6f, 0.6f);
        [SerializeField] private Color uncommonColor = new Color(0.2f, 0.8f, 0.2f);
        [SerializeField] private Color rareColor = new Color(0.2f, 0.4f, 1f);
        [SerializeField] private Color epicColor = new Color(0.6f, 0.2f, 0.8f);
        [SerializeField] private Color legendaryColor = new Color(1f, 0.6f, 0f);
        
        [Header("Colores de Estado")]
        [SerializeField] private Color normalBackground = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        [SerializeField] private Color hoverBackground = new Color(0.3f, 0.3f, 0.3f, 0.9f);
        [SerializeField] private Color selectedBackground = new Color(0.4f, 0.4f, 0.2f, 0.9f);
        [SerializeField] private Color disabledBackground = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        [SerializeField] private Color disabledIconTint = new Color(0.4f, 0.4f, 0.4f, 0.6f);
        
        // Estado
        private Inventory.InventoryStack itemStack;
        private bool isSelected;
        private bool isEmpty = true;
        private bool isDisabled = false;
        private string disabledReason = "";
        
        // Eventos
        public event System.Action<InventorySlotUI> OnSlotClicked;
        public event System.Action<InventorySlotUI> OnSlotRightClicked;
        public event System.Action<InventorySlotUI> OnSlotHoverEnter;
        public event System.Action<InventorySlotUI> OnSlotHoverExit;
        
        #region Properties
        
        public Inventory.InventoryStack ItemStack => itemStack;
        public Inventory.IInventoryItem Item => itemStack?.Item;
        public bool IsEmpty => isEmpty;
        public bool IsSelected => isSelected;
        public bool IsDisabled => isDisabled;
        public string DisabledReason => disabledReason;
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Configura el slot con un stack de items.
        /// </summary>
        public void SetItem(Inventory.InventoryStack stack)
        {
            SetItem(stack, false, "");
        }
        
        /// <summary>
        /// Configura el slot con un stack de items, con opción de deshabilitarlo.
        /// </summary>
        /// <param name="stack">Stack de items a mostrar</param>
        /// <param name="disabled">Si el slot está deshabilitado (no seleccionable)</param>
        /// <param name="reason">Razón por la que está deshabilitado (para tooltip)</param>
        public void SetItem(Inventory.InventoryStack stack, bool disabled, string reason = "")
        {
            itemStack = stack;
            isEmpty = stack == null || stack.IsEmpty;
            isDisabled = disabled;
            disabledReason = reason;
            
            if (isEmpty)
            {
                ClearSlot();
                return;
            }
            
            // Configurar ícono
            if (iconImage != null)
            {
                iconImage.sprite = stack.Item.Icon;
                iconImage.enabled = stack.Item.Icon != null;
                // Aplicar tinte si está deshabilitado
                iconImage.color = isDisabled ? disabledIconTint : Color.white;
            }
            
            // Configurar cantidad
            if (quantityText != null)
            {
                if (stack.Quantity > 1)
                {
                    quantityText.text = stack.Quantity.ToString();
                    quantityText.enabled = true;
                    // Oscurecer texto si está deshabilitado
                    quantityText.color = isDisabled ? new Color(0.5f, 0.5f, 0.5f) : Color.white;
                }
                else
                {
                    quantityText.enabled = false;
                }
            }
            
            // Configurar borde de rareza
            if (borderImage != null)
            {
                Color rarityColor = GetRarityColor(stack.Item.Rarity);
                // Oscurecer borde si está deshabilitado
                if (isDisabled)
                {
                    rarityColor = new Color(rarityColor.r * 0.4f, rarityColor.g * 0.4f, rarityColor.b * 0.4f, rarityColor.a);
                }
                borderImage.color = rarityColor;
                borderImage.enabled = true;
            }
            
            // Background según estado
            if (backgroundImage != null)
            {
                backgroundImage.color = isDisabled ? disabledBackground : normalBackground;
            }
        }
        
        /// <summary>
        /// Limpia el slot.
        /// </summary>
        public void ClearSlot()
        {
            itemStack = null;
            isEmpty = true;
            
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }
            
            if (quantityText != null)
            {
                quantityText.enabled = false;
            }
            
            if (borderImage != null)
            {
                borderImage.color = commonColor;
            }
            
            if (backgroundImage != null)
            {
                backgroundImage.color = normalBackground;
            }
            
            SetSelected(false);
        }
        
        /// <summary>
        /// Establece si el slot está seleccionado.
        /// </summary>
        public void SetSelected(bool selected)
        {
            isSelected = selected;
            
            if (selectedIndicator != null)
            {
                selectedIndicator.SetActive(selected);
            }
            
            if (backgroundImage != null && !isEmpty)
            {
                backgroundImage.color = selected ? selectedBackground : normalBackground;
            }
        }
        
        /// <summary>
        /// Actualiza la cantidad mostrada.
        /// </summary>
        public void UpdateQuantity(int quantity)
        {
            if (itemStack != null)
            {
                itemStack.Quantity = quantity;
            }
            
            if (quantityText != null)
            {
                if (quantity > 1)
                {
                    quantityText.text = quantity.ToString();
                    quantityText.enabled = true;
                }
                else
                {
                    quantityText.enabled = false;
                }
            }
        }
        
        #endregion
        
        #region Event Handlers
        
        public void OnPointerClick(PointerEventData eventData)
        {
            if (isEmpty) return;
            
            // No permitir clicks en slots deshabilitados
            if (isDisabled) return;
            
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                OnSlotClicked?.Invoke(this);
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                OnSlotRightClicked?.Invoke(this);
            }
        }
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            // Mostrar hover solo si no está deshabilitado
            if (!isEmpty && backgroundImage != null && !isSelected && !isDisabled)
            {
                backgroundImage.color = hoverBackground;
            }
            
            OnSlotHoverEnter?.Invoke(this);
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isEmpty && backgroundImage != null && !isSelected)
            {
                backgroundImage.color = normalBackground;
            }
            
            OnSlotHoverExit?.Invoke(this);
        }
        
        #endregion
        
        #region Private Methods
        
        private Color GetRarityColor(Inventory.ItemRarity rarity)
        {
            switch (rarity)
            {
                case Inventory.ItemRarity.Common:
                    return commonColor;
                case Inventory.ItemRarity.Uncommon:
                    return uncommonColor;
                case Inventory.ItemRarity.Rare:
                    return rareColor;
                case Inventory.ItemRarity.Epic:
                    return epicColor;
                case Inventory.ItemRarity.Legendary:
                    return legendaryColor;
                default:
                    return commonColor;
            }
        }
        
        #endregion
    }
}
