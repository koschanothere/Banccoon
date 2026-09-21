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
    private readonly IAccountRepository accountRepository;

    private Guid batchId;
    private Guid accountId;
    private string currency = "EUR";
    private bool selectMode;
    private CategoryOptionViewModel? bulkCategory;
    private string newBulkCategoryName = string.Empty;
    private string statusText = string.Empty;
    private string selectionSummaryText = string.Empty;

    public StatementImportReviewViewModel(
        IStatementImportService statementImportService,
        IStatementImportRepository statementImportRepository,
        ICategoryRepository categoryRepository,
        IAccountRepository accountRepository)
    {
        this.statementImportService = statementImportService;
        this.statementImportRepository = statementImportRepository;
        this.categoryRepository = categoryRepository;
        this.accountRepository = accountRepository;

        Rows = [];
        CategoryOptions = [];
        OtherAccountOptions = [];

        ToggleSelectModeCommand = new RelayCommand(ToggleSelectMode);
        ApproveSelectedCommand = new RelayCommand(() => _ = ApproveSelectedAsync());
        SkipSelectedCommand = new RelayCommand(() => _ = SkipSelectedAsync());
        CancelImportCommand = new RelayCommand(() => _ = CancelImportAsync());
    }

    public ObservableCollection<StatementImportRowViewModel> Rows { get; }

    public ObservableCollection<CategoryOptionViewModel> CategoryOptions { get; }

    // Every other tracked account, offered as the "other side" when a row is marked Transfer.
    public ObservableCollection<NamedOptionViewModel> OtherAccountOptions { get; }

    public bool IsComplete => Rows.Count == 0;

    public bool SelectMode
    {
        get => selectMode;
        private set => SetProperty(ref selectMode, value);
    }

    public CategoryOptionViewModel? BulkCategory
    {
        get => bulkCategory;
        set
        {
            if (SetProperty(ref bulkCategory, value))
            {
                OnPropertyChanged(nameof(IsCreatingNewBulkCategory));
            }
        }
    }

    public bool IsCreatingNewBulkCategory => BulkCategory?.IsCreateNew == true;

    public string NewBulkCategoryName
    {
        get => newBulkCategoryName;
        set => SetProperty(ref newBulkCategoryName, value);
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

    public ICommand ApproveSelectedCommand { get; }

    public ICommand SkipSelectedCommand { get; }

    public ICommand CancelImportCommand { get; }

    public async Task LoadAsync(Guid batch, Guid batchAccountId, string currencyCode, CancellationToken cancellationToken = default)
    {
        batchId = batch;
        accountId = batchAccountId;
        currency = currencyCode;

        // This method is itself called after another ViewModel's await chain (see
        // ViewModelBase.RunOnMainThreadAsync), so even this "before my own first await" mutation
        // isn't guaranteed to be on the UI thread.
        await RunOnMainThreadAsync(() => StatusText = string.Empty);

        var categories = await categoryRepository.GetAllAsync(cancellationToken);
        var accounts = await accountRepository.GetAllAsync(cancellationToken);
        var otherAccounts = accounts
            .Where(account => !account.IsArchived && account.Id != accountId)
            .OrderBy(account => account.Name)
            .ToList();

        await RunOnMainThreadAsync(() =>
        {
            CategoryOptionsHelper.Repopulate(CategoryOptions, categories);

            OtherAccountOptions.Clear();
            foreach (var account in otherAccounts)
            {
                OtherAccountOptions.Add(new NamedOptionViewModel(account.Id, account.Name));
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
                var rowViewModel = new StatementImportRowViewModel(row, currency, CategoryOptions, OtherAccountOptions, ApproveRowAsync, SkipRowAsync)
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
        if (row.IsCreatingNewCategory && string.IsNullOrWhiteSpace(row.NewCategoryName))
        {
            await RunOnMainThreadAsync(() => StatusText = "Name the new category first.");
            return;
        }

        var (categoryId, newOption) = await CategoryOptionsHelper.ResolveOrCreateAsync(row.Category, row.NewCategoryName, categoryRepository);
        if (newOption is not null)
        {
            await RunOnMainThreadAsync(() => CategoryOptionsHelper.InsertBeforeSentinel(CategoryOptions, newOption));
        }

        await statementImportService.ApproveRowAsync(row.Id, categoryId, row.Type, row.OtherAccount?.Id);
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
        if (IsCreatingNewBulkCategory && string.IsNullOrWhiteSpace(NewBulkCategoryName))
        {
            await RunOnMainThreadAsync(() => StatusText = "Name the new category first.");
            return;
        }

        var selected = Rows.Where(row => row.IsSelected).ToList();

        // A bulk category (including "create new") wins over each row's own pick, matching the
        // original BulkCategory?.Id ?? row.Category?.Id fallback - but a bulk "create new" is
        // resolved once, up front, so every selected row lands in the same new category rather
        // than creating one per row.
        var (bulkCategoryId, newBulkOption) = await CategoryOptionsHelper.ResolveOrCreateAsync(BulkCategory, NewBulkCategoryName, categoryRepository);
        if (newBulkOption is not null)
        {
            await RunOnMainThreadAsync(() => CategoryOptionsHelper.InsertBeforeSentinel(CategoryOptions, newBulkOption));
        }

        foreach (var row in selected)
        {
            Guid? categoryId = bulkCategoryId;
            if (categoryId is null)
            {
                var (rowCategoryId, newRowOption) = await CategoryOptionsHelper.ResolveOrCreateAsync(row.Category, row.NewCategoryName, categoryRepository);
                if (newRowOption is not null)
                {
                    await RunOnMainThreadAsync(() => CategoryOptionsHelper.InsertBeforeSentinel(CategoryOptions, newRowOption));
                }

                categoryId = rowCategoryId;
            }

            await statementImportService.ApproveRowAsync(row.Id, categoryId, row.Type, row.OtherAccount?.Id);
        }

        // Touches UI-bound state after an await that may have resumed off the UI thread (see
        // ViewModelBase.RunOnMainThreadAsync).
        await RunOnMainThreadAsync(() =>
        {
            SelectMode = false;
            BulkCategory = null;
            NewBulkCategoryName = string.Empty;
        });
        await RefreshRowsAsync();
    }

    private async Task SkipSelectedAsync()
    {
        var selected = Rows.Where(row => row.IsSelected).ToList();
        foreach (var row in selected)
        {
            await statementImportService.SkipRowAsync(row.Id);
        }

        await RunOnMainThreadAsync(() => SelectMode = false);
        await RefreshRowsAsync();
    }

    private async Task CancelImportAsync()
    {
        var result = await statementImportService.CancelImportAsync(batchId);

        await RunOnMainThreadAsync(() =>
        {
            StatusText = result.Message;
            if (result.Cancelled)
            {
                Rows.Clear();
                OnPropertyChanged(nameof(IsComplete));
            }
        });
    }
}
