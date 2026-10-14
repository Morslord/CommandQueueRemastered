using System.Text.RegularExpressions;

namespace CommandQueueRemastered.Core
{
    public static class DescriptionFormatting
    {
        // Native TMP resolves the game's named styles (including LookingGlass colors).
        // Only remove geometry/size overrides so descriptions retain a uniform base size.
        public static string ForTmp(string text)
            => Regex.Replace(text ?? "", @"</?(?:size|font|line-height|voffset|cspace|mspace|align|margin|indent|link|sprite|nobr|width|pos)\b[^>]*>", "", RegexOptions.IgnoreCase);
    }
}
