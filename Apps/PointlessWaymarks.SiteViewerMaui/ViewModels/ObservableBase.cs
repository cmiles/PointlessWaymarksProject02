using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PointlessWaymarks.SiteViewerMaui.ViewModels;

/// <summary>
///     Minimal, self-contained <see cref="INotifyPropertyChanged" /> base for the view models. Kept
///     deliberately tiny so the app does not need to reference an external MVVM toolkit.
/// </summary>
public abstract class ObservableBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
