namespace Cattas
{
    // The 8 special abilities you can give a cat.
    public enum CatTrait
    {
        LitterLord,  // digs straight down (like a very important cat in a litterbox)
        Fluffchute,  // floats safely down from any height
        Loaf,        // sits still and blocks other cats
        Scratcher,   // claws diagonally down
        Clawber,     // climbs walls
        Headbutter,  // tunnels straight through a wall
        HissyFit,    // 5 second countdown, then POOF
        YarnBridge,  // knits a little bridge
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
                case CatTrait.Loaf: return "Loaf";
                case CatTrait.Scratcher: return "Scratcher";
                case CatTrait.Clawber: return "Clawber";
                case CatTrait.Headbutter: return "Headbutter";
                case CatTrait.HissyFit: return "Hissy Fit";
                case CatTrait.YarnBridge: return "Yarn Bridge";
                default: return t.ToString();
            }
        }

        // Which traits have been built so far. We switch these on one at a time.
        public static bool IsImplemented(CatTrait t)
        {
            return t == CatTrait.LitterLord;
        }
    }
}
