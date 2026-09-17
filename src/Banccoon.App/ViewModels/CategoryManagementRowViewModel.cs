using System.Windows.Input;
using Banccoon.Core.Models;

namespace Banccoon.App.ViewModels;

public sealed class CategoryManagementRowViewModel : ViewModelBase
{
    private string name;

    public CategoryManagementRowViewModel(Category category, Func<Guid, string, Task> onRename, Func<Guid, Task> onDelete)
    {
        Id = category.Id;
        name = category.Name;
        RenameCommand = new RelayCommand(() => _ = onRename(Id, Name));
        DeleteCommand = new RelayCommand(() => _ = onDelete(Id));
    }

    public Guid Id { get; }

    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }

    public ICommand RenameCommand { get; }

    public ICommand DeleteCommand { get; }
}
