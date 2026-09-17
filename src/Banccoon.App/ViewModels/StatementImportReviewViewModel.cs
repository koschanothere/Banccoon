using System.Collections.ObjectModel;
using System.Windows.Input;
using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Core.Statements;

namespace Banccoon.App.ViewModels;

public sealed class StatementImportReviewViewModel : ViewModelBase
{
    private readonly IStatementImportService statementImportService;
    private readonly IStatementImportRepository statementImportRepository;
    private readonly ICategoryRepository categoryRepository;

    private Guid batchId;
    private string currency = "EUR";
    private bool selectMode;
    private NamedOptionViewModel? bulkCategory;
    private bool isAddingCategory;
    private string newCategoryName = string.Empty;
    private string statusText = string.Empty;
    private string selectionSummaryText = string.Empty;

    public StatementImportReviewViewModel(
        IStatementImportService statementImportService,
        IStatementImportRepository statementImportRepository,
        ICategoryRepository categoryRepository)
    {
        this.statementImportService = statementImportService;
        this.statementImportRepository = statementImportRepository;
        this.categoryRepository = categoryRepository;

        Rows = [];
        CategoryOptions = [];

        ToggleSelectModeCommand = new RelayCommand(ToggleSelectMode);
        ToggleAddCategoryCommand = new RelayCommand(() => IsAddingCategory = !IsAddingCategory);
        CreateCategoryCommand = new RelayCommand(() => _ = CreateCategoryAsync());
        ApproveSelectedCommand = new RelayCommand(() => _ = ApproveSelectedAsync());
        SkipSelectedCommand = new RelayCommand(() => _ = SkipSelectedAsync());
        CancelImportCommand = new RelayCommand(() => _ = CancelImportAsync());
    }

    public ObservableCollection<StatementImportRowViewModel> Rows { get; }

    public ObservableCollection<NamedOptionViewModel> CategoryOptions { get; }

    public bool IsComplete => Rows.Count == 0;

    public bool SelectMode
    {
        get => selectMode;
        private set => SetProperty(ref selectMode, value);
    }

    public NamedOptionViewModel? BulkCategory
    {
        get => bulkCategory;
        set => SetProperty(ref bulkCategory, value);
    }

    public bool IsAddingCategory
    {
        get => isAddingCategory;
        private set => SetProperty(ref isAddingCategory, value);
    }

    public string NewCategoryName
    {
        get => newCategoryName;
        set => SetProperty(ref newCategoryName, value);
    }

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public string SelectionSummaryText
    {
        get => selectionSummaryText;
        private set => SetProperty(ref selectionSummaryText, value);
    }

    public ICommand ToggleSelectModeCommand { get; }

    public ICommand ToggleAddCategoryCommand { get; }

    public ICommand CreateCategoryCommand { get; }

    public ICommand ApproveSelectedCommand { get; }

    public ICommand SkipSelectedCommand { get; }

    public ICommand CancelImportCommand { get; }

    public async Task LoadAsync(Guid batch, string currencyCode, CancellationToken cancellationToken = default)
    {
        batchId = batch;
        currency = currencyCode;
        StatusText = string.Empty;

        var categories = await categoryRepository.GetAllAsync(cancellationToken);

        await RunOnMainThreadAsync(() =>
        {
            CategoryOptions.Clear();
            foreach (var category in categories.OrderBy(category => category.Name))
            {
                CategoryOptions.Add(new NamedOptionViewModel(category.Id, category.Name));
            }
        });

        await RefreshRowsAsync(cancellationToken);
    }

    private async Task RefreshRowsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await statementImportRepository.GetRowsByBatchIdAsync(batchId, cancellationToken);
        var pendingRows = rows
            .Where(row => row.Status == StatementImportRowStatus.Pending)
            .OrderBy(row => row.Date)
            .ToList();

        await RunOnMainThreadAsync(() =>
        {
            Rows.Clear();
            foreach (var row in pendingRows)
            {
                var rowViewModel = new StatementImportRowViewModel(row, currency, CategoryOptions, ApproveRowAsync, SkipRowAsync)
                {
                    IsSelectModeActive = SelectMode
                };
                Rows.Add(rowViewModel);
            }

            OnPropertyChanged(nameof(IsComplete));
            UpdateSelectionSummary();
        });
    }

    private async Task ApproveRowAsync(StatementImportRowViewModel row)
    {
        await statementImportService.ApproveRowAsync(row.Id, row.Category?.Id);
        await RefreshRowsAsync();
    }

    private async Task SkipRowAsync(StatementImportRowViewModel row)
    {
        await statementImportService.SkipRowAsync(row.Id);
        await RefreshRowsAsync();
    }

    private void ToggleSelectMode()
    {
        SelectMode = !SelectMode;
        foreach (var row in Rows)
        {
            row.IsSelectModeActive = SelectMode;
            if (!SelectMode)
            {
                row.IsSelected = false;
            }
        }

        UpdateSelectionSummary();
    }

    private void UpdateSelectionSummary()
    {
        var count = Rows.Count(row => row.IsSelected);
        SelectionSummaryText = count == 1 ? "1 selected" : $"{count} selected";
    }

    private async Task ApproveSelectedAsync()
    {
        var selected = Rows.Where(row => row.IsSelected).ToList();
        foreach (var row in selected)
        {
            await statementImportService.ApproveRowAsync(row.Id, BulkCategory?.Id ?? row.Category?.Id);
        }

        SelectMode = false;
        await RefreshRowsAsync();
    }

    private async Task SkipSelectedAsync()
    {
        var selected = Rows.Where(row => row.IsSelected).ToList();
        foreach (var row in selected)
        {
            await statementImportService.SkipRowAsync(row.Id);
        }

        SelectMode = false;
        await RefreshRowsAsync();
    }

    private async Task CreateCategoryAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCategoryName))
        {
            return;
        }

        var newCategory = new Category(Guid.NewGuid(), NewCategoryName.Trim());
        await categoryRepository.SaveAsync(newCategory);

        var option = new NamedOptionViewModel(newCategory.Id, newCategory.Name);
        CategoryOptions.Add(option);
        BulkCategory = option;
        IsAddingCategory = false;
        NewCategoryName = string.Empty;
    }

    private async Task CancelImportAsync()
    {
        var result = await statementImportService.CancelImportAsync(batchId);
        StatusText = result.Message;
        if (result.Cancelled)
        {
            await RunOnMainThreadAsync(() =>
            {
                Rows.Clear();
                OnPropertyChanged(nameof(IsComplete));
            });
        }
    }
}
