using System.Globalization;
using System.Windows.Input;
using Banccoon.Core.Models;
using Banccoon.Core.Repositories;
using Banccoon.Core.Security;

namespace Banccoon.App.ViewModels;

public sealed class AppLockSettingsViewModel : ViewModelBase
{
    private readonly ISettingsRepository settingsRepository;

    private bool isPinSet;
    private string newPinText = string.Empty;
    private string confirmPinText = string.Empty;
    private string autoLockMinutesText = "5";
    private string statusText = string.Empty;

    public AppLockSettingsViewModel(ISettingsRepository settingsRepository)
    {
        this.settingsRepository = settingsRepository;

        SetPinCommand = new RelayCommand(() => _ = SetPinAsync());
        RemovePinCommand = new RelayCommand(() => _ = RemovePinAsync());
        SaveAutoLockCommand = new RelayCommand(() => _ = SaveAutoLockAsync());
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

    public string AutoLockMinutesText
    {
        get => autoLockMinutesText;
        set => SetProperty(ref autoLockMinutesText, value);
    }

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public ICommand SetPinCommand { get; }

    public ICommand RemovePinCommand { get; }

    public ICommand SaveAutoLockCommand { get; }

    public Task InitializeAsync(AppSettings settings)
    {
        IsPinSet = settings.AppLockPinHash is not null;
        AutoLockMinutesText = settings.AppLockAutoLockMinutes.ToString(CultureInfo.InvariantCulture);
        NewPinText = string.Empty;
        ConfirmPinText = string.Empty;
        StatusText = string.Empty;
        return Task.CompletedTask;
    }

    private async Task SetPinAsync()
    {
        if (string.IsNullOrWhiteSpace(NewPinText) || NewPinText.Length < 4 || !NewPinText.All(char.IsDigit))
        {
            StatusText = "PIN must be at least 4 digits.";
            return;
        }

        if (NewPinText != ConfirmPinText)
        {
            StatusText = "PINs don't match.";
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
            StatusText = "PIN set - the app will ask for it next time it locks.";
        });
    }

    private async Task RemovePinAsync()
    {
        var settings = await settingsRepository.GetAsync();
        await settingsRepository.SaveAsync(settings with { AppLockPinHash = null, AppLockPinSalt = null });

        await RunOnMainThreadAsync(() =>
        {
            IsPinSet = false;
            StatusText = "PIN removed.";
        });
    }

    private async Task SaveAutoLockAsync()
    {
        if (!int.TryParse(AutoLockMinutesText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes) || minutes < 1)
        {
            StatusText = "Must be a whole number of at least 1 minute.";
            return;
        }

        var settings = await settingsRepository.GetAsync();
        await settingsRepository.SaveAsync(settings with { AppLockAutoLockMinutes = minutes });

        await RunOnMainThreadAsync(() => StatusText = "Saved.");
    }
}
