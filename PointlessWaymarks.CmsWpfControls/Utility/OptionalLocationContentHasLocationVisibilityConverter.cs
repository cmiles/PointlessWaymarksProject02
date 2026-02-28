using System.Globalization;
using System.Windows;
using System.Windows.Data;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.SpatialTools;

namespace PointlessWaymarks.CmsWpfControls.Utility;

public class OptionalLocationContentHasLocationVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var converted = value as IOptionalLocation;
        if (converted?.Latitude is null || converted?.Longitude is null) return Visibility.Hidden;

        var latitudeValidation = SpatialValue.LatitudeValidation(converted.Latitude.Value).Result;
        var longitudeValidation = SpatialValueValidations.LongitudeValidation(converted.Longitude.Value).Result;
        if (!latitudeValidation.Valid || !longitudeValidation.Valid) return Visibility.Hidden;
        return Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}