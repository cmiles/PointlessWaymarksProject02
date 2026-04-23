using System.Windows.Controls;

namespace PointlessWaymarks.PhotoMetadataBasicsGui.Controls;

public partial class ImportPhotosControl
{
    public ImportPhotosControl()
    {
        InitializeComponent();
    }

    private void LogTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
            textBox.ScrollToHome();
    }
}
