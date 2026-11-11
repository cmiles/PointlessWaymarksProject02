#nullable enable
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PointedHorizons.Services;

namespace PointedHorizons.Controls;

public partial class AboutControl : UserControl
{
    public AboutControl()
    {
        InitializeComponent();
        Loaded += (s, e) => UpdateTheme(ThemeManager.Current.IsDark);
    }

    private void Hyperlink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is HyperlinkButton btn && btn.NavigateUri != null)
        {
            try
            {
                OpenSilver.Interop.ExecuteJavaScriptVoid($"window.open('{btn.NavigateUri.AbsoluteUri}', '_blank');");
            }
            catch
            {
                // OpenSilver default navigation
            }
        }
    }

    public void UpdateTheme(bool isDark)
    {
        var tm = ThemeManager.Current;

        if (MainBorder != null)
        {
            MainBorder.Background = tm.HeaderBackground;
            MainBorder.BorderBrush = tm.HeaderBorder;
        }

        if (HeaderBorder != null)
        {
            HeaderBorder.Background = tm.ButtonInactiveBackground;
            HeaderBorder.BorderBrush = tm.HeaderBorder;
        }

        if (TxtAboutTitle != null) TxtAboutTitle.Foreground = tm.TextPrimary;

        if (FooterBorder != null)
        {
            FooterBorder.Background = tm.ButtonInactiveBackground;
            FooterBorder.BorderBrush = tm.HeaderBorder;
        }

        if (TxtFooter != null) TxtFooter.Foreground = tm.TextMuted;

        Border?[] cards = [
            CardTopInfo,
            CardPackageOpenSilver,
            CardPackageMapLibre,
            CardPackagePmTiles,
            CardPackageDem,
            CardLayerOsm,
            CardLayerOpenTopo,
            CardLayerEsri,
            CardLayerUsgs,
            CardLayerPadUs
        ];
        foreach (var c in cards)
        {
            if (c == null) continue;
            c.Background = tm.ButtonInactiveBackground;
            c.BorderBrush = tm.ButtonInactiveBorder;
        }

        TextBlock?[] headings = [
            TxtHeadingPackages,
            TxtHeadingMapLayers,
            TxtPkg1Title,
            TxtPkg2Title,
            TxtPkgPmTilesTitle,
            TxtPkg3Title,
            TxtLayerOsmTitle,
            TxtLayerOpenTopoTitle,
            TxtLayerEsriTitle,
            TxtLayerUsgsTitle,
            TxtLayerPadUsTitle
        ];
        foreach (var h in headings)
        {
            if (h == null) continue;
            h.Foreground = tm.TextPrimary;
        }

        TextBlock?[] bodies = [
            TxtBioText1,
            TxtBioText2,
            TxtPkg1Desc,
            TxtPkg2Desc,
            TxtPkgPmTilesDesc,
            TxtPkg3Desc,
            TxtLayerOsmDesc,
            TxtLayerOpenTopoDesc,
            TxtLayerEsriDesc,
            TxtLayerUsgsDesc,
            TxtLayerPadUsDesc
        ];
        foreach (var b in bodies)
        {
            if (b == null) continue;
            b.Foreground = tm.TextSecondary;
        }
    }
}

