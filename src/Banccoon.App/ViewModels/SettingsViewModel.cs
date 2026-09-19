using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.Core.Appearance;
using Banccoon.Core.Forecasting;
using Banccoon.Core.ImportExport;
using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Infrastructure.Database;
using Microsoft.Maui.Graphics;

namespace Banccoon.App.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsRepository settingsRepository;
    private AppThemeMode themeMode = AppThemeMode.Light;
    private FreeToSpendWindowMode windowMode = FreeToSpendWindowMode.RollingDays;
    private string windowDaysText = "7";
    private string safetyBufferText = "0";
    private string majorPaymentThresholdText = "0";
    private string freeToSpendStatusText = string.Empty;
    private string resolveUpcomingNearTermDaysText = "3";
    private string resolveUpcomingStatusText = string.Empty;
    private IReadOnlyList<DashboardSection> dashboardSectionOrder = DashboardSectionOrdering.Default;
    private ForecastPeriod selectedForecastPeriod = ForecastPeriod.ThirtyDays;
    private string dashboardStatusText = string.Empty;

    public SettingsViewModel(
        ISettingsRepository settingsRepository,
        IBackupService backupService,
        ILocalDataResetService localDataResetService,
        IDatabasePathProvider databasePathProvider,
        ICategoryLearningRuleRepository categoryLearningRuleRepository,
        ICategoryRepository categoryRepository,
        IAccountRepository accountRepository)
    {
        this.settingsRepository = settingsRepository;
        Data = new DataManagementViewModel(backupService, localDataResetService, settingsRepository, databasePathProvider);
        General = new GeneralPreferencesViewModel(settingsRepository);
        AppLock = new AppLockSettingsViewModel(settingsRepository);
        LearningRules = new CategoryLearningRulesViewModel(categoryLearningRuleRepository, categoryRepository, accountRepository);
        DashboardSectionRows = [];

        SetLightCommand = new RelayCommand(() => _ = SetThemeModeAsync(AppThemeMode.Light));
        SetDarkCommand = new RelayCommand(() => _ = SetThemeModeAsync(AppThemeMode.Dark));
        SetSystemCommand = new RelayCommand(() => _ = SetThemeModeAsync(AppThemeMode.System));

        SetWindowRollingCommand = new RelayCommand(() => WindowMode = FreeToSpendWindowMode.RollingDays);
        SetWindowCalendarWeekCommand = new RelayCommand(() => WindowMode = FreeToSpendWindowMode.CalendarWeek);
        SetWindowCalendarMonthCommand = new RelayCommand(() => WindowMode = FreeToSpendWindowMode.CalendarMonth);
        SetWindowUntilMajorPaymentCommand = new RelayCommand(() => WindowMode = FreeToSpendWindowMode.UntilNextMajorPayment);
        SaveFreeToSpendCommand = new RelayCommand(() => _ = SaveFreeToSpendAsync());
        SaveResolveUpcomingCommand = new RelayCommand(() => _ = SaveResolveUpcomingAsync());
        SaveForecastPeriodCommand = new RelayCommand(() => _ = SaveForecastPeriodAsync());
    }

    public DataManagementViewModel Data { get; }

    public GeneralPreferencesViewModel General { get; }

    public AppLockSettingsViewModel AppLock { get; }

    public CategoryLearningRulesViewModel LearningRules { get; }

    public ObservableCollection<DashboardSectionRowViewModel> DashboardSectionRows { get; }

    public IReadOnlyList<ForecastPeriod> ForecastPeriods { get; } = Enum.GetValues<ForecastPeriod>();

    public ForecastPeriod SelectedForecastPeriod
    {
        get => selectedForecastPeriod;
        set => SetProperty(ref selectedForecastPeriod, value);
    }

    public string DashboardStatusText
    {
        get => dashboardStatusText;
        private set => SetProperty(ref dashboardStatusText, value);
    }

    public ICommand SaveForecastPeriodCommand { get; }

    // Read-only preview of the fixed category-color palette (docs/visual-design-language.md).
    // Not all 7 colors need to be interactive here - this is just visibility into the scheme;
    // the actual hex values still live in code (CategoryColorPalette), not in Settings/the DB.
    public IReadOnlyList<Color> CategoryColorPreview { get; } =
        CategoryColorPalette.AllColors.Select(CategoryColorPalette.GetColor).ToArray();

    public AppThemeMode ThemeMode
    {
        get => themeMode;
        private set
        {
            if (SetProperty(ref themeMode, value))
            {
                OnPropertyChanged(nameof(IsLightSelected));
                OnPropertyChanged(nameof(IsDarkSelected));
                OnPropertyChanged(nameof(IsSystemSelected));
            }
        }
    }

    public bool IsLightSelected => ThemeMode == AppThemeMode.Light;

    public bool IsDarkSelected => ThemeMode == AppThemeMode.Dark;

    public bool IsSystemSelected => ThemeMode == AppThemeMode.System;

    public ICommand SetLightCommand { get; }

    public ICommand SetDarkCommand { get; }

    public ICommand SetSystemCommand { get; }

    public FreeToSpendWindowMode WindowMode
    {
        get => windowMode;
        private set
        {
            if (SetProperty(ref windowMode, value))
            {
                OnPropertyChanged(nameof(IsWindowRollingSelected));
                OnPropertyChanged(nameof(IsWindowCalendarWeekSelected));
                OnPropertyChanged(nameof(IsWindowCalendarMonthSelected));
                OnPropertyChanged(nameof(IsWindowUntilMajorPaymentSelected));
                OnPropertyChanged(nameof(IsRollingDaysFieldVisible));
                OnPropertyChanged(nameof(IsMajorPaymentThresholdFieldVisible));
            }
        }
    }

    public bool IsWindowRollingSelected => WindowMode == FreeToSpendWindowMode.RollingDays;

    public bool IsWindowCalendarWeekSelected => WindowMode == FreeToSpendWindowMode.CalendarWeek;

    public bool IsWindowCalendarMonthSelected => WindowMode == FreeToSpendWindowMode.CalendarMonth;

    public bool IsWindowUntilMajorPaymentSelected => WindowMode == FreeToSpendWindowMode.UntilNextMajorPayment;

    public bool IsRollingDaysFieldVisible => IsWindowRollingSelected;

    public bool IsMajorPaymentThresholdFieldVisible => IsWindowUntilMajorPaymentSelected;

    public string WindowDaysText
    {
        get => windowDaysText;
        set => SetProperty(ref windowDaysText, value);
    }

    public string SafetyBufferText
    {
        get => safetyBufferText;
        set => SetProperty(ref safetyBufferText, value);
    }

    public string MajorPaymentThresholdText
    {
        get => majorPaymentThresholdText;
        set => SetProperty(ref majorPaymentThresholdText, value);
    }

    public string FreeToSpendStatusText
    {
        get => freeToSpendStatusText;
        private set => SetProperty(ref freeToSpendStatusText, value);
    }

    public ICommand SetWindowRollingCommand { get; }

    public ICommand SetWindowCalendarWeekCommand { get; }

    public ICommand SetWindowCalendarMonthCommand { get; }

    public ICommand SetWindowUntilMajorPaymentCommand { get; }

    public ICommand SaveFreeToSpendCommand { get; }

    public string ResolveUpcomingNearTermDaysText
    {
        get => resolveUpcomingNearTermDaysText;
        set => SetProperty(ref resolveUpcomingNearTermDaysText, value);
    }

    public string ResolveUpcomingStatusText
    {
        get => resolveUpcomingStatusText;
        private set => SetProperty(ref resolveUpcomingStatusText, value);
    }

    public ICommand SaveResolveUpcomingCommand { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var settings = await settingsRepository.GetAsync(cancellationToken);

        // Touches UI-bound state (and, via ApplyTheme, a native platform property) after an await
        // that may have resumed off the UI thread (see ViewModelBase.RunOnMainThreadAsync).
        await RunOnMainThreadAsync(() =>
        {
            ThemeMode = settings.ThemeMode;
            ApplyTheme(settings.ThemeMode);

            WindowMode = settings.FreeToSpendWindowMode;
            WindowDaysText = settings.FreeToSpendWindowDays.ToString(CultureInfo.InvariantCulture);
            SafetyBufferText = settings.SafetyBuffer.ToString(CultureInfo.InvariantCulture);
            MajorPaymentThresholdText = settings.MajorPaymentThreshold.ToString(CultureInfo.InvariantCulture);
            FreeToSpendStatusText = string.Empty;

            ResolveUpcomingNearTermDaysText = settings.ResolveUpcomingNearTermDays.ToString(CultureInfo.InvariantCulture);
            ResolveUpcomingStatusText = string.Empty;

            SelectedForecastPeriod = settings.DefaultForecastPeriod;
            DashboardStatusText = string.Empty;
            dashboardSectionOrder = DashboardSectionOrdering.Parse(settings.DashboardSectionOrder);
            RebuildDashboardSectionRows();

            PrivacyMode.IsEnabled = settings.PrivacyModeEnabled;
        });

        await General.InitializeAsync(settings);
        await AppLock.InitializeAsync(settings);
        await Data.InitializeAsync(settings);
        await LearningRules.InitializeAsync(cancellationToken);
    }

    private void RebuildDashboardSectionRows()
    {
        DashboardSectionRows.Clear();
        for (var index = 0; index < dashboardSectionOrder.Count; index++)
        {
            DashboardSectionRows.Add(new DashboardSectionRowViewModel(
                dashboardSectionOrder[index],
                canMoveUp: index > 0,
                canMoveDown: index < dashboardSectionOrder.Count - 1,
                onMoveUp: section => _ = MoveDashboardSectionAsync(section, -1),
                onMoveDown: section => _ = MoveDashboardSectionAsync(section, 1)));
        }
    }

    private async Task MoveDashboardSectionAsync(DashboardSection section, int direction)
    {
        var order = dashboardSectionOrder.ToList();
        var index = order.IndexOf(section);
        var targetIndex = index + direction;
        if (index < 0 || targetIndex < 0 || targetIndex >= order.Count)
        {
            return;
        }

        (order[index], order[targetIndex]) = (order[targetIndex], order[index]);
        dashboardSectionOrder = order;
        RebuildDashboardSectionRows();

        var settings = await settingsRepository.GetAsync();
        await settingsRepository.SaveAsync(settings with { DashboardSectionOrder = DashboardSectionOrdering.Format(order) });
    }

    private async Task SaveForecastPeriodAsync()
    {
        var settings = await settingsRepository.GetAsync();
        await settingsRepository.SaveAsync(settings with { DefaultForecastPeriod = SelectedForecastPeriod });

        await RunOnMainThreadAsync(() => DashboardStatusText = "Saved.");
    }

    private async Task SetThemeModeAsync(AppThemeMode mode)
    {
        var settings = await settingsRepository.GetAsync();
        await settingsRepository.SaveAsync(settings with { ThemeMode = mode });

        await RunOnMainThreadAsync(() =>
        {
            ThemeMode = mode;
            ApplyTheme(mode);
        });
    }

    private async Task SaveFreeToSpendAsync()
    {
        if (!int.TryParse(WindowDaysText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var windowDays) || windowDays < 1)
        {
            FreeToSpendStatusText = "Rolling days must be a whole number of at least 1.";
            return;
        }

        if (!decimal.TryParse(SafetyBufferText, NumberStyles.Number, CultureInfo.InvariantCulture, out var safetyBuffer) || safetyBuffer < 0m)
        {
            FreeToSpendStatusText = "Safety buffer must be a number of 0 or more.";
            return;
        }

        if (!decimal.TryParse(MajorPaymentThresholdText, NumberStyles.Number, CultureInfo.InvariantCulture, out var majorPaymentThreshold) || majorPaymentThreshold < 0m)
        {
            FreeToSpendStatusText = "Major payment threshold must be a number of 0 or more.";
            return;
        }

        var settings = await settingsRepository.GetAsync();
        await settingsRepository.SaveAsync(settings with
        {
            FreeToSpendWindowMode = WindowMode,
            FreeToSpendWindowDays = windowDays,
            SafetyBuffer = safetyBuffer,
            MajorPaymentThreshold = majorPaymentThreshold
        });

        await RunOnMainThreadAsync(() => FreeToSpendStatusText = "Saved.");
    }

    private async Task SaveResolveUpcomingAsync()
    {
        if (!int.TryParse(ResolveUpcomingNearTermDaysText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var nearTermDays) || nearTermDays < 0)
        {
            ResolveUpcomingStatusText = "Must be a whole number of 0 or more.";
            return;
        }

        var settings = await settingsRepository.GetAsync();
        await settingsRepository.SaveAsync(settings with { ResolveUpcomingNearTermDays = nearTermDays });

        await RunOnMainThreadAsync(() => ResolveUpcomingStatusText = "Saved.");
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
