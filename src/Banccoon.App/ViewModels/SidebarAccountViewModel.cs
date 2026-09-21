using Banccoon.App.Formatting;
using Banccoon.Core.Models;

namespace Banccoon.App.ViewModels;

public sealed class SidebarAccountViewModel
{
    public SidebarAccountViewModel(Account account)
    {
        Name = account.Name;
        BalanceText = MoneyFormat.Format(account.CurrentBalance, account.Currency);
    }

    public string Name { get; }

    public string BalanceText { get; }
}
