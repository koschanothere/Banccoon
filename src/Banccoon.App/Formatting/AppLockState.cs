namespace Banccoon.App.Formatting;

// Ambient state for the app-lock feature, same "static, read from anywhere" treatment as
// PrivacyMode. IsLockScreenActive exists specifically to stop AppShell's idle-check from pushing
// a second lock screen on top of one that's already showing (Shell.Navigated fires again for the
// very navigation that shows the lock screen itself).
public static class AppLockState
{
    private static DateTimeOffset lastActivityAt = DateTimeOffset.UtcNow;

    public static bool IsLockScreenActive { get; set; }

    public static void RecordActivity()
    {
        lastActivityAt = DateTimeOffset.UtcNow;
    }

    public static bool IsIdleTooLong(int autoLockMinutes)
    {
        return DateTimeOffset.UtcNow - lastActivityAt >= TimeSpan.FromMinutes(Math.Max(1, autoLockMinutes));
    }
}
