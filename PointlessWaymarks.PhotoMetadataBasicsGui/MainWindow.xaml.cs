using System.ComponentModel;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.PhotoMetadataBasicsGui.Controls;
using PointlessWaymarks.WpfCommon.MarkdownDisplay;
using PointlessWaymarks.WpfCommon.ProgramUpdateMessage;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;
using Serilog;

namespace PointlessWaymarks.PhotoMetadataBasicsGui;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
[NotifyPropertyChanged]
public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();

        JotServices.Tracker.Track(this);

        if (Width < 900) Width = 900;
        if (Height < 650) Height = 650;

        WindowInitialPositionHelpers.EnsureWindowIsVisible(this);

        var versionInfo =
            ProgramInfoTools.StandardAppInformationString(AppContext.BaseDirectory,
                "Photo Metadata Basics Beta");

        InfoTitle = versionInfo.humanTitleString;

        var currentDateVersion = versionInfo.dateVersion;

        StatusContext = new StatusControlContext();

        DataContext = this;

        UpdateMessageContext = new ProgramUpdateMessageContext(StatusContext);

        HelpTabContext = new HelpDisplayContext([
            HelpText
        ]);

        StatusContext.RunFireAndForgetBlockingTask(Setup);

        StatusContext.RunFireAndForgetBlockingTask(async () => { await CheckForProgramUpdate(currentDateVersion); });
    }

    public AppSettingsContext? AppSettingsTabContext { get; set; }
    public HelpDisplayContext HelpTabContext { get; set; }
    public ImportPhotosContext? ImportPhotosTabContext { get; set; }

    public string HelpText =>
        $"""
         ## Photo Metadata Basics

         ALPHA SOFTWARE - EXPECT BUGS - USE WITH CAUTION

         {HelpMarkdown.CombinedAboutToolsAndPackages}
         """;

    public string InfoTitle { get; set; }
    public PhotoListContext? PhotoTabContext { get; set; }
    public StatusControlContext StatusContext { get; set; }
    public ProgramUpdateMessageContext UpdateMessageContext { get; set; }

    public async Task CheckForProgramUpdate(string currentDateVersion)
    {
        var settings = PhotoMetadataBasicsGuiSettingTools.ReadSettings();

        Log.Information(
            $"Program Update Check - Current Version {currentDateVersion}, Installer Directory {settings.ProgramUpdateDirectory}");

        if (string.IsNullOrEmpty(currentDateVersion)) return;

        var (dateString, setupFile) = await ProgramInfoTools.LatestInstaller(
            settings.ProgramUpdateDirectory,
            "PointlessWaymarks-PhotoMetadataBasicsGui-Setup");

        Log.Information(
            $"Program Update Check - Current Version {currentDateVersion}, Installer Directory {settings.ProgramUpdateDirectory}, Installer Date Found {dateString ?? string.Empty}, Setup File Found {setupFile ?? string.Empty}");

        await UpdateMessageContext.LoadData(currentDateVersion, dateString, setupFile);
    }

    private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        Log.CloseAndFlush();
    }

    public async Task Setup()
    {
        PhotoTabContext = await PhotoListContext.CreateInstance(StatusContext);
        ImportPhotosTabContext = await ImportPhotosContext.CreateInstance(StatusContext);
        AppSettingsTabContext = await AppSettingsContext.CreateInstance(StatusContext);
    }
}