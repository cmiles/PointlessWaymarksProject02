using System.Windows;

namespace PointlessWaymarks.PhotoMetadataBasicsGui;

public enum MainWindowTab
{
    Photos = 0,
    PhotoImportToDateFolders = 1,
    Settings = 2,
    AboutHelp = 3
}

public class MainWindowTabChangeRequestedEventArgs(MainWindowTab requestedTab) : EventArgs
{
    public MainWindowTab RequestedTab { get; } = requestedTab;
}

public sealed class MainWindowTabChangeRequest
{
    public void AddHandler(EventHandler<MainWindowTabChangeRequestedEventArgs> handler)
    {
        WeakEventManager<MainWindowTabChangeRequest, MainWindowTabChangeRequestedEventArgs>.AddHandler(this,
            nameof(Requested), handler);
    }

    public void Raise(MainWindowTab requestedTab)
    {
        Requested?.Invoke(this, new MainWindowTabChangeRequestedEventArgs(requestedTab));
    }

    public void RemoveHandler(EventHandler<MainWindowTabChangeRequestedEventArgs> handler)
    {
        WeakEventManager<MainWindowTabChangeRequest, MainWindowTabChangeRequestedEventArgs>.RemoveHandler(this,
            nameof(Requested), handler);
    }

    public event EventHandler<MainWindowTabChangeRequestedEventArgs>? Requested;
}

public static class MainWindowEvents
{
    public static MainWindowTabChangeRequest RequestMainTabChange { get; } = new();
}