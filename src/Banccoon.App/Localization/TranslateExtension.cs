using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;

namespace Banccoon.App.Localization;

// {loc:Translate Key} - binds to Translator.Instance's indexer so the bound Text/Title/etc.
// re-evaluates automatically when Translator.SetLanguage fires its change notification, the same
// live-refresh requirement AppThemeBinding already gives theme-bound properties.
[ContentProperty(nameof(Key))]
public sealed class TranslateExtension : IMarkupExtension<BindingBase>
{
    public string Key { get; set; } = string.Empty;

    public BindingBase ProvideValue(IServiceProvider serviceProvider)
    {
        return new Binding(
            path: $"[{Key}]",
            mode: BindingMode.OneWay,
            source: Translator.Instance);
    }

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}
