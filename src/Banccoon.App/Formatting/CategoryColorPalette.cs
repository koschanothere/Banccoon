using Banccoon.Core.Appearance;
using Microsoft.Maui.Graphics;

namespace Banccoon.App.Formatting;

public static class CategoryColorPalette
{
    // The 7 selectable category colors (docs/visual-design-language.md's "Category colors"
    // section). Actual hex values are provisional - reused from the old hash-only palette rather
    // than invented fresh, since the real values are still TBD (same "architecture ready, values
    // later" approach already used for AccentColor).
    private static readonly IReadOnlyDictionary<CategoryColor, Color> NamedColors = new Dictionary<CategoryColor, Color>
    {
        [CategoryColor.Teal] = Color.FromArgb("#0D9488"),
        [CategoryColor.Brown] = Color.FromArgb("#8B5E34"),
        [CategoryColor.Blue] = Color.FromArgb("#2563EB"),
        [CategoryColor.Violet] = Color.FromArgb("#7C3AED"),
        [CategoryColor.Pink] = Color.FromArgb("#DB2777"),
        [CategoryColor.Slate] = Color.FromArgb("#475569"),
        [CategoryColor.Amber] = Color.FromArgb("#A16207")
    };

    private static readonly Color[] HashFallbackColors = NamedColors.Values.ToArray();

    private static readonly Color TransferColor = Color.FromArgb("#94A3B8");

    public static IReadOnlyList<CategoryColor> AllColors { get; } = Enum.GetValues<CategoryColor>();

    public static Color GetColor(CategoryColor color) => NamedColors[color];

    /// <summary>
    /// Resolves a category's badge color: its own explicit choice if set, otherwise a color
    /// deterministically derived from its Id so categories without one still look visually
    /// distinct and stable across sessions.
    /// </summary>
    public static Color GetColorForCategory(Guid categoryId, CategoryColor? explicitColor)
    {
        if (explicitColor is { } color)
        {
            return GetColor(color);
        }

        var index = (int)((uint)categoryId.GetHashCode() % HashFallbackColors.Length);
        return HashFallbackColors[index];
    }

    public static Color GetTransferColor() => TransferColor;
}
