using System.Globalization;

namespace Banccoon.App.Converters;

// Picks an IconButton's glyph from a bool, for buttons whose label already flips with state
// ("Show number" / "Hide number", "Attach…" / "Cancel") so the icon flips with it. Declared once
// per pairing in App.xaml, e.g. TrueGlyph="{x:Static controls:Heroicons.EyeSlash}".
public sealed class BoolToGlyphConverter : IValueConverter
{
    public string TrueGlyph { get; set; } = string.Empty;

    public string FalseGlyph { get; set; } = string.Empty;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? TrueGlyph : FalseGlyph;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
