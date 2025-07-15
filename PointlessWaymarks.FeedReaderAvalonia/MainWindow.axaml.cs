using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PointlessWaymarks.AvaloniaCommon;
using PointlessWaymarks.AvaloniaCommon.AppToast;
using PointlessWaymarks.AvaloniaCommon.LocalHtml;
using PointlessWaymarks.AvaloniaCommon.MarkdownDisplay;
using PointlessWaymarks.AvaloniaCommon.ProgramUpdateMessage;
using PointlessWaymarks.AvaloniaCommon.Status;
using PointlessWaymarks.AvaloniaCommon.Utility;
using PointlessWaymarks.AvaloniaLlamaAspects;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.FeedReaderAvalonia.Controls;
using PointlessWaymarks.FeedReaderData;
using Serilog;
using System.IO;
using Avalonia.Platform.Storage;

namespace PointlessWaymarks.FeedReaderAvalonia;

[GenerateStatusCommands]
public partial class MainWindow : Window
{
    // Define direct properties
    public static readonly DirectProperty<MainWindow, AppSettingsContext?> AppSettingsTabContextProperty =
        AvaloniaProperty.RegisterDirect<MainWindow, AppSettingsContext?>(
            nameof(AppSettingsTabContext),
            o => o.AppSettingsTabContext,
            (o, v) => o.AppSettingsTabContext = v);

    public static readonly DirectProperty<MainWindow, FeedItemListContext?> FeedItemListTabContextProperty =
        AvaloniaProperty.RegisterDirect<MainWindow, FeedItemListContext?>(
            nameof(FeedItemListTabContext),
            o => o.FeedItemListTabContext,
            (o, v) => o.FeedItemListTabContext = v);

    public static readonly DirectProperty<MainWindow, FeedListContext?> FeedListTabContextProperty =
        AvaloniaProperty.RegisterDirect<MainWindow, FeedListContext?>(
            nameof(FeedListTabContext),
            o => o.FeedListTabContext,
            (o, v) => o.FeedListTabContext = v);

    public static readonly DirectProperty<MainWindow, HelpDisplayContext?> HelpTabContextProperty =
        AvaloniaProperty.RegisterDirect<MainWindow, HelpDisplayContext?>(
            nameof(HelpTabContext),
            o => o.HelpTabContext,
            (o, v) => o.HelpTabContext = v);


    public static readonly DirectProperty<MainWindow, string> InfoTitleProperty =
        AvaloniaProperty.RegisterDirect<MainWindow, string>(
            nameof(InfoTitle),
            o => o.InfoTitle,
            (o, v) => o.InfoTitle = v);

    public static readonly DirectProperty<MainWindow, SavedFeedItemListContext?> SavedFeedItemListTabContextProperty =
        AvaloniaProperty.RegisterDirect<MainWindow, SavedFeedItemListContext?>(
            nameof(SavedFeedItemListTabContext),
            o => o.SavedFeedItemListTabContext,
            (o, v) => o.SavedFeedItemListTabContext = v);

    public static readonly DirectProperty<MainWindow, StatusControlContext?> StatusContextProperty =
        AvaloniaProperty.RegisterDirect<MainWindow, StatusControlContext?>(
            nameof(StatusContext),
            o => o.StatusContext,
            (o, v) => o.StatusContext = v);

    public static readonly DirectProperty<MainWindow, ProgramUpdateMessageContext?> UpdateMessageContextProperty =
        AvaloniaProperty.RegisterDirect<MainWindow, ProgramUpdateMessageContext?>(
            nameof(UpdateMessageContext),
            o => o.UpdateMessageContext,
            (o, v) => o.UpdateMessageContext = v);

    // Backing fields
    private AppSettingsContext? _appSettingsTabContext;
    private FeedItemListContext? _feedItemListTabContext;
    private FeedListContext? _feedListTabContext;
    private HelpDisplayContext? _helpTabContext;
    private string _infoTitle = string.Empty;
    private SavedFeedItemListContext? _savedFeedItemListTabContext;
    private StatusControlContext? _statusContext;
    private ProgramUpdateMessageContext? _updateMessageContext;

    // CLR property wrappers
    public AppSettingsContext? AppSettingsTabContext
    {
        get => _appSettingsTabContext;
        set => SetAndRaise(AppSettingsTabContextProperty, ref _appSettingsTabContext, value);
    }

    public FeedItemListContext? FeedItemListTabContext
    {
        get => _feedItemListTabContext;
        set => SetAndRaise(FeedItemListTabContextProperty, ref _feedItemListTabContext, value);
    }

    public FeedListContext? FeedListTabContext
    {
        get => _feedListTabContext;
        set => SetAndRaise(FeedListTabContextProperty, ref _feedListTabContext, value);
    }

    public HelpDisplayContext? HelpTabContext
    {
        get => _helpTabContext;
        set => SetAndRaise(HelpTabContextProperty, ref _helpTabContext, value);
    }

    public string InfoTitle
    {
        get => _infoTitle;
        set => SetAndRaise(InfoTitleProperty, ref _infoTitle, value);
    }

    public SavedFeedItemListContext? SavedFeedItemListTabContext
    {
        get => _savedFeedItemListTabContext;
        set => SetAndRaise(SavedFeedItemListTabContextProperty, ref _savedFeedItemListTabContext, value);
    }

    public StatusControlContext? StatusContext
    {
        get => _statusContext;
        set => SetAndRaise(StatusContextProperty, ref _statusContext, value);
    }

    public ProgramUpdateMessageContext? UpdateMessageContext
    {
        get => _updateMessageContext;
        set => SetAndRaise(UpdateMessageContextProperty, ref _updateMessageContext, value);
    }

    public readonly string HelpText =
        """
        ## Pointless Waymarks Feed Reader

        The Pointless Waymarks Feed Reader is a Windows Desktop (only!) Feed Reader. The program uses a SQLite database to store data about Feeds and Feed Items. The emphasis in this program is NOT displaying the RSS Content in a feed, but rather displaying the URL the Feed Links to.

        There are a number of great options for Feed (RSS) Readers - so why write another one is a good question...
         - Windows Desktop Only: After many years of RSS use my strong preference is that I don't want to read feeds all the time everywhere! Also I like: sitting in front of a desktop computer with a big screen (or screens!), desktop programs, owning my own data, keeping my data local and I like that I can't sit in front of the computer all day both because of 'life' and because I know how terrible that is for me...
         - Emphasize Displaying Linked Content Not the Feed Content: Feeds are just data and can be used in an awesome number of ways - but the convention is that a Feed Item links to content and I just want to see the content, in full...
         - Simple Feed List: I wonder at this point if I have spent a full day of my life organizing and tweaking the display of Feeds/Folders in Feed Readers? Clicking/unclicking/manipulating tree like structures of Feeds... I'm interested in a simpler display of Feeds that removes the temptation to fiddle and presents fewer options.
         - Joy! I love the art and craft of writing software and I love the feeling of using software that directly addresses my needs/wants/workflow/ideas.

        While the GUI, approach, vision, scope, design and nearly every detail is different this program will always be based on my memories of using [FeedDemon](https://nick.typepad.com/blog/2013/03/the-end-of-feeddemon.html) especially in the late 2000s!
        """;

    public MainWindow()
    {
        InitializeComponent();

        if (Width < 900) Width = 900;
        if (Height < 650) Height = 650;

        var versionInfo =
            ProgramInfoTools.StandardAppInformationString(AppContext.BaseDirectory,
                "Pointless Waymarks Feed Reader Beta");

        InfoTitle = versionInfo.humanTitleString;

        var currentDateVersion = versionInfo.dateVersion;

        StatusContext = new StatusControlContext { BlockUi = false };

        BuildCommands();

        DataContext = this;

        StatusContext.RunFireAndForgetNonBlockingTask(async () =>
        {
            await CheckForProgramUpdate(currentDateVersion);

            await LoadData();
        });
    }

    public async Task CheckForProgramUpdate(string currentDateVersion)
    {
        var settings = FeedReaderGuiSettingTools.ReadSettings();

        Log.Information(
            $"Program Update Check - Current Version {currentDateVersion}, Installer Directory {settings.ProgramUpdateDirectory}");

        if (string.IsNullOrEmpty(currentDateVersion)) return;

        var (dateString, setupFile) = await ProgramInfoTools.LatestInstaller(
            settings.ProgramUpdateDirectory,
            "PointlessWaymarks-FeedReaderGui-Setup");

        Log.Information(
            $"Program Update Check - Current Version {currentDateVersion}, Installer Directory {settings.ProgramUpdateDirectory}, Installer Date Found {dateString ?? string.Empty}, Setup File Found {setupFile ?? string.Empty}");

        await UpdateMessageContext.LoadData(currentDateVersion, dateString, setupFile);
    }

    public async Task<string> DbFileExistsCheckWithUserInteraction(string dbFile)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (string.IsNullOrWhiteSpace(dbFile) || !File.Exists(dbFile))
        {
            var nextAction = await StatusContext.ShowMessage("Database Does Not Exist",
                "The database file does not exist? You can create a new database or pick another file...",
                ["New", "Choose a File"]);

            if (nextAction.Equals("New"))
                return UniqueFileTools.UniqueFile(
                               FileLocationHelpers.DefaultStorageDirectory(), "PointlessWaymarks-FeedReader.db")
                           ?.FullName ??
                       string.Empty;

            if (nextAction.Equals("Choose a File"))
            {
                await ThreadSwitcher.ResumeForegroundAsync();

                // Using StorageProvider instead of OpenFileDialog
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Open Database",
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new FilePickerFileType("Database files")
                        {
                            Patterns = ["*.db"],
                        },
                        new FilePickerFileType("All files")
                        {
                            Patterns = ["*"]
                        }
                    ],
                    SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(
                        FeedReaderGuiSettingTools.GetLastDirectory().FullName)
                });

                if (files.Count == 0) return string.Empty;

                var filePath = files[0].TryGetLocalPath();
                if (filePath != null)
                {
                    var newFile = new FileInfo(filePath);
                    if (newFile.Directory?.Exists ?? false)
                        await FeedReaderGuiSettingTools.SetLastDirectory(newFile.Directory.FullName);
                    
                    return filePath;
                }
                return string.Empty;
            }
        }

        return dbFile;
    }

    public async Task<string> DbIsValidCheckWithUserInteraction(string dbFile)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var invalidFile = true;
        var dbFileName = string.Empty;

        while (invalidFile)
        {
            dbFileName = await DbFileExistsCheckWithUserInteraction(dbFile);
            if (string.IsNullOrWhiteSpace(dbFileName) || !File.Exists(dbFileName)) continue;

            var dbTest = await FeedContext.TryCreateInstance(dbFileName);
            if (!dbTest.success)
                await StatusContext.ShowMessageWithOkButton("DB Not Valid?",
                    $"There was a problem with the selected db - {dbTest.message}");
            invalidFile = !dbTest.success;
        }

        return dbFileName;
    }

    private async Task LoadData(string? loadWithDatabaseFile = null)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        UpdateMessageContext = new ProgramUpdateMessageContext(StatusContext!);

        HelpTabContext = new HelpDisplayContext([
            HelpText,
            HelpMarkdown.CombinedAboutToolsAndPackages
        ]);
        
        await ThreadSwitcher.ResumeBackgroundAsync();

        _ = await AppPageServer.GetInstance();

        var settings = FeedReaderGuiSettingTools.ReadSettings();

        var dbFileName = string.IsNullOrWhiteSpace(loadWithDatabaseFile)
            ? settings.LastDatabaseFile
            : loadWithDatabaseFile;

        //If the settings file has a blank db then assume this is a first run and create a db without asking
        if (string.IsNullOrWhiteSpace(dbFileName))
        {
            dbFileName = UniqueFileTools.UniqueFile(
                                 FileLocationHelpers.DefaultStorageDirectory(), "PointlessWaymarks-FeedReader.db")
                             ?.FullName ??
                         string.Empty;
            await FeedContext.CreateInstanceWithEnsureCreated(dbFileName);
        }

        dbFileName = await DbIsValidCheckWithUserInteraction(dbFileName);

        settings.LastDatabaseFile = dbFileName;

        await FeedReaderGuiSettingTools.WriteSettings(settings);

        var versionInfo =
            ProgramInfoTools.StandardAppInformationString(AppContext.BaseDirectory,
                "Pointless Waymarks Feed Reader Beta");

        await ThreadSwitcher.ResumeForegroundAsync();
        
        InfoTitle = $"{versionInfo.humanTitleString} - {dbFileName}";

        FeedItemListTabContext = await FeedItemListContext.CreateInstance(StatusContext, dbFileName);
        FeedListTabContext = await FeedListContext.CreateInstance(StatusContext, dbFileName);
        SavedFeedItemListTabContext = await SavedFeedItemListContext.CreateInstance(StatusContext, dbFileName);
        AppSettingsTabContext = new AppSettingsContext(StatusContext);
    }

    [BlockingCommand]
    public async Task NewDatabase()
    {
        await ThreadSwitcher.ResumeForegroundAsync();
        
        // Using StorageProvider instead of OpenFolderDialog
        var folder = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "New Db Directory",
            AllowMultiple = false,
            SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(
                FileLocationHelpers.DefaultStorageDirectory().FullName)
        });

        if (folder.Count == 0) return;

        var result = folder[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(result) || !Directory.Exists(result))
        {
            await StatusContext.ToastError("Selected Directory Does Not Exist or Cannot Be Accessed");
            return;
        }

        var userFileBase = await StatusContext.ShowStringEntry("New Db File Name", "Enter the file name for a new Db.",
            "PointlessWaymarks-FeedReader");

        if (!userFileBase.Item1) return;

        if (string.IsNullOrWhiteSpace(userFileBase.Item2))
        {
            await StatusContext.ToastError("File name is blank?");
            return;
        }

        var baseFile = Path.HasExtension(userFileBase.Item2)
            ? userFileBase.Item2.Replace(Path.GetExtension(userFileBase.Item2), string.Empty)
            : userFileBase.Item2;

        if (string.IsNullOrWhiteSpace(baseFile))
        {
            await StatusContext.ToastError("File name is blank?");
            return;
        }

        var newFile = UniqueFileTools.UniqueFile(new DirectoryInfo(result), $"{baseFile}.db");

        if (newFile == null)
        {
            await StatusContext.ToastError("Could not create a unique file name?");
            return;
        }

        await FeedContext.CreateInstanceWithEnsureCreated(newFile.FullName);

        await LoadData(newFile.FullName);
    }

    [BlockingCommand]
    public async Task PickNewDatabase()
    {
        // Using StorageProvider instead of OpenFileDialog
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Database",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Database files")
                {
                    Patterns = ["*.db"],
                },
                new FilePickerFileType("All files")
                {
                    Patterns = ["*"]
                }
            ],
            SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(
                FeedReaderGuiSettingTools.GetLastDirectory().FullName)
        });

        if (files.Count == 0) return;

        var filePath = files[0].TryGetLocalPath();
        if (filePath != null)
        {
            var newFile = new FileInfo(filePath);
            if (newFile.Directory?.Exists ?? false)
                await FeedReaderGuiSettingTools.SetLastDirectory(newFile.Directory.FullName);
            
            await LoadData(filePath);
        }
    }
}