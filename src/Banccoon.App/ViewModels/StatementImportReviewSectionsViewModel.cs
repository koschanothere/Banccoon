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
// - rows Banccoon already categorised from what it learned - collapsed by default;
// - rows that need a decision (nothing learned yet, or a transfer with no learned other account).
// "Approve all categorised" approves every non-duplicate row that is ready as it stands, in either
// of the last two blocks, so categorising a few "needs attention" rows and then approving
// everything at once works too. A row's block is decided when it's loaded, and only changes when a
// newer suggestion fills in a row the user hasn't touched (ApplySuggestions) - rows never jump
// between blocks while being edited.
public sealed class StatementImportReviewSectionsViewModel : ViewModelBase
{
    private readonly StatementImportReviewViewModel review;
    private bool isReadyExpanded;

    public StatementImportReviewSectionsViewModel(StatementImportReviewViewModel review)
    {
        this.review = review;

        DuplicateRows = [];
        AttentionRows = [];
        ReadyRows = [];

        ToggleReadyExpandedCommand = new RelayCommand(() => IsReadyExpanded = !IsReadyExpanded);
        ApproveAllCategorisedCommand = new RelayCommand(() => _ = ApproveAllCategorisedAsync());
        SkipAllDuplicatesCommand = new RelayCommand(() => _ = SkipAllDuplicatesAsync());
    }

    public ObservableCollection<StatementImportRowViewModel> DuplicateRows { get; }

    public ObservableCollection<StatementImportRowViewModel> AttentionRows { get; }

    public ObservableCollection<StatementImportRowViewModel> ReadyRows { get; }

    public bool HasDuplicates => DuplicateRows.Count > 0;

    public bool HasAttention => AttentionRows.Count > 0;

    public bool HasReady => ReadyRows.Count > 0;

    public string DuplicatesHeaderText => string.Format(Translator.Get("StatementImport_DuplicatesHeaderFormat"), DuplicateRows.Count);

    public string AttentionHeaderText => string.Format(Translator.Get("StatementImport_AttentionHeaderFormat"), AttentionRows.Count);

    public string ReadyHeaderText => string.Format(Translator.Get("StatementImport_ReadyHeaderFormat"), ReadyRows.Count);

    public bool IsReadyExpanded
    {
        get => isReadyExpanded;
        private set
        {
            if (SetProperty(ref isReadyExpanded, value))
            {
                OnPropertyChanged(nameof(IsReadyCollapsed));
            }
        }
    }

    public bool IsReadyCollapsed => !IsReadyExpanded;

    public int CategorisedCount => review.Rows.Count(row => row.IsReadyToApprove);

    public bool CanApproveAllCategorised => CategorisedCount > 0;

    public string ApproveAllCategorisedText => string.Format(Translator.Get("StatementImport_ApproveAllCategorisedFormat"), CategorisedCount);

    public ICommand ToggleReadyExpandedCommand { get; }

    public ICommand ApproveAllCategorisedCommand { get; }

    public ICommand SkipAllDuplicatesCommand { get; }

    // UI-thread only - called by the parent from inside its own RunOnMainThreadAsync blocks.
    public void OnRowAdded(StatementImportRowViewModel row)
    {
        InsertByDate(SectionFor(row.Section), row);
        row.PropertyChanged += OnRowPropertyChanged;
        RaiseAll();
    }

    public void OnRowRemoved(StatementImportRowViewModel row)
    {
        row.PropertyChanged -= OnRowPropertyChanged;
        SectionFor(row.Section).Remove(row);
        RaiseAll();
    }

    // UI-thread only. Fills every row the user hasn't touched (StatementImportRowViewModel.IsUntouched)
    // with Banccoon's current suggestion for it, and moves the ones whose block changed - typically a
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
            if (row.ApplySuggestion(suggestion.Type, category, otherAccount))
            {
                SectionFor(previousSection).Remove(row);
                InsertByDate(SectionFor(row.Section), row);
            }
        }

        RaiseAll();
    }

    public void Clear()
    {
        foreach (var row in DuplicateRows.Concat(AttentionRows).Concat(ReadyRows))
        {
            row.PropertyChanged -= OnRowPropertyChanged;
        }

        DuplicateRows.Clear();
        AttentionRows.Clear();
        ReadyRows.Clear();
        IsReadyExpanded = false;
        RaiseAll();
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

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StatementImportRowViewModel.IsReadyToApprove))
        {
            RaiseCategorisedCount();
        }
    }

    private async Task ApproveAllCategorisedAsync()
    {
        IReadOnlyList<StatementImportRowViewModel> targets = [];
        await RunOnMainThreadAsync(() => targets = review.Rows.Where(row => row.IsReadyToApprove && row.TryBeginAction()).ToList());
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

    private async Task SkipAllDuplicatesAsync()
    {
        IReadOnlyList<StatementImportRowViewModel> targets = [];
        await RunOnMainThreadAsync(() => targets = DuplicateRows.Where(row => row.TryBeginAction()).ToList());
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
