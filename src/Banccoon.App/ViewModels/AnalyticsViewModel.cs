using System.Collections.ObjectModel;
using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.Core.Abstractions;
using Banccoon.Core.Analytics;
using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Microsoft.Maui.Graphics;

namespace Banccoon.App.ViewModels;

public sealed class AnalyticsViewModel : ViewModelBase
{
    private const int TrendPeriodCount = 6;

    private readonly IDateProvider dateProvider;
    private readonly ITransactionRepository transactionRepository;
    private readonly ICategoryRepository categoryRepository;
    private readonly IAnalyticsService analyticsService;
    private readonly Func<Guid?, Task> onCategorySelected;

    private DateOnly periodAnchor;
    private DateOnly today;
    private string currency = "EUR";
    private string monthLabel = string.Empty;
    private string incomeText = string.Empty;
    private string expenseText = string.Empty;
    private string netText = string.Empty;
    private bool canGoToNextMonth;
    private decimal donutTotal;
    private AnalyticsCategoryRowViewModel? highlightedSegment;
    private string donutCaptionText = "Hover a slice to see its share.";

    public AnalyticsViewModel(
        IDateProvider dateProvider,
        ITransactionRepository transactionRepository,
        ICategoryRepository categoryRepository,
        IAnalyticsService analyticsService,
        Func<Guid?, Task> onCategorySelected)
    {
        this.dateProvider = dateProvider;
        this.transactionRepository = transactionRepository;
        this.categoryRepository = categoryRepository;
        this.analyticsService = analyticsService;
        this.onCategorySelected = onCategorySelected;

        CategoryRows = [];
        TopMovers = [];
        DonutSegments = [];

        PreviousMonthCommand = new RelayCommand(() => _ = ChangeMonthAsync(-1));
        NextMonthCommand = new RelayCommand(() => _ = ChangeMonthAsync(1));
    }

    public string MonthLabel
    {
        get => monthLabel;
        private set => SetProperty(ref monthLabel, value);
    }

    public string IncomeText
    {
        get => incomeText;
        private set => SetProperty(ref incomeText, value);
    }

    public string ExpenseText
    {
        get => expenseText;
        private set => SetProperty(ref expenseText, value);
    }

    public string NetText
    {
        get => netText;
        private set => SetProperty(ref netText, value);
    }

    public bool CanGoToNextMonth
    {
        get => canGoToNextMonth;
        private set => SetProperty(ref canGoToNextMonth, value);
    }

    public ObservableCollection<AnalyticsCategoryRowViewModel> CategoryRows { get; }

    public ObservableCollection<AnalyticsCategoryRowViewModel> TopMovers { get; }

    public ObservableCollection<AnalyticsCategoryRowViewModel> DonutSegments { get; }

    // Two-way: the donut chart control sets this on hover, and clears it (null) on pointer-exit.
    public AnalyticsCategoryRowViewModel? HighlightedSegment
    {
        get => highlightedSegment;
        set
        {
            if (SetProperty(ref highlightedSegment, value))
            {
                UpdateDonutCaption();
            }
        }
    }

    public string DonutCaptionText
    {
        get => donutCaptionText;
        private set => SetProperty(ref donutCaptionText, value);
    }

    public ICommand PreviousMonthCommand { get; }

    public ICommand NextMonthCommand { get; }

    public async Task InitializeAsync(string defaultCurrency, CancellationToken cancellationToken = default)
    {
        currency = defaultCurrency;
        today = dateProvider.Today;
        periodAnchor = today;
        await RefreshAsync(cancellationToken);
    }

    private async Task ChangeMonthAsync(int monthDelta)
    {
        periodAnchor = periodAnchor.AddMonths(monthDelta);
        await RefreshAsync();
    }

    private async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var transactions = await transactionRepository.GetAllAsync(cancellationToken);
        var categories = await categoryRepository.GetAllAsync(cancellationToken);
        var categoriesById = categories.ToDictionary(category => category.Id);
        var report = analyticsService.BuildReport(periodAnchor, TrendPeriodCount, transactions, categories);

        // Mutates collections bound to live UI after awaits that may have resumed off the UI
        // thread (see ViewModelBase.RunOnMainThreadAsync).
        await RunOnMainThreadAsync(() =>
        {
            MonthLabel = report.CurrentPeriodStart.ToString("MMMM yyyy");
            IncomeText = MoneyFormat.Format(report.CurrentPeriodIncome, currency);
            ExpenseText = MoneyFormat.Format(-report.CurrentPeriodExpense, currency);
            NetText = MoneyFormat.Format(report.CurrentPeriodIncome - report.CurrentPeriodExpense, currency);

            CategoryRows.Clear();
            foreach (var trend in report.CategoryTrends)
            {
                CategoryRows.Add(new AnalyticsCategoryRowViewModel(trend, ResolveColor(trend.CategoryId, categoriesById), currency, onCategorySelected));
            }

            TopMovers.Clear();
            foreach (var trend in report.TopMovers)
            {
                TopMovers.Add(new AnalyticsCategoryRowViewModel(trend, ResolveColor(trend.CategoryId, categoriesById), currency, onCategorySelected));
            }

            DonutSegments.Clear();
            foreach (var row in CategoryRows.Where(row => row.Amount > 0m))
            {
                DonutSegments.Add(row);
            }

            donutTotal = DonutSegments.Sum(row => row.Amount);
            HighlightedSegment = null;
            UpdateDonutCaption();

            var anchorMonth = new DateOnly(periodAnchor.Year, periodAnchor.Month, 1);
            var todayMonth = new DateOnly(today.Year, today.Month, 1);
            CanGoToNextMonth = anchorMonth < todayMonth;
        });
    }

    private void UpdateDonutCaption()
    {
        if (HighlightedSegment is null)
        {
            DonutCaptionText = donutTotal > 0m
                ? $"Total spend: {MoneyFormat.Format(donutTotal, currency)}"
                : "No expenses recorded yet.";
            return;
        }

        var share = donutTotal > 0m ? HighlightedSegment.Amount / donutTotal : 0m;
        DonutCaptionText = $"{HighlightedSegment.CategoryName}: {HighlightedSegment.CurrentTotalText} ({share:P0})";
    }

    private static Color ResolveColor(Guid? categoryId, IReadOnlyDictionary<Guid, Category> categoriesById)
    {
        if (categoryId is { } id)
        {
            var explicitColor = categoriesById.TryGetValue(id, out var category) ? category.Color : null;
            return CategoryColorPalette.GetColorForCategory(id, explicitColor);
        }

        return CategoryColorPalette.GetTransferColor();
    }
}
