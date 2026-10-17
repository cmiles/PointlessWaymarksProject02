using System;
using System.Windows.Media;
using WaterDaze.Services;
using Xunit;

namespace WaterDaze.Tests;

public class ThemeManagerTests
{
    [Fact]
    public void SetTheme_ExplicitLightAndDark_UpdatesThemeModeAndIsDark()
    {
        ThemeManager.SetTheme(AppTheme.Light);
        Assert.Equal(AppTheme.Light, ThemeManager.CurrentTheme);
        Assert.False(ThemeManager.IsDark);

        ThemeManager.SetTheme(AppTheme.Dark);
        Assert.Equal(AppTheme.Dark, ThemeManager.CurrentTheme);
        Assert.True(ThemeManager.IsDark);
    }

    [Fact]
    public void SetTheme_FiresThemeChangedEvent()
    {
        ThemeManager.SetTheme(AppTheme.Light);

        bool eventFired = false;
        bool isDarkResult = false;

        EventHandler<bool> handler = (sender, isDark) =>
        {
            eventFired = true;
            isDarkResult = isDark;
        };

        ThemeManager.ThemeChanged += handler;
        try
        {
            ThemeManager.SetTheme(AppTheme.Dark);

            Assert.True(eventFired);
            Assert.True(isDarkResult);
        }
        finally
        {
            ThemeManager.ThemeChanged -= handler;
        }
    }

    [Fact]
    public void SetTheme_SystemMode_SetsCurrentThemeToSystem()
    {
        ThemeManager.SetTheme(AppTheme.System);
        Assert.Equal(AppTheme.System, ThemeManager.CurrentTheme);
    }

    [Fact]
    public void SetSystemDark_WhenSystemMode_UpdatesIsDarkAndFiresThemeChanged()
    {
        ThemeManager.SetTheme(AppTheme.System);

        bool eventFired = false;
        bool lastIsDark = false;
        EventHandler<bool> handler = (s, isDark) =>
        {
            eventFired = true;
            lastIsDark = isDark;
        };

        ThemeManager.ThemeChanged += handler;
        try
        {
            ThemeManager.SetSystemDark(true);
            Assert.True(ThemeManager.IsSystemDark);
            Assert.True(ThemeManager.IsDark);
            Assert.True(eventFired);
            Assert.True(lastIsDark);

            eventFired = false;
            ThemeManager.SetSystemDark(false);
            Assert.False(ThemeManager.IsSystemDark);
            Assert.False(ThemeManager.IsDark);
            Assert.True(eventFired);
            Assert.False(lastIsDark);
        }
        finally
        {
            ThemeManager.ThemeChanged -= handler;
        }
    }

    [Fact]
    public void SetSystemDark_WhenExplicitLightOrDark_PreservesExplicitIsDark()
    {
        ThemeManager.SetTheme(AppTheme.Light);
        ThemeManager.SetSystemDark(true);
        Assert.True(ThemeManager.IsSystemDark);
        Assert.False(ThemeManager.IsDark); // Still explicit Light

        ThemeManager.SetTheme(AppTheme.Dark);
        ThemeManager.SetSystemDark(false);
        Assert.False(ThemeManager.IsSystemDark);
        Assert.True(ThemeManager.IsDark); // Still explicit Dark

        // Switching back to system immediately picks up the updated system preference
        ThemeManager.SetTheme(AppTheme.System);
        Assert.False(ThemeManager.IsDark);
    }

    [Fact]
    public void ThemeButtonLabel_ReflectsThemeAndSystemState()
    {
        ThemeManager.SetTheme(AppTheme.Light);
        Assert.Equal("☀️ Light", ThemeManager.ThemeButtonLabel);

        ThemeManager.SetTheme(AppTheme.Dark);
        Assert.Equal("🌙 Dark", ThemeManager.ThemeButtonLabel);

        ThemeManager.SetTheme(AppTheme.System);
        ThemeManager.SetSystemDark(true);
        Assert.Equal("🌓 System (Dark)", ThemeManager.ThemeButtonLabel);

        ThemeManager.SetSystemDark(false);
        Assert.Equal("🌓 System (Light)", ThemeManager.ThemeButtonLabel);
    }
}
