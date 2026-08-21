using System.Windows.Input;
using PointlessWaymarks.SiteViewerMaui.Models;
using PointlessWaymarks.SiteViewerMaui.S3;
using PointlessWaymarks.SiteViewerMaui.Storage;
using PointlessWaymarks.SiteViewerMaui.Tools;

namespace PointlessWaymarks.SiteViewerMaui.ViewModels;

public class ConnectionEditViewModel : ObservableBase, IQueryAttributable
{
    private readonly ISecureCredentialStore _credentialStore;
    private readonly ProfileRepository _repository;

    private string _accessKey = string.Empty;
    private string _bucket = string.Empty;
    private Guid _id = Guid.NewGuid();
    private string _name = string.Empty;
    private string _provider = nameof(S3Providers.Amazon);
    private string _region = string.Empty;
    private string _secret = string.Empty;
    private string _serviceUrl = string.Empty;
    private string _siteDomain = string.Empty;
    private string _title = "Add Connection";

    public ConnectionEditViewModel(ProfileRepository repository, ISecureCredentialStore credentialStore)
    {
        _repository = repository;
        _credentialStore = credentialStore;

        SaveCommand = new Command(async () => await SaveAsync());
        CancelCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
        LoadFromFileCommand = new Command(async () => await LoadFromFileAsync());
    }

    public List<string> ProviderChoices { get; } = Enum.GetNames(typeof(S3Providers)).ToList();

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Provider
    {
        get => _provider;
        set
        {
            if (SetProperty(ref _provider, value))
            {
                OnPropertyChanged(nameof(IsAmazon));
                OnPropertyChanged(nameof(IsServiceUrlProvider));
            }
        }
    }

    public string Region
    {
        get => _region;
        set => SetProperty(ref _region, value);
    }

    public string Bucket
    {
        get => _bucket;
        set => SetProperty(ref _bucket, value);
    }

    public string SiteDomain
    {
        get => _siteDomain;
        set => SetProperty(ref _siteDomain, value);
    }

    public string ServiceUrl
    {
        get => _serviceUrl;
        set => SetProperty(ref _serviceUrl, value);
    }

    public string AccessKey
    {
        get => _accessKey;
        set => SetProperty(ref _accessKey, value);
    }

    public string Secret
    {
        get => _secret;
        set => SetProperty(ref _secret, value);
    }

    public bool IsAmazon => Provider == nameof(S3Providers.Amazon);
    public bool IsServiceUrlProvider => !IsAmazon;

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand LoadFromFileCommand { get; }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("profileId", out var value) && Guid.TryParse(value?.ToString(), out var parsedId))
            await LoadAsync(parsedId);
    }

    private async Task LoadAsync(Guid id)
    {
        var profiles = await _repository.LoadAsync();
        var profile = profiles.FirstOrDefault(x => x.Id == id);
        if (profile is null) return;

        _id = profile.Id;
        Name = profile.Name;
        Provider = string.IsNullOrWhiteSpace(profile.Provider) ? nameof(S3Providers.Amazon) : profile.Provider;
        Region = profile.Region;
        Bucket = profile.Bucket;
        SiteDomain = profile.SiteDomain;
        Title = "Edit Connection";

        var secrets = await _credentialStore.GetAsync(id);
        AccessKey = secrets.AccessKey;
        Secret = secrets.Secret;
        ServiceUrl = secrets.ServiceUrl ?? string.Empty;
    }

    /// <summary>
    ///     Lets the user pick a settings file (a plain JSON file or a JSON file encrypted with
    ///     <see cref="ObfuscationTools" />) and overwrites any settings found in it onto the editor.
    ///     Partial files are fine (only the values present are applied) and any failure to pick, read,
    ///     decrypt or deserialize is reported to the user without crashing.
    /// </summary>
    private async Task LoadFromFileAsync()
    {
        try
        {
            FileResult? pick;
            try
            {
                pick = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Pick a Connection Settings File (JSON or Encrypted JSON)"
                });
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Load Failed",
                    $"The file could not be opened: {ex.Message}", "OK");
                return;
            }

            if (pick is null) return; // user cancelled the picker

            string fileText;
            try
            {
                await using var stream = await pick.OpenReadAsync();
                using var reader = new StreamReader(stream);
                fileText = await reader.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Load Failed",
                    $"The file could not be read: {ex.Message}", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(fileText))
            {
                await Shell.Current.DisplayAlertAsync("Load Failed", "The selected file was empty.", "OK");
                return;
            }

            ConnectionSettingsImport? imported;

            if (string.Equals(ConnectionSettingsImport.ReadSettingsType(fileText), "SecureCloudViewer",
                    StringComparison.OrdinalIgnoreCase))
            {
                var password = await Shell.Current.DisplayPromptAsync("Password",
                    "Enter the password used to encrypt this file.", "OK", "Cancel");

                if (string.IsNullOrEmpty(password)) return; // cancelled or no password entered

                if (!ConnectionSettingsImport.TryDeserialize(fileText, password, out imported) || imported is null)
                {
                    await Shell.Current.DisplayAlertAsync("Load Failed",
                        "The file could not be decrypted - the password may be incorrect or the file may not be a valid encrypted settings file.",
                        "OK");
                    return;
                }
            }
            else
            {
                if (!ConnectionSettingsImport.TryDeserialize(fileText, out imported) || imported is null)
                {
                    await Shell.Current.DisplayAlertAsync("Load Failed",
                        "The file did not contain valid connection settings JSON.", "OK");
                    return;
                }
            }

            var applied = ApplyImportedSettings(imported);

            if (applied == 0)
            {
                await Shell.Current.DisplayAlertAsync("Nothing Loaded",
                    "No recognized settings were found in the file.", "OK");
                return;
            }

            await Shell.Current.DisplayAlertAsync("Settings Loaded",
                $"Loaded {applied} setting(s) from the file. Review the values and tap Save to keep the changes.",
                "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Load Failed",
                $"The settings file could not be loaded: {ex.Message}", "OK");
        }
    }

    /// <summary>
    ///     Applies every non-null value from <paramref name="imported" /> onto the editor, overwriting the
    ///     existing value. Values missing from the file (null) are left unchanged. Returns the number of
    ///     settings that were applied.
    /// </summary>
    private int ApplyImportedSettings(ConnectionSettingsImport imported)
    {
        var applied = 0;

        if (imported.Name is not null)
        {
            Name = imported.Name;
            applied++;
        }

        if (imported.SiteDomain is not null)
        {
            SiteDomain = imported.SiteDomain;
            applied++;
        }

        if (imported.Provider is not null)
        {
            Provider = imported.Provider;
            applied++;
        }

        if (imported.Region is not null)
        {
            Region = imported.Region;
            applied++;
        }

        if (imported.ServiceUrl is not null)
        {
            ServiceUrl = imported.ServiceUrl;
            applied++;
        }

        if (imported.Bucket is not null)
        {
            Bucket = imported.Bucket;
            applied++;
        }

        if (imported.AccessKey is not null)
        {
            AccessKey = imported.AccessKey;
            applied++;
        }

        if (imported.Secret is not null)
        {
            Secret = imported.Secret;
            applied++;
        }

        return applied;
    }

    private async Task SaveAsync()
    {
        var profile = new CloudViewerProfile
        {
            Id = _id,
            Name = Name.Trim(),
            Provider = Provider,
            Region = Region.Trim(),
            Bucket = Bucket.Trim(),
            SiteDomain = SiteDomain.Trim()
        };

        var serviceUrlForValidation = IsAmazon ? null : ServiceUrl?.Trim();

        var validation = ConnectionValidation.Validate(profile, AccessKey?.Trim() ?? string.Empty,
            Secret?.Trim() ?? string.Empty, serviceUrlForValidation);

        if (!validation.IsValid)
        {
            await Shell.Current.DisplayAlertAsync("Cannot Save", validation.Error, "OK");
            return;
        }

        // For Amazon the service URL is derived from the region and is not stored as a secret.
        await _credentialStore.SaveAsync(profile.Id, AccessKey!.Trim(), Secret!.Trim(),
            IsAmazon ? null : ServiceUrl?.Trim());

        await _repository.AddOrUpdateAsync(profile);

        await Shell.Current.GoToAsync("..");
    }
}
