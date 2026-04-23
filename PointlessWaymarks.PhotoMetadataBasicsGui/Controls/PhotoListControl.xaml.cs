using System.Windows.Controls;

namespace PointlessWaymarks.PhotoMetadataBasicsGui.Controls;

/// <summary>
///     Interaction logic for FileListControl.xaml
/// </summary>
public partial class PhotoListControl
{
    public PhotoListControl()
    {
        InitializeComponent();
    }

    private void PhotoGroupList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PhotoGroupList.SelectedItem != null)
            PhotoGroupList.ScrollIntoView(PhotoGroupList.SelectedItem);
    }
}