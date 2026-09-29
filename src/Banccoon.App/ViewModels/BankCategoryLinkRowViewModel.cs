using System.Collections.ObjectModel;
using System.Windows.Input;
using Banccoon.Core.Categories;
using Banccoon.Core.Statements;
using Microsoft.Maui.Graphics;

namespace Banccoon.App.ViewModels;

// One of a bank's categories in Settings -> Bank categories. Picking "Not linked" or a category
// saves straight away; "+ New category" saves once the name is committed (Enter / Add).
// UI-thread only.
public sealed class BankCategoryLinkRowViewModel : ViewModelBase
{
    private readonly Func<BankCategoryLinkRowViewModel, Guid?, Task> onLinkChanged;
    private CategoryOptionViewModel? category;
    private string newCategoryName = string.Empty;

    // Set while the subcategory picker is rebuilt from code, so that isn't saved as a new link.
    private bool isSyncingSubcategory;

    public BankCategoryLinkRowViewModel(
        BankCategoryLink link,
        ObservableCollection<CategoryOptionViewModel> categoryOptions,
        CategoryTree categoryTree,
        Func<BankCategoryLinkRowViewModel, Guid?, Task> onLinkChanged,
        Func<BankCategoryLinkRowViewModel, Task> onCreateCategory)
    {
        this.onLinkChanged = onLinkChanged;
        Name = link.BankCategory;
        CategoryOptions = categoryOptions;
        Subcategory = new SubcategoryPickerViewModel(OnSubcategoryChanged);
        // A link to a child shows its parent here and the child next to it.
        category = (link.CategoryId is { } linkedId ? CategoryOptionsHelper.FindParentOption(categoryOptions, categoryTree, linkedId) : null)
            ?? categoryOptions.FirstOrDefault(option => option.IsNone);
        SyncSubcategory(() =>
        {
            Subcategory.Reset(categoryTree);
            if (category is { IsCategory: true } && link.CategoryId is { } childOrParentId)
            {
                Subcategory.SelectCategory(childOrParentId);
            }
        });

        CreateCategoryCommand = new RelayCommand(() => _ = onCreateCategory(this));
    }

    public string Name { get; }

    public ObservableCollection<CategoryOptionViewModel> CategoryOptions { get; }

    // The chosen parent's children, when it has any; choosing one saves the link to it.
    public SubcategoryPickerViewModel Subcategory { get; }

    public CategoryOptionViewModel? Category
    {
        get => category;
        set
        {
            if (!SetProperty(ref category, value))
            {
                return;
            }

            RaiseCategoryChanged();
            SyncSubcategory(() => Subcategory.ShowChildrenOf(value is { IsCategory: true } ? value.Id : null));
            if (value is { IsNone: true })
            {
                _ = onLinkChanged(this, null);
            }
            else if (value is { IsCategory: true })
            {
                _ = onLinkChanged(this, value.Id);
            }
        }
    }

    public bool IsCreatingNewCategory => Category?.IsCreateNew == true;

    public Color CategoryBorderColor => Category?.Color ?? Colors.Transparent;

    public string NewCategoryName
    {
        get => newCategoryName;
        set => SetProperty(ref newCategoryName, value);
    }

    public ICommand CreateCategoryCommand { get; }

    // Puts the picker back without saving - see BankCategoryLinksViewModel.AddOption.
    public void RestoreCategory(CategoryOptionViewModel? value)
    {
        if (SetProperty(ref category, value))
        {
            RaiseCategoryChanged();
            SyncSubcategory(() => Subcategory.ShowChildrenOf(value is { IsCategory: true } ? value.Id : null));
        }
    }

    private void OnSubcategoryChanged()
    {
        if (!isSyncingSubcategory && Category is { IsCategory: true } parent)
        {
            _ = onLinkChanged(this, Subcategory.Resolve(parent.Id));
        }
    }

    private void SyncSubcategory(Action action)
    {
        isSyncingSubcategory = true;
        try
        {
            action();
        }
        finally
        {
            isSyncingSubcategory = false;
        }
    }

    private void RaiseCategoryChanged()
    {
        OnPropertyChanged(nameof(IsCreatingNewCategory));
        OnPropertyChanged(nameof(CategoryBorderColor));
    }
}
