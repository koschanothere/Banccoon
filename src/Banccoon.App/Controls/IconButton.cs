using Banccoon.App.Services;

namespace Banccoon.App.Controls;

// The one way a button gets a Heroicons glyph (docs/visual-design-language.md, Iconography).
// A real Button, so hover, pressed, focus, disabled and the existing button styles
// (QuietButton, PrimaryButton, DestructiveButton...) all work unchanged:
//
//   <controls:IconButton Icon="{x:Static controls:Heroicons.Funnel}"
//                        Label="{loc:Translate Transactions_FilterButton}"
//                        Command="..." Style="{StaticResource QuietButton}" />
//
// Set Label (and TextOnlyLabel if needed), never Text - the control decides what Text is:
//   - icons on, ShowLabel false (the default): icon only, Label as the hover tooltip
//     (ToolTipProperties, as on the import review's amount) and the screen-reader name.
//   - icons on, ShowLabel true: icon and Label side by side, no tooltip.
//   - icons off (Settings -> General, IconPreference.ShowIcons): Label as plain text, exactly
//     like the text-only buttons before icons existed.
// The glyph is drawn in the button's own TextColor, so it follows the theme and the semantic
// button colors (red Destructive, white on Primary...) without any per-button color.
public class IconButton : Button
{
    public static readonly BindableProperty IconProperty =
        BindableProperty.Create(nameof(Icon), typeof(string), typeof(IconButton), null, propertyChanged: Refresh);

    public static readonly BindableProperty LabelProperty =
        BindableProperty.Create(nameof(Label), typeof(string), typeof(IconButton), null, propertyChanged: Refresh);

    public static readonly BindableProperty TextOnlyLabelProperty =
        BindableProperty.Create(nameof(TextOnlyLabel), typeof(string), typeof(IconButton), null, propertyChanged: Refresh);

    public static readonly BindableProperty ShowLabelProperty =
        BindableProperty.Create(nameof(ShowLabel), typeof(bool), typeof(IconButton), false, propertyChanged: Refresh);

    public static readonly BindableProperty IconSizeProperty =
        BindableProperty.Create(nameof(IconSize), typeof(double), typeof(IconButton), 16d, propertyChanged: Refresh);

    // Bound to IconPreference.Instance.ShowIcons (the binding engine holds that subscription
    // weakly, so a button in a recycled list row doesn't leak through the app-wide singleton).
    private static readonly BindableProperty ShowIconsProperty =
        BindableProperty.Create("ShowIcons", typeof(bool), typeof(IconButton), true, propertyChanged: Refresh);

    public IconButton()
    {
        ContentLayout = new ButtonContentLayout(ButtonContentLayout.ImagePosition.Left, 6);
        SetBinding(ShowIconsProperty, new Binding(nameof(IconPreference.ShowIcons), source: IconPreference.Instance));
        Apply();
    }

    public string? Icon
    {
        get => (string?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string? Label
    {
        get => (string?)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    // What the button says when icons are off, if not Label: for labels whose text-only form
    // carries its own glyph ("+ Add account", "‹ Previous") that would double up beside the icon.
    public string? TextOnlyLabel
    {
        get => (string?)GetValue(TextOnlyLabelProperty);
        set => SetValue(TextOnlyLabelProperty, value);
    }

    // Keep the label visible next to the icon (primary calls to action, the colored semantic
    // buttons) instead of moving it into a tooltip.
    public bool ShowLabel
    {
        get => (bool)GetValue(ShowLabelProperty);
        set => SetValue(ShowLabelProperty, value);
    }

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        // Theme switches change TextColor through the style's AppThemeBinding; a Command's
        // CanExecute changes IsEnabled. Either way the glyph has to be redrawn in the new color.
        if (propertyName == nameof(TextColor) || propertyName == nameof(IsEnabled))
        {
            ApplyImage();
        }
    }

    private static void Refresh(BindableObject bindable, object oldValue, object newValue)
    {
        ((IconButton)bindable).Apply();
    }

    private bool IsShowingIcon => (bool)GetValue(ShowIconsProperty) && !string.IsNullOrEmpty(Icon);

    private void Apply()
    {
        var iconOnly = IsShowingIcon && !ShowLabel;
        Text = iconOnly ? string.Empty : IsShowingIcon ? Label : TextOnlyLabel ?? Label;

        if (iconOnly)
        {
            ToolTipProperties.SetText(this, Label ?? string.Empty);
            SemanticProperties.SetDescription(this, Label);
        }
        else
        {
            ClearValue(ToolTipProperties.TextProperty);
            ClearValue(SemanticProperties.DescriptionProperty);
        }

        ApplyImage();
    }

    private void ApplyImage()
    {
        if (!IsShowingIcon)
        {
            ImageSource = null;
            return;
        }

        var color = TextColor ?? ThemeTextColor();
        ImageSource = new FontImageSource
        {
            FontFamily = Heroicons.FontFamily,
            Glyph = Icon,
            Size = IconSize,
            // WinUI greys a disabled button's text itself but not its image.
            Color = IsEnabled ? color : color.WithAlpha(0.4f),
        };
    }

    private static Color ThemeTextColor()
    {
        var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var key = dark ? "DarkTextPrimary" : "LightTextPrimary";
        return Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
            ? color
            : dark ? Colors.White : Colors.Black;
    }
}
