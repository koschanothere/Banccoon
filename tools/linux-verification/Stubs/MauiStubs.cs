// Minimal signatures only - enough for the real view-model code to type-check.
namespace Microsoft.Maui
{
    public enum StubPlaceholder { }
    public interface IActivationState { }
    public readonly struct Thickness { public Thickness(double left, double top, double right, double bottom) { } }
}
namespace Microsoft.Maui.ApplicationModel
{
    public static class MainThread
    {
        public static Task InvokeOnMainThreadAsync(Action action) { action(); return Task.CompletedTask; }
        public static Task<T> InvokeOnMainThreadAsync<T>(Func<T> func) => Task.FromResult(func());
        public static Task InvokeOnMainThreadAsync(Func<Task> func) => func();
        public static void BeginInvokeOnMainThread(Action action) => action();
        public static bool IsMainThread => true;
    }
    public interface IAppInfo { string VersionString { get; } Version Version { get; } string BuildString { get; } string Name { get; } }
    public static class AppInfo
    {
        private sealed class Impl : IAppInfo { public string VersionString => "1.0"; public Version Version => new(1, 0); public string BuildString => "1"; public string Name => "Banccoon"; }
        public static IAppInfo Current { get; } = new Impl();
        public static string VersionString => "1.0";
        public static string BuildString => "1";
    }
    public enum AppTheme { Unspecified, Light, Dark }
}
namespace Microsoft.Maui.Controls
{
    using Microsoft.Maui.ApplicationModel;
    public class Application
    {
        public static Application? Current { get; set; }
        public AppTheme UserAppTheme { get; set; }
        public AppTheme RequestedTheme { get; set; }
        public IDictionary<string, object> Resources { get; } = new Dictionary<string, object>();
        protected virtual Window CreateWindow(Microsoft.Maui.IActivationState? activationState) => new(new Page());
    }
    public class Window { public Window(Page page) { } }
    public class VisualElement { public object? BindingContext { get; set; } public double Width { get; } public event EventHandler? SizeChanged { add { } remove { } } }
    public class View : VisualElement { public Thickness Margin { get; set; } }
    public class Button : View { }
    public class Border : View { }
    public class Page : VisualElement { protected virtual void OnAppearing() { } protected virtual void OnDisappearing() { } }
    public class ContentPage : Page { }
    public interface IQueryAttributable { void ApplyQueryAttributes(IDictionary<string, object> query); }
    public class ShellNavigationState { public ShellNavigationState(string s) { } public static implicit operator ShellNavigationState(string s) => new(s); }
    public class Shell
    {
        public static Shell Current { get; set; } = new();
        public Task GoToAsync(ShellNavigationState state) => Task.CompletedTask;
        public Task GoToAsync(ShellNavigationState state, bool animate) => Task.CompletedTask;
        public Task GoToAsync(ShellNavigationState state, IDictionary<string, object> parameters) => Task.CompletedTask;
    }
}
namespace Microsoft.Maui.Devices
{
    public readonly struct DevicePlatform : IEquatable<DevicePlatform>
    {
        private readonly string name;
        private DevicePlatform(string name) { this.name = name; }
        public static DevicePlatform WinUI { get; } = new("WinUI");
        public static DevicePlatform Android { get; } = new("Android");
        public static DevicePlatform iOS { get; } = new("iOS");
        public static DevicePlatform MacCatalyst { get; } = new("MacCatalyst");
        public bool Equals(DevicePlatform other) => name == other.name;
        public override bool Equals(object? obj) => obj is DevicePlatform p && Equals(p);
        public override int GetHashCode() => name?.GetHashCode() ?? 0;
    }
}
namespace Microsoft.Maui.Storage
{
    using Microsoft.Maui.Devices;
    public class FilePickerFileType { public FilePickerFileType(IDictionary<DevicePlatform, IEnumerable<string>> fileTypes) { } }
    public class PickOptions { public string? PickerTitle { get; set; } public FilePickerFileType? FileTypes { get; set; } }
    public class FileBase { public string FullPath { get; } = ""; public string FileName { get; set; } = ""; public string ContentType { get; set; } = ""; }
    public class FileResult : FileBase { }
    public interface IFilePicker { Task<FileResult?> PickAsync(PickOptions? options = null); }
    public static class FilePicker
    {
        private sealed class Impl : IFilePicker { public Task<FileResult?> PickAsync(PickOptions? options = null) => Task.FromResult<FileResult?>(null); }
        public static IFilePicker Default { get; } = new Impl();
    }
    public static class FileSystem { public static string AppDataDirectory => "/tmp"; }
}
