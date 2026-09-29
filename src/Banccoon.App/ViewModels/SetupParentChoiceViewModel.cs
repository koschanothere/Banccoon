using Banccoon.App.Localization;

namespace Banccoon.App.ViewModels;

public enum SetupParentChoiceKind
{
    // Under one of the ticked main categories (ParentKey).
    Existing,

    // Under a new main category the user names.
    NewParent,

    // On its own, as a main category.
    KeepAsMain
}

// One entry of the "Put under:" picker a first-run setup subcategory gets when it's ticked but
// its own main category isn't (SetupCategoryItemViewModel.NeedsParentChoice).
public sealed class SetupParentChoiceViewModel
{
    private readonly Func<string> name;

    private SetupParentChoiceViewModel(SetupParentChoiceKind kind, string? parentKey, Func<string> name)
    {
        Kind = kind;
        ParentKey = parentKey;
        this.name = name;
    }

    public SetupParentChoiceKind Kind { get; }

    public string? ParentKey { get; }

    public string Name => name();

    public static SetupParentChoiceViewModel ForParent(SetupCategoryItemViewModel parent) =>
        new(SetupParentChoiceKind.Existing, parent.Key, () => parent.Name);

    public static SetupParentChoiceViewModel NewParent() =>
        new(SetupParentChoiceKind.NewParent, null, () => Translator.Get("Setup_ParentChoiceNew"));

    public static SetupParentChoiceViewModel KeepAsMain() =>
        new(SetupParentChoiceKind.KeepAsMain, null, () => Translator.Get("Setup_ParentChoiceKeepAsMain"));

    public bool IsSameChoiceAs(SetupParentChoiceViewModel? other) =>
        other is not null && other.Kind == Kind && other.ParentKey == ParentKey;

    public override string ToString() => Name;
}
