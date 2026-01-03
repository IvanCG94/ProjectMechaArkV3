using UnityEngine;

namespace RobotGame.Inventory
{
    /// <summary>
    /// Representa un stack de items en el inventario.
    /// Un stack contiene una referencia al item y la cantidad.
    /// </summary>
    [System.Serializable]
    public class InventoryStack
    {
        [SerializeField] private ScriptableObject itemData;
        [SerializeField] private int quantity;
        
        /// <summary>
        /// El ScriptableObject que contiene los datos del item.
        /// Debe implementar IInventoryItem.
        /// </summary>
        public ScriptableObject ItemData => itemData;
        
        /// <summary>
        /// Acceso tipado al item como IInventoryItem.
        /// </summary>
        public IInventoryItem Item => itemData as IInventoryItem;
        
        /// <summary>
        /// Cantidad de items en este stack.
        /// </summary>
        public int Quantity
        {
            get => quantity;
            set => quantity = Mathf.Max(0, value);
        }
        
        /// <summary>
        /// Si el stack está vacío.
        /// </summary>
        public bool IsEmpty => quantity <= 0 || itemData == null;
        
        /// <summary>
        /// Si el stack está lleno.
        /// </summary>
        public bool IsFull => Item != null && quantity >= Item.MaxStackSize;
        
        /// <summary>
        /// Espacio disponible en el stack.
        /// </summary>
        public int SpaceAvailable => Item != null ? Item.MaxStackSize - quantity : 0;
        
        /// <summary>
        /// Constructor vacío para serialización.
        /// </summary>
        public InventoryStack()
        {
            itemData = null;
            quantity = 0;
        }
        
        /// <summary>
        /// Constructor con item y cantidad.
        /// </summary>
        public InventoryStack(ScriptableObject item, int qty = 1)
        {
            itemData = item;
            quantity = qty;
        }
        
        /// <summary>
        /// Intenta agregar cantidad al stack.
        /// </summary>
        /// <param name="amount">Cantidad a agregar.</param>
        /// <returns>Cantidad que no cupo (overflow).</returns>
        public int Add(int amount)
        {
            if (Item == null) return amount;
            
            int canAdd = Mathf.Min(amount, SpaceAvailable);
            quantity += canAdd;
            return amount - canAdd;
        }
        
        /// <summary>
        /// Intenta remover cantidad del stack.
        /// </summary>
        /// <param name="amount">Cantidad a remover.</param>
        /// <returns>Cantidad que se pudo remover.</returns>
        public int Remove(int amount)
        {
            int canRemove = Mathf.Min(amount, quantity);
            quantity -= canRemove;
            return canRemove;
        }
        
        /// <summary>
        /// Crea una copia del stack.
        /// </summary>
        public InventoryStack Clone()
        {
            return new InventoryStack(itemData, quantity);
        }
        
        public override string ToString()
        {
            if (IsEmpty) return "[Empty]";
            return $"[{Item?.DisplayName ?? "Unknown"} x{quantity}]";
        }
    }
}
