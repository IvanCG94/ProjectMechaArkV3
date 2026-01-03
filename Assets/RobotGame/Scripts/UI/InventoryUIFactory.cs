using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RobotGame.UI
{
    /// <summary>
    /// Factory para crear elementos de UI programáticamente.
    /// Útil para testing o cuando no hay prefabs disponibles.
    /// </summary>
    public static class InventoryUIFactory
    {
        /// <summary>
        /// Crea un Canvas completo para el inventario.
        /// </summary>
        public static Canvas CreateInventoryCanvas()
        {
            // Canvas
            GameObject canvasGO = new GameObject("InventoryCanvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            
            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            
            canvasGO.AddComponent<GraphicRaycaster>();
            
            return canvas;
        }
        
        /// <summary>
        /// Crea el panel principal del inventario.
        /// </summary>
        public static InventoryPanelUI CreateInventoryPanel(Transform parent)
        {
            // Panel root
            GameObject panelGO = new GameObject("InventoryPanel");
            panelGO.transform.SetParent(parent, false);
            
            RectTransform panelRect = panelGO.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1, 0.5f);
            panelRect.anchorMax = new Vector2(1, 0.5f);
            panelRect.pivot = new Vector2(1, 0.5f);
            panelRect.anchoredPosition = new Vector2(-20, 0);
            panelRect.sizeDelta = new Vector2(450, 500);
            
            // Background
            Image bgImage = panelGO.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            
            // Panel component
            InventoryPanelUI panel = panelGO.AddComponent<InventoryPanelUI>();
            
            // Tabs container
            GameObject tabsGO = new GameObject("TabsContainer");
            tabsGO.transform.SetParent(panelGO.transform, false);
            
            RectTransform tabsRect = tabsGO.AddComponent<RectTransform>();
            tabsRect.anchorMin = new Vector2(0, 1);
            tabsRect.anchorMax = new Vector2(1, 1);
            tabsRect.pivot = new Vector2(0.5f, 1);
            tabsRect.anchoredPosition = new Vector2(0, 0);
            tabsRect.sizeDelta = new Vector2(0, 60);
            
            HorizontalLayoutGroup tabsLayout = tabsGO.AddComponent<HorizontalLayoutGroup>();
            tabsLayout.spacing = 5;
            tabsLayout.padding = new RectOffset(10, 10, 5, 5);
            tabsLayout.childAlignment = TextAnchor.MiddleLeft;
            tabsLayout.childForceExpandWidth = false;
            tabsLayout.childForceExpandHeight = true;
            
            // Grid container con scroll
            GameObject scrollGO = new GameObject("ScrollView");
            scrollGO.transform.SetParent(panelGO.transform, false);
            
            RectTransform scrollRect = scrollGO.AddComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0, 0);
            scrollRect.anchorMax = new Vector2(1, 1);
            scrollRect.offsetMin = new Vector2(10, 10);
            scrollRect.offsetMax = new Vector2(-10, -70);
            
            ScrollRect scroll = scrollGO.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            
            Image scrollBg = scrollGO.AddComponent<Image>();
            scrollBg.color = new Color(0.05f, 0.05f, 0.05f, 0.5f);
            
            scrollGO.AddComponent<Mask>().showMaskGraphic = true;
            
            // Content para el scroll
            GameObject contentGO = new GameObject("Content");
            contentGO.transform.SetParent(scrollGO.transform, false);
            
            RectTransform contentRect = contentGO.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            
            GridLayoutGroup gridLayout = contentGO.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(80, 80);
            gridLayout.spacing = new Vector2(5, 5);
            gridLayout.padding = new RectOffset(5, 5, 5, 5);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 5;
            gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayout.childAlignment = TextAnchor.UpperLeft;
            
            ContentSizeFitter fitter = contentGO.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            scroll.content = contentRect;
            
            return panel;
        }
        
        /// <summary>
        /// Crea un prefab de slot de inventario.
        /// </summary>
        public static GameObject CreateSlotPrefab()
        {
            // Slot root
            GameObject slotGO = new GameObject("InventorySlot");
            
            RectTransform slotRect = slotGO.AddComponent<RectTransform>();
            slotRect.sizeDelta = new Vector2(80, 80);
            
            // Background
            Image bgImage = slotGO.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            
            // Border
            GameObject borderGO = new GameObject("Border");
            borderGO.transform.SetParent(slotGO.transform, false);
            
            RectTransform borderRect = borderGO.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;
            
            Image borderImage = borderGO.AddComponent<Image>();
            borderImage.color = new Color(0.4f, 0.4f, 0.4f, 1f);
            borderImage.type = Image.Type.Sliced;
            // Necesitarías asignar un sprite con borde
            
            // Icon
            GameObject iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(slotGO.transform, false);
            
            RectTransform iconRect = iconGO.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.1f, 0.1f);
            iconRect.anchorMax = new Vector2(0.9f, 0.9f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            
            Image iconImage = iconGO.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.enabled = false;
            
            // Quantity text
            GameObject qtyGO = new GameObject("Quantity");
            qtyGO.transform.SetParent(slotGO.transform, false);
            
            RectTransform qtyRect = qtyGO.AddComponent<RectTransform>();
            qtyRect.anchorMin = new Vector2(1, 0);
            qtyRect.anchorMax = new Vector2(1, 0);
            qtyRect.pivot = new Vector2(1, 0);
            qtyRect.anchoredPosition = new Vector2(-5, 5);
            qtyRect.sizeDelta = new Vector2(40, 20);
            
            TextMeshProUGUI qtyText = qtyGO.AddComponent<TextMeshProUGUI>();
            qtyText.fontSize = 14;
            qtyText.alignment = TextAlignmentOptions.BottomRight;
            qtyText.color = Color.white;
            qtyText.enableAutoSizing = false;
            qtyText.enabled = false;
            
            // Selected indicator
            GameObject selectedGO = new GameObject("Selected");
            selectedGO.transform.SetParent(slotGO.transform, false);
            
            RectTransform selectedRect = selectedGO.AddComponent<RectTransform>();
            selectedRect.anchorMin = Vector2.zero;
            selectedRect.anchorMax = Vector2.one;
            selectedRect.offsetMin = new Vector2(-2, -2);
            selectedRect.offsetMax = new Vector2(2, 2);
            
            Image selectedImage = selectedGO.AddComponent<Image>();
            selectedImage.color = new Color(1f, 0.8f, 0f, 0.8f);
            selectedImage.type = Image.Type.Sliced;
            selectedGO.SetActive(false);
            
            // Add slot component
            InventorySlotUI slot = slotGO.AddComponent<InventorySlotUI>();
            
            return slotGO;
        }
        
        /// <summary>
        /// Crea un prefab de pestaña.
        /// </summary>
        public static GameObject CreateTabPrefab()
        {
            // Tab root
            GameObject tabGO = new GameObject("InventoryTab");
            
            RectTransform tabRect = tabGO.AddComponent<RectTransform>();
            tabRect.sizeDelta = new Vector2(50, 50);
            
            // Background
            Image bgImage = tabGO.AddComponent<Image>();
            bgImage.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
            
            // Icon
            GameObject iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(tabGO.transform, false);
            
            RectTransform iconRect = iconGO.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.15f, 0.15f);
            iconRect.anchorMax = new Vector2(0.85f, 0.85f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            
            Image iconImage = iconGO.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.color = Color.gray;
            
            // Selected indicator
            GameObject selectedGO = new GameObject("Selected");
            selectedGO.transform.SetParent(tabGO.transform, false);
            
            RectTransform selectedRect = selectedGO.AddComponent<RectTransform>();
            selectedRect.anchorMin = new Vector2(0, 0);
            selectedRect.anchorMax = new Vector2(1, 0);
            selectedRect.pivot = new Vector2(0.5f, 0);
            selectedRect.anchoredPosition = Vector2.zero;
            selectedRect.sizeDelta = new Vector2(0, 3);
            
            Image selectedImage = selectedGO.AddComponent<Image>();
            selectedImage.color = new Color(1f, 0.7f, 0.2f, 1f);
            selectedGO.SetActive(false);
            
            // Add tab component
            InventoryTabUI tab = tabGO.AddComponent<InventoryTabUI>();
            
            return tabGO;
        }
        
        /// <summary>
        /// Crea el tooltip.
        /// </summary>
        public static ItemTooltipUI CreateTooltip(Transform parent)
        {
            GameObject tooltipGO = new GameObject("ItemTooltip");
            tooltipGO.transform.SetParent(parent, false);
            
            RectTransform tooltipRect = tooltipGO.AddComponent<RectTransform>();
            tooltipRect.pivot = new Vector2(0, 1);
            tooltipRect.sizeDelta = new Vector2(250, 150);
            
            // Background
            Image bgImage = tooltipGO.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            
            // Canvas group for fading
            CanvasGroup canvasGroup = tooltipGO.AddComponent<CanvasGroup>();
            
            // Vertical layout
            VerticalLayoutGroup layout = tooltipGO.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 5;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            
            ContentSizeFitter fitter = tooltipGO.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            // Name text
            GameObject nameGO = new GameObject("Name");
            nameGO.transform.SetParent(tooltipGO.transform, false);
            TextMeshProUGUI nameText = nameGO.AddComponent<TextMeshProUGUI>();
            nameText.fontSize = 18;
            nameText.fontStyle = FontStyles.Bold;
            nameText.color = Color.white;
            
            // Rarity bar
            GameObject rarityGO = new GameObject("RarityBar");
            rarityGO.transform.SetParent(tooltipGO.transform, false);
            Image rarityBar = rarityGO.AddComponent<Image>();
            rarityBar.color = Color.gray;
            LayoutElement rarityLayout = rarityGO.AddComponent<LayoutElement>();
            rarityLayout.preferredHeight = 2;
            
            // Description text
            GameObject descGO = new GameObject("Description");
            descGO.transform.SetParent(tooltipGO.transform, false);
            TextMeshProUGUI descText = descGO.AddComponent<TextMeshProUGUI>();
            descText.fontSize = 14;
            descText.color = new Color(0.8f, 0.8f, 0.8f);
            
            // Stats text
            GameObject statsGO = new GameObject("Stats");
            statsGO.transform.SetParent(tooltipGO.transform, false);
            TextMeshProUGUI statsText = statsGO.AddComponent<TextMeshProUGUI>();
            statsText.fontSize = 14;
            statsText.color = Color.white;
            statsText.richText = true;
            
            // Tier text
            GameObject tierGO = new GameObject("Tier");
            tierGO.transform.SetParent(tooltipGO.transform, false);
            TextMeshProUGUI tierText = tierGO.AddComponent<TextMeshProUGUI>();
            tierText.fontSize = 12;
            tierText.color = new Color(0.6f, 0.6f, 0.6f);
            
            // Add component
            ItemTooltipUI tooltip = tooltipGO.AddComponent<ItemTooltipUI>();
            
            tooltipGO.SetActive(false);
            
            return tooltip;
        }
    }
}
