using System.Collections.ObjectModel;
using System.Windows.Input;
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

    public BankCategoryLinkRowViewModel(
        BankCategoryLink link,
        ObservableCollection<CategoryOptionViewModel> categoryOptions,
        Func<BankCategoryLinkRowViewModel, Guid?, Task> onLinkChanged,
        Func<BankCategoryLinkRowViewModel, Task> onCreateCategory)
    {
        this.onLinkChanged = onLinkChanged;
        Name = link.BankCategory;
        CategoryOptions = categoryOptions;
        category = categoryOptions.FirstOrDefault(option => option.IsCategory && option.Id == link.CategoryId)
            ?? categoryOptions.FirstOrDefault(option => option.IsNone);

        CreateCategoryCommand = new RelayCommand(() => _ = onCreateCategory(this));
    }

    public string Name { get; }

    public ObservableCollection<CategoryOptionViewModel> CategoryOptions { get; }

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
        }
    }

    private void RaiseCategoryChanged()
    {
        OnPropertyChanged(nameof(IsCreatingNewCategory));
        OnPropertyChanged(nameof(CategoryBorderColor));
    }
}
