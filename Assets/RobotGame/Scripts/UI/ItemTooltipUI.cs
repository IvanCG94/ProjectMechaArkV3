using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RobotGame.UI
{
    /// <summary>
    /// Tooltip que muestra información detallada de un item.
    /// Aparece al pasar el mouse sobre un slot.
    /// </summary>
    public class ItemTooltipUI : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private RectTransform tooltipRect;
        [SerializeField] private CanvasGroup canvasGroup;
        
        [Header("Contenido")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI statsText;
        [SerializeField] private TextMeshProUGUI tierText;
        [SerializeField] private Image rarityBar;
        
        [Header("Colores de Rareza")]
        [SerializeField] private Color commonColor = new Color(0.6f, 0.6f, 0.6f);
        [SerializeField] private Color uncommonColor = new Color(0.2f, 0.8f, 0.2f);
        [SerializeField] private Color rareColor = new Color(0.2f, 0.4f, 1f);
        [SerializeField] private Color epicColor = new Color(0.6f, 0.2f, 0.8f);
        [SerializeField] private Color legendaryColor = new Color(1f, 0.6f, 0f);
        
        [Header("Configuración")]
        [SerializeField] private Vector2 offset = new Vector2(10f, -10f);
        [SerializeField] private float fadeSpeed = 10f;
        
        private bool isShowing;
        private float targetAlpha;
        private Canvas parentCanvas;
        
        #region Singleton
        
        private static ItemTooltipUI _instance;
        public static ItemTooltipUI Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<ItemTooltipUI>();
                }
                return _instance;
            }
        }
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            _instance = this;
            
            if (tooltipRect == null)
            {
                tooltipRect = GetComponent<RectTransform>();
            }
            
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }
            
            parentCanvas = GetComponentInParent<Canvas>();
            
            Hide();
        }
        
        private void Update()
        {
            // Fade in/out
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeSpeed * Time.unscaledDeltaTime);
            }
            
            // Seguir mouse
            if (isShowing)
            {
                UpdatePosition();
            }
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Muestra el tooltip con la información del item.
        /// </summary>
        public void Show(Inventory.IInventoryItem item)
        {
            if (item == null)
            {
                Hide();
                return;
            }
            
            isShowing = true;
            targetAlpha = 1f;
            gameObject.SetActive(true);
            
            // Nombre con color de rareza
            if (nameText != null)
            {
                nameText.text = item.DisplayName;
                nameText.color = GetRarityColor(item.Rarity);
            }
            
            // Descripción
            if (descriptionText != null)
            {
                descriptionText.text = item.Description;
            }
            
            // Tier
            if (tierText != null)
            {
                if (item.Tier > 0)
                {
                    tierText.text = $"Tier {item.Tier}";
                    tierText.enabled = true;
                }
                else
                {
                    tierText.enabled = false;
                }
            }
            
            // Barra de rareza
            if (rarityBar != null)
            {
                rarityBar.color = GetRarityColor(item.Rarity);
            }
            
            // Stats (si es PartDataBase)
            if (statsText != null)
            {
                string stats = BuildStatsText(item);
                statsText.text = stats;
                statsText.enabled = !string.IsNullOrEmpty(stats);
            }
            
            // Forzar rebuild del layout
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect);
            
            UpdatePosition();
        }
        
        /// <summary>
        /// Muestra el tooltip para un stack.
        /// </summary>
        public void Show(Inventory.InventoryStack stack)
        {
            if (stack == null || stack.Item == null)
            {
                Hide();
                return;
            }
            
            Show(stack.Item);
        }
        
        /// <summary>
        /// Oculta el tooltip.
        /// </summary>
        public void Hide()
        {
            isShowing = false;
            targetAlpha = 0f;
            
            if (canvasGroup != null && canvasGroup.alpha <= 0.01f)
            {
                gameObject.SetActive(false);
            }
        }
        
        #endregion
        
        #region Private Methods
        
        private void UpdatePosition()
        {
            Vector2 mousePos = Input.mousePosition;
            
            // Convertir a posición del canvas
            if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentCanvas.transform as RectTransform,
                    mousePos,
                    parentCanvas.worldCamera,
                    out mousePos
                );
            }
            
            // Aplicar offset
            Vector2 tooltipPos = mousePos + offset;
            
            // Mantener dentro de pantalla
            Vector2 tooltipSize = tooltipRect.sizeDelta;
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            
            // Ajustar si sale por la derecha
            if (tooltipPos.x + tooltipSize.x > screenSize.x)
            {
                tooltipPos.x = mousePos.x - tooltipSize.x - offset.x;
            }
            
            // Ajustar si sale por abajo
            if (tooltipPos.y - tooltipSize.y < 0)
            {
                tooltipPos.y = mousePos.y + tooltipSize.y - offset.y;
            }
            
            tooltipRect.anchoredPosition = tooltipPos;
        }
        
        private string BuildStatsText(Inventory.IInventoryItem item)
        {
            // Intentar obtener stats de PartDataBase
            if (item is Data.PartDataBase partData)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                
                if (partData.durability > 0)
                {
                    sb.AppendLine($"<color=#FF6666>♥ {partData.durability:F0}</color>");
                }
                
                if (partData.weight > 0)
                {
                    sb.AppendLine($"<color=#AAAAAA>⚖ {partData.weight:F1} kg</color>");
                }
                
                return sb.ToString().TrimEnd();
            }
            
            return string.Empty;
        }
        
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
