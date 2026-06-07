using System.Windows.Input;
using Banccoon.Core.Appearance;

namespace Banccoon.App.ViewModels;

public sealed class ShellViewModel : ViewModelBase
{
    private AppSection selectedSection = AppSection.Dashboard;
    private bool isNavigationExpanded = true;
    private NavigationStyle navigationStyle = UiPreferences.Default.NavigationStyle;
    private AppThemeMode themeMode = UiPreferences.Default.ThemeMode;
    private AccentColor accentColor = UiPreferences.Default.AccentColor;
    private bool showPowerUserFeatures = UiPreferences.Default.ShowPowerUserFeatures;

    public ShellViewModel()
    {
        NavigationItems =
        [
            new(AppSection.Dashboard, "Dashboard", "D", "Today, obligations, and safe-to-spend."),
            new(AppSection.Accounts, "Accounts", "A", "Balances, cards, cash, and savings."),
            new(AppSection.Transactions, "Transactions", "T", "Manual and grouped spending entries."),
            new(AppSection.Scheduled, "Scheduled", "S", "Recurring income, bills, and reminders."),
            new(AppSection.Forecast, "Forecast", "F", "Future balances and lowest balance points."),
            new(AppSection.Analytics, "Analytics", "N", "Trends, categories, and money patterns."),
            new(AppSection.Preferences, "Preferences", "P", "Theme, navigation, privacy, and defaults.")
        ];

        SelectSectionCommand = new RelayCommand<AppSection>(SelectSection);
        ToggleNavigationCommand = new RelayCommand(ToggleNavigation);
        UseRailNavigationCommand = new RelayCommand(() => NavigationStyle = NavigationStyle.Rail);
        UseCompactNavigationCommand = new RelayCommand(() => NavigationStyle = NavigationStyle.CompactRail);
        UseTopTabsCommand = new RelayCommand(() => NavigationStyle = NavigationStyle.TopTabs);

        UpdateSelectedNavigationItem();
    }

    public IReadOnlyList<NavigationItemViewModel> NavigationItems { get; }

    public ICommand SelectSectionCommand { get; }

    public ICommand ToggleNavigationCommand { get; }

    public ICommand UseRailNavigationCommand { get; }

    public ICommand UseCompactNavigationCommand { get; }

    public ICommand UseTopTabsCommand { get; }

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
                OnPropertyChanged(nameof(IsScheduledSelected));
                OnPropertyChanged(nameof(IsForecastSelected));
                OnPropertyChanged(nameof(IsAnalyticsSelected));
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

    public string SelectedTitle => SelectedItem.Title;

    public string SelectedDescription => SelectedItem.Description;

    public bool IsDashboardSelected => SelectedSection == AppSection.Dashboard;

    public bool IsAccountsSelected => SelectedSection == AppSection.Accounts;

    public bool IsTransactionsSelected => SelectedSection == AppSection.Transactions;

    public bool IsScheduledSelected => SelectedSection == AppSection.Scheduled;

    public bool IsForecastSelected => SelectedSection == AppSection.Forecast;

    public bool IsAnalyticsSelected => SelectedSection == AppSection.Analytics;

    public bool IsPreferencesSelected => SelectedSection == AppSection.Preferences;

    private NavigationItemViewModel SelectedItem => NavigationItems.First(item => item.Section == SelectedSection);

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
}
