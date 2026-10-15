namespace HistoricTempestsDataDownloader;

public static class UnitConversions
{
    public static double CelsiusToFahrenheit(double celsius)
    {
        return (celsius * 9.0 / 5.0) + 32.0;
    }

    public static double? CelsiusToFahrenheit(double? celsius)
    {
        return celsius.HasValue ? CelsiusToFahrenheit(celsius.Value) : null;
    }

    public static double FahrenheitToCelsius(double fahrenheit)
    {
        return (fahrenheit - 32.0) * 5.0 / 9.0;
    }

    public static double MillimetersToInches(double mm)
    {
        return mm / 25.4;
    }

    public static double? MillimetersToInches(double? mm)
    {
        return mm.HasValue ? MillimetersToInches(mm.Value) : null;
    }

    public static double MetersPerSecondToMph(double mps)
    {
        return mps * 2.23693629;
    }

    public static double? MetersPerSecondToMph(double? mps)
    {
        return mps.HasValue ? MetersPerSecondToMph(mps.Value) : null;
    }

    public static double MillibarsToInchesOfMercury(double mb)
    {
        return mb * 0.0295299830714;
    }

    public static double? MillibarsToInchesOfMercury(double? mb)
    {
        return mb.HasValue ? MillibarsToInchesOfMercury(mb.Value) : null;
    }

    public static double MetersToFeet(double meters)
    {
        return meters * 3.28084;
    }

    public static double Round1(double val) => Math.Round(val, 1, MidpointRounding.AwayFromZero);
    public static double Round2(double val) => Math.Round(val, 2, MidpointRounding.AwayFromZero);
    public static double Round3(double val) => Math.Round(val, 3, MidpointRounding.AwayFromZero);
    public static double? Round1(double? val) => val.HasValue ? Math.Round(val.Value, 1, MidpointRounding.AwayFromZero) : null;
    public static double? Round2(double? val) => val.HasValue ? Math.Round(val.Value, 2, MidpointRounding.AwayFromZero) : null;
}
