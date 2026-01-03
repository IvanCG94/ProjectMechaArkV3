using System;
using System.Collections.Generic;
using UnityEngine;

namespace RobotGame.Inventory
{
    /// <summary>
    /// Inventario del jugador.
    /// Singleton que maneja todos los items que el jugador posee.
    /// 
    /// CARACTERÍSTICAS:
    /// - Organizado por categorías
    /// - Soporte para items apilables y no apilables
    /// - Eventos para actualizar UI
    /// - Serializable para guardado
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        #region Singleton
        
        private static PlayerInventory _instance;
        private static bool isShuttingDown = false;
        
        public static PlayerInventory Instance
        {
            get
            {
                // No crear nuevos objetos durante shutdown
                if (isShuttingDown)
                {
                    return null;
                }
                
                if (_instance == null)
                {
                    _instance = FindObjectOfType<PlayerInventory>();
                    // No crear automáticamente - debe existir en la escena
                    // if (_instance == null)
                    // {
                    //     GameObject go = new GameObject("PlayerInventory");
                    //     _instance = go.AddComponent<PlayerInventory>();
                    // }
                }
                return _instance;
            }
        }
        
        #endregion
        
        #region Serialized Fields
        
        [Header("Configuración")]
        [Tooltip("Capacidad máxima por categoría (0 = ilimitado)")]
        [SerializeField] private int maxSlotsPerCategory = 50;
        
        [Header("Items Iniciales (Debug)")]
        [SerializeField] private List<InitialItem> startingItems = new List<InitialItem>();
        
        [Header("Estado Actual (Solo lectura)")]
        [SerializeField] private List<InventoryStack> allItems = new List<InventoryStack>();
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Se dispara cuando un item es agregado.
        /// Parámetros: (IInventoryItem item, int cantidad, int totalActual)
        /// </summary>
        public event Action<IInventoryItem, int, int> OnItemAdded;
        
        /// <summary>
        /// Se dispara cuando un item es removido.
        /// Parámetros: (IInventoryItem item, int cantidad, int totalRestante)
        /// </summary>
        public event Action<IInventoryItem, int, int> OnItemRemoved;
        
        /// <summary>
        /// Se dispara cuando el inventario cambia (cualquier modificación).
        /// </summary>
        public event Action OnInventoryChanged;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            // Comentado para evitar problemas en editor
            // DontDestroyOnLoad(gameObject);
        }
        
        private void Start()
        {
            Debug.Log($"PlayerInventory: Inicializando con {startingItems.Count} items iniciales");
            
            // Agregar items iniciales (para testing)
            foreach (var initialItem in startingItems)
            {
                if (initialItem.item != null)
                {
                    AddItem(initialItem.item, initialItem.quantity);
                    Debug.Log($"PlayerInventory: Agregado {initialItem.quantity}x {initialItem.item.name}");
                }
                else
                {
                    Debug.LogWarning("PlayerInventory: Item inicial es null");
                }
            }
            
            Debug.Log($"PlayerInventory: Total items después de inicializar: {allItems.Count}");
        }
        
        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
        
        private void OnApplicationQuit()
        {
            isShuttingDown = true;
        }
        
        #endregion
        
        #region Public Methods - Add
        
        /// <summary>
        /// Agrega un item al inventario.
        /// </summary>
        /// <param name="item">ScriptableObject del item (debe implementar IInventoryItem).</param>
        /// <param name="quantity">Cantidad a agregar.</param>
        /// <returns>Cantidad que no cupo (0 si todo se agregó).</returns>
        public int AddItem(ScriptableObject item, int quantity = 1)
        {
            if (item == null || quantity <= 0) return quantity;
            
            IInventoryItem invItem = item as IInventoryItem;
            if (invItem == null)
            {
                Debug.LogWarning($"PlayerInventory: {item.name} no implementa IInventoryItem");
                return quantity;
            }
            
            int remaining = quantity;
            
            // Primero, intentar agregar a stacks existentes
            if (invItem.MaxStackSize > 1)
            {
                foreach (var stack in allItems)
                {
                    if (stack.ItemData == item && !stack.IsFull)
                    {
                        remaining = stack.Add(remaining);
                        if (remaining <= 0) break;
                    }
                }
            }
            
            // Luego, crear nuevos stacks si es necesario
            while (remaining > 0)
            {
                // Verificar límite de slots
                int categoryCount = GetCategoryItemCount(invItem.Category);
                if (maxSlotsPerCategory > 0 && categoryCount >= maxSlotsPerCategory)
                {
                    Debug.LogWarning($"PlayerInventory: Categoría {invItem.Category} llena");
                    break;
                }
                
                int toAdd = Mathf.Min(remaining, invItem.MaxStackSize);
                InventoryStack newStack = new InventoryStack(item, toAdd);
                allItems.Add(newStack);
                remaining -= toAdd;
            }
            
            int added = quantity - remaining;
            if (added > 0)
            {
                OnItemAdded?.Invoke(invItem, added, GetItemCount(item));
                OnInventoryChanged?.Invoke();
                Debug.Log($"PlayerInventory: +{added} {invItem.DisplayName} (Total: {GetItemCount(item)})");
            }
            
            return remaining;
        }
        
        /// <summary>
        /// Agrega un item usando su ID.
        /// </summary>
        public int AddItemById(string itemId, int quantity = 1)
        {
            ScriptableObject item = FindItemById(itemId);
            if (item != null)
            {
                return AddItem(item, quantity);
            }
            Debug.LogWarning($"PlayerInventory: Item con ID '{itemId}' no encontrado");
            return quantity;
        }
        
        #endregion
        
        #region Public Methods - Remove
        
        /// <summary>
        /// Remueve un item del inventario.
        /// </summary>
        /// <param name="item">ScriptableObject del item.</param>
        /// <param name="quantity">Cantidad a remover.</param>
        /// <returns>Cantidad que se pudo remover.</returns>
        public int RemoveItem(ScriptableObject item, int quantity = 1)
        {
            if (item == null || quantity <= 0) return 0;
            
            IInventoryItem invItem = item as IInventoryItem;
            if (invItem == null) return 0;
            
            int toRemove = quantity;
            int removed = 0;
            
            // Remover de stacks existentes
            for (int i = allItems.Count - 1; i >= 0 && toRemove > 0; i--)
            {
                if (allItems[i].ItemData == item)
                {
                    int removedFromStack = allItems[i].Remove(toRemove);
                    toRemove -= removedFromStack;
                    removed += removedFromStack;
                    
                    // Eliminar stack si está vacío
                    if (allItems[i].IsEmpty)
                    {
                        allItems.RemoveAt(i);
                    }
                }
            }
            
            if (removed > 0)
            {
                OnItemRemoved?.Invoke(invItem, removed, GetItemCount(item));
                OnInventoryChanged?.Invoke();
                Debug.Log($"PlayerInventory: -{removed} {invItem.DisplayName} (Restante: {GetItemCount(item)})");
            }
            
            return removed;
        }
        
        /// <summary>
        /// Remueve un item usando su ID.
        /// </summary>
        public int RemoveItemById(string itemId, int quantity = 1)
        {
            ScriptableObject item = FindItemById(itemId);
            if (item != null)
            {
                return RemoveItem(item, quantity);
            }
            return 0;
        }
        
        #endregion
        
        #region Public Methods - Query
        
        /// <summary>
        /// Obtiene la cantidad total de un item en el inventario.
        /// </summary>
        public int GetItemCount(ScriptableObject item)
        {
            if (item == null) return 0;
            
            int count = 0;
            foreach (var stack in allItems)
            {
                if (stack.ItemData == item)
                {
                    count += stack.Quantity;
                }
            }
            return count;
        }
        
        /// <summary>
        /// Obtiene la cantidad de un item por ID.
        /// </summary>
        public int GetItemCountById(string itemId)
        {
            ScriptableObject item = FindItemById(itemId);
            return item != null ? GetItemCount(item) : 0;
        }
        
        /// <summary>
        /// Verifica si el jugador tiene al menos cierta cantidad de un item.
        /// </summary>
        public bool HasItem(ScriptableObject item, int quantity = 1)
        {
            return GetItemCount(item) >= quantity;
        }
        
        /// <summary>
        /// Verifica si el jugador tiene al menos cierta cantidad de un item por ID.
        /// </summary>
        public bool HasItemById(string itemId, int quantity = 1)
        {
            return GetItemCountById(itemId) >= quantity;
        }
        
        /// <summary>
        /// Obtiene todos los stacks de una categoría.
        /// </summary>
        public List<InventoryStack> GetItemsByCategory(InventoryCategory category)
        {
            List<InventoryStack> result = new List<InventoryStack>();
            
            foreach (var stack in allItems)
            {
                if (stack.Item?.Category == category)
                {
                    result.Add(stack);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Obtiene todos los stacks de una subcategoría.
        /// </summary>
        public List<InventoryStack> GetItemsBySubCategory(InventorySubCategory subCategory)
        {
            List<InventoryStack> result = new List<InventoryStack>();
            
            foreach (var stack in allItems)
            {
                if (stack.Item?.SubCategory == subCategory)
                {
                    result.Add(stack);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Obtiene todos los items (copia de la lista).
        /// </summary>
        public List<InventoryStack> GetAllItems()
        {
            return new List<InventoryStack>(allItems);
        }
        
        /// <summary>
        /// Cantidad de slots usados en una categoría.
        /// </summary>
        public int GetCategoryItemCount(InventoryCategory category)
        {
            int count = 0;
            foreach (var stack in allItems)
            {
                if (stack.Item?.Category == category)
                {
                    count++;
                }
            }
            return count;
        }
        
        /// <summary>
        /// Cantidad total de slots usados.
        /// </summary>
        public int GetTotalSlotCount()
        {
            return allItems.Count;
        }
        
        #endregion
        
        #region Public Methods - Utility
        
        /// <summary>
        /// Limpia todo el inventario.
        /// </summary>
        public void Clear()
        {
            allItems.Clear();
            OnInventoryChanged?.Invoke();
            Debug.Log("PlayerInventory: Inventario limpiado");
        }
        
        /// <summary>
        /// Limpia items de una categoría específica.
        /// </summary>
        public void ClearCategory(InventoryCategory category)
        {
            allItems.RemoveAll(s => s.Item?.Category == category);
            OnInventoryChanged?.Invoke();
            Debug.Log($"PlayerInventory: Categoría {category} limpiada");
        }
        
        #endregion
        
        #region Private Methods
        
        /// <summary>
        /// Busca un item por su ID en los assets cargados.
        /// </summary>
        private ScriptableObject FindItemById(string itemId)
        {
            // Buscar en los items ya en inventario
            foreach (var stack in allItems)
            {
                if (stack.Item?.ItemId == itemId)
                {
                    return stack.ItemData;
                }
            }
            
            // TODO: Implementar búsqueda en Resources o catálogo de items
            return null;
        }
        
        #endregion
        
        #region Debug
        
        /// <summary>
        /// Imprime el contenido del inventario en consola.
        /// </summary>
        [ContextMenu("Print Inventory")]
        public void PrintInventory()
        {
            Debug.Log("=== PLAYER INVENTORY ===");
            
            foreach (InventoryCategory category in Enum.GetValues(typeof(InventoryCategory)))
            {
                var items = GetItemsByCategory(category);
                if (items.Count > 0)
                {
                    Debug.Log($"[{category}]");
                    foreach (var stack in items)
                    {
                        Debug.Log($"  - {stack}");
                    }
                }
            }
            
            Debug.Log($"Total slots: {GetTotalSlotCount()}");
        }
        
        #endregion
        
        #region Helper Classes
        
        /// <summary>
        /// Estructura para items iniciales (debug/testing).
        /// </summary>
        [Serializable]
        public class InitialItem
        {
            public ScriptableObject item;
            public int quantity = 1;
        }
        
        #endregion
    }
}
