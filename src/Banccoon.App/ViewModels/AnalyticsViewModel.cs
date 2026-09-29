using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.App.Localization;
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
    private string donutCaptionText = Translator.Get("Dashboard_HoverSliceShare");
    private AnalyticsCategoryRowViewModel? breakdownParent;
    private AnalyticsCategoryRowViewModel? highlightedBreakdownSegment;
    private string breakdownCaptionText = string.Empty;
    private bool hasBreakdowns;

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
        BreakdownSegments = [];

        PreviousMonthCommand = new RelayCommand(() => _ = ChangeMonthAsync(-1));
        NextMonthCommand = new RelayCommand(() => _ = ChangeMonthAsync(1));
        SelectDonutSegmentCommand = new RelayCommand<AnalyticsCategoryRowViewModel>(SelectDonutSegment);
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

    // The second donut, to the right of the first: one parent category's total broken down into
    // its own share and each child's. Opened by clicking a parent's slice (DonutChartView's
    // SegmentClickedCommand); a parent with no children does nothing. Clicking the same slice
    // again closes it.
    public ObservableCollection<AnalyticsCategoryRowViewModel> BreakdownSegments { get; }

    public bool IsBreakdownOpen => breakdownParent is not null;

    // Some slice this month can be broken down - shows the "click a slice" hint.
    public bool HasBreakdowns
    {
        get => hasBreakdowns;
        private set => SetProperty(ref hasBreakdowns, value);
    }

    public AnalyticsCategoryRowViewModel? HighlightedBreakdownSegment
    {
        get => highlightedBreakdownSegment;
        set
        {
            if (SetProperty(ref highlightedBreakdownSegment, value))
            {
                UpdateBreakdownCaption();
            }
        }
    }

    public string BreakdownCaptionText
    {
        get => breakdownCaptionText;
        private set => SetProperty(ref breakdownCaptionText, value);
    }

    public ICommand SelectDonutSegmentCommand { get; }

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
        // Just the months the report covers - older ones come from the database, not memory.
        var anchorMonth = new DateOnly(periodAnchor.Year, periodAnchor.Month, 1);
        var transactions = await transactionRepository.GetInRangeAsync(
            anchorMonth.AddMonths(-(TrendPeriodCount - 1)),
            anchorMonth.AddMonths(1).AddDays(-1),
            cancellationToken);
        var categories = await categoryRepository.GetAllAsync(cancellationToken);
        var categoriesById = categories.ToDictionary(category => category.Id);
        var report = analyticsService.BuildReport(periodAnchor, TrendPeriodCount, transactions, categories);

        // Mutates collections bound to live UI after awaits that may have resumed off the UI
        // thread (see ViewModelBase.RunOnMainThreadAsync).
        await RunOnMainThreadAsync(() =>
        {
            MonthLabel = report.CurrentPeriodStart.ToString("MMMM yyyy", CultureInfo.CurrentUICulture);
            IncomeText = MoneyFormat.Format(report.CurrentPeriodIncome, currency);
            ExpenseText = MoneyFormat.Format(-report.CurrentPeriodExpense, currency);
            NetText = MoneyFormat.Format(report.CurrentPeriodIncome - report.CurrentPeriodExpense, currency);

            CategoryRows.Clear();
            foreach (var trend in report.CategoryTrends)
            {
                var color = ResolveColor(trend.CategoryId, categoriesById);
                CategoryRows.Add(new AnalyticsCategoryRowViewModel(trend, color, currency, onCategorySelected, BuildBreakdown(trend, color)));
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

            // A new month: reopen the same parent's breakdown if it still has one, else close it.
            var previousParentId = breakdownParent?.CategoryId;
            HasBreakdowns = DonutSegments.Any(row => row.HasBreakdown);
            OpenBreakdown(DonutSegments.FirstOrDefault(row => previousParentId is not null && row.CategoryId == previousParentId && row.HasBreakdown));

            var anchorMonth = new DateOnly(periodAnchor.Year, periodAnchor.Month, 1);
            var todayMonth = new DateOnly(today.Year, today.Month, 1);
            CanGoToNextMonth = anchorMonth < todayMonth;
        });
    }

    private void SelectDonutSegment(AnalyticsCategoryRowViewModel? segment)
    {
        if (segment is not { HasBreakdown: true })
        {
            return;
        }

        OpenBreakdown(ReferenceEquals(segment, breakdownParent) ? null : segment);
    }

    // UI-thread only.
    private void OpenBreakdown(AnalyticsCategoryRowViewModel? parent)
    {
        breakdownParent = parent;
        BreakdownSegments.Clear();
        if (parent is not null)
        {
            foreach (var entry in parent.Breakdown.Where(entry => entry.Amount > 0m))
            {
                BreakdownSegments.Add(entry);
            }
        }

        HighlightedBreakdownSegment = null;
        UpdateBreakdownCaption();
        OnPropertyChanged(nameof(IsBreakdownOpen));
    }

    private void UpdateBreakdownCaption()
    {
        if (breakdownParent is null)
        {
            BreakdownCaptionText = string.Empty;
            return;
        }

        if (HighlightedBreakdownSegment is not { } segment)
        {
            BreakdownCaptionText = $"{breakdownParent.CategoryName}: {breakdownParent.CurrentTotalText}";
            return;
        }

        var share = breakdownParent.Amount > 0m ? segment.Amount / breakdownParent.Amount : 0m;
        BreakdownCaptionText = $"{segment.CategoryName}: {segment.CurrentTotalText} ({share:P0})";
    }

    // The second donut's slices for a parent: its own share (named "Food (no subcategory)") and
    // each child's, in shades of the parent's color - children share it, so the slices need
    // telling apart.
    private IReadOnlyList<AnalyticsCategoryRowViewModel> BuildBreakdown(AnalyticsCategoryTrend trend, Color parentColor)
    {
        var parentName = trend.CategoryName ?? string.Empty;
        var count = trend.Breakdown.Count;
        return trend.Breakdown
            .Select((entry, index) => new AnalyticsCategoryRowViewModel(
                entry,
                Shade(parentColor, index, count),
                currency,
                onCategorySelected,
                displayName: entry.IsParentOwnShare
                    ? string.Format(Translator.Get("Dashboard_ParentOwnShareFormat"), parentName)
                    : null))
            .ToList();
    }

    // From a little darker to clearly lighter than the parent's own color, evenly spread.
    private static Color Shade(Color color, int index, int count)
    {
        if (count <= 1)
        {
            return color;
        }

        var luminosity = 0.28f + (0.42f * index / (count - 1));
        return color.WithLuminosity(luminosity);
    }

    private void UpdateDonutCaption()
    {
        if (HighlightedSegment is null)
        {
            DonutCaptionText = donutTotal > 0m
                ? string.Format(Translator.Get("Dashboard_TotalSpendFormat"), MoneyFormat.Format(donutTotal, currency))
                : Translator.Get("Dashboard_NoExpensesRecordedYet");
            return;
        }

        var share = donutTotal > 0m ? HighlightedSegment.Amount / donutTotal : 0m;
        DonutCaptionText = $"{HighlightedSegment.CategoryName}: {HighlightedSegment.CurrentTotalText} ({share:P0})";
    }

    private static Color ResolveColor(Guid? categoryId, IReadOnlyDictionary<Guid, Category> categoriesById)
    {
        if (categoryId is { } id)
        {
            // A child's color is its parent's (and a trend here is normally a parent anyway).
            return categoriesById.TryGetValue(id, out var category)
                ? CategoryColorPalette.GetColorForCategory(category.ColorSourceId, category.Color)
                : CategoryColorPalette.GetColorForCategory(id, null);
        }

        return CategoryColorPalette.GetTransferColor();
    }
}
