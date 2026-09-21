using System.Windows.Input;
using Banccoon.Core.Models;

namespace Banccoon.App.ViewModels;

public sealed class DashboardSectionRowViewModel
{
    public DashboardSectionRowViewModel(
        DashboardSection section,
        bool canMoveUp,
        bool canMoveDown,
        Action<DashboardSection> onMoveUp,
        Action<DashboardSection> onMoveDown)
    {
        Section = section;
        Name = section switch
        {
            DashboardSection.Upcoming => "Upcoming",
            DashboardSection.Analytics => "Analytics",
            DashboardSection.Goals => "Goals",
            _ => section.ToString()
        };
        CanMoveUp = canMoveUp;
        CanMoveDown = canMoveDown;

        MoveUpCommand = new RelayCommand(() => onMoveUp(Section));
        MoveDownCommand = new RelayCommand(() => onMoveDown(Section));
    }

    public DashboardSection Section { get; }

    public string Name { get; }

    public bool CanMoveUp { get; }

    public bool CanMoveDown { get; }

    public ICommand MoveUpCommand { get; }

    public ICommand MoveDownCommand { get; }
}
