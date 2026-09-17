using System.Collections.ObjectModel;
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
    }

    public bool IsLoading
    {
        get => isLoading;
        private set => SetProperty(ref isLoading, value);
    }

    public ObservableCollection<AccountRowViewModel> Accounts { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            var accounts = await accountRepository.GetAllAsync(cancellationToken);
            Accounts.Clear();
            foreach (var account in accounts.Where(account => !account.IsArchived))
            {
                Accounts.Add(new AccountRowViewModel(account));
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
}
