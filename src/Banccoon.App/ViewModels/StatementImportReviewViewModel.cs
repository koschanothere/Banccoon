using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using Banccoon.App.Diagnostics;
using Banccoon.App.Formatting;
using Banccoon.App.Localization;
using Banccoon.Core.Repositories;
using Banccoon.Core.Statements;

namespace Banccoon.App.ViewModels;

public sealed class StatementImportReviewViewModel : ViewModelBase
{
    // A big statement is drawn this many rows at a time, with a short pause between steps so the
    // window keeps up with clicks and scrolling (see StatementImportReviewSectionsViewModel.DrawNext).
    private const int RowsPerDrawStep = 20;
    private static readonly TimeSpan DrawPause = TimeSpan.FromMilliseconds(40);

    private readonly IStatementImportService statementImportService;
    private readonly IStatementImportRepository statementImportRepository;
    private readonly ICategoryRepository categoryRepository;
    private readonly IAccountRepository accountRepository;

    // Every approve/skip (single row or bulk) runs one at a time, in click order. Each one reads an
    // account, applies a transaction and saves the account back - two running concurrently (easy
    // now that rows no longer vanish-and-rebuild between clicks, so approving row after row quickly
    // is the natural flow) could both read the same starting balance and silently lose one update.
    private readonly SemaphoreSlim actionGate = new(1, 1);

    private Guid batchId;
    private Guid accountId;
    private string currency = "EUR";
    private string statusText = string.Empty;
    private int totalRowCount;
    private bool isCancelled;
    private CancellationTokenSource? drawing;

    // The rows the most recent approve/skip action reviewed (one row, or every row of a bulk
    // action), for Undo. Collected while an action runs, published when it finishes.
    private List<StatementImportRowViewModel> currentActionRows = [];
    private IReadOnlyList<StatementImportRowViewModel>? lastActionRows;

    // Whether the running action approved anything - approvals are what teach Banccoon new rules.
    private bool currentActionApproved;

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
        Bulk = new StatementImportBulkActionsViewModel(this);
        Sections = new StatementImportReviewSectionsViewModel(this);
        Categories = new StatementImportCategoriesViewModel(this, categoryRepository);

        CancelImportCommand = new RelayCommand(() => _ = CancelImportAsync());
        UndoCommand = new RelayCommand(() => _ = UndoAsync());
    }

    // Raised (on the UI thread) when the last pending row has been approved or skipped - i.e. the
    // batch is fully reviewed and its statement closing balance has just become the account's
    // balance (see StatementImportService.CompleteBatchIfReviewedAsync).
    public event Action? ReviewCompleted;

    public ObservableCollection<StatementImportRowViewModel> Rows { get; }

    public ObservableCollection<CategoryOptionViewModel> CategoryOptions { get; }

    // Every other tracked account, offered as the "other side" when a row is marked Transfer.
    public ObservableCollection<NamedOptionViewModel> OtherAccountOptions { get; }

    public StatementImportBulkActionsViewModel Bulk { get; }

    public StatementImportReviewSectionsViewModel Sections { get; }

    public StatementImportCategoriesViewModel Categories { get; }

    // Rows reviewed by the running action and by the last finished one - both can come back
    // through Undo. UI-thread only.
    public IEnumerable<StatementImportRowViewModel> RecentlyReviewedRows => currentActionRows.Concat(lastActionRows ?? []);

    public Guid AccountId => accountId;

    public string Currency => currency;

    // Every row reviewed (not cancelled) - the batch is complete and its closing balance applied.
    public bool IsComplete => Rows.Count == 0 && totalRowCount > 0 && !isCancelled;

    public bool HasRows => Rows.Count > 0;

    public string ProgressText => string.Format(
        Translator.Get("StatementImport_ProgressFormat"),
        totalRowCount - Rows.Count,
        totalRowCount);

    // Undo is only offered while the batch is still under review: the last row's approval applies
    // the statement's closing balance, which can't be unwound (see IStatementImportService.UndoReviewAsync).
    public bool CanUndo => lastActionRows is { Count: > 0 } && Rows.Count > 0;

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public ICommand CancelImportCommand { get; }

    public ICommand UndoCommand { get; }

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

        await LoadRowsAsync(cancellationToken);
    }

    // Runs an approve/skip action behind the shared gate (see actionGate), clearing any old status
    // first and turning a failure into a visible message instead of an unobserved exception from a
    // fire-and-forget command. After approvals, what they taught is passed on to the rows still
    // waiting (ApplyLatestLearningAsync) before the next action can start.
    public async Task RunExclusiveAsync(Func<Task> action)
    {
        await actionGate.WaitAsync();
        currentActionRows = [];
        currentActionApproved = false;
        try
        {
            await SetStatusAsync(string.Empty);
            await RunReportingFailureAsync(action);

            // Even a half-finished bulk action can be undone for the rows it did get through.
            var reviewed = currentActionRows;
            if (reviewed.Count > 0)
            {
                await RunOnMainThreadAsync(() =>
                {
                    lastActionRows = reviewed;
                    currentActionRows = [];
                    OnPropertyChanged(nameof(CanUndo));
                });
            }

            if (currentActionApproved && Rows.Count > 0)
            {
                await RunReportingFailureAsync(ApplyLatestLearningAsync);
            }
        }
        finally
        {
            actionGate.Release();
        }
    }

    public Task SetStatusAsync(string text)
    {
        return RunOnMainThreadAsync(() => StatusText = text);
    }

    // Only called from inside RunExclusiveAsync.
    public async Task ApproveAsync(StatementImportRowViewModel row, Guid? bulkCategoryId)
    {
        var categoryId = bulkCategoryId ?? await Categories.ResolveAsync(row.Category, row.NewCategoryName);
        await statementImportService.ApproveRowAsync(row.Id, categoryId, row.Type, row.OtherAccount?.Id);
        currentActionRows.Add(row);
        currentActionApproved = true;
        await RemoveReviewedRowAsync(row);
    }

    // Only called from inside RunExclusiveAsync.
    public async Task SkipAsync(StatementImportRowViewModel row)
    {
        await statementImportService.SkipRowAsync(row.Id);
        currentActionRows.Add(row);
        await RemoveReviewedRowAsync(row);
    }

    // Returns null when every row can be approved as-is, otherwise the (translated) reason one
    // can't - checked before calling the service, which would otherwise throw.
    public async Task<string?> GetApprovalProblemAsync(IReadOnlyList<StatementImportRowViewModel> rows, bool checkRowCategories = true)
    {
        string? problem = null;
        await RunOnMainThreadAsync(() =>
        {
            if (checkRowCategories && rows.Any(row => row.IsCreatingNewCategory && string.IsNullOrWhiteSpace(row.NewCategoryName)))
            {
                problem = Translator.Get("StatementImport_NameNewCategoryFirst");
            }
            else if (rows.Any(row => row.IsTransferType && row.OtherAccount is null))
            {
                problem = Translator.Get("StatementImport_ChooseOtherAccountFirst");
            }
        });

        return problem;
    }

    // The only full rebuild of Rows - on first load. Every later action removes just the rows it
    // acted on (RemoveReviewedRowAsync): a rebuild recreates every row from its persisted (unedited)
    // data, which would silently throw away the category/type/other-account picks the user has
    // made on every row they haven't approved yet.
    private async Task LoadRowsAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var rows = await statementImportRepository.GetRowsByBatchIdAsync(batchId, cancellationToken);
        var pendingRows = rows
            .Where(row => row.Status == StatementImportRowStatus.Pending)
            .OrderBy(row => row.Date)
            .ToList();

        var moreToDraw = false;
        CancellationTokenSource? newDrawing = null;
        await RunOnMainThreadAsync(() =>
        {
            ClearRows();
            totalRowCount = rows.Count;
            isCancelled = false;
            lastActionRows = null;
            var rowViewModels = pendingRows
                .Select(row => new StatementImportRowViewModel(row, currency, CategoryOptions, OtherAccountOptions, ApproveRowAsync, SkipRowAsync, Categories.CreateForRowAsync))
                .ToList();
            foreach (var rowViewModel in rowViewModels)
            {
                Rows.Add(rowViewModel);
                Bulk.OnRowAdded(rowViewModel);
            }

            Sections.QueueRows(rowViewModels);
            moreToDraw = Sections.DrawNext(RowsPerDrawStep);
            OnRowsChanged();
            if (moreToDraw)
            {
                newDrawing = new CancellationTokenSource();
                drawing = newDrawing;
            }
        });

        DiagnosticLog.Write($"Statement import timing: first {Math.Min(pendingRows.Count, RowsPerDrawStep)} of {pendingRows.Count} rows on screen after {stopwatch.ElapsedMilliseconds} ms");
        if (newDrawing is not null)
        {
            _ = DrawRemainingRowsAsync(pendingRows.Count, stopwatch, newDrawing.Token);
        }
    }

    // Fire-and-forget from LoadRowsAsync; stopped by ClearRows (a new load, or Cancel import).
    private async Task DrawRemainingRowsAsync(int rowCount, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        try
        {
            var moreToDraw = true;
            while (moreToDraw)
            {
                await Task.Delay(DrawPause, cancellationToken);
                await RunOnMainThreadAsync(() => moreToDraw = !cancellationToken.IsCancellationRequested && Sections.DrawNext(RowsPerDrawStep));
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                DiagnosticLog.Write($"Statement import timing: all {rowCount} rows on screen after {stopwatch.ElapsedMilliseconds} ms");
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write($"Statement import review: drawing rows failed: {ex}");
        }
    }

    private async Task ApproveRowAsync(StatementImportRowViewModel row)
    {
        await RunRowActionAsync(row, async () =>
        {
            if (await GetApprovalProblemAsync([row]) is { } problem)
            {
                await SetStatusAsync(problem);
                return;
            }

            await ApproveAsync(row, bulkCategoryId: null);
        });
    }

    // Re-fills every row the user hasn't touched with what Banccoon would suggest now - the approval
    // that just ran may have taught it a new rule, or changed one (see
    // StatementImportReviewSectionsViewModel.ApplySuggestions). Also offers any category the
    // approval created (Core's "Other" fallback).
    private async Task ApplyLatestLearningAsync()
    {
        await Categories.SyncAsync();

        // Off the UI thread: it re-scores every pending row, which on a long statement is real work.
        var suggestions = (await Task.Run(() => statementImportService.GetPendingSuggestionsAsync(batchId)))
            .ToDictionary(suggestion => suggestion.RowId);

        await RunOnMainThreadAsync(() => Sections.ApplySuggestions(suggestions));
    }

    private async Task RunReportingFailureAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write($"Statement import review action failed: {ex}");
            await SetStatusAsync(Translator.Get("StatementImport_ActionFailed"));
        }
    }

    private Task SkipRowAsync(StatementImportRowViewModel row)
    {
        return RunRowActionAsync(row, () => SkipAsync(row));
    }

    // Marks the row busy on the UI thread before anything else. Commands start on the UI thread,
    // where RunOnMainThreadAsync runs inline - so a second click on the same row (queued behind
    // this one on the UI thread) always finds the flag already set and is ignored, rather than
    // starting a second approve of a row that's still Pending in the database.
    private async Task RunRowActionAsync(StatementImportRowViewModel row, Func<Task> action)
    {
        var started = false;
        await RunOnMainThreadAsync(() => started = row.TryBeginAction());
        if (!started)
        {
            return;
        }

        try
        {
            await RunExclusiveAsync(action);
        }
        finally
        {
            await RunOnMainThreadAsync(row.EndAction);
        }
    }

    private async Task RemoveReviewedRowAsync(StatementImportRowViewModel row)
    {
        await RunOnMainThreadAsync(() =>
        {
            Rows.Remove(row);
            Bulk.OnRowRemoved(row);
            Sections.OnRowRemoved(row);
            OnRowsChanged();

            if (Rows.Count == 0)
            {
                ReviewCompleted?.Invoke();
            }
        });
    }

    // Puts every row the last action reviewed back into the list, in reverse order, via the
    // service (which deletes an approved row's transaction and reverses its balance effect). The
    // same row view models go back in, so their picks are exactly as the user left them.
    private async Task UndoAsync()
    {
        await actionGate.WaitAsync();
        try
        {
            var rowsToRestore = lastActionRows;
            if (rowsToRestore is not { Count: > 0 } || Rows.Count == 0)
            {
                return;
            }

            await RunOnMainThreadAsync(() =>
            {
                StatusText = string.Empty;
                lastActionRows = null;
                OnPropertyChanged(nameof(CanUndo));
            });

            foreach (var row in rowsToRestore.Reverse())
            {
                await statementImportService.UndoReviewAsync(row.Id);
                await RunOnMainThreadAsync(() => InsertRow(row));
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write($"Statement import review undo failed: {ex}");
            await SetStatusAsync(Translator.Get("StatementImport_ActionFailed"));
        }
        finally
        {
            actionGate.Release();
        }
    }

    // UI-thread only. Rows stays in date order, same as the initial load.
    private void InsertRow(StatementImportRowViewModel row)
    {
        row.PrepareForReinsert();
        var index = 0;
        while (index < Rows.Count && Rows[index].Date <= row.Date)
        {
            index++;
        }

        Rows.Insert(index, row);
        Bulk.OnRowAdded(row);
        Sections.OnRowAdded(row);
        OnRowsChanged();
    }

    private void ClearRows()
    {
        drawing?.Cancel();
        drawing = null;
        foreach (var row in Rows)
        {
            Bulk.OnRowRemoved(row);
        }

        Rows.Clear();
        Sections.Clear();
    }

    private void OnRowsChanged()
    {
        OnPropertyChanged(nameof(IsComplete));
        OnPropertyChanged(nameof(HasRows));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(CanUndo));
        Bulk.OnRowsChanged();
    }

    private async Task CancelImportAsync()
    {
        var result = await statementImportService.CancelImportAsync(batchId);

        await RunOnMainThreadAsync(() =>
        {
            StatusText = StatementImportMessageFormatter.Format(result.Message);
            if (result.Cancelled)
            {
                isCancelled = true;
                lastActionRows = null;
                ClearRows();
                OnRowsChanged();
            }
        });
    }
}
