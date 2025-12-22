namespace RobotGame.Enums
{
    /// <summary>
    /// Tipos de sockets para piezas estructurales.
    /// Estos definen cómo se conectan las piezas del esqueleto del robot.
    /// </summary>
    public enum StructuralSocketType
    {
        // Sockets principales
        Hips,           // Raíz del robot, contiene las piernas
        Torso,          // Se conecta a Hips
        Head,           // Se conecta a Torso
        
        // Extremidades
        ArmLeft,
        ArmRight,
        LegLeft,        // Parte de Hips pero puede ser modular
        LegRight,
        
        // Extensiones opcionales
        Tail,
        WingLeft,
        WingRight,
        
        // Especiales
        Core,           // Donde se inserta el núcleo del jugador
        
        // Para futuras expansiones
        Custom
    }
}
