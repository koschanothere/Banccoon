using System.Collections.ObjectModel;
using System.Windows.Input;
using Banccoon.Core.Models;
using Banccoon.Core.Setup;

namespace Banccoon.App.ViewModels;

// First-run setup, step 3: the default categories (Core's DefaultCategories) as a two-level
// ticklist, everything ticked to start with (decided 2026-09-28).
// - Unticking a main category unticks its subcategories; ticking it ticks them all again.
// - Ticking a subcategory whose main category is unticked asks where it goes instead
//   (SetupCategoryItemViewModel.NeedsParentChoice); Next waits until every one has an answer.
// - "Other" stays ticked (the import's catch-all).
// UI-thread only.
public sealed class SetupCategoriesViewModel : ViewModelBase
{
    private readonly List<SetupCategoryItemViewModel> parents = [];
    private readonly Action onChanged;
    private bool isCascading;

    public SetupCategoriesViewModel(Action onChanged)
    {
        this.onChanged = onChanged;
        ExpenseParents = [];
        IncomeParents = [];

        foreach (var definition in DefaultCategories.All)
        {
            var parent = new SetupCategoryItemViewModel(
                definition.Key,
                definition.Type,
                definition.Color,
                parent: null,
                isLocked: definition.Key == DefaultCategories.OtherKey,
                OnToggled,
                OnChoiceChanged);
            foreach (var childKey in definition.ChildKeys)
            {
                parent.Children.Add(new SetupCategoryItemViewModel(childKey, definition.Type, definition.Color, parent, isLocked: false, OnToggled, OnChoiceChanged));
            }

            parents.Add(parent);
            (definition.Type == TransactionType.Income ? IncomeParents : ExpenseParents).Add(parent);
        }

        SelectAllCommand = new RelayCommand(() => SetAll(true));
        ClearAllCommand = new RelayCommand(() => SetAll(false));
    }

    public ObservableCollection<SetupCategoryItemViewModel> ExpenseParents { get; }

    public ObservableCollection<SetupCategoryItemViewModel> IncomeParents { get; }

    // Every ticked subcategory whose main category is unticked has been given somewhere to go.
    public bool CanContinue => AllChildren.All(child => child.HasValidParentChoice);

    public ICommand SelectAllCommand { get; }

    public ICommand ClearAllCommand { get; }

    private IEnumerable<SetupCategoryItemViewModel> AllChildren => parents.SelectMany(parent => parent.Children);

    public void RefreshLanguage()
    {
        foreach (var item in parents.Concat(AllChildren))
        {
            item.RefreshLanguage();
        }

        RefreshParentChoices();
    }

    // The tree to create, names in the current language. Subcategories that were moved go under
    // their chosen main category (a new one named the same by several of them is one category).
    public IReadOnlyList<SetupCategory> BuildRequest()
    {
        var extraChildren = new Dictionary<string, List<SetupCategory>>();
        var newParents = new List<(string Name, SetupCategoryItemViewModel FirstChild, List<SetupCategory> Children)>();
        var standalone = new List<SetupCategory>();

        foreach (var child in AllChildren.Where(child => child.NeedsParentChoice))
        {
            var node = new SetupCategory(child.Name, child.Type, null, []);
            switch (child.ParentChoice?.Kind)
            {
                case SetupParentChoiceKind.Existing when child.ParentChoice.ParentKey is { } key:
                    if (!extraChildren.TryGetValue(key, out var list))
                    {
                        list = [];
                        extraChildren[key] = list;
                    }

                    list.Add(node);
                    break;
                case SetupParentChoiceKind.NewParent:
                    var name = child.NewParentName.Trim();
                    var group = newParents.FirstOrDefault(entry => string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase));
                    if (group.Name is null)
                    {
                        group = (name, child, []);
                        newParents.Add(group);
                    }

                    group.Children.Add(node);
                    break;
                case SetupParentChoiceKind.KeepAsMain:
                    standalone.Add(new SetupCategory(child.Name, child.Type, child.CategoryColor, []));
                    break;
            }
        }

        var result = new List<SetupCategory>();
        foreach (var parent in parents.Where(parent => parent.IsChecked))
        {
            var children = parent.Children
                .Where(child => child.IsChecked)
                .Select(child => new SetupCategory(child.Name, child.Type, null, []))
                .Concat(extraChildren.TryGetValue(parent.Key, out var moved) ? moved : [])
                .ToList();
            result.Add(new SetupCategory(parent.Name, parent.Type, parent.CategoryColor, children, IsFallback: parent.Key == DefaultCategories.OtherKey));
        }

        result.AddRange(newParents.Select(entry => new SetupCategory(entry.Name, entry.FirstChild.Type, entry.FirstChild.CategoryColor, entry.Children)));
        result.AddRange(standalone);
        return result;
    }

    private void OnToggled(SetupCategoryItemViewModel item)
    {
        if (isCascading)
        {
            return;
        }

        if (!item.IsChild)
        {
            var ticked = item.IsChecked;
            isCascading = true;
            try
            {
                foreach (var child in item.Children)
                {
                    child.SetChecked(ticked);
                }
            }
            finally
            {
                isCascading = false;
            }
        }

        RefreshParentChoices();
    }

    private void OnChoiceChanged()
    {
        OnPropertyChanged(nameof(CanContinue));
        onChanged();
    }

    private void SetAll(bool ticked)
    {
        isCascading = true;
        try
        {
            foreach (var item in parents.Concat(AllChildren))
            {
                item.SetChecked(ticked);
            }
        }
        finally
        {
            isCascading = false;
        }

        RefreshParentChoices();
    }

    private void RefreshParentChoices()
    {
        isCascading = true;
        try
        {
            var tickedParents = parents.Where(parent => parent.IsChecked).ToList();
            foreach (var child in AllChildren)
            {
                child.RefreshParentChoices(tickedParents);
            }
        }
        finally
        {
            isCascading = false;
        }

        OnPropertyChanged(nameof(CanContinue));
        onChanged();
    }
}
