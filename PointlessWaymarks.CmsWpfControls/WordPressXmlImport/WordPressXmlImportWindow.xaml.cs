using PointlessWaymarks.CmsData;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;

namespace PointlessWaymarks.CmsWpfControls.WordPressXmlImport;

/// <summary>
///     Interaction logic for WordPressXmlImportWindow.xaml
/// </summary>
[NotifyPropertyChanged]
[StaThreadConstructorGuard]
public partial class WordPressXmlImportWindow
{
    public WordPressXmlImportWindow(StatusControlContext statusContext)
    {
        StatusContext = statusContext;
        
        InitializeComponent();
        DataContext = this;
        ImportContext = new WordPressXmlImportContext(StatusContext);
        WindowTitle = $"WordPress Import - {UserSettingsSingleton.CurrentSettings().SiteName}";
    }

    public WordPressXmlImportContext ImportContext { get; set; }
    public StatusControlContext StatusContext { get; set; }
    public string WindowTitle { get; set; }

    public static async Task<WordPressXmlImportWindow> CreateInstance()
    {
        await ThreadSwitcher.ResumeForegroundAsync();
        var statusContext = await StatusControlContext.CreateInstance();
        return new WordPressXmlImportWindow(statusContext);
    }
}