using System;
using WaterDaze.Controls;
using Xunit;

namespace WaterDaze.Tests;

public class MonthlyHeatmapControlTests
{
    [Fact]
    public void MonthNames_CountsAndInitials_MatchExpectedMonths()
    {
        Assert.Equal(12, MonthlyHeatmapControl.FullMonthNames.Length);
        Assert.Equal(12, MonthlyHeatmapControl.SingleLetterMonthNames.Length);

        var expectedInitials = new[] { "J", "F", "M", "A", "M", "J", "J", "A", "S", "O", "N", "D" };
        var expectedFull = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

        for (int i = 0; i < 12; i++)
        {
            Assert.Equal(expectedFull[i], MonthlyHeatmapControl.FullMonthNames[i]);
            Assert.Equal(expectedInitials[i], MonthlyHeatmapControl.SingleLetterMonthNames[i]);
            Assert.Equal(expectedFull[i][0].ToString(), MonthlyHeatmapControl.SingleLetterMonthNames[i]);
        }
    }
}
