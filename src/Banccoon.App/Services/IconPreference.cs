using System.ComponentModel;

namespace Banccoon.App.Services;

// Ambient "show icons on buttons" state (AppSettings.ShowIcons), same "static, no DI" shape as
// Formatting.PrivacyMode and Localization.Translator. Unlike PrivacyMode it notifies: every
// Controls.IconButton binds to ShowIcons, so flipping it in Settings swaps every button on every
// page between icon and text at once, no restart - the same live refresh Translator gives
// {loc:Translate}.
public sealed class IconPreference : INotifyPropertyChanged
{
    private bool showIcons = true;

    private IconPreference()
    {
    }

    public static IconPreference Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool ShowIcons
    {
        get => showIcons;
        set
        {
            if (showIcons == value)
            {
                return;
            }

            showIcons = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowIcons)));
        }
    }
}
