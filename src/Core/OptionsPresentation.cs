namespace CommandQueueRemastered.Core
{
    // Change UI labels without renaming persisted BepInEx keys or item tiers.
    public static class OptionsPresentation
    {
        public static string Category(string section)
            => section == "Essence drops" ? "Item Distribution" : section == "Special items" ? "Special Items" : section;
        public static string Name(string section, string key)
        {
            if (section != "Essence drops") return key;
            switch (key)
            {
                case "Tier1": return "White";
                case "Tier2": return "Green";
                case "Tier3": return "Red";
                case "VoidTier1": return "VoidWhite";
                case "VoidTier2": return "VoidGreen";
                case "VoidTier3": return "VoidRed";
                default: return key;
            }
        }
    }
}
