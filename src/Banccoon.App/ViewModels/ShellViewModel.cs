using System.Windows.Input;
using Banccoon.Core.Appearance;
using Banccoon.Core.Repositories;

namespace Banccoon.App.ViewModels;

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
    private bool isWorkflowOverlayOpen;
    private string workflowOverlayTitle = string.Empty;
    private string workflowOverlayMessage = string.Empty;
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
        CloseWorkflowOverlayCommand = new RelayCommand(CloseWorkflowOverlay);

        UpdateSelectedNavigationItem();
    }

    public FinanceDataViewModel Data { get; }

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

    public ICommand CloseWorkflowOverlayCommand { get; }

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
                OnPropertyChanged(nameof(IsGoalsSelected));
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

    public bool IsWorkflowOverlayOpen
    {
        get => isWorkflowOverlayOpen;
        private set => SetProperty(ref isWorkflowOverlayOpen, value);
    }

    public string WorkflowOverlayTitle
    {
        get => workflowOverlayTitle;
        private set => SetProperty(ref workflowOverlayTitle, value);
    }

    public string WorkflowOverlayMessage
    {
        get => workflowOverlayMessage;
        private set => SetProperty(ref workflowOverlayMessage, value);
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

    public bool IsStatementsSelected => SelectedSection == AppSection.Statements;

    public bool IsScheduledSelected => SelectedSection == AppSection.Scheduled;

    public bool IsGoalsSelected => SelectedSection == AppSection.Goals;

    public bool IsForecastSelected => SelectedSection == AppSection.Forecast;

    public bool IsReconciliationSelected => SelectedSection == AppSection.Reconciliation;

    public bool IsAnalyticsSelected => SelectedSection == AppSection.Analytics;

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
        WorkflowOverlayTitle = title;
        WorkflowOverlayMessage = message;
        IsWorkflowOverlayOpen = true;
    }

    private void CloseWorkflowOverlay()
    {
        IsWorkflowOverlayOpen = false;
        WorkflowOverlayTitle = string.Empty;
        WorkflowOverlayMessage = string.Empty;
    }

    private void SelectSection(AppSection section)
    {
        SelectedSection = section;
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
