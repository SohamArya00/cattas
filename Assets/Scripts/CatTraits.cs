namespace Cattas
{
    // The 8 special abilities you can give a cat, in the order the levels teach them
    // (this is also the order of the buttons in the trait bar).
    public enum CatTrait
    {
        LitterLord,     // level 1: digs straight down (like a very important cat in a litterbox)
        Fluffchute,     // level 2: floats safely down from any height
        BiscuitBaker,   // level 3: kneads biscuits on the spot, blocking other cats
        Zoomies,        // level 4: zooms straight through walls
        CurtainClimber, // level 5: climbs walls
        CableChewer,    // level 6: chews a cable for 5 seconds, then ZAP! blasts a hole
        Scratcher,      // level 7: claws diagonally down
        HairballBridge, // level 8: hacks up 12 hairballs, building a staircase bridge
    }

    public static class CatTraits
    {
        public const int Count = 8;

        public static string DisplayName(CatTrait t)
        {
            switch (t)
            {
                case CatTrait.LitterLord: return "Litter Lord";
                case CatTrait.Fluffchute: return "Fluffchute";
                case CatTrait.BiscuitBaker: return "Biscuit Baker";
                case CatTrait.Scratcher: return "Scratcher";
                case CatTrait.CurtainClimber: return "Curtain Climber";
                case CatTrait.Zoomies: return "Zoomies";
                case CatTrait.CableChewer: return "Cable Chewer";
                case CatTrait.HairballBridge: return "Hairball Bridge";
                default: return t.ToString();
            }
        }

        // Which traits have been built so far. All 8 are done!
        public static bool IsImplemented(CatTrait t)
        {
            return true;
        }
    }
}
