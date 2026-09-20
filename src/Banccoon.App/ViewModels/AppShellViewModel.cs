using System.Collections.ObjectModel;
using Banccoon.App.Diagnostics;
using Banccoon.Core.Repositories;

namespace Banccoon.App.ViewModels;

public sealed class AppShellViewModel : ViewModelBase
{
    private readonly IAccountRepository accountRepository;

    private string currentTabImageSource = "raccoon_dashboard_icon.png";

    public AppShellViewModel(IAccountRepository accountRepository)
    {
        this.accountRepository = accountRepository;
        FavoriteAccounts = [];
    }

    public ObservableCollection<SidebarAccountViewModel> FavoriteAccounts { get; }

    public string CurrentTabImageSource
    {
        get => currentTabImageSource;
        private set => SetProperty(ref currentTabImageSource, value);
    }

    // Called from AppShell's Shell.Navigated hook with the new route's location - swaps the
    // sidebar badge to match whichever of the 4 main tabs is active. Anything else (e.g. the
    // statementImport route, which isn't a FlyoutItem/tab) falls back to the dashboard image.
    public void SetCurrentTab(string routeLocation)
    {
        CurrentTabImageSource = routeLocation switch
        {
            _ when routeLocation.Contains("transactions", StringComparison.OrdinalIgnoreCase) => "raccoon_transactions_icon.png",
            _ when routeLocation.Contains("accounts", StringComparison.OrdinalIgnoreCase) => "raccoon_accounts_icon.png",
            _ when routeLocation.Contains("settings", StringComparison.OrdinalIgnoreCase) => "raccoon_settings_icon.png",
            _ => "raccoon_dashboard_icon.png"
        };
    }

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
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            DiagnosticLog.Write($"RefreshFavoritesAsync swallowed exception: {ex}");
        }
    }
}
