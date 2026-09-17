using System.Collections.ObjectModel;
using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.Core.Forecasting;
using Banccoon.Core.Statements;

namespace Banccoon.App.ViewModels;

public sealed class StatementImportRowViewModel : ViewModelBase
{
    private NamedOptionViewModel? category;
    private bool isSelected;
    private bool isSelectModeActive;

    public StatementImportRowViewModel(
        StatementImportRow row,
        string currency,
        ObservableCollection<NamedOptionViewModel> categoryOptions,
        Func<StatementImportRowViewModel, Task> onApprove,
        Func<StatementImportRowViewModel, Task> onSkip)
    {
        Id = row.Id;
        DateText = row.Date.ToString("dd/MM/yyyy");
        Description = string.IsNullOrWhiteSpace(row.Counterparty) ? row.Description : row.Counterparty;
        AmountText = MoneyFormat.Format(MoneyFlow.GetSignedAmount(row.Amount, row.Type), currency);
        IsDuplicate = row.IsDuplicate;
        CategoryOptions = categoryOptions;

        var selectedCategoryId = row.CategoryId ?? row.SuggestedCategoryId;
        category = selectedCategoryId is { } categoryId
            ? categoryOptions.FirstOrDefault(option => option.Id == categoryId)
            : categoryOptions.FirstOrDefault();

        ApproveCommand = new RelayCommand(() => _ = onApprove(this));
        SkipCommand = new RelayCommand(() => _ = onSkip(this));
    }

    public Guid Id { get; }

    public string DateText { get; }

    public string Description { get; }

    public string AmountText { get; }

    public bool IsDuplicate { get; }

    public ObservableCollection<NamedOptionViewModel> CategoryOptions { get; }

    public NamedOptionViewModel? Category
    {
        get => category;
        set => SetProperty(ref category, value);
    }

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public bool IsSelectModeActive
    {
        get => isSelectModeActive;
        set => SetProperty(ref isSelectModeActive, value);
    }

    public ICommand ApproveCommand { get; }

    public ICommand SkipCommand { get; }
}
