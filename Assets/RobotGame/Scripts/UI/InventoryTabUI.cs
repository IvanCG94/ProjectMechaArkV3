using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace RobotGame.UI
{
    /// <summary>
    /// Pestaña individual en el panel de inventario.
    /// Representa una categoría (Estructurales, Armadura, Recursos, etc.)
    /// </summary>
    public class InventoryTabUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Referencias")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private GameObject selectedIndicator;
        
        [Header("Configuración")]
        [SerializeField] private Inventory.InventoryCategory category;
        [SerializeField] private Sprite tabIcon;
        
        [Header("Colores")]
        [SerializeField] private Color normalColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        [SerializeField] private Color hoverColor = new Color(0.4f, 0.4f, 0.4f, 0.9f);
        [SerializeField] private Color selectedColor = new Color(0.5f, 0.4f, 0.2f, 1f);
        [SerializeField] private Color iconNormalColor = Color.gray;
        [SerializeField] private Color iconSelectedColor = Color.white;
        
        private bool isSelected;
        
        // Eventos
        public event System.Action<InventoryTabUI> OnTabClicked;
        
        #region Properties
        
        public Inventory.InventoryCategory Category => category;
        public bool IsSelected => isSelected;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Start()
        {
            if (iconImage != null && tabIcon != null)
            {
                iconImage.sprite = tabIcon;
            }
            
            SetSelected(false);
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Configura la pestaña.
        /// </summary>
        public void Setup(Inventory.InventoryCategory cat, Sprite icon)
        {
            category = cat;
            tabIcon = icon;
            
            if (iconImage != null && icon != null)
            {
                iconImage.sprite = icon;
            }
        }
        
        /// <summary>
        /// Establece si la pestaña está seleccionada.
        /// </summary>
        public void SetSelected(bool selected)
        {
            isSelected = selected;
            
            if (backgroundImage != null)
            {
                backgroundImage.color = selected ? selectedColor : normalColor;
            }
            
            if (iconImage != null)
            {
                iconImage.color = selected ? iconSelectedColor : iconNormalColor;
            }
            
            if (selectedIndicator != null)
            {
                selectedIndicator.SetActive(selected);
            }
        }
        
        #endregion
        
        #region Event Handlers
        
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                OnTabClicked?.Invoke(this);
            }
        }
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!isSelected && backgroundImage != null)
            {
                backgroundImage.color = hoverColor;
            }
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isSelected && backgroundImage != null)
            {
                backgroundImage.color = normalColor;
            }
        }
        
        #endregion
    }
}
