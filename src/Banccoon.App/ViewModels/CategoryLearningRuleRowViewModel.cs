using System.Collections.ObjectModel;
using System.Windows.Input;
using Banccoon.Core.Models;
using Banccoon.Core.Statements;

namespace Banccoon.App.ViewModels;

public sealed class CategoryLearningRuleRowViewModel : ViewModelBase
{
    private bool isEditing;
    private string editMatchText;
    private TransactionType editType;
    private NamedOptionViewModel? editCategory;
    private NamedOptionViewModel? editDestinationAccount;

    public CategoryLearningRuleRowViewModel(
        CategoryLearningRule rule,
        string categoryName,
        string? destinationAccountName,
        ObservableCollection<NamedOptionViewModel> categoryOptions,
        ObservableCollection<NamedOptionViewModel> accountOptions,
        Func<Guid, Task> onForget,
        Func<CategoryLearningRuleRowViewModel, Task> onSave)
    {
        Id = rule.Id;
        MatchText = rule.MatchText;
        Type = rule.Type;
        CategoryId = rule.CategoryId;
        DestinationAccountId = rule.DestinationAccountId;
        TypeText = rule.Type.ToString();
        CategoryName = categoryName;
        DestinationAccountName = destinationAccountName;
        HasDestinationAccount = !string.IsNullOrWhiteSpace(destinationAccountName);
        MatchCountText = rule.MatchCount == 1 ? "Learned from 1 correction" : $"Learned from {rule.MatchCount} corrections";
        LastUpdatedText = rule.UpdatedAt.ToString("dd MMM yyyy");

        CategoryOptions = categoryOptions;
        AccountOptions = accountOptions;
        TypeOptions = Enum.GetValues<TransactionType>();

        editMatchText = MatchText;
        editType = Type;
        editCategory = categoryOptions.FirstOrDefault(option => option.Id == rule.CategoryId);
        editDestinationAccount = rule.DestinationAccountId is { } destinationAccountId
            ? accountOptions.FirstOrDefault(option => option.Id == destinationAccountId)
            : null;

        ForgetCommand = new RelayCommand(() => _ = onForget(Id));
        EditCommand = new RelayCommand(() => IsEditing = true);
        CancelEditCommand = new RelayCommand(() =>
        {
            ResetEditFields();
            IsEditing = false;
        });
        SaveEditCommand = new RelayCommand(() => _ = SaveEditAsync(onSave));
    }

    public Guid Id { get; }

    public string MatchText { get; }

    public TransactionType Type { get; }

    public Guid CategoryId { get; }

    public Guid? DestinationAccountId { get; }

    public string TypeText { get; }

    public string CategoryName { get; }

    public string? DestinationAccountName { get; }

    public bool HasDestinationAccount { get; }

    public string MatchCountText { get; }

    public string LastUpdatedText { get; }

    public ObservableCollection<NamedOptionViewModel> CategoryOptions { get; }

    public ObservableCollection<NamedOptionViewModel> AccountOptions { get; }

    public IReadOnlyList<TransactionType> TypeOptions { get; }

    public bool IsEditing
    {
        get => isEditing;
        private set => SetProperty(ref isEditing, value);
    }

    public string EditMatchText
    {
        get => editMatchText;
        set => SetProperty(ref editMatchText, value);
    }

    public TransactionType EditType
    {
        get => editType;
        set
        {
            if (SetProperty(ref editType, value))
            {
                OnPropertyChanged(nameof(IsEditTransferType));
            }
        }
    }

    public bool IsEditTransferType => EditType == TransactionType.Transfer;

    public NamedOptionViewModel? EditCategory
    {
        get => editCategory;
        set => SetProperty(ref editCategory, value);
    }

    public NamedOptionViewModel? EditDestinationAccount
    {
        get => editDestinationAccount;
        set => SetProperty(ref editDestinationAccount, value);
    }

    public ICommand ForgetCommand { get; }

    public ICommand EditCommand { get; }

    public ICommand CancelEditCommand { get; }

    public ICommand SaveEditCommand { get; }

    private void ResetEditFields()
    {
        EditMatchText = MatchText;
        EditType = Type;
        EditCategory = CategoryOptions.FirstOrDefault(option => option.Id == CategoryId);
        EditDestinationAccount = DestinationAccountId is { } destinationAccountId
            ? AccountOptions.FirstOrDefault(option => option.Id == destinationAccountId)
            : null;
    }

    private async Task SaveEditAsync(Func<CategoryLearningRuleRowViewModel, Task> onSave)
    {
        await onSave(this);
    }
}
