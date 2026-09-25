using System.Collections.ObjectModel;
using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.App.Localization;
using Banccoon.Core.Models;
using Microsoft.Maui.Graphics;

namespace Banccoon.App.ViewModels;

// Ready rows that share one sender name and the same category, type and other account - shown in
// the ready block (with "Group by name" on) as one collapsible line with its own "Approve all", so
// twelve coffees from the same café are one decision. A group of one is drawn as a plain row.
// Membership is fixed when a row joins (by what the row showed then), so editing a row inside a
// group never moves it; approving uses each row's own picks. UI-thread only, like every bound
// view model here.
public sealed class StatementImportRowGroupViewModel : ViewModelBase
{
    private readonly List<StatementImportRowViewModel> rows = [];
    private readonly string currency;
    private bool isExpanded;

    public StatementImportRowGroupViewModel(
        StatementImportRowGroupKey key,
        StatementImportRowViewModel firstRow,
        string currency,
        Func<IReadOnlyList<StatementImportRowViewModel>, Task> onApprove)
    {
        Key = key;
        this.currency = currency;
        Name = firstRow.Description;
        Type = firstRow.Type;
        CategoryName = firstRow.Category?.Name ?? string.Empty;
        CategoryColor = firstRow.Category?.Color ?? Colors.Transparent;
        DisplayedRows = [];

        ToggleExpandedCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
        ApproveCommand = new RelayCommand(() => _ = onApprove(rows.ToList()));

        Add(firstRow);
    }

    public StatementImportRowGroupKey Key { get; }

    public string Name { get; }

    public TransactionType Type { get; }

    public string CategoryName { get; }

    public Color CategoryColor { get; }

    public IReadOnlyList<StatementImportRowViewModel> Rows => rows;

    // What the group's row list is bound to: empty while a group of two or more is collapsed, so a
    // collapsed group costs one header line to draw rather than every row in it.
    public ObservableCollection<StatementImportRowViewModel> DisplayedRows { get; }

    public int Count => rows.Count;

    public bool IsMultiple => rows.Count > 1;

    public bool IsExpanded
    {
        get => isExpanded;
        private set
        {
            if (SetProperty(ref isExpanded, value))
            {
                SyncDisplayedRows();
            }
        }
    }

    public string CountText => Translator.GetPlural("StatementImport_GroupCount", rows.Count);

    public string TotalText => MoneyFormat.Format(rows.Sum(row => row.SignedAmount), currency);

    public string ApproveText => string.Format(Translator.Get("StatementImport_ApproveGroupFormat"), rows.Count);

    public ICommand ToggleExpandedCommand { get; }

    public ICommand ApproveCommand { get; }

    public void Add(StatementImportRowViewModel row)
    {
        var index = 0;
        while (index < rows.Count && rows[index].Date <= row.Date)
        {
            index++;
        }

        rows.Insert(index, row);
        OnRowsChanged();
    }

    public void Remove(StatementImportRowViewModel row)
    {
        if (rows.Remove(row))
        {
            OnRowsChanged();
        }
    }

    private void OnRowsChanged()
    {
        SyncDisplayedRows();
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(IsMultiple));
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(ApproveText));
    }

    private void SyncDisplayedRows()
    {
        IReadOnlyList<StatementImportRowViewModel> shown = IsMultiple && !IsExpanded ? [] : rows;
        if (DisplayedRows.SequenceEqual(shown))
        {
            return;
        }

        DisplayedRows.Clear();
        foreach (var row in shown)
        {
            DisplayedRows.Add(row);
        }
    }
}

// Same sender name (ignoring case and surrounding spaces), category, type and other account.
public readonly record struct StatementImportRowGroupKey(string Name, Guid? CategoryId, TransactionType Type, Guid? OtherAccountId)
{
    public static StatementImportRowGroupKey For(StatementImportRowViewModel row) => new(
        row.Description.Trim().ToUpperInvariant(),
        row.Category?.Id,
        row.Type,
        row.IsTransferType ? row.OtherAccount?.Id : null);
}
