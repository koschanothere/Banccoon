using Banccoon.App.Formatting;
using Banccoon.Core.Models;

namespace Banccoon.App.ViewModels;

public sealed class AccountRowViewModel
{
    public AccountRowViewModel(Account account)
    {
        Name = account.Name;
        TypeText = DisplayText.Format(account.Type);
        BalanceText = MoneyFormat.Format(account.CurrentBalance, account.Currency);
        IsArchived = account.IsArchived;
    }

    public string Name { get; }

    public string TypeText { get; }

    public string BalanceText { get; }

    public bool IsArchived { get; }
}
