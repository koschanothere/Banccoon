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
// Big statements are drawn a step at a time (QueueRows / DrawNext): every row is loaded, counted and
// approvable from the start, but only DrawNext puts rows into the lists on screen - duplicates first,
// then ready rows, then the ones needing a decision - so the page appears at once and keeps
// responding while the rest are drawn.
public sealed class StatementImportReviewSectionsViewModel : ViewModelBase
{
    private readonly StatementImportReviewViewModel review;

    // Loaded but not drawn yet, in drawing order. UI-thread only.
    private readonly List<StatementImportRowViewModel> undrawnRows = [];

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
    }

    public ObservableCollection<StatementImportRowViewModel> DuplicateRows { get; }

    public ObservableCollection<StatementImportRowViewModel> AttentionRows { get; }

    // The ready block as a flat, date-ordered list ("Group by name" off)...
    public ObservableCollection<StatementImportRowViewModel> ReadyRows { get; }

    // ...and as groups in name order ("Group by name" on). Both are kept up to date; only one shows.
    public ObservableCollection<StatementImportRowGroupViewModel> ReadyGroups { get; }

    public bool HasDuplicates => CountIn(StatementImportRowSection.Duplicate) > 0;

    public bool HasAttention => CountIn(StatementImportRowSection.Attention) > 0;

    public bool HasReady => CountIn(StatementImportRowSection.Ready) > 0;

    public string DuplicatesHeaderText => string.Format(Translator.Get("StatementImport_DuplicatesHeaderFormat"), CountIn(StatementImportRowSection.Duplicate));

    public string AttentionHeaderText => string.Format(Translator.Get("StatementImport_AttentionHeaderFormat"), CountIn(StatementImportRowSection.Attention));

    public string ReadyHeaderText => string.Format(Translator.Get("StatementImport_ReadyHeaderFormat"), CountIn(StatementImportRowSection.Ready));

    public bool IsReadyExpanded
    {
        get => isReadyExpanded;
        private set
        {
            if (SetProperty(ref isReadyExpanded, value))
            {
                OnPropertyChanged(nameof(IsReadyCollapsed));
                RaiseReadyListVisibility();
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
                RaiseReadyListVisibility();
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

    // Every method below is UI-thread only - called by the parent from inside its own
    // RunOnMainThreadAsync blocks.

    // A freshly loaded statement's rows, to be drawn by DrawNext.
    public void QueueRows(IEnumerable<StatementImportRowViewModel> rows)
    {
        foreach (var row in rows)
        {
            row.PropertyChanged += OnRowPropertyChanged;
            undrawnRows.Add(row);
        }

        var ordered = undrawnRows.OrderBy(row => DrawingPriority(row.Section)).ThenBy(row => row.Date).ToList();
        undrawnRows.Clear();
        undrawnRows.AddRange(ordered);
        RaiseAll();
    }

    // Draws up to `count` more queued rows, each into its current block. Returns whether any are
    // still waiting.
    public bool DrawNext(int count)
    {
        var batch = undrawnRows.Take(count).ToList();
        undrawnRows.RemoveRange(0, batch.Count);
        foreach (var row in batch)
        {
            Draw(row);
        }

        return undrawnRows.Count > 0;
    }

    // A row added on its own (Undo putting one back) is drawn straight away.
    public void OnRowAdded(StatementImportRowViewModel row)
    {
        row.PropertyChanged += OnRowPropertyChanged;
        Draw(row);
        RaiseAll();
    }

    public void OnRowRemoved(StatementImportRowViewModel row)
    {
        row.PropertyChanged -= OnRowPropertyChanged;
        if (!undrawnRows.Remove(row))
        {
            Undraw(row, row.Section);
        }

        RaiseAll();
    }

    public void Clear()
    {
        foreach (var row in DuplicateRows.Concat(AttentionRows).Concat(ReadyRows).Concat(undrawnRows))
        {
            row.PropertyChanged -= OnRowPropertyChanged;
        }

        undrawnRows.Clear();
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

            var category = suggestion.CategoryId is { } categoryId
                ? review.CategoryOptions.FirstOrDefault(option => !option.IsCreateNew && option.Id == categoryId)
                : null;
            var otherAccount = suggestion.DestinationAccountId is { } otherAccountId
                ? review.OtherAccountOptions.FirstOrDefault(option => option.Id == otherAccountId)
                : null;
            var previousSection = row.Section;
            var previousGroup = StatementImportRowGroupKey.For(row);
            var sectionChanged = row.ApplySuggestion(suggestion.Type, category, otherAccount);
            var regroup = row.Section == StatementImportRowSection.Ready && StatementImportRowGroupKey.For(row) != previousGroup;

            // Not-yet-drawn rows need nothing more: DrawNext uses the block a row is in by then.
            if ((sectionChanged || regroup) && !undrawnRows.Contains(row))
            {
                Undraw(row, previousSection);
                Draw(row);
            }
        }

        RaiseAll();
    }

    // Approves every row in `candidates` that's ready as it stands - "Approve all categorised"
    // (every row) and a group's own "Approve all" (that group's rows).
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

    private static int DrawingPriority(StatementImportRowSection section) => section switch
    {
        StatementImportRowSection.Duplicate => 0,
        StatementImportRowSection.Ready => 1,
        _ => 2
    };

    private void Draw(StatementImportRowViewModel row)
    {
        InsertByDate(SectionFor(row.Section), row);
        if (row.Section == StatementImportRowSection.Ready)
        {
            AddToGroup(row);
        }
    }

    private void Undraw(StatementImportRowViewModel row, StatementImportRowSection section)
    {
        SectionFor(section).Remove(row);
        if (section == StatementImportRowSection.Ready)
        {
            RemoveFromGroup(row);
        }
    }

    private void AddToGroup(StatementImportRowViewModel row)
    {
        var key = StatementImportRowGroupKey.For(row);
        var group = ReadyGroups.FirstOrDefault(candidate => candidate.Key == key);
        if (group is not null)
        {
            group.Add(row);
            return;
        }

        group = new StatementImportRowGroupViewModel(key, row, review.Currency, ApproveReadyRowsAsync);
        var index = 0;
        while (index < ReadyGroups.Count && string.Compare(ReadyGroups[index].Name, group.Name, StringComparison.CurrentCultureIgnoreCase) <= 0)
        {
            index++;
        }

        ReadyGroups.Insert(index, group);
    }

    private void RemoveFromGroup(StatementImportRowViewModel row)
    {
        var group = ReadyGroups.FirstOrDefault(candidate => candidate.Rows.Contains(row));
        if (group is null)
        {
            return;
        }

        group.Remove(row);
        if (group.Count == 0)
        {
            ReadyGroups.Remove(group);
        }
    }

    private static void InsertByDate(ObservableCollection<StatementImportRowViewModel> target, StatementImportRowViewModel row)
    {
        var index = 0;
        while (index < target.Count && target[index].Date <= row.Date)
        {
            index++;
        }

        target.Insert(index, row);
    }

    private ObservableCollection<StatementImportRowViewModel> SectionFor(StatementImportRowSection section) => section switch
    {
        StatementImportRowSection.Duplicate => DuplicateRows,
        StatementImportRowSection.Ready => ReadyRows,
        _ => AttentionRows
    };

    private int CountIn(StatementImportRowSection section) => review.Rows.Count(row => row.Section == section);

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
    }

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(HasDuplicates));
        OnPropertyChanged(nameof(HasAttention));
        OnPropertyChanged(nameof(HasReady));
        OnPropertyChanged(nameof(DuplicatesHeaderText));
        OnPropertyChanged(nameof(AttentionHeaderText));
        OnPropertyChanged(nameof(ReadyHeaderText));
        RaiseCategorisedCount();
    }

    private void RaiseCategorisedCount()
    {
        OnPropertyChanged(nameof(CategorisedCount));
        OnPropertyChanged(nameof(CanApproveAllCategorised));
        OnPropertyChanged(nameof(ApproveAllCategorisedText));
    }
}
