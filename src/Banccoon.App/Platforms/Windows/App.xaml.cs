using Banccoon.App.Diagnostics;
using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace Banccoon.App.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        InitializeComponent();
        // XAML/dispatcher-thread exceptions in WinUI don't reliably surface through
        // AppDomain.UnhandledException - this is the WinUI-specific hook for those, part of the
        // same diagnostics effort as MauiProgram.CreateMauiApp for the intermittent sidebar bug.
        UnhandledException += OnUnhandledException;
    }

    protected override MauiApp CreateMauiApp()
    {
        return MauiProgram.CreateMauiApp();
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        DiagnosticLog.Write($"WinUI unhandled exception (handled={e.Handled}): {e.Exception}");
    }
}
