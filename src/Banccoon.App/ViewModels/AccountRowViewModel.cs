using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.Core.Models;

namespace Banccoon.App.ViewModels;

public sealed class AccountRowViewModel : ViewModelBase
{
    private bool isFavorite;

    public AccountRowViewModel(Account account, ICommand toggleFavoriteCommand)
    {
        Id = account.Id;
        Name = account.Name;
        TypeText = DisplayText.Format(account.Type);
        BalanceText = MoneyFormat.Format(account.CurrentBalance, account.Currency);
        IsArchived = account.IsArchived;
        isFavorite = account.IsFavorite;
        ToggleFavoriteCommand = toggleFavoriteCommand;
    }

    public Guid Id { get; }

    public string Name { get; }

    public string TypeText { get; }

    public string BalanceText { get; }

    public bool IsArchived { get; }

    public bool IsFavorite
    {
        get => isFavorite;
        set => SetProperty(ref isFavorite, value);
    }

    public ICommand ToggleFavoriteCommand { get; }
}
