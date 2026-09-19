using System.Windows.Input;

namespace Banccoon.App.ViewModels;

// Rebuilt wholesale on every selection change (see SettingsViewModel.RebuildCategoryRows) rather
// than made individually mutable - only 5 of these ever exist, so recreating them on each click is
// simpler than wiring up INotifyPropertyChanged for a row this small.
public sealed class SettingsCategoryRowViewModel
{
    public SettingsCategoryRowViewModel(SettingsCategory category, string name, bool isSelected, Action<SettingsCategory> onSelect)
    {
        Category = category;
        Name = name;
        IsSelected = isSelected;
        SelectCommand = new RelayCommand(() => onSelect(Category));
    }

    public SettingsCategory Category { get; }

    public string Name { get; }

    public bool IsSelected { get; }

    public ICommand SelectCommand { get; }
}
