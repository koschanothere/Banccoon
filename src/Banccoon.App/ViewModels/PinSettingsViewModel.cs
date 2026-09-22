using System.Windows.Input;
using Banccoon.App.Localization;
using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Core.Security;

namespace Banccoon.App.ViewModels;

public sealed class PinSettingsViewModel : ViewModelBase
{
    private readonly ISettingsRepository settingsRepository;

    private bool isPinSet;
    private string newPinText = string.Empty;
    private string confirmPinText = string.Empty;
    private string statusText = string.Empty;

    public PinSettingsViewModel(ISettingsRepository settingsRepository)
    {
        this.settingsRepository = settingsRepository;

        SetPinCommand = new RelayCommand(() => _ = SetPinAsync());
        RemovePinCommand = new RelayCommand(() => _ = RemovePinAsync());
    }

    public bool IsPinSet
    {
        get => isPinSet;
        private set => SetProperty(ref isPinSet, value);
    }

    public string NewPinText
    {
        get => newPinText;
        set => SetProperty(ref newPinText, value);
    }

    public string ConfirmPinText
    {
        get => confirmPinText;
        set => SetProperty(ref confirmPinText, value);
    }

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public ICommand SetPinCommand { get; }

    public ICommand RemovePinCommand { get; }

    public Task InitializeAsync(AppSettings settings)
    {
        IsPinSet = settings.AppLockPinHash is not null;
        NewPinText = string.Empty;
        ConfirmPinText = string.Empty;
        StatusText = string.Empty;
        return Task.CompletedTask;
    }

    private async Task SetPinAsync()
    {
        if (string.IsNullOrWhiteSpace(NewPinText) || NewPinText.Length < 4 || !NewPinText.All(char.IsDigit))
        {
            StatusText = Translator.Get("Settings_PinMinDigits");
            return;
        }

        if (NewPinText != ConfirmPinText)
        {
            StatusText = Translator.Get("Settings_PinsDontMatch");
            return;
        }

        var salt = PinHasher.GenerateSalt();
        var hash = PinHasher.Hash(NewPinText, salt);

        var settings = await settingsRepository.GetAsync();
        await settingsRepository.SaveAsync(settings with { AppLockPinHash = hash, AppLockPinSalt = salt });

        // Touches UI-bound state after an await that may have resumed off the UI thread (see
        // ViewModelBase.RunOnMainThreadAsync).
        await RunOnMainThreadAsync(() =>
        {
            IsPinSet = true;
            NewPinText = string.Empty;
            ConfirmPinText = string.Empty;
            StatusText = Translator.Get("Settings_PinSetConfirmation");
        });
    }

    private async Task RemovePinAsync()
    {
        var settings = await settingsRepository.GetAsync();
        await settingsRepository.SaveAsync(settings with { AppLockPinHash = null, AppLockPinSalt = null });

        await RunOnMainThreadAsync(() =>
        {
            IsPinSet = false;
            StatusText = Translator.Get("Settings_PinRemoved");
        });
    }
}
