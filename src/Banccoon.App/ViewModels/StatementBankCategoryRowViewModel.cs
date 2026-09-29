using System.Collections.ObjectModel;
using System.Windows.Input;
using Banccoon.App.Localization;
using Banccoon.Core.Categories;
using Banccoon.Core.Statements;
using Microsoft.Maui.Graphics;

namespace Banccoon.App.ViewModels;

// One bank category seen for the first time, in the import's linking step: pick one of the user's
// categories (or name a new one), or Skip. Leaving it empty counts as skipped (user decision
// 2026-09-25). UI-thread only.
public sealed class StatementBankCategoryRowViewModel : ViewModelBase
{
    private CategoryOptionViewModel? category;
    private string newCategoryName = string.Empty;
    private bool isSkipped;

    public StatementBankCategoryRowViewModel(
        DetectedBankCategory detected,
        ObservableCollection<CategoryOptionViewModel> categoryOptions,
        CategoryTree categoryTree)
    {
        Subcategory = new SubcategoryPickerViewModel();
        Subcategory.Reset(categoryTree);
        Name = detected.Name;
        CountText = Translator.GetPlural("StatementImport_GroupCount", detected.OperationCount);
        CategoryOptions = categoryOptions;

        SkipCommand = new RelayCommand(() => IsSkipped = true);
        UndoSkipCommand = new RelayCommand(() => IsSkipped = false);
    }

    public string Name { get; }

    // How many of this statement's operations use it - "12 transactions".
    public string CountText { get; }

    public ObservableCollection<CategoryOptionViewModel> CategoryOptions { get; }

    // The chosen parent's children, when it has any (see SubcategoryPickerViewModel).
    public SubcategoryPickerViewModel Subcategory { get; }

    // The existing category chosen (a parent, or one of its children), or null.
    public Guid? ChosenCategoryId => Category is { IsCategory: true } option ? Subcategory.Resolve(option.Id) : null;

    public CategoryOptionViewModel? Category
    {
        get => category;
        set
        {
            if (SetProperty(ref category, value))
            {
                OnPropertyChanged(nameof(IsCreatingNewCategory));
                OnPropertyChanged(nameof(CategoryBorderColor));
                Subcategory.ShowChildrenOf(value is { IsCategory: true } ? value.Id : null);
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

    public bool IsSkipped
    {
        get => isSkipped;
        private set
        {
            if (SetProperty(ref isSkipped, value))
            {
                OnPropertyChanged(nameof(IsLinkable));
            }
        }
    }

    public bool IsLinkable => !IsSkipped;

    public ICommand SkipCommand { get; }

    public ICommand UndoSkipCommand { get; }
}
