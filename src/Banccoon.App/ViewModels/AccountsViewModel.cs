using System.Collections.ObjectModel;
using System.Windows.Input;
using Banccoon.Core.Abstractions;
using Banccoon.Core.CreditCards;
using Banccoon.Core.Repositories;

namespace Banccoon.App.ViewModels;

public sealed class AccountsViewModel : ViewModelBase
{
    private readonly IAccountRepository accountRepository;
    private readonly ISettingsRepository settingsRepository;

    private string currency = "EUR";
    private Guid? primaryAccountId;
    private bool isLoading;
    private bool showArchived;

    public AccountsViewModel(
        IAccountRepository accountRepository,
        ISettingsRepository settingsRepository,
        ICreditCardForecastService creditCardForecastService,
        IDateProvider dateProvider)
    {
        this.accountRepository = accountRepository;
        this.settingsRepository = settingsRepository;

        Form = new AccountFormViewModel(accountRepository, () => InitializeAsync());
        CardDetails = new CreditCardDetailsViewModel(creditCardForecastService, dateProvider);

        Accounts = [];
        ToggleFavoriteCommand = new RelayCommand<Guid>(id => _ = ToggleFavoriteAsync(id));
        ToggleShowArchivedCommand = new RelayCommand(() => ShowArchived = !ShowArchived);
        OpenAddAccountCommand = new RelayCommand(() => Form.OpenForCreate(currency));
    }

    public AccountFormViewModel Form { get; }

    public CreditCardDetailsViewModel CardDetails { get; }

    public bool IsLoading
    {
        get => isLoading;
        private set => SetProperty(ref isLoading, value);
    }

    public bool ShowArchived
    {
        get => showArchived;
        private set
        {
            if (SetProperty(ref showArchived, value))
            {
                OnPropertyChanged(nameof(ShowArchivedToggleText));
                _ = InitializeAsync();
            }
        }
    }

    public string ShowArchivedToggleText => ShowArchived ? "Show active" : "Show archived";

    public ObservableCollection<AccountRowViewModel> Accounts { get; }

    public ICommand ToggleFavoriteCommand { get; }

    public ICommand ToggleShowArchivedCommand { get; }

    public ICommand OpenAddAccountCommand { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            var settings = await settingsRepository.GetAsync(cancellationToken);
            currency = settings.DefaultCurrency;
            primaryAccountId = settings.PrimaryAccountId;

            var accounts = await accountRepository.GetAllAsync(cancellationToken);
            var visible = accounts
                .Where(account => account.IsArchived == ShowArchived)
                .OrderBy(account => account.Name)
                .ToList();

            // Mutates a collection bound to live UI - must run on the UI thread, which the awaits
            // above may have resumed off of (see ViewModelBase.RunOnMainThreadAsync).
            await RunOnMainThreadAsync(() =>
            {
                Accounts.Clear();
                foreach (var account in visible)
                {
                    Accounts.Add(new AccountRowViewModel(
                        account,
                        account.Id == primaryAccountId,
                        ToggleFavoriteCommand,
                        onEdit: Form.OpenForEdit,
                        onArchive: ArchiveAsync,
                        onUnarchive: UnarchiveAsync,
                        onSetPrimary: SetPrimaryAsync,
                        onOpenCardDetails: CardDetails.Open));
                }
            });
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

    private async Task ArchiveAsync(Guid accountId)
    {
        var account = await accountRepository.GetByIdAsync(accountId);
        if (account is null)
        {
            return;
        }

        await accountRepository.SaveAsync(account with { IsArchived = true });
        await InitializeAsync();
    }

    private async Task UnarchiveAsync(Guid accountId)
    {
        var account = await accountRepository.GetByIdAsync(accountId);
        if (account is null)
        {
            return;
        }

        await accountRepository.SaveAsync(account with { IsArchived = false });
        await InitializeAsync();
    }

    private async Task SetPrimaryAsync(Guid accountId)
    {
        var settings = await settingsRepository.GetAsync();
        await settingsRepository.SaveAsync(settings with { PrimaryAccountId = accountId });
        await InitializeAsync();
    }
}
