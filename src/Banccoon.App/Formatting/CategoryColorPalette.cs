using Microsoft.Maui.Graphics;

namespace Banccoon.App.Formatting;

public static class CategoryColorPalette
{
    private static readonly Color[] Colors =
    [
        Color.FromArgb("#0D9488"),
        Color.FromArgb("#8B5E34"),
        Color.FromArgb("#2563EB"),
        Color.FromArgb("#7C3AED"),
        Color.FromArgb("#DB2777"),
        Color.FromArgb("#475569"),
        Color.FromArgb("#4338CA"),
        Color.FromArgb("#0E7490"),
        Color.FromArgb("#A16207"),
        Color.FromArgb("#6B7280")
    ];

    private static readonly Color TransferColor = Color.FromArgb("#94A3B8");

    public static Color GetColorForCategory(Guid categoryId)
    {
        var index = (int)((uint)categoryId.GetHashCode() % Colors.Length);
        return Colors[index];
    }

    public static Color GetTransferColor() => TransferColor;
}
