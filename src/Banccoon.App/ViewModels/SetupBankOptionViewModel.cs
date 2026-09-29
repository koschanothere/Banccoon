namespace Banccoon.App.ViewModels;

// One bank (statement parser) in first-run setup's step 4.
public sealed class SetupBankOptionViewModel(string parserId, string name) : ViewModelBase
{
    private bool isSelected;

    public string ParserId { get; } = parserId;

    public string Name { get; } = name;

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }
}
