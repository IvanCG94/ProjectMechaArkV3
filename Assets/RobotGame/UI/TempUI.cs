// Agregar a cualquier GameObject en la escena, ejecutar, y luego borrar el script
using UnityEngine;
using RobotGame.UI;

public class SetupInventoryUI : MonoBehaviour
{
    void Start()
    {
        // Crear canvas
        Canvas canvas = InventoryUIFactory.CreateInventoryCanvas();
        
        // Crear panel de inventario
        InventoryPanelUI panel = InventoryUIFactory.CreateInventoryPanel(canvas.transform);
        
        // Crear tooltip
        ItemTooltipUI tooltip = InventoryUIFactory.CreateTooltip(canvas.transform);
        
        // Crear prefabs y asignarlos
        GameObject slotPrefab = InventoryUIFactory.CreateSlotPrefab();
        GameObject tabPrefab = InventoryUIFactory.CreateTabPrefab();
        
        // Los prefabs se deben guardar en Assets/Prefabs/ para asignar en el inspector
        
        Debug.Log("UI de inventario creada. Configura las referencias en el inspector.");
    }
}