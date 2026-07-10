using System.Windows.Input;
using Banccoon.Core.Appearance;
using Banccoon.Core.Repositories;

namespace Banccoon.App.ViewModels;

public enum ShellWorkflowKind
{
    None,
    StartupSetup,
    TransactionWorkflow
}

public sealed class ShellViewModel : ViewModelBase
{
    private AppSection selectedSection = AppSection.Dashboard;
    private bool isNavigationExpanded = true;
    private NavigationStyle navigationStyle = UiPreferences.Default.NavigationStyle;
    private AppThemeMode themeMode = UiPreferences.Default.ThemeMode;
    private AccentColor accentColor = UiPreferences.Default.AccentColor;
    private bool showPowerUserFeatures = UiPreferences.Default.ShowPowerUserFeatures;
    private string defaultCurrency = "EUR";
    private bool isLoaded;
    private bool isDashboardForecastExpanded;
    private bool isDashboardAnalyticsExpanded;
    private bool isDashboardReconciliationExpanded;
    private bool isTransactionStatementImportExpanded;
    private bool isTransactionScheduledExpanded;
    private bool startupSetupShown;
    private ShellWorkflowKind activeWorkflowKind = ShellWorkflowKind.None;
    private readonly ISettingsRepository settingsRepository;

    public ShellViewModel(FinanceDataViewModel data, ISettingsRepository settingsRepository)
    {
        Data = data;
        this.settingsRepository = settingsRepository;

        NavigationItems =
        [
            new(AppSection.Dashboard, "Dashboard", "D", "Today, obligations, and safe-to-spend."),
            new(AppSection.Transactions, "Transactions", "T", "Manual and grouped spending entries."),
            new(AppSection.Accounts, "Accounts", "A", "Balances, cards, cash, and savings."),
            new(AppSection.Settings, "Settings", "S", "Preferences, backups, restores, and local data control.")
        ];

        SelectSectionCommand = new RelayCommand<AppSection>(SelectSection);
        ToggleNavigationCommand = new RelayCommand(ToggleNavigation);
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        ToggleDashboardForecastCommand = new RelayCommand(() => IsDashboardForecastExpanded = !IsDashboardForecastExpanded);
        ToggleDashboardAnalyticsCommand = new RelayCommand(() => IsDashboardAnalyticsExpanded = !IsDashboardAnalyticsExpanded);
        ToggleDashboardReconciliationCommand = new RelayCommand(() => IsDashboardReconciliationExpanded = !IsDashboardReconciliationExpanded);
        ToggleTransactionStatementImportCommand = new RelayCommand(() => IsTransactionStatementImportExpanded = !IsTransactionStatementImportExpanded);
        ToggleTransactionScheduledCommand = new RelayCommand(() => IsTransactionScheduledExpanded = !IsTransactionScheduledExpanded);
        OpenExpenseTransactionWorkflowCommand = new RelayCommand(() => OpenTransactionWorkflow(TransactionWorkflowKind.Expense));
        OpenIncomeTransactionWorkflowCommand = new RelayCommand(() => OpenTransactionWorkflow(TransactionWorkflowKind.Income));
        OpenTransferTransactionWorkflowCommand = new RelayCommand(() => OpenTransactionWorkflow(TransactionWorkflowKind.Transfer));
        OpenScheduledWorkflowCommand = new RelayCommand(() => OpenTransactionWorkflow(TransactionWorkflowKind.Scheduled));
        OpenStatementImportWorkflowCommand = new RelayCommand(() => OpenTransactionWorkflow(TransactionWorkflowKind.StatementImport));
        OpenStartupStatementImportCommand = new RelayCommand(() => OpenTransactionWorkflow(TransactionWorkflowKind.StatementImport));
        OpenStartupManualSetupCommand = new RelayCommand(() => OpenTransactionWorkflow(TransactionWorkflowKind.ManualSetup));
        OpenStartupBackupRestoreCommand = new RelayCommand(() => OpenTransactionWorkflow(TransactionWorkflowKind.BackupRestore));

        UpdateSelectedNavigationItem();
    }

    public FinanceDataViewModel Data { get; }

    public WorkflowOverlayViewModel WorkflowOverlay { get; } = new();

    public IReadOnlyList<NavigationItemViewModel> NavigationItems { get; }

    public IReadOnlyList<NavigationStyle> NavigationStyles { get; } = Enum.GetValues<NavigationStyle>();

    public IReadOnlyList<AppThemeMode> ThemeModes { get; } = Enum.GetValues<AppThemeMode>();

    public IReadOnlyList<AccentColor> AccentColors { get; } = Enum.GetValues<AccentColor>();

    public IReadOnlyList<string> SupportedCurrencies { get; } =
    [
        "EUR",
        "USD",
        "GBP",
        "PLN",
        "CZK",
        "CHF",
        "NOK",
        "SEK",
        "DKK",
        "JPY",
        "CAD",
        "AUD",
        "RUB"
    ];

    public ICommand SelectSectionCommand { get; }

    public ICommand ToggleNavigationCommand { get; }

    public ICommand SaveSettingsCommand { get; }

    public ICommand ToggleDashboardForecastCommand { get; }

    public ICommand ToggleDashboardAnalyticsCommand { get; }

    public ICommand ToggleDashboardReconciliationCommand { get; }

    public ICommand ToggleTransactionStatementImportCommand { get; }

    public ICommand ToggleTransactionScheduledCommand { get; }

    public ICommand OpenExpenseTransactionWorkflowCommand { get; }

    public ICommand OpenIncomeTransactionWorkflowCommand { get; }

    public ICommand OpenTransferTransactionWorkflowCommand { get; }

    public ICommand OpenScheduledWorkflowCommand { get; }

    public ICommand OpenStatementImportWorkflowCommand { get; }

    public ICommand OpenStartupStatementImportCommand { get; }

    public ICommand OpenStartupManualSetupCommand { get; }

    public ICommand OpenStartupBackupRestoreCommand { get; }

    public AppSection SelectedSection
    {
        get => selectedSection;
        set
        {
            if (SetProperty(ref selectedSection, value))
            {
                UpdateSelectedNavigationItem();
                OnPropertyChanged(nameof(SelectedTitle));
                OnPropertyChanged(nameof(SelectedDescription));
                OnPropertyChanged(nameof(IsDashboardSelected));
                OnPropertyChanged(nameof(IsAccountsSelected));
                OnPropertyChanged(nameof(IsTransactionsSelected));
                OnPropertyChanged(nameof(IsSettingsSelected));
                OnPropertyChanged(nameof(IsStatementsSelected));
                OnPropertyChanged(nameof(IsScheduledSelected));
                OnPropertyChanged(nameof(IsForecastSelected));
                OnPropertyChanged(nameof(IsReconciliationSelected));
                OnPropertyChanged(nameof(IsAnalyticsSelected));
                OnPropertyChanged(nameof(IsDataSelected));
                OnPropertyChanged(nameof(IsPreferencesSelected));
            }
        }
    }

    public bool IsNavigationExpanded
    {
        get => isNavigationExpanded;
        set
        {
            if (SetProperty(ref isNavigationExpanded, value))
            {
                OnPropertyChanged(nameof(NavigationColumnWidth));
                OnPropertyChanged(nameof(IsMenuButtonVisible));
            }
        }
    }

    public NavigationStyle NavigationStyle
    {
        get => navigationStyle;
        set
        {
            if (SetProperty(ref navigationStyle, value))
            {
                IsNavigationExpanded = value == NavigationStyle.Rail;
                OnPropertyChanged(nameof(IsRailMode));
                OnPropertyChanged(nameof(IsTopTabsMode));
                OnPropertyChanged(nameof(IsMenuButtonVisible));
                OnPropertyChanged(nameof(NavigationColumnWidth));
            }
        }
    }

    public AppThemeMode ThemeMode
    {
        get => themeMode;
        set => SetProperty(ref themeMode, value);
    }

    public AccentColor AccentColor
    {
        get => accentColor;
        set => SetProperty(ref accentColor, value);
    }

    public bool ShowPowerUserFeatures
    {
        get => showPowerUserFeatures;
        set => SetProperty(ref showPowerUserFeatures, value);
    }

    public string DefaultCurrency
    {
        get => defaultCurrency;
        set => SetProperty(ref defaultCurrency, value);
    }

    public ShellWorkflowKind ActiveWorkflowKind
    {
        get => activeWorkflowKind;
        private set
        {
            if (SetProperty(ref activeWorkflowKind, value))
            {
                OnPropertyChanged(nameof(IsStartupSetupWorkflow));
                OnPropertyChanged(nameof(IsTransactionWorkflow));
            }
        }
    }

    public bool IsStartupSetupWorkflow => ActiveWorkflowKind == ShellWorkflowKind.StartupSetup;

    public bool IsTransactionWorkflow => ActiveWorkflowKind == ShellWorkflowKind.TransactionWorkflow;

    public bool IsDashboardForecastExpanded
    {
        get => isDashboardForecastExpanded;
        set
        {
            if (SetProperty(ref isDashboardForecastExpanded, value))
            {
                OnPropertyChanged(nameof(IsForecastSelected));
            }
        }
    }

    public bool IsDashboardAnalyticsExpanded
    {
        get => isDashboardAnalyticsExpanded;
        set
        {
            if (SetProperty(ref isDashboardAnalyticsExpanded, value))
            {
                OnPropertyChanged(nameof(IsAnalyticsSelected));
            }
        }
    }

    public bool IsDashboardReconciliationExpanded
    {
        get => isDashboardReconciliationExpanded;
        set
        {
            if (SetProperty(ref isDashboardReconciliationExpanded, value))
            {
                OnPropertyChanged(nameof(IsReconciliationSelected));
            }
        }
    }

    public bool IsTransactionStatementImportExpanded
    {
        get => isTransactionStatementImportExpanded;
        set
        {
            if (SetProperty(ref isTransactionStatementImportExpanded, value))
            {
                OnPropertyChanged(nameof(IsStatementsSelected));
            }
        }
    }

    public bool IsTransactionScheduledExpanded
    {
        get => isTransactionScheduledExpanded;
        set
        {
            if (SetProperty(ref isTransactionScheduledExpanded, value))
            {
                OnPropertyChanged(nameof(IsScheduledSelected));
            }
        }
    }

    public GridLength NavigationColumnWidth
    {
        get
        {
            if (NavigationStyle == NavigationStyle.TopTabs)
            {
                return new GridLength(0);
            }

            return IsNavigationExpanded ? new GridLength(176) : new GridLength(0);
        }
    }

    public bool IsRailMode => NavigationStyle != NavigationStyle.TopTabs;

    public bool IsTopTabsMode => NavigationStyle == NavigationStyle.TopTabs;

    public bool IsMenuButtonVisible => NavigationStyle != NavigationStyle.Rail;

    public string SelectedTitle => GetSectionTitle(SelectedSection);

    public string SelectedDescription => GetSectionDescription(SelectedSection);

    public bool IsDashboardSelected => SelectedSection == AppSection.Dashboard;

    public bool IsAccountsSelected => SelectedSection == AppSection.Accounts;

    public bool IsTransactionsSelected => SelectedSection == AppSection.Transactions;

    public bool IsSettingsSelected => SelectedSection == AppSection.Settings;

    public bool IsStatementsSelected => IsTransactionsSelected && IsTransactionStatementImportExpanded;

    public bool IsScheduledSelected => IsTransactionsSelected && IsTransactionScheduledExpanded;

    public bool IsForecastSelected => IsDashboardSelected && IsDashboardForecastExpanded;

    public bool IsReconciliationSelected => IsDashboardSelected && IsDashboardReconciliationExpanded;

    public bool IsAnalyticsSelected => IsDashboardSelected && IsDashboardAnalyticsExpanded;

    public bool IsDataSelected => SelectedSection == AppSection.Settings;

    public bool IsPreferencesSelected => SelectedSection == AppSection.Settings;

    public async Task LoadAsync()
    {
        if (isLoaded)
        {
            return;
        }

        await Data.LoadAsync();
        await LoadSettingsAsync();
        isLoaded = true;
        if (Data.IsBlankDataset && !startupSetupShown)
        {
            OpenStartupSetupWorkflow();
        }
    }

    private async Task LoadSettingsAsync()
    {
        var settings = await settingsRepository.GetAsync();
        themeMode = settings.ThemeMode;
        accentColor = settings.AccentColor;
        showPowerUserFeatures = settings.ShowPowerUserFeatures;
        NavigationStyle = settings.NavigationStyle;
        OnPropertyChanged(nameof(ThemeMode));
        OnPropertyChanged(nameof(AccentColor));
        OnPropertyChanged(nameof(ShowPowerUserFeatures));
    }

    private async Task SaveSettingsAsync()
    {
        await Data.SavePreferencesAsync(
            ThemeMode,
            AccentColor,
            NavigationStyle,
            ShowPowerUserFeatures);
    }

    public void OpenWorkflowOverlay(string title, string message)
    {
        ActiveWorkflowKind = ShellWorkflowKind.None;
        WorkflowOverlay.Open(title, message);
    }

    private void OpenStartupSetupWorkflow()
    {
        startupSetupShown = true;
        ActiveWorkflowKind = ShellWorkflowKind.StartupSetup;
        Data.ActiveTransactionWorkflow = TransactionWorkflowKind.None;
        WorkflowOverlay.Open(
            "Set up Banccoon",
            "Choose the fastest path to create your local financial workspace.",
            canDismiss: false);
    }

    private void OpenTransactionWorkflow(TransactionWorkflowKind workflowKind)
    {
        SelectedSection = workflowKind is TransactionWorkflowKind.ManualSetup or TransactionWorkflowKind.BackupRestore
            ? SelectedSection
            : AppSection.Transactions;
        ActiveWorkflowKind = ShellWorkflowKind.TransactionWorkflow;
        Data.PrepareTransactionWorkflow(workflowKind);
        var title = workflowKind switch
        {
            TransactionWorkflowKind.Expense => "New expense",
            TransactionWorkflowKind.Income => "New income",
            TransactionWorkflowKind.Transfer => "New transfer",
            TransactionWorkflowKind.Scheduled => "Scheduled transactions",
            TransactionWorkflowKind.StatementImport => "Statement import",
            TransactionWorkflowKind.ManualSetup => "Manual setup",
            TransactionWorkflowKind.BackupRestore => "Restore backup",
            _ => "Workflow"
        };
        var message = workflowKind switch
        {
            TransactionWorkflowKind.Expense => "Record money leaving an account.",
            TransactionWorkflowKind.Income => "Record money entering an account.",
            TransactionWorkflowKind.Transfer => "Move money from one account to another account or goal.",
            TransactionWorkflowKind.Scheduled => "Create and manage recurring income, bills, and transfers.",
            TransactionWorkflowKind.StatementImport => "Import a statement and review detected rows.",
            TransactionWorkflowKind.ManualSetup => "Create the first account without opening the full Accounts screen.",
            TransactionWorkflowKind.BackupRestore => "Validate and restore a Banccoon JSON backup.",
            _ => string.Empty
        };
        WorkflowOverlay.Open(title, message, stepTitle: title);
    }

    private void SelectSection(AppSection section)
    {
        switch (section)
        {
            case AppSection.Statements:
                SelectedSection = AppSection.Transactions;
                IsTransactionStatementImportExpanded = true;
                break;
            case AppSection.Scheduled:
                SelectedSection = AppSection.Transactions;
                IsTransactionScheduledExpanded = true;
                break;
            case AppSection.Goals:
                SelectedSection = AppSection.Accounts;
                break;
            case AppSection.Forecast:
                SelectedSection = AppSection.Dashboard;
                IsDashboardForecastExpanded = true;
                break;
            case AppSection.Reconciliation:
                SelectedSection = AppSection.Dashboard;
                IsDashboardReconciliationExpanded = true;
                break;
            case AppSection.Analytics:
                SelectedSection = AppSection.Dashboard;
                IsDashboardAnalyticsExpanded = true;
                break;
            case AppSection.Data:
            case AppSection.Preferences:
                SelectedSection = AppSection.Settings;
                break;
            default:
                SelectedSection = section;
                break;
        }
    }

    private void ToggleNavigation()
    {
        NavigationStyle = NavigationStyle == NavigationStyle.Rail
            ? NavigationStyle.CompactRail
            : NavigationStyle.Rail;
    }

    private void UpdateSelectedNavigationItem()
    {
        foreach (var item in NavigationItems)
        {
            item.IsSelected = item.Section == SelectedSection;
        }
    }

    private static string GetSectionTitle(AppSection section)
    {
        return section switch
        {
            AppSection.Dashboard => "Dashboard",
            AppSection.Transactions => "Transactions",
            AppSection.Accounts => "Accounts",
            AppSection.Settings => "Settings",
            AppSection.Statements => "Statement import",
            AppSection.Scheduled => "Scheduled",
            AppSection.Goals => "Goals",
            AppSection.Forecast => "Forecast",
            AppSection.Reconciliation => "Reconciliation",
            AppSection.Analytics => "Analytics",
            AppSection.Data => "Settings",
            AppSection.Preferences => "Settings",
            _ => "Banccoon"
        };
    }

    private static string GetSectionDescription(AppSection section)
    {
        return section switch
        {
            AppSection.Dashboard => "Today, obligations, and safe-to-spend.",
            AppSection.Transactions => "Manual entries, scheduled items, imports, and transaction history.",
            AppSection.Accounts => "Balances, cards, cash, savings, and goals.",
            AppSection.Settings => "Preferences, backups, restores, and local data control.",
            AppSection.Statements => "Review bank statement imports before applying them.",
            AppSection.Scheduled => "Recurring income, bills, and reminders.",
            AppSection.Goals => "Savings reservations that reduce safe-to-spend.",
            AppSection.Forecast => "Future balances and lowest balance points.",
            AppSection.Reconciliation => "Check expected events against real balances.",
            AppSection.Analytics => "Trends, categories, and money patterns.",
            AppSection.Data => "Portable backups, restores, and local data control.",
            AppSection.Preferences => "Theme, navigation, privacy, and defaults.",
            _ => string.Empty
        };
    }
}
