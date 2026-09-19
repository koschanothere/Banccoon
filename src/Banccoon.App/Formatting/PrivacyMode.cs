namespace Banccoon.App.Formatting;

// Global, ambient read state for whether amounts should be masked - same "read from anywhere,
// no DI" treatment this app already gives Application.Current.RequestedTheme (see the
// Increase/CurrentBar chart color converters). Every page already recomputes its formatted
// strings on each visit (InitializeAsync/OnAppearing), so a plain static flag checked at
// format-time is enough for toggling this to take effect - no reactive event plumbing needed,
// since navigating to/from Settings to flip it naturally revisits whatever page you land on next.
public static class PrivacyMode
{
    public static bool IsEnabled { get; set; }
}
