using Banccoon.App.Localization;
using Banccoon.App.Services;
using Banccoon.App.ViewModels;
using Banccoon.Tests.Infrastructure;
using Xunit;

// Settings -> General "Icons on buttons" (2026-09-29): on by default, saved with the rest of the
// card, and applied app-wide through IconPreference (what every IconButton binds to) on Save.
public sealed class ShowIconsSettingTests
{
    [Fact]
    public async Task ShowIcons_DefaultsOn_AndSavingItOffPersistsAndFlipsIconPreference()
    {
        Translator.SetLanguage("en");
        await using var store = new SqliteTestStore();
        var vm = new GeneralPreferencesViewModel(store.Settings);
        await vm.InitializeAsync(await store.Settings.GetAsync());
        Assert.True(vm.ShowIcons);

        try
        {
            vm.ShowIcons = false;
            vm.SaveCommand.Execute(null);
            await WaitUntilAsync(() => vm.StatusText == Translator.Get("Common_Saved"));

            Assert.False((await store.Settings.GetAsync()).ShowIcons);
            Assert.False(IconPreference.Instance.ShowIcons);
        }
        finally
        {
            IconPreference.Instance.ShowIcons = true;
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(50);
        }

        Assert.True(condition());
    }
}
