namespace Banccoon.App.ViewModels;

public sealed class NamedOptionViewModel
{
    public NamedOptionViewModel(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public Guid Id { get; }

    public string Name { get; }

    public override string ToString() => Name;
}
