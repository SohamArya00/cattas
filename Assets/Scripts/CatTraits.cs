namespace Cattas
{
    // The 8 special abilities you can give a cat.
    public enum CatTrait
    {
        LitterLord,  // digs straight down (like a very important cat in a litterbox)
        Fluffchute,  // floats safely down from any height
        BiscuitBaker,// kneads biscuits on the spot, blocking other cats
        Scratcher,   // claws diagonally down
        CurtainClimber, // climbs walls
        Zoomies,     // zooms straight through walls
        CableChewer, // chews a cable for 5 seconds, then ZAP! blasts a hole
        HairballBridge, // hacks up 12 hairballs, building a staircase bridge
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

        // Which traits have been built so far. We switch these on one at a time.
        public static bool IsImplemented(CatTrait t)
        {
            return t == CatTrait.LitterLord || t == CatTrait.BiscuitBaker || t == CatTrait.Fluffchute
                || t == CatTrait.Zoomies || t == CatTrait.CurtainClimber
                || t == CatTrait.CableChewer || t == CatTrait.HairballBridge;
        }
    }
}
