using System.Collections.ObjectModel;
using System.Windows.Input;
using PointlessWaymarks.SiteViewerMaui.Models;
using PointlessWaymarks.SiteViewerMaui.Services;
using PointlessWaymarks.SiteViewerMaui.Storage;
using PointlessWaymarks.SiteViewerMaui.Tools;

namespace PointlessWaymarks.SiteViewerMaui.ViewModels;

public class ConnectionsListViewModel : ObservableBase
{
    private readonly ProfileRepository _repository;
    private bool _hasLoadedOnce;
    private bool _isBusy;

    public ConnectionsListViewModel(ProfileRepository repository)
    {
        _repository = repository;

        Profiles = new SortedObservableCollection<CloudViewerProfile>(
            Comparer<CloudViewerProfile>.Create((a, b) =>
            {
                if (ReferenceEquals(a, b)) return 0;
                if (a is null) return -1;
                if (b is null) return 1;
                var nameComparison = StringComparer.CurrentCultureIgnoreCase.Compare(a.Name ?? string.Empty, b.Name ?? string.Empty);
                return nameComparison != 0 ? nameComparison : a.Id.CompareTo(b.Id);
            }));

        AddCommand = new Command(async () => await AddAsync());
        EditCommand = new Command<CloudViewerProfile>(async profile => await EditAsync(profile));
        DeleteCommand = new Command<CloudViewerProfile>(async profile => await DeleteAsync(profile));
        OpenCommand = new Command<CloudViewerProfile>(async profile => await OpenAsync(profile));
    }

    public ObservableCollection<CloudViewerProfile> Profiles { get; }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public ICommand AddCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand OpenCommand { get; }

    public event EventHandler<CloudViewerProfile>? ScrollToRequested;

    public void RequestScrollTo(CloudViewerProfile profile)
    {
        ScrollToRequested?.Invoke(this, profile);
    }

    public async Task LoadAsync(Guid? scrollToProfileId = null)
    {
        IsBusy = true;
        try
        {
            var previousIds = _hasLoadedOnce ? Profiles.Select(x => x.Id).ToHashSet() : null;
            var profiles = await _repository.LoadAsync();

            Profiles.Clear();
            foreach (var profile in profiles.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase))
                Profiles.Add(profile);

            if (scrollToProfileId.HasValue)
            {
                var target = Profiles.FirstOrDefault(x => x.Id == scrollToProfileId.Value);
                if (target is not null)
                {
                    ScrollToRequested?.Invoke(this, target);
                }
            }
            else if (_hasLoadedOnce && previousIds is not null)
            {
                var newlyAdded = Profiles.FirstOrDefault(x => !previousIds.Contains(x.Id));
                if (newlyAdded is not null)
                {
                    ScrollToRequested?.Invoke(this, newlyAdded);
                }
            }

            _hasLoadedOnce = true;
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
