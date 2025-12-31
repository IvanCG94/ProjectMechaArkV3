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
    
    /// <summary>
    /// Métodos de extensión para RobotTier.
    /// </summary>
    public static class RobotTierExtensions
    {
        /// <summary>
        /// Obtiene el tier principal (1, 2, 3, o 4).
        /// </summary>
        public static int GetMainTier(this RobotTier tier)
        {
            switch (tier)
            {
                case RobotTier.Tier1_1:
                case RobotTier.Tier1_2:
                case RobotTier.Tier1_3:
                    return 1;
                    
                case RobotTier.Tier2_1:
                case RobotTier.Tier2_2:
                case RobotTier.Tier2_3:
                    return 2;
                    
                case RobotTier.Tier3_1:
                case RobotTier.Tier3_2:
                case RobotTier.Tier3_3:
                    return 3;
                    
                case RobotTier.Tier4_1:
                case RobotTier.Tier4_2:
                case RobotTier.Tier4_3:
                    return 4;
                    
                default:
                    return 1;
            }
        }
        
        /// <summary>
        /// Obtiene la variante (1, 2, o 3).
        /// </summary>
        public static int GetVariant(this RobotTier tier)
        {
            switch (tier)
            {
                case RobotTier.Tier1_1:
                case RobotTier.Tier2_1:
                case RobotTier.Tier3_1:
                case RobotTier.Tier4_1:
                    return 1;
                    
                case RobotTier.Tier1_2:
                case RobotTier.Tier2_2:
                case RobotTier.Tier3_2:
                case RobotTier.Tier4_2:
                    return 2;
                    
                case RobotTier.Tier1_3:
                case RobotTier.Tier2_3:
                case RobotTier.Tier3_3:
                case RobotTier.Tier4_3:
                    return 3;
                    
                default:
                    return 1;
            }
        }
        
        /// <summary>
        /// Crea un RobotTier a partir de tier principal y variante.
        /// </summary>
        public static RobotTier FromTierAndVariant(int mainTier, int variant)
        {
            // Clamp valores
            mainTier = System.Math.Max(1, System.Math.Min(4, mainTier));
            variant = System.Math.Max(1, System.Math.Min(3, variant));
            
            int index = (mainTier - 1) * 3 + (variant - 1);
            return (RobotTier)index;
        }
    }
}
