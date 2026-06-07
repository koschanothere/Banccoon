namespace Banccoon.App.ViewModels;

public sealed class NavigationItemViewModel : ViewModelBase
{
    private bool isSelected;

    public NavigationItemViewModel(AppSection section, string title, string glyph, string description)
    {
        Section = section;
        Title = title;
        Glyph = glyph;
        Description = description;
    }

    public AppSection Section { get; }

    public string Title { get; }

    public string Glyph { get; }

    public string Description { get; }

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }
}
