using System.Collections.ObjectModel;
using Banccoon.Core.Repositories;

namespace Banccoon.App.ViewModels;

public sealed class AppShellViewModel : ViewModelBase
{
    private readonly IAccountRepository accountRepository;

    public AppShellViewModel(IAccountRepository accountRepository)
    {
        this.accountRepository = accountRepository;
        FavoriteAccounts = [];
    }

    public ObservableCollection<SidebarAccountViewModel> FavoriteAccounts { get; }

    public async Task RefreshFavoritesAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await accountRepository.GetAllAsync(cancellationToken);
        FavoriteAccounts.Clear();
        foreach (var account in accounts.Where(account => account.IsFavorite && !account.IsArchived))
        {
            FavoriteAccounts.Add(new SidebarAccountViewModel(account));
        }
    }
}
