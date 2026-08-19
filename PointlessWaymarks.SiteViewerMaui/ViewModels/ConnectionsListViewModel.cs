using System.Collections.ObjectModel;
using System.Windows.Input;
using PointlessWaymarks.SiteViewerMaui.Models;
using PointlessWaymarks.SiteViewerMaui.Services;
using PointlessWaymarks.SiteViewerMaui.Storage;

namespace PointlessWaymarks.SiteViewerMaui.ViewModels;

public class ConnectionsListViewModel : ObservableBase
{
    private readonly ProfileRepository _repository;
    private bool _isBusy;

    public ConnectionsListViewModel(ProfileRepository repository)
    {
        _repository = repository;

        AddCommand = new Command(async () => await AddAsync());
        EditCommand = new Command<CloudViewerProfile>(async profile => await EditAsync(profile));
        DeleteCommand = new Command<CloudViewerProfile>(async profile => await DeleteAsync(profile));
        OpenCommand = new Command<CloudViewerProfile>(async profile => await OpenAsync(profile));
    }

    public ObservableCollection<CloudViewerProfile> Profiles { get; } = new();

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public ICommand AddCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand OpenCommand { get; }

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var profiles = await _repository.LoadAsync();

            Profiles.Clear();
            foreach (var profile in profiles.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase))
                Profiles.Add(profile);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static Task AddAsync()
    {
        return Shell.Current.GoToAsync(Routes.ConnectionEdit);
    }

    private static Task EditAsync(CloudViewerProfile? profile)
    {
        if (profile is null) return Task.CompletedTask;
        return Shell.Current.GoToAsync($"{Routes.ConnectionEdit}?profileId={profile.Id}");
    }

    private async Task DeleteAsync(CloudViewerProfile? profile)
    {
        if (profile is null) return;

        var confirm = await Shell.Current.DisplayAlertAsync("Delete Connection",
            $"Delete '{profile.Name}'? This also removes its stored credentials.", "Delete", "Cancel");

        if (!confirm) return;

        await _repository.DeleteAsync(profile.Id);
        await LoadAsync();
    }

    private static Task OpenAsync(CloudViewerProfile? profile)
    {
        if (profile is null) return Task.CompletedTask;
        return Shell.Current.GoToAsync($"{Routes.Viewer}?profileId={profile.Id}");
    }
}
