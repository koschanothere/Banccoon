using System.Collections.ObjectModel;
using Banccoon.App.Formatting;
using Banccoon.App.Localization;
using Banccoon.Core.Appearance;
using Banccoon.Core.Models;
using Microsoft.Maui.Graphics;

namespace Banccoon.App.ViewModels;

// One suggested category in first-run setup's ticklist (a main category, or one of its
// subcategories). A subcategory ticked while its main category isn't must be given somewhere to
// go (NeedsParentChoice): another ticked main category, a new one, or on its own as a main
// category. UI-thread only.
public sealed class SetupCategoryItemViewModel : ViewModelBase
{
    private readonly Action<SetupCategoryItemViewModel> onToggled;
    private readonly Action onChoiceChanged;
    private bool isChecked = true;
    private SetupParentChoiceViewModel? parentChoice;
    private string newParentName = string.Empty;
    private bool isRebuildingChoices;

    public SetupCategoryItemViewModel(
        string key,
        TransactionType type,
        CategoryColor color,
        SetupCategoryItemViewModel? parent,
        bool isLocked,
        Action<SetupCategoryItemViewModel> onToggled,
        Action onChoiceChanged)
    {
        Key = key;
        Type = type;
        CategoryColor = color;
        Parent = parent;
        IsLocked = isLocked;
        this.onToggled = onToggled;
        this.onChoiceChanged = onChoiceChanged;
        Swatch = CategoryColorPalette.GetColor(color);
        Children = [];
        ParentChoices = [];
    }

    public string Key { get; }

    public TransactionType Type { get; }

    // A subcategory's is its main category's - children always share their parent's color.
    public CategoryColor CategoryColor { get; }

    public Color Swatch { get; }

    public SetupCategoryItemViewModel? Parent { get; }

    public bool IsChild => Parent is not null;

    public ObservableCollection<SetupCategoryItemViewModel> Children { get; }

    public string Name => Translator.Get($"DefaultCategory_{Key}");

    // "Other" can't be unticked: imported rows with no category are filed under it.
    public bool IsLocked { get; }

    public bool IsEnabled => !IsLocked;

    public bool IsChecked
    {
        get => isChecked;
        set
        {
            if (IsLocked && !value)
            {
                OnPropertyChanged();
                return;
            }

            if (SetProperty(ref isChecked, value))
            {
                onToggled(this);
            }
        }
    }

    // Sets the tick from code without the cascade (SetupCategoriesViewModel does the cascading).
    public void SetChecked(bool value)
    {
        if (IsLocked && !value)
        {
            return;
        }

        SetProperty(ref isChecked, value, nameof(IsChecked));
    }

    public bool NeedsParentChoice => IsChild && IsChecked && Parent is { IsChecked: false };

    public ObservableCollection<SetupParentChoiceViewModel> ParentChoices { get; }

    public SetupParentChoiceViewModel? ParentChoice
    {
        get => parentChoice;
        set
        {
            // Rebuilding the list makes the bound picker push null in; the rebuild restores it.
            if (isRebuildingChoices)
            {
                return;
            }

            if (SetProperty(ref parentChoice, value))
            {
                OnPropertyChanged(nameof(IsNamingNewParent));
                onChoiceChanged();
            }
        }
    }

    public bool IsNamingNewParent => NeedsParentChoice && ParentChoice?.Kind == SetupParentChoiceKind.NewParent;

    public string NewParentName
    {
        get => newParentName;
        set
        {
            if (SetProperty(ref newParentName, value))
            {
                onChoiceChanged();
            }
        }
    }

    // Has somewhere to go (or doesn't need one).
    public bool HasValidParentChoice =>
        !NeedsParentChoice
        || ParentChoice is { Kind: SetupParentChoiceKind.Existing or SetupParentChoiceKind.KeepAsMain }
        || (ParentChoice is { Kind: SetupParentChoiceKind.NewParent } && !string.IsNullOrWhiteSpace(NewParentName));

    // Offers the ticked main categories of the same kind (expense/income), then "New main
    // category…" and "Keep it as a main category", keeping the current choice if it's still there.
    public void RefreshParentChoices(IEnumerable<SetupCategoryItemViewModel> tickedParents)
    {
        var previous = parentChoice;
        isRebuildingChoices = true;
        try
        {
            ParentChoices.Clear();
            if (NeedsParentChoice)
            {
                foreach (var parent in tickedParents.Where(parent => parent.Type == Type))
                {
                    ParentChoices.Add(SetupParentChoiceViewModel.ForParent(parent));
                }

                ParentChoices.Add(SetupParentChoiceViewModel.NewParent());
                ParentChoices.Add(SetupParentChoiceViewModel.KeepAsMain());
            }
        }
        finally
        {
            isRebuildingChoices = false;
        }

        parentChoice = ParentChoices.FirstOrDefault(choice => choice.IsSameChoiceAs(previous));
        OnPropertyChanged(nameof(ParentChoice));
        OnPropertyChanged(nameof(NeedsParentChoice));
        OnPropertyChanged(nameof(IsNamingNewParent));
    }

    // After a language switch: names are read from resx each time.
    public void RefreshLanguage()
    {
        OnPropertyChanged(nameof(Name));
    }
}
