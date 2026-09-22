using System.ComponentModel;
using System.Globalization;
using System.Resources;
using Banccoon.Core.Localization;

namespace Banccoon.App.Localization;

// Ambient translation state, same "static, no DI" shape as Banccoon.App.Formatting.PrivacyMode -
// but exposed through a singleton instance rather than a `static class`, since it must implement
// INotifyPropertyChanged for {loc:Translate} bindings (TranslateExtension) to refresh live when
// the language changes, the same way theme changes flip Application.Current.UserAppTheme live
// (see SettingsViewModel.ApplyTheme) instead of requiring an app restart.
public sealed class Translator : INotifyPropertyChanged
{
    private static readonly ResourceManager ResourceManager =
        new("Banccoon.App.Resources.Strings.AppStrings", typeof(Translator).Assembly);

    private CultureInfo currentCulture = CultureInfo.CurrentUICulture;

    private Translator()
    {
    }

    public static Translator Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    // Falls back to the key itself (rather than throwing or returning empty) so a missing
    // translation is visibly wrong in the UI instead of silently blank.
    public string this[string key] => ResourceManager.GetString(key, currentCulture) ?? key;

    public static string Get(string key) => Instance[key];

    // keyBase resolves to "{keyBase}_One"/"_Few"/"_Many"/"_Other" depending on count and the
    // current language's plural rule (PluralRules.GetForm) - English only ever needs "_One" and
    // "_Other", Russian only ever needs "_One"/"_Few"/"_Many", so each resx only defines the
    // suffixes its own language's rule can produce. Falls back to "_Other" for a form whose key
    // wasn't translated yet, then to the raw key, so a gap never comes back as a blank string.
    // The resolved template takes a single {0} placeholder for the count.
    public static string GetPlural(string keyBase, int count)
    {
        var form = PluralRules.GetForm(count, Instance.currentCulture.TwoLetterISOLanguageName);
        var suffix = form switch
        {
            PluralForm.One => "One",
            PluralForm.Few => "Few",
            PluralForm.Many => "Many",
            _ => "Other"
        };

        var template = ResourceManager.GetString($"{keyBase}_{suffix}", Instance.currentCulture)
            ?? ResourceManager.GetString($"{keyBase}_Other", Instance.currentCulture)
            ?? $"{keyBase}_{suffix}";

        return string.Format(Instance.currentCulture, template, count);
    }

    // languageCode matches AppSettings.DisplayLanguage / LanguageOption.Code ("en", "ru", ...).
    // An unrecognized code (a future language whose resx doesn't exist yet) falls back to the
    // neutral (English) resource set rather than throwing - standard ResourceManager behavior.
    public static void SetLanguage(string languageCode)
    {
        var culture = new CultureInfo(languageCode);
        if (Equals(Instance.currentCulture, culture))
        {
            return;
        }

        Instance.currentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        // A null/empty property name is the standard "everything changed" signal, which refreshes
        // every {loc:Translate} binding (each one bound to this[key], i.e. this indexer) in one
        // shot rather than needing to know which keys are currently on screen.
        Instance.PropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs(null));
    }
}
