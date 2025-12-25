using System.Windows;
using System.Windows.Controls;

namespace PointlessWaymarks.SiteViewerGui;

public class SiteChooserDataTemplateSelector : DataTemplateSelector
{
    public DataTemplate? OpenCloudViewerSettingsFileTemplate { get; set; }
    public DataTemplate? SecureCloudViewerSettingsFileTemplate { get; set; }
    public DataTemplate? SiteDirectoryTemplate { get; set; }
    public DataTemplate? SiteSettingsFileTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        return item switch
        {
            SiteSettingsFileListItem => SiteSettingsFileTemplate,
            SiteDirectoryListItem => SiteDirectoryTemplate,
            SecureCloudViewerSettingsFileListItem => SecureCloudViewerSettingsFileTemplate,
            OpenCloudViewerSettingsFileListItem => OpenCloudViewerSettingsFileTemplate,
            _ => null
        };
    }
}