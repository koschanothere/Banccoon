using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Banccoon.App.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // Microsoft.Data.Sqlite's async calls use ConfigureAwait(false) internally, so code after
    // "await someRepository.GetXAsync()" can resume on a thread-pool thread instead of the UI
    // thread. Mutating an ObservableCollection bound to a live CollectionView from off the UI
    // thread is illegal in WinUI and throws a native COMException (0x8000FFFF/E_UNEXPECTED) -
    // confirmed via diagnostics.log after a real report of the sidebar going blank mid-navigation.
    // Every ViewModel that rebuilds a bound collection after an await should route the mutation
    // through this rather than touching the collection directly.
    protected static Task RunOnMainThreadAsync(Action action) => MainThread.InvokeOnMainThreadAsync(action);
}
