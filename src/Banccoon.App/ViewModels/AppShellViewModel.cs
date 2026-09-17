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
        // Fired on every navigation and never awaited by its caller (AppShell), so a transient
        // failure here (e.g. momentary SQLite lock contention from another in-flight call) must
        // not surface as an unobserved exception - this is a best-effort sidebar refresh, not a
        // correctness-critical read.
        try
        {
            var accounts = await accountRepository.GetAllAsync(cancellationToken);
            var favorites = accounts
                .Where(account => account.IsFavorite && !account.IsArchived)
                .Select(account => new SidebarAccountViewModel(account))
                .ToList();

            await RunOnMainThreadAsync(() =>
            {
                FavoriteAccounts.Clear();
                foreach (var favorite in favorites)
                {
                    FavoriteAccounts.Add(favorite);
                }
            });
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
        }
    }
}
