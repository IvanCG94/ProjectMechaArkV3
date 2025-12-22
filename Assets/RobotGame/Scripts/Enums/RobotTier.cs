namespace RobotGame.Enums
{
    /// <summary>
    /// Define los tiers de robots y sus variantes.
    /// El formato X_Y indica que el tier X_Y acepta piezas de tier X_1 hasta X_Y.
    /// </summary>
    public enum RobotTier
    {
        // Tier 1: Robots pequeños (tamaño jugador)
        Tier1_1,
        Tier1_2,
        Tier1_3,
        
        // Tier 2: Robots medianos-pequeños (meca-velociraptors)
        Tier2_1,
        Tier2_2,
        Tier2_3,
        
        // Tier 3: Robots medianos (meca-trikes)
        Tier3_1,
        Tier3_2,
        Tier3_3,
        
        // Tier 4: Robots grandes (meca-trex)
        Tier4_1,
        Tier4_2,
        Tier4_3
    }
}
