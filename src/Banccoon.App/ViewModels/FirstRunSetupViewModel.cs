using System.Globalization;
using System.Windows.Input;
using Banccoon.App.Diagnostics;
using Banccoon.App.Localization;
using Banccoon.App.Services;
using Banccoon.Core.Appearance;
using Banccoon.Core.Forecasting;
using Banccoon.Core.ImportExport;
using Banccoon.Core.Repositories;
using Banccoon.Core.Setup;
using Banccoon.Core.Statements;
using Banccoon.Infrastructure.Database;

namespace Banccoon.App.ViewModels;

// Where the user goes once setup is done (step 5's choice).
public enum FirstRunSetupDestination
{
    Dashboard,
    StatementImport,
    AddAccount
}

// First-run setup, shown full-screen over a brand-new install (FirstRunSetupService decides when)
// and from a DEBUG-only button in Settings. Five steps: language (with "restore a backup
// instead"), basic settings, default categories, banks, and how to start. Nothing is saved until
// a step-5 choice, so quitting halfway leaves the install blank and setup comes back next launch.
// The language applies to the setup screens at once (and names the categories created).
public sealed class FirstRunSetupViewModel : ViewModelBase
{
    public const int StepCount = 5;

    private readonly IFirstRunSetupService setupService;
    private readonly ISettingsRepository settingsRepository;
    private int stepIndex;
    private LanguageOption selectedLanguage;
    private string currencyText = "EUR";
    private bool currencyEdited;
    private bool isApplyingDefaultCurrency;
    private AppThemeMode themeMode = AppThemeMode.System;
    private bool autoBackupEnabled = true;
    private string autoBackupFrequencyDaysText = "7";
    private FreeToSpendWindowMode windowMode = FreeToSpendWindowMode.RollingDays;
    private bool isRestoreOpen;
    private bool isBusy;
    private bool canClose;
    private string statusText = string.Empty;

    public FirstRunSetupViewModel(
        IFirstRunSetupService setupService,
        ISettingsRepository settingsRepository,
        IStatementParserRegistry parserRegistry,
        IBackupService backupService,
        ILocalDataResetService localDataResetService,
        IDatabasePathProvider databasePathProvider)
    {
        this.setupService = setupService;
        this.settingsRepository = settingsRepository;

        Languages =
        [
            new LanguageOption("en", "English"),
            new LanguageOption("ru", "Русский")
        ];
        selectedLanguage = Languages[0];

        Categories = new SetupCategoriesViewModel(() => OnPropertyChanged(nameof(CanGoNext)));
        Banks = new SetupBanksViewModel(parserRegistry);
        Restore = new DataManagementViewModel(backupService, localDataResetService, settingsRepository, databasePathProvider);
        Restore.RestoreCompleted += () => _ = OnRestoredAsync();

        NextCommand = new RelayCommand(GoNext);
        BackCommand = new RelayCommand(() => StepIndex = Math.Max(0, StepIndex - 1));
        SetLanguageEnglishCommand = new RelayCommand(() => SelectedLanguage = Languages[0]);
        SetLanguageRussianCommand = new RelayCommand(() => SelectedLanguage = Languages[1]);
        ToggleRestoreCommand = new RelayCommand(() => IsRestoreOpen = !IsRestoreOpen);
        SetThemeLightCommand = new RelayCommand(() => ThemeMode = AppThemeMode.Light);
        SetThemeDarkCommand = new RelayCommand(() => ThemeMode = AppThemeMode.Dark);
        SetThemeSystemCommand = new RelayCommand(() => ThemeMode = AppThemeMode.System);
        SetWindowRollingCommand = new RelayCommand(() => WindowMode = FreeToSpendWindowMode.RollingDays);
        SetWindowWeekCommand = new RelayCommand(() => WindowMode = FreeToSpendWindowMode.CalendarWeek);
        SetWindowMonthCommand = new RelayCommand(() => WindowMode = FreeToSpendWindowMode.CalendarMonth);
        SkipBanksCommand = new RelayCommand(() =>
        {
            Banks.ClearSelection();
            GoNext();
        });
        StartWithImportCommand = new RelayCommand(() => _ = FinishAsync(FirstRunSetupDestination.StatementImport));
        StartWithAccountCommand = new RelayCommand(() => _ = FinishAsync(FirstRunSetupDestination.AddAccount));
        StartEmptyCommand = new RelayCommand(() => _ = FinishAsync(FirstRunSetupDestination.Dashboard));
        CloseCommand = new RelayCommand(() => _ = CloseAsync());

        ApplyDefaultCurrency();
    }

    // Raised on the UI thread once setup is saved (or a backup restored); the page closes itself.
    public event Action<FirstRunSetupDestination>? Finished;

    // Raised on the UI thread when setup is closed without saving (only possible when it was
    // opened from Settings' dev button - a new install has to finish it).
    public event Action? Closed;

    // Opened from Settings (dev button): can be closed without saving anything.
    public bool CanClose
    {
        get => canClose;
        set => SetProperty(ref canClose, value);
    }

    public IReadOnlyList<LanguageOption> Languages { get; }

    public SetupCategoriesViewModel Categories { get; }

    public SetupBanksViewModel Banks { get; }

    // Step 1's "restore a backup instead" - the same restore flow as Settings -> Data.
    public DataManagementViewModel Restore { get; }

    public int StepIndex
    {
        get => stepIndex;
        private set
        {
            if (SetProperty(ref stepIndex, value))
            {
                StatusText = string.Empty;
                OnPropertyChanged(nameof(StepText));
                OnPropertyChanged(nameof(IsLanguageStep));
                OnPropertyChanged(nameof(IsSettingsStep));
                OnPropertyChanged(nameof(IsCategoriesStep));
                OnPropertyChanged(nameof(IsBanksStep));
                OnPropertyChanged(nameof(IsStartStep));
                OnPropertyChanged(nameof(CanGoBack));
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(ShowsNextButton));
            }
        }
    }

    public string StepText => string.Format(Translator.Get("Setup_StepFormat"), StepIndex + 1, StepCount);

    public bool IsLanguageStep => StepIndex == 0;

    public bool IsSettingsStep => StepIndex == 1;

    public bool IsCategoriesStep => StepIndex == 2;

    public bool IsBanksStep => StepIndex == 3;

    public bool IsStartStep => StepIndex == 4;

    public bool CanGoBack => StepIndex > 0 && !IsBusy;

    // The last step has its three start buttons instead of Next.
    public bool ShowsNextButton => StepIndex < StepCount - 1;

    public bool CanGoNext => !IsBusy && (!IsCategoriesStep || Categories.CanContinue);

    public LanguageOption SelectedLanguage
    {
        get => selectedLanguage;
        set
        {
            if (value is null || !SetProperty(ref selectedLanguage, value))
            {
                return;
            }

            Translator.SetLanguage(value.Code);
            OnPropertyChanged(nameof(IsEnglishSelected));
            OnPropertyChanged(nameof(IsRussianSelected));
            OnPropertyChanged(nameof(StepText));
            Categories.RefreshLanguage();
            ApplyDefaultCurrency();
        }
    }

    public bool IsEnglishSelected => SelectedLanguage.Code == "en";

    public bool IsRussianSelected => SelectedLanguage.Code == "ru";

    // Follows the language (RUB for Russian, EUR otherwise) until the user types their own.
    public string CurrencyText
    {
        get => currencyText;
        set
        {
            if (SetProperty(ref currencyText, value) && !isApplyingDefaultCurrency)
            {
                currencyEdited = true;
            }
        }
    }

    // Applied at once (like Settings' theme buttons) and saved at the end.
    public AppThemeMode ThemeMode
    {
        get => themeMode;
        private set
        {
            if (SetProperty(ref themeMode, value))
            {
                ApplyTheme(value);
                OnPropertyChanged(nameof(IsThemeLightSelected));
                OnPropertyChanged(nameof(IsThemeDarkSelected));
                OnPropertyChanged(nameof(IsThemeSystemSelected));
            }
        }
    }

    public bool IsThemeLightSelected => ThemeMode == AppThemeMode.Light;

    public bool IsThemeDarkSelected => ThemeMode == AppThemeMode.Dark;

    public bool IsThemeSystemSelected => ThemeMode == AppThemeMode.System;

    public bool AutoBackupEnabled
    {
        get => autoBackupEnabled;
        set => SetProperty(ref autoBackupEnabled, value);
    }

    public string AutoBackupFrequencyDaysText
    {
        get => autoBackupFrequencyDaysText;
        set => SetProperty(ref autoBackupFrequencyDaysText, value);
    }

    public FreeToSpendWindowMode WindowMode
    {
        get => windowMode;
        private set
        {
            if (SetProperty(ref windowMode, value))
            {
                OnPropertyChanged(nameof(IsWindowRollingSelected));
                OnPropertyChanged(nameof(IsWindowWeekSelected));
                OnPropertyChanged(nameof(IsWindowMonthSelected));
            }
        }
    }

    public bool IsWindowRollingSelected => WindowMode == FreeToSpendWindowMode.RollingDays;

    public bool IsWindowWeekSelected => WindowMode == FreeToSpendWindowMode.CalendarWeek;

    public bool IsWindowMonthSelected => WindowMode == FreeToSpendWindowMode.CalendarMonth;

    public bool IsRestoreOpen
    {
        get => isRestoreOpen;
        private set => SetProperty(ref isRestoreOpen, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetProperty(ref isBusy, value))
            {
                OnPropertyChanged(nameof(CanGoBack));
                OnPropertyChanged(nameof(CanGoNext));
            }
        }
    }

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public ICommand NextCommand { get; }

    public ICommand BackCommand { get; }

    public ICommand SetLanguageEnglishCommand { get; }

    public ICommand SetLanguageRussianCommand { get; }

    public ICommand ToggleRestoreCommand { get; }

    public ICommand SetThemeLightCommand { get; }

    public ICommand SetThemeDarkCommand { get; }

    public ICommand SetThemeSystemCommand { get; }

    public ICommand SetWindowRollingCommand { get; }

    public ICommand SetWindowWeekCommand { get; }

    public ICommand SetWindowMonthCommand { get; }

    public ICommand SkipBanksCommand { get; }

    public ICommand StartWithImportCommand { get; }

    public ICommand StartWithAccountCommand { get; }

    public ICommand StartEmptyCommand { get; }

    public ICommand CloseCommand { get; }

    // Starts from the app's current language (normally English on a new install; the saved one
    // when opened again from Settings). UI-thread callers only (the page's constructor).
    public async Task InitializeAsync()
    {
        var settings = await settingsRepository.GetAsync();
        await RunOnMainThreadAsync(() =>
        {
            SelectedLanguage = Languages.FirstOrDefault(language => language.Code == settings.DisplayLanguage) ?? Languages[0];
            StepIndex = 0;
        });
    }

    private void GoNext()
    {
        if (!CanGoNext)
        {
            if (IsCategoriesStep)
            {
                StatusText = Translator.Get("Setup_ChooseParentsFirst");
            }

            return;
        }

        if (IsSettingsStep && ValidateSettings() is { } problem)
        {
            StatusText = problem;
            return;
        }

        StepIndex = Math.Min(StepCount - 1, StepIndex + 1);
    }

    private string? ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(CurrencyText))
        {
            return Translator.Get("Accounts_CurrencyRequired");
        }

        if (AutoBackupEnabled && (!int.TryParse(AutoBackupFrequencyDaysText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days) || days < 1))
        {
            return Translator.Get("Setup_BackupDaysInvalid");
        }

        return null;
    }

    private async Task FinishAsync(FirstRunSetupDestination destination)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var days = int.TryParse(AutoBackupFrequencyDaysText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 7;
            var request = new FirstRunSetupRequest(
                SelectedLanguage.Code,
                CurrencyText,
                ThemeMode,
                AutoBackupEnabled,
                days,
                WindowMode,
                Categories.BuildRequest(),
                Banks.SelectedParserIds);
            await setupService.CompleteAsync(request);
            await RunOnMainThreadAsync(() => Finished?.Invoke(destination));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write($"First-run setup failed to save: {ex}");
            await RunOnMainThreadAsync(() => StatusText = string.Format(Translator.Get("Setup_SaveFailedFormat"), ex.Message));
        }
        finally
        {
            await RunOnMainThreadAsync(() => IsBusy = false);
        }
    }

    // The backup brought its own settings: switch to its language and theme and leave setup.
    private async Task OnRestoredAsync()
    {
        try
        {
            await setupService.MarkCompletedAsync();
            var settings = await settingsRepository.GetAsync();
            await RunOnMainThreadAsync(() =>
            {
                Translator.SetLanguage(settings.DisplayLanguage);
                ApplyTheme(settings.ThemeMode);
                IconPreference.Instance.ShowIcons = settings.ShowIcons;
                Finished?.Invoke(FirstRunSetupDestination.Dashboard);
            });
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write($"First-run setup: finishing after a restore failed: {ex}");
        }
    }

    // Nothing saved: put the language and theme back to what's saved, then close.
    private async Task CloseAsync()
    {
        var settings = await settingsRepository.GetAsync();
        await RunOnMainThreadAsync(() =>
        {
            Translator.SetLanguage(settings.DisplayLanguage);
            ApplyTheme(settings.ThemeMode);
            Closed?.Invoke();
        });
    }

    private void ApplyDefaultCurrency()
    {
        if (currencyEdited)
        {
            return;
        }

        isApplyingDefaultCurrency = true;
        CurrencyText = SelectedLanguage.Code == "ru" ? "RUB" : "EUR";
        isApplyingDefaultCurrency = false;
    }

    private static void ApplyTheme(AppThemeMode mode)
    {
        if (Application.Current is null)
        {
            return;
        }

        Application.Current.UserAppTheme = mode switch
        {
            AppThemeMode.Light => AppTheme.Light,
            AppThemeMode.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };
    }
}
