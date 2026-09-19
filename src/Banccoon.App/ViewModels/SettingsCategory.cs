namespace Banccoon.App.ViewModels;

// Purely a Settings-page navigation concept (which group of cards is showing) - not persisted,
// not shared outside this page, so it lives here rather than in Core alongside the real settings.
public enum SettingsCategory
{
    General,
    SecurityAndPrivacy,
    Dashboard,
    Transactions,
    DataAndBackup
}
