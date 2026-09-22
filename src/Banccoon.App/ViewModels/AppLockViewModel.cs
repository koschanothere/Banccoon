using System.Windows.Input;
using Banccoon.App.Formatting;
using Banccoon.App.Localization;
using Banccoon.Core.Repositories;
using Banccoon.Core.Security;

namespace Banccoon.App.ViewModels;

public sealed class AppLockViewModel : ViewModelBase
{
    private readonly ISettingsRepository settingsRepository;
    private string pinText = string.Empty;
    private string statusText = string.Empty;

    public AppLockViewModel(ISettingsRepository settingsRepository)
    {
        this.settingsRepository = settingsRepository;
        UnlockCommand = new RelayCommand(() => _ = UnlockAsync());
    }

    // The page's code-behind pops the modal lock screen on success - ViewModels in this app don't
    // touch Navigation directly (see StatementImportPage.xaml.cs's OnCloseClicked).
    public event Action? Unlocked;

    public string PinText
    {
        get => pinText;
        set => SetProperty(ref pinText, value);
    }

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public ICommand UnlockCommand { get; }

    private async Task UnlockAsync()
    {
        var settings = await settingsRepository.GetAsync();

        // No PIN configured any more (e.g. removed from another already-open window) - nothing
        // left to check against, so just let it through rather than stranding the user.
        var isCorrect = settings.AppLockPinHash is null || settings.AppLockPinSalt is null
            || PinHasher.Verify(PinText, settings.AppLockPinSalt, settings.AppLockPinHash);

        // Touches UI-bound state after an await that may have resumed off the UI thread (see
        // ViewModelBase.RunOnMainThreadAsync).
        await RunOnMainThreadAsync(() =>
        {
            PinText = string.Empty;
            if (isCorrect)
            {
                StatusText = string.Empty;
                Unlocked?.Invoke();
            }
            else
            {
                StatusText = Translator.Get("AppLock_IncorrectPin");
            }
        });
    }
}
