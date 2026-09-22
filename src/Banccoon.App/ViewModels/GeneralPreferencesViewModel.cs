using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.App.Localization;
using Banccoon.Core.Models;
using Banccoon.Core.Repositories;

namespace Banccoon.App.ViewModels;

// Groups the settings that are each just "pick a value, hit Save" with no sub-flow of their own -
// language, reminders, account defaults, and privacy mode. Composed into SettingsViewModel the
// same way Data/CategoryManagement are composed into other screens.
public sealed class GeneralPreferencesViewModel : ViewModelBase
{
    private readonly ISettingsRepository settingsRepository;

    private string defaultCurrencyText = "EUR";
    private AccountType defaultAccountType = AccountType.DebitCard;
    private bool privacyModeEnabled;
    private ReminderFrequency reminderFrequency = ReminderFrequency.Weekly;
    private LanguageOption selectedLanguage;
    private string statusText = string.Empty;

    public GeneralPreferencesViewModel(ISettingsRepository settingsRepository)
    {
        this.settingsRepository = settingsRepository;
        AccountTypes = Enum.GetValues<AccountType>();
        ReminderFrequencies = Enum.GetValues<ReminderFrequency>();
        Languages =
        [
            new LanguageOption("en", "English"),
            new LanguageOption("ru", "Русский")
        ];
        selectedLanguage = Languages[0];

        SaveCommand = new RelayCommand(() => _ = SaveAsync());
    }

    public IReadOnlyList<AccountType> AccountTypes { get; }

    public IReadOnlyList<ReminderFrequency> ReminderFrequencies { get; }

    public IReadOnlyList<LanguageOption> Languages { get; }

    public string DefaultCurrencyText
    {
        get => defaultCurrencyText;
        set => SetProperty(ref defaultCurrencyText, value);
    }

    public AccountType DefaultAccountType
    {
        get => defaultAccountType;
        set => SetProperty(ref defaultAccountType, value);
    }

    public bool PrivacyModeEnabled
    {
        get => privacyModeEnabled;
        set => SetProperty(ref privacyModeEnabled, value);
    }

    public ReminderFrequency ReminderFrequency
    {
        get => reminderFrequency;
        set => SetProperty(ref reminderFrequency, value);
    }

    public LanguageOption SelectedLanguage
    {
        get => selectedLanguage;
        set => SetProperty(ref selectedLanguage, value);
    }

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public ICommand SaveCommand { get; }

    public Task InitializeAsync(AppSettings settings)
    {
        DefaultCurrencyText = settings.DefaultCurrency;
        DefaultAccountType = settings.DefaultAccountType;
        PrivacyModeEnabled = settings.PrivacyModeEnabled;
        ReminderFrequency = settings.ReminderFrequency;
        SelectedLanguage = Languages.FirstOrDefault(language => language.Code == settings.DisplayLanguage) ?? Languages[0];
        StatusText = string.Empty;
        return Task.CompletedTask;
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(DefaultCurrencyText))
        {
            StatusText = Translator.Get("Accounts_CurrencyRequired");
            return;
        }

        var normalizedCurrency = DefaultCurrencyText.Trim().ToUpperInvariant();

        var settings = await settingsRepository.GetAsync();
        await settingsRepository.SaveAsync(settings with
        {
            DefaultCurrency = normalizedCurrency,
            DefaultAccountType = DefaultAccountType,
            PrivacyModeEnabled = PrivacyModeEnabled,
            ReminderFrequency = ReminderFrequency,
            DisplayLanguage = SelectedLanguage.Code
        });

        // A scalar property set after an await that may have resumed off the UI thread - same
        // rule as collection mutations (see ViewModelBase.RunOnMainThreadAsync).
        await RunOnMainThreadAsync(() =>
        {
            PrivacyMode.IsEnabled = PrivacyModeEnabled;
            Translator.SetLanguage(SelectedLanguage.Code);
            DefaultCurrencyText = normalizedCurrency;
            StatusText = Translator.Get("Common_Saved");
        });
    }
}
