using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon.S3BucketDestroyer;
using PointlessWaymarks.WpfCommon.Status;

namespace PointlessWaymarks.CloudBackupGui.Controls;

[NotifyPropertyChanged]
public partial class AppSettingsContext
{
    public AppSettingsContext(StatusControlContext statusContext)
    {
        StatusContext = statusContext;
        Settings = CloudBackupGuiSettingTools.ReadSettings();

        ProgramUpdateLocation = Settings.ProgramUpdateDirectory;

        PropertyChanged += AppSettingsContext_PropertyChanged;

        ShowBucketDestroyerWindow =
            new RelayCommand(() =>
                StatusContext.RunBlockingTask(async () => await BucketDestroyerWindow.CreateInstanceAndShow()));
    }

    public string ProgramUpdateLocation { get; set; }
    public CloudBackupGuiSettings Settings { get; set; }

    public RelayCommand ShowBucketDestroyerWindow { get; set; }

    public StatusControlContext StatusContext { get; set; }

    private void AppSettingsContext_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName)) return;

        if (nameof(ProgramUpdateLocation).Equals(e.PropertyName))
        {
            Settings.ProgramUpdateDirectory = ProgramUpdateLocation;
#pragma warning disable CS4014
            //Allow call to continue without waiting and write settings
            CloudBackupGuiSettingTools.WriteSettings(Settings);
#pragma warning restore CS4014
        }
    }
}