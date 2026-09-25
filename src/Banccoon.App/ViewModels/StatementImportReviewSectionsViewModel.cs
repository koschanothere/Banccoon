using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Banccoon.App.Localization;
using Banccoon.Core.Statements;

namespace Banccoon.App.ViewModels;

// The statement import review list, split into three blocks (composed into
// StatementImportReviewViewModel as Review.Sections) so a 40-row statement reads as a handful of
// real decisions instead of 40 identical rows:
// - possible duplicates, at the top, with "Skip all duplicates" (re-importing an overlapping or
//   half-reviewed statement flags every already-recorded row here, so this also covers resuming);
// - rows Banccoon already categorised from what it learned - collapsed by default, and grouped by
//   sender name unless "Group by name" is unticked (see StatementImportRowGroupViewModel);
// - rows that need a decision (nothing learned yet, or a transfer with no learned other account).
// "Approve all categorised" approves every non-duplicate row that is ready as it stands, in either
// of the last two blocks, so categorising a few "needs attention" rows and then approving
// everything at once works too. A row's block is decided when it's loaded, and only changes when a
// newer suggestion fills in a row the user hasn't touched (ApplySuggestions) - rows never jump
// between blocks while being edited.
//
// Only a few rows are ever on screen (each is heavy: three pickers). Every row is loaded, counted
// and approvable from the start, but the duplicates and needs-a-decision blocks share room for
// RowsPerStep rows, duplicates first; as rows are approved or skipped the next ones slide in, and
// "Show more" makes room for another RowsPerStep. The ready block draws nothing while collapsed,
// and when open only the view that's showing - RowsPerStep rows (flat) or groups (grouped) - so a
// row is never drawn twice (a second, hidden copy of each row's picker was what let MAUI reset the
// categories of a group's rows when it was opened, 2026-09-25). Nothing is drawn in the background
// - that froze the window every few seconds on a big statement.
public sealed class StatementImportReviewSectionsViewModel : ViewModelBase
{
    public const int RowsPerStep = 20;

    private readonly StatementImportReviewViewModel review;

    // Every loaded row this listens to, drawn or not. UI-thread only.
    private readonly HashSet<StatementImportRowViewModel> trackedRows = [];

    // Every ready group in name order; ReadyGroups shows the first readyShownLimit of them.
    private readonly List<StatementImportRowGroupViewModel> allGroups = [];

    private int shownLimit = RowsPerStep;
    private int readyShownLimit = RowsPerStep;
    private bool isReadyExpanded;
    private bool isGroupedByName = true;

    public StatementImportReviewSectionsViewModel(StatementImportReviewViewModel review)
    {
        this.review = review;

        DuplicateRows = [];
        AttentionRows = [];
        ReadyRows = [];
        ReadyGroups = [];

        ToggleReadyExpandedCommand = new RelayCommand(() => IsReadyExpanded = !IsReadyExpanded);
        ApproveAllCategorisedCommand = new RelayCommand(() => _ = ApproveReadyRowsAsync(review.Rows.ToList()));
        SkipAllDuplicatesCommand = new RelayCommand(() => _ = SkipAllDuplicatesAsync());
        ShowMoreCommand = new RelayCommand(() =>
        {
            shownLimit += RowsPerStep;
            SyncShownRows();
        });
        ShowMoreReadyCommand = new RelayCommand(() =>
        {
            readyShownLimit += RowsPerStep;
            SyncShownRows();
        });
    }

    public ObservableCollection<StatementImportRowViewModel> DuplicateRows { get; }

    public ObservableCollection<StatementImportRowViewModel> AttentionRows { get; }

    // The ready block as a flat, date-ordered list ("Group by name" off)...
    public ObservableCollection<StatementImportRowViewModel> ReadyRows { get; }

    // ...and as groups in name order ("Group by name" on). Only the one on screen is filled.
    public ObservableCollection<StatementImportRowGroupViewModel> ReadyGroups { get; }

    public bool HasDuplicates => CountIn(StatementImportRowSection.Duplicate) > 0;

    public bool HasAttention => CountIn(StatementImportRowSection.Attention) > 0;

    public bool HasReady => CountIn(StatementImportRowSection.Ready) > 0;

    public string DuplicatesHeaderText => string.Format(Translator.Get("StatementImport_DuplicatesHeaderFormat"), CountIn(StatementImportRowSection.Duplicate));

    public string AttentionHeaderText => string.Format(Translator.Get("StatementImport_AttentionHeaderFormat"), CountIn(StatementImportRowSection.Attention));

    public string ReadyHeaderText => string.Format(Translator.Get("StatementImport_ReadyHeaderFormat"), CountIn(StatementImportRowSection.Ready));

    public bool HasHiddenDuplicates => HiddenDuplicates > 0;

    public string ShowMoreDuplicatesText => ShowMoreText(HiddenDuplicates);

    public bool HasHiddenAttention => HiddenAttention > 0;

    public string ShowMoreAttentionText => ShowMoreText(HiddenAttention);

    public bool HasHiddenReady => IsReadyExpanded && HiddenReady > 0;

    public string ShowMoreReadyText => ShowMoreText(HiddenReady);

    public bool IsReadyExpanded
    {
        get => isReadyExpanded;
        private set
        {
            if (SetProperty(ref isReadyExpanded, value))
            {
                OnPropertyChanged(nameof(IsReadyCollapsed));
                SyncShownRows();
            }
        }
    }

    public bool IsReadyCollapsed => !IsReadyExpanded;

    public bool IsGroupedByName
    {
        get => isGroupedByName;
        set
        {
            if (SetProperty(ref isGroupedByName, value))
            {
                SyncShownRows();
            }
        }
    }

    public bool IsReadyListFlat => IsReadyExpanded && !IsGroupedByName;

    public bool IsReadyListGrouped => IsReadyExpanded && IsGroupedByName;

    public int CategorisedCount => review.Rows.Count(row => row.IsReadyToApprove);

    public bool CanApproveAllCategorised => CategorisedCount > 0;

    public string ApproveAllCategorisedText => string.Format(Translator.Get("StatementImport_ApproveAllCategorisedFormat"), CategorisedCount);

    public ICommand ToggleReadyExpandedCommand { get; }

    public ICommand ApproveAllCategorisedCommand { get; }

    public ICommand SkipAllDuplicatesCommand { get; }

    public ICommand ShowMoreCommand { get; }

    public ICommand ShowMoreReadyCommand { get; }

    private int HiddenDuplicates => CountIn(StatementImportRowSection.Duplicate) - DuplicateRows.Count;

    private int HiddenAttention => CountIn(StatementImportRowSection.Attention) - AttentionRows.Count;

    private int HiddenReady => IsGroupedByName
        ? allGroups.Count - ReadyGroups.Count
        : CountIn(StatementImportRowSection.Ready) - ReadyRows.Count;

    // Every method below is UI-thread only - called by the parent from inside its own
    // RunOnMainThreadAsync blocks.

    // A freshly loaded statement: puts the first rows on screen.
    public void OnRowsLoaded(IEnumerable<StatementImportRowViewModel> rows)
    {
        shownLimit = RowsPerStep;
        readyShownLimit = RowsPerStep;
        foreach (var row in rows)
        {
            Track(row);
        }

        SyncShownRows();
    }

    // A row put back by Undo. Room is made for it, so nothing the user was looking at disappears.
    public void OnRowAdded(StatementImportRowViewModel row)
    {
        if (row.Section != StatementImportRowSection.Ready)
        {
            shownLimit++;
        }

        Track(row);
        SyncShownRows();
    }

    // An approved or skipped row: the next hidden row (if any) takes its place.
    public void OnRowRemoved(StatementImportRowViewModel row)
    {
        if (trackedRows.Remove(row))
        {
            row.PropertyChanged -= OnRowPropertyChanged;
            if (row.Section == StatementImportRowSection.Ready)
            {
                RemoveFromGroup(row);
            }
        }

        SyncShownRows();
    }

    public void Clear()
    {
        foreach (var row in trackedRows)
        {
            row.PropertyChanged -= OnRowPropertyChanged;
        }

        trackedRows.Clear();
        allGroups.Clear();
        DuplicateRows.Clear();
        AttentionRows.Clear();
        ReadyRows.Clear();
        ReadyGroups.Clear();
        IsReadyExpanded = false;
        RaiseAll();
    }

    // Fills every row the user hasn't touched (StatementImportRowViewModel.IsUntouched) with
    // Banccoon's current suggestion for it, and moves the ones whose block changed - typically a
    // row that had nothing learned and is now fully categorised, from "needs a decision" to ready.
    // Duplicates and rows with an approve/skip in flight are left alone.
    public void ApplySuggestions(IReadOnlyDictionary<Guid, StatementImportRowSuggestion> suggestions)
    {
        foreach (var row in review.Rows.ToList())
        {
            if (row.IsDuplicate || row.IsBusy || !row.IsUntouched || !suggestions.TryGetValue(row.Id, out var suggestion))
            {
                continue;
            }

            var otherAccount = suggestion.DestinationAccountId is { } otherAccountId
                ? review.OtherAccountOptions.FirstOrDefault(option => option.Id == otherAccountId)
                : null;
            var wasReady = row.Section == StatementImportRowSection.Ready;
            var previousGroup = StatementImportRowGroupKey.For(row);
            row.ApplySuggestion(suggestion.Type, suggestion.CategoryId, otherAccount);
            var isReady = row.Section == StatementImportRowSection.Ready;

            if (wasReady && (!isReady || StatementImportRowGroupKey.For(row) != previousGroup))
            {
                RemoveFromGroup(row);
            }

            if (isReady && (!wasReady || StatementImportRowGroupKey.For(row) != previousGroup))
            {
                AddToGroup(row);
            }
        }

        SyncShownRows();
    }

    // Approves every row in `candidates` that's ready as it stands - "Approve all categorised"
    // (every row) and a group's "Approve all" (that group's rows).
    public async Task ApproveReadyRowsAsync(IReadOnlyList<StatementImportRowViewModel> candidates)
    {
        IReadOnlyList<StatementImportRowViewModel> targets = [];
        await RunOnMainThreadAsync(() => targets = candidates.Where(row => row.IsReadyToApprove && row.TryBeginAction()).ToList());
        if (targets.Count == 0)
        {
            return;
        }

        try
        {
            await review.RunExclusiveAsync(async () =>
            {
                foreach (var row in targets)
                {
                    await review.ApproveAsync(row, bulkCategoryId: null);
                }
            });
        }
        finally
        {
            await EndRowActionsAsync(targets);
        }
    }

    private async Task ApproveGroupAsync(StatementImportRowGroupViewModel group)
    {
        var creatingCategory = false;
        var hasName = false;
        await RunOnMainThreadAsync(() =>
        {
            creatingCategory = group.IsCreatingNewCategory;
            hasName = !string.IsNullOrWhiteSpace(group.NewCategoryName);
        });

        if (creatingCategory && !hasName)
        {
            await review.SetStatusAsync(Translator.Get("StatementImport_NameNewCategoryFirst"));
            return;
        }

        if (creatingCategory)
        {
            await review.Categories.CreateForGroupAsync(group);
        }

        IReadOnlyList<StatementImportRowViewModel> rows = [];
        await RunOnMainThreadAsync(() => rows = group.Rows.ToList());
        await ApproveReadyRowsAsync(rows);
    }

    // Which rows each block should show right now: duplicates, then rows needing a decision, up to
    // shownLimit between them; and, only while the ready block is open, its first readyShownLimit
    // rows (flat) or groups (grouped) - whichever is showing.
    private void SyncShownRows()
    {
        var ordered = review.Rows
            .Where(row => row.Section != StatementImportRowSection.Ready && trackedRows.Contains(row))
            .OrderBy(row => row.Section == StatementImportRowSection.Duplicate ? 0 : 1)
            .ThenBy(row => row.Date)
            .Take(shownLimit)
            .ToList();
        CollectionSync.Apply(DuplicateRows, ordered.Where(row => row.Section == StatementImportRowSection.Duplicate).ToList());
        CollectionSync.Apply(AttentionRows, ordered.Where(row => row.Section == StatementImportRowSection.Attention).ToList());
        CollectionSync.Apply(ReadyRows, IsReadyListFlat
            ? review.Rows
                .Where(row => row.Section == StatementImportRowSection.Ready && trackedRows.Contains(row))
                .OrderBy(row => row.Date)
                .Take(readyShownLimit)
                .ToList()
            : []);
        CollectionSync.Apply(ReadyGroups, IsReadyListGrouped ? allGroups.Take(readyShownLimit).ToList() : []);
        RaiseAll();
    }

    private void Track(StatementImportRowViewModel row)
    {
        if (!trackedRows.Add(row))
        {
            return;
        }

        row.PropertyChanged += OnRowPropertyChanged;
        if (row.Section == StatementImportRowSection.Ready)
        {
            AddToGroup(row);
        }
    }

    private void AddToGroup(StatementImportRowViewModel row)
    {
        var key = StatementImportRowGroupKey.For(row);
        var group = allGroups.FirstOrDefault(candidate => candidate.Key == key);
        if (group is not null)
        {
            group.Add(row);
            return;
        }

        group = new StatementImportRowGroupViewModel(key, row, review.Currency, review.CategoryOptions, review.CategoryTree, ApproveGroupAsync, review.Categories.CreateForGroupAsync);
        var index = 0;
        while (index < allGroups.Count && string.Compare(allGroups[index].Name, group.Name, StringComparison.CurrentCultureIgnoreCase) <= 0)
        {
            index++;
        }

        allGroups.Insert(index, group);
    }

    private void RemoveFromGroup(StatementImportRowViewModel row)
    {
        var group = allGroups.FirstOrDefault(candidate => candidate.Rows.Contains(row));
        if (group is null)
        {
            return;
        }

        group.Remove(row);
        if (group.Count == 0)
        {
            allGroups.Remove(group);
        }
    }

    private int CountIn(StatementImportRowSection section) => review.Rows.Count(row => row.Section == section);

    private static string ShowMoreText(int hidden) => string.Format(Translator.Get("StatementImport_ShowMoreFormat"), hidden);

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StatementImportRowViewModel.IsReadyToApprove))
        {
            RaiseCategorisedCount();
        }
    }

    private async Task SkipAllDuplicatesAsync()
    {
        IReadOnlyList<StatementImportRowViewModel> targets = [];
        await RunOnMainThreadAsync(() => targets = review.Rows.Where(row => row.Section == StatementImportRowSection.Duplicate && row.TryBeginAction()).ToList());
        if (targets.Count == 0)
        {
            return;
        }

        try
        {
            await review.RunExclusiveAsync(async () =>
            {
                foreach (var row in targets)
                {
                    await review.SkipAsync(row);
                }
            });
        }
        finally
        {
            await EndRowActionsAsync(targets);
        }
    }

    private static Task EndRowActionsAsync(IReadOnlyList<StatementImportRowViewModel> rows)
    {
        return RunOnMainThreadAsync(() =>
        {
            foreach (var row in rows)
            {
                row.EndAction();
            }
        });
    }

    private void RaiseReadyListVisibility()
    {
        OnPropertyChanged(nameof(IsReadyListFlat));
        OnPropertyChanged(nameof(IsReadyListGrouped));
        OnPropertyChanged(nameof(HasHiddenReady));
        OnPropertyChanged(nameof(ShowMoreReadyText));
    }

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(HasDuplicates));
        OnPropertyChanged(nameof(HasAttention));
        OnPropertyChanged(nameof(HasReady));
        OnPropertyChanged(nameof(DuplicatesHeaderText));
        OnPropertyChanged(nameof(AttentionHeaderText));
        OnPropertyChanged(nameof(ReadyHeaderText));
        OnPropertyChanged(nameof(HasHiddenDuplicates));
        OnPropertyChanged(nameof(ShowMoreDuplicatesText));
        OnPropertyChanged(nameof(HasHiddenAttention));
        OnPropertyChanged(nameof(ShowMoreAttentionText));
        RaiseReadyListVisibility();
        RaiseCategorisedCount();
    }

    private void RaiseCategorisedCount()
    {
        OnPropertyChanged(nameof(CategorisedCount));
        OnPropertyChanged(nameof(CanApproveAllCategorised));
        OnPropertyChanged(nameof(ApproveAllCategorisedText));
    }
}
