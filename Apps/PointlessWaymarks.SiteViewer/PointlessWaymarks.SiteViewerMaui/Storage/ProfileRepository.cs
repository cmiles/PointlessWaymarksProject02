using System.Text.Json;
using Microsoft.Maui.Storage;
using PointlessWaymarks.SiteViewerMaui.Models;

namespace PointlessWaymarks.SiteViewerMaui.Storage;

/// <summary>
///     Persists the list of <see cref="CloudViewerProfile" /> (non-secret) metadata as JSON in the
///     app data directory. Secrets are never written here - they are stored separately by an
///     <see cref="ISecureCredentialStore" />, and deleting a profile cascades to removing its secrets.
/// </summary>
public class ProfileRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ISecureCredentialStore _credentialStore;
    private readonly string _filePath;

    public ProfileRepository(ISecureCredentialStore credentialStore)
        : this(credentialStore, Path.Combine(FileSystem.AppDataDirectory, "cloud-viewer-profiles.json"))
    {
    }

    public ProfileRepository(ISecureCredentialStore credentialStore, string filePath)
    {
        _credentialStore = credentialStore;
        _filePath = filePath;
    }

    public async Task<List<CloudViewerProfile>> LoadAsync()
    {
        if (!File.Exists(_filePath)) return new List<CloudViewerProfile>();

        await using var stream = File.OpenRead(_filePath);
        var profiles = await JsonSerializer.DeserializeAsync<List<CloudViewerProfile>>(stream, JsonOptions);
        return profiles ?? new List<CloudViewerProfile>();
    }

    public async Task SaveAllAsync(List<CloudViewerProfile> profiles)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, profiles, JsonOptions);
    }

    /// <summary>Adds a new profile or updates the existing profile with the same <see cref="CloudViewerProfile.Id" />.</summary>
    public async Task AddOrUpdateAsync(CloudViewerProfile profile)
    {
        var profiles = await LoadAsync();

        var existingIndex = profiles.FindIndex(x => x.Id == profile.Id);
        if (existingIndex >= 0)
            profiles[existingIndex] = profile;
        else
            profiles.Add(profile);

        await SaveAllAsync(profiles);
    }

    /// <summary>Removes the profile with the given id and cascades removal of its secrets.</summary>
    public async Task DeleteAsync(Guid id)
    {
        var profiles = await LoadAsync();
        profiles.RemoveAll(x => x.Id == id);
        await SaveAllAsync(profiles);

        await _credentialStore.RemoveAsync(id);
    }
}
