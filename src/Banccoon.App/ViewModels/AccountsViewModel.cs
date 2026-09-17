using System.Collections.ObjectModel;
using System.Windows.Input;
using Banccoon.Core.Repositories;

namespace Banccoon.App.ViewModels;

public sealed class AccountsViewModel : ViewModelBase
{
    private readonly IAccountRepository accountRepository;
    private bool isLoading;

    public AccountsViewModel(IAccountRepository accountRepository)
    {
        this.accountRepository = accountRepository;
        Accounts = [];
        ToggleFavoriteCommand = new RelayCommand<Guid>(id => _ = ToggleFavoriteAsync(id));
    }

    public bool IsLoading
    {
        get => isLoading;
        private set => SetProperty(ref isLoading, value);
    }

    public ObservableCollection<AccountRowViewModel> Accounts { get; }

    public ICommand ToggleFavoriteCommand { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            var accounts = await accountRepository.GetAllAsync(cancellationToken);
            Accounts.Clear();
            foreach (var account in accounts.Where(account => !account.IsArchived))
            {
                Accounts.Add(new AccountRowViewModel(account, ToggleFavoriteCommand));
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ToggleFavoriteAsync(Guid accountId)
    {
        var row = Accounts.FirstOrDefault(account => account.Id == accountId);
        if (row is null)
        {
            return;
        }

        var account = await accountRepository.GetByIdAsync(accountId);
        if (account is null)
        {
            return;
        }

        var updated = account with { IsFavorite = !account.IsFavorite };
        await accountRepository.SaveAsync(updated);
        row.IsFavorite = updated.IsFavorite;
    }
}
