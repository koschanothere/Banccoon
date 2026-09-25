using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.App.Localization;
using Banccoon.Core.Categories;
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
        CategoryTree categoryTree,
        ObservableCollection<NamedOptionViewModel> accountOptions,
        Func<Guid, Task> onForget,
        Func<CategoryLearningRuleRowViewModel, Task> onSave)
    {
        Id = rule.Id;
        MatchText = rule.MatchText;
        Type = rule.Type;
        CategoryId = rule.CategoryId;
        DestinationAccountId = rule.DestinationAccountId;
        TypeText = DisplayText.Format(rule.Type);
        CategoryName = categoryName;
        DestinationAccountName = destinationAccountName;
        HasDestinationAccount = !string.IsNullOrWhiteSpace(destinationAccountName);
        MatchCountText = Translator.GetPlural("CategoryLearning_MatchCount", rule.MatchCount);
        LastUpdatedText = rule.UpdatedAt.ToString("dd MMM yyyy", CultureInfo.CurrentUICulture);

        CategoryOptions = categoryOptions;
        AccountOptions = accountOptions;
        Subcategory = new SubcategoryPickerViewModel();
        Subcategory.Reset(categoryTree);
        TypeOptions = Enum.GetValues<TransactionType>();

        editMatchText = MatchText;
        editType = Type;
        // A rule filed under a child shows its parent here and the child next to it.
        editCategory = categoryOptions.FirstOrDefault(option => option.Id == categoryTree.RootIdOf(rule.CategoryId));
        if (editCategory is not null)
        {
            Subcategory.SelectCategory(rule.CategoryId);
        }
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

    // The edited category's children, when it has any (see SubcategoryPickerViewModel).
    public SubcategoryPickerViewModel Subcategory { get; }

    // What Save stores: the chosen child, else the chosen parent.
    public Guid? EditCategoryId => EditCategory is { } option ? Subcategory.Resolve(option.Id) : null;

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
        set
        {
            if (SetProperty(ref editCategory, value))
            {
                Subcategory.ShowChildrenOf(value?.Id);
            }
        }
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
        EditCategory = CategoryOptions.FirstOrDefault(option => option.Id == Subcategory.Tree.RootIdOf(CategoryId));
        if (EditCategory is not null)
        {
            Subcategory.SelectCategory(CategoryId);
        }
        EditDestinationAccount = DestinationAccountId is { } destinationAccountId
            ? AccountOptions.FirstOrDefault(option => option.Id == destinationAccountId)
            : null;
    }

    private async Task SaveEditAsync(Func<CategoryLearningRuleRowViewModel, Task> onSave)
    {
        await onSave(this);
    }
}
