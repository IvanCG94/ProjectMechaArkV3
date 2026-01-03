using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RobotGame.Inventory;
using RobotGame.Assembly;
using RobotGame.Enums;
using RobotGame.Data;

namespace RobotGame.UI
{
    /// <summary>
    /// Panel principal del inventario.
    /// Muestra pestañas de categorías y grid de items.
    /// Se sincroniza con el modo de edición del ensamblaje.
    /// </summary>
    public class InventoryPanelUI : MonoBehaviour
    {
        #region Serialized Fields
        
        [Header("Referencias Principales")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Transform tabsContainer;
        [SerializeField] private Transform gridContainer;
        [SerializeField] private ScrollRect scrollRect;
        
        [Header("Prefabs")]
        [SerializeField] private GameObject slotPrefab;
        [SerializeField] private GameObject tabPrefab;
        
        [Header("Configuración de Grid")]
        [SerializeField] private int columns = 5;
        [SerializeField] private int visibleRows = 4;
        [SerializeField] private float slotSize = 80f;
        [SerializeField] private float slotSpacing = 5f;
        
        [Header("Pestañas")]
        [SerializeField] private List<TabConfig> tabConfigs = new List<TabConfig>();
        
        [Header("Input")]
        [SerializeField] private KeyCode toggleKey = KeyCode.I;
        [SerializeField] private KeyCode nextTabKey = KeyCode.Tab;
        
        #endregion
        
        #region Private Fields
        
        private List<InventoryTabUI> tabs = new List<InventoryTabUI>();
        private List<InventorySlotUI> slots = new List<InventorySlotUI>();
        private InventoryCategory currentCategory;
        private InventorySlotUI selectedSlot;
        private bool isOpen;
        private bool isInEditMode;
        
        // Referencia al controlador de ensamblaje
        private RobotAssemblyController assemblyController;
        
        // Tier de la estación actual (para filtrar piezas incompatibles)
        private TierInfo currentStationTier = TierInfo.Tier1_1;
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Se dispara cuando se selecciona un item.
        /// </summary>
        public event System.Action<IInventoryItem> OnItemSelected;
        
        /// <summary>
        /// Se dispara cuando se cambia de pestaña.
        /// </summary>
        public event System.Action<InventoryCategory> OnCategoryChanged;
        
        #endregion
        
        #region Properties
        
        public bool IsOpen => isOpen;
        public InventoryCategory CurrentCategory => currentCategory;
        public IInventoryItem SelectedItem => selectedSlot?.Item;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            // Configurar tabs por defecto si no hay
            if (tabConfigs.Count == 0)
            {
                SetupDefaultTabs();
            }
        }
        
        private void Start()
        {
            // Buscar assembly controller
            assemblyController = FindObjectOfType<RobotAssemblyController>();
            
            // Suscribirse a eventos del inventario
            if (PlayerInventory.Instance != null)
            {
                PlayerInventory.Instance.OnInventoryChanged += RefreshCurrentCategory;
            }
            
            // Crear UI
            CreateTabs();
            CreateSlots();
            
            // Estado inicial
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
            isOpen = false;
            
            // Seleccionar primera pestaña
            if (tabs.Count > 0)
            {
                SelectTab(tabs[0]);
            }
        }
        
        private void OnDestroy()
        {
            if (PlayerInventory.Instance != null)
            {
                PlayerInventory.Instance.OnInventoryChanged -= RefreshCurrentCategory;
            }
        }
        
        private void Update()
        {
            // Toggle con I (solo fuera de modo edición) o Tab en modo edición
            if (!isInEditMode && Input.GetKeyDown(toggleKey))
            {
                Toggle();
            }
            
            // Cambiar pestaña con Tab
            if (isOpen && Input.GetKeyDown(nextTabKey))
            {
                CycleToNextTab();
            }
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Abre el panel de inventario.
        /// </summary>
        public void Open()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }
            isOpen = true;
            RefreshCurrentCategory();
        }
        
        /// <summary>
        /// Cierra el panel de inventario.
        /// </summary>
        public void Close()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
            isOpen = false;
            ClearSelection();
        }
        
        /// <summary>
        /// Toggle abrir/cerrar.
        /// </summary>
        public void Toggle()
        {
            if (isOpen)
                Close();
            else
                Open();
        }
        
        /// <summary>
        /// Entra en modo edición (sincronizado con AssemblyController).
        /// </summary>
        public void EnterEditMode(AssemblyMode mode)
        {
            EnterEditMode(mode, TierInfo.Tier1_1);
        }
        
        /// <summary>
        /// Entra en modo edición con tier de estación específico.
        /// </summary>
        /// <param name="mode">Modo de ensamblaje (Armor/Structural)</param>
        /// <param name="stationTier">Tier de la estación para filtrar piezas</param>
        public void EnterEditMode(AssemblyMode mode, TierInfo stationTier)
        {
            isInEditMode = true;
            currentStationTier = stationTier;
            Open();
            
            // Seleccionar pestaña según modo
            InventoryCategory targetCategory = mode == AssemblyMode.Structural 
                ? InventoryCategory.StructuralParts 
                : InventoryCategory.ArmorParts;
            
            SelectCategory(targetCategory);
        }
        
        /// <summary>
        /// Sale del modo edición.
        /// </summary>
        public void ExitEditMode()
        {
            isInEditMode = false;
            currentStationTier = TierInfo.Tier1_1; // Reset
            Close();
        }
        
        /// <summary>
        /// Selecciona una categoría por enum.
        /// </summary>
        public void SelectCategory(InventoryCategory category)
        {
            foreach (var tab in tabs)
            {
                if (tab.Category == category)
                {
                    SelectTab(tab);
                    return;
                }
            }
        }
        
        /// <summary>
        /// Refresca los items de la categoría actual.
        /// </summary>
        public void RefreshCurrentCategory()
        {
            if (!isOpen) return;
            
            var items = PlayerInventory.Instance?.GetItemsByCategory(currentCategory);
            
            // Limpiar slots
            foreach (var slot in slots)
            {
                slot.ClearSlot();
            }
            
            // Llenar slots
            if (items != null)
            {
                for (int i = 0; i < items.Count && i < slots.Count; i++)
                {
                    var stack = items[i];
                    
                    // Verificar si la pieza está deshabilitada por tier
                    bool isDisabled = false;
                    string disabledReason = "";
                    
                    if (isInEditMode && currentCategory == InventoryCategory.StructuralParts)
                    {
                        // Verificar compatibilidad de tier para piezas estructurales
                        isDisabled = !IsPartCompatibleWithStation(stack);
                        if (isDisabled)
                        {
                            disabledReason = GetIncompatibilityReason(stack);
                        }
                    }
                    
                    slots[i].SetItem(stack, isDisabled, disabledReason);
                }
            }
        }
        
        /// <summary>
        /// Verifica si una pieza es compatible con el tier de la estación actual.
        /// Usa TierInfo.IsCompatibleWith para verificar las reglas:
        /// - Tier principal DEBE SER IGUAL al de la estación
        /// - Subtier debe ser IGUAL O INFERIOR al de la estación
        /// </summary>
        private bool IsPartCompatibleWithStation(InventoryStack stack)
        {
            if (stack?.ItemData == null) return true;
            
            var partData = stack.ItemData as PartDataBase;
            if (partData == null) return true;
            
            // Usar el método IsCompatibleWith de TierInfo
            return currentStationTier.IsCompatibleWith(partData.tier);
        }
        
        /// <summary>
        /// Obtiene el string del tier de una pieza para mostrar en tooltip.
        /// </summary>
        private string GetPartTierString(InventoryStack stack)
        {
            var partData = stack?.ItemData as PartDataBase;
            if (partData == null) return "?";
            
            return partData.tier.ToString();
        }
        
        /// <summary>
        /// Obtiene la razón por la que una pieza no es compatible.
        /// </summary>
        private string GetIncompatibilityReason(InventoryStack stack)
        {
            var partData = stack?.ItemData as PartDataBase;
            if (partData == null) return "Pieza inválida";
            
            TierInfo partTier = partData.tier;
            
            if (partTier.MainTier != currentStationTier.MainTier)
            {
                return $"Requiere estación Tier {partTier.MainTier}";
            }
            
            if (partTier.SubTier > currentStationTier.SubTier)
            {
                return $"Requiere estación Tier {partTier.MainTier}.{partTier.SubTier}+";
            }
            
            return "Incompatible";
        }
        
        /// <summary>
        /// Obtiene el item seleccionado actual.
        /// </summary>
        public ScriptableObject GetSelectedItemData()
        {
            return selectedSlot?.ItemStack?.ItemData;
        }
        
        #endregion
        
        #region Private Methods - Setup
        
        private void SetupDefaultTabs()
        {
            tabConfigs.Add(new TabConfig { category = InventoryCategory.StructuralParts, icon = null });
            tabConfigs.Add(new TabConfig { category = InventoryCategory.ArmorParts, icon = null });
            tabConfigs.Add(new TabConfig { category = InventoryCategory.Resources, icon = null });
            tabConfigs.Add(new TabConfig { category = InventoryCategory.BuildingParts, icon = null });
        }
        
        private void CreateTabs()
        {
            if (tabsContainer == null || tabPrefab == null) return;
            
            // Limpiar tabs existentes
            foreach (Transform child in tabsContainer)
            {
                Destroy(child.gameObject);
            }
            tabs.Clear();
            
            // Crear tabs
            foreach (var config in tabConfigs)
            {
                GameObject tabGO = Instantiate(tabPrefab, tabsContainer);
                InventoryTabUI tab = tabGO.GetComponent<InventoryTabUI>();
                
                if (tab != null)
                {
                    tab.Setup(config.category, config.icon);
                    tab.OnTabClicked += OnTabClicked;
                    tabs.Add(tab);
                }
            }
        }
        
        private void CreateSlots()
        {
            Debug.Log($"InventoryPanelUI.CreateSlots: gridContainer={gridContainer != null}, slotPrefab={slotPrefab != null}");
            
            if (gridContainer == null || slotPrefab == null)
            {
                Debug.LogError("InventoryPanelUI: gridContainer o slotPrefab es null!");
                return;
            }
            
            // Limpiar slots existentes
            foreach (Transform child in gridContainer)
            {
                Destroy(child.gameObject);
            }
            slots.Clear();
            
            // Configurar grid layout si existe
            GridLayoutGroup grid = gridContainer.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                grid.cellSize = new Vector2(slotSize, slotSize);
                grid.spacing = new Vector2(slotSpacing, slotSpacing);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = columns;
            }
            
            // Crear slots suficientes
            int totalSlots = columns * (visibleRows + 2); // Extra para scroll
            
            Debug.Log($"InventoryPanelUI: Creando {totalSlots} slots");
            
            for (int i = 0; i < totalSlots; i++)
            {
                GameObject slotGO = Instantiate(slotPrefab, gridContainer);
                InventorySlotUI slot = slotGO.GetComponent<InventorySlotUI>();
                
                if (slot != null)
                {
                    slot.OnSlotClicked += OnSlotClicked;
                    slot.OnSlotHoverEnter += OnSlotHoverEnter;
                    slot.OnSlotHoverExit += OnSlotHoverExit;
                    slots.Add(slot);
                }
                else
                {
                    Debug.LogWarning($"InventoryPanelUI: Slot {i} no tiene componente InventorySlotUI!");
                }
            }
            
            Debug.Log($"InventoryPanelUI: {slots.Count} slots creados con InventorySlotUI");
        }
        
        #endregion
        
        #region Private Methods - Tab Management
        
        private void SelectTab(InventoryTabUI tab)
        {
            if (tab == null) return;
            
            // Deseleccionar todas
            foreach (var t in tabs)
            {
                t.SetSelected(t == tab);
            }
            
            currentCategory = tab.Category;
            ClearSelection();
            RefreshCurrentCategory();
            
            OnCategoryChanged?.Invoke(currentCategory);
            
            // Sincronizar con modo de ensamblaje si estamos en edición
            if (isInEditMode && assemblyController != null)
            {
                // El cambio de pestaña cambia el modo de ensamblaje
                if (currentCategory == InventoryCategory.StructuralParts)
                {
                    // Notificar cambio a modo estructural
                    Debug.Log("InventoryPanelUI: Cambiado a modo Structural");
                }
                else if (currentCategory == InventoryCategory.ArmorParts)
                {
                    // Notificar cambio a modo armadura
                    Debug.Log("InventoryPanelUI: Cambiado a modo Armor");
                }
            }
        }
        
        private void CycleToNextTab()
        {
            if (tabs.Count == 0) return;
            
            int currentIndex = tabs.FindIndex(t => t.IsSelected);
            int nextIndex = (currentIndex + 1) % tabs.Count;
            
            SelectTab(tabs[nextIndex]);
        }
        
        private void OnTabClicked(InventoryTabUI tab)
        {
            SelectTab(tab);
        }
        
        #endregion
        
        #region Private Methods - Slot Management
        
        private void OnSlotClicked(InventorySlotUI slot)
        {
            // Deseleccionar anterior
            if (selectedSlot != null)
            {
                selectedSlot.SetSelected(false);
            }
            
            // Seleccionar nuevo
            selectedSlot = slot;
            slot.SetSelected(true);
            
            OnItemSelected?.Invoke(slot.Item);
            
            Debug.Log($"InventoryPanelUI: Item seleccionado - {slot.Item?.DisplayName}");
        }
        
        private void OnSlotHoverEnter(InventorySlotUI slot)
        {
            if (slot.Item != null && ItemTooltipUI.Instance != null)
            {
                ItemTooltipUI.Instance.Show(slot.ItemStack);
            }
        }
        
        private void OnSlotHoverExit(InventorySlotUI slot)
        {
            if (ItemTooltipUI.Instance != null)
            {
                ItemTooltipUI.Instance.Hide();
            }
        }
        
        private void ClearSelection()
        {
            if (selectedSlot != null)
            {
                selectedSlot.SetSelected(false);
                selectedSlot = null;
            }
        }
        
        #endregion
        
        #region Helper Classes
        
        [System.Serializable]
        public class TabConfig
        {
            public InventoryCategory category;
            public Sprite icon;
        }
        
        #endregion
    }
}
