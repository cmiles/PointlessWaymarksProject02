using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HistoricTempestsDataDownloader;

public class JsonReportGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task GenerateAllReportsAsync(
        StationMetadata metadata,
        IReadOnlyList<DailyWeatherSummary> summaries,
        string outputDirectory,
        DateOnly todayLocal)
    {
        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var sortedSummaries = summaries.OrderBy(s => s.Date).ToList();

        // 1. Long-term comparisons (Months & Years)
        var longTermReport = BuildLongTermReport(metadata, sortedSummaries);
        var longTermPath = Path.Combine(outputDirectory, "long_term_comparisons.json");
        await WriteJsonAsync(longTermPath, longTermReport);

        // Also write monthly_yearly_comparisons.json as an alias
        var monthlyYearlyPath = Path.Combine(outputDirectory, "monthly_yearly_comparisons.json");
        await WriteJsonAsync(monthlyYearlyPath, longTermReport);

        // 2. Recent comparisons of days between previous years
        var recentDaysReport = BuildRecentDaysReport(metadata, sortedSummaries, todayLocal);
        var recentDaysPath = Path.Combine(outputDirectory, "recent_day_comparisons.json");
        await WriteJsonAsync(recentDaysPath, recentDaysReport);

        // 3. The weather for the next week in days from the previous year(s)
        var nextWeekReport = BuildNextWeekReport(metadata, sortedSummaries, todayLocal);
        var nextWeekPath = Path.Combine(outputDirectory, "next_week_historical_comparisons.json");
        await WriteJsonAsync(nextWeekPath, nextWeekReport);

        // 4. Overall weather summary overview
        var overviewReport = BuildOverviewReport(metadata, sortedSummaries, todayLocal);
        var overviewPath = Path.Combine(outputDirectory, "weather_summary_overview.json");
        await WriteJsonAsync(overviewPath, overviewReport);

        // 5. Full chronological daily summaries
        var allSummariesReport = BuildAllDailySummariesReport(metadata, sortedSummaries);
        var allSummariesPath = Path.Combine(outputDirectory, "all_daily_summaries.json");
        await WriteJsonAsync(allSummariesPath, allSummariesReport);
    }

    private static object BuildLongTermReport(StationMetadata meta, List<DailyWeatherSummary> summaries)
    {
        var years = summaries
            .Select(s => s.ToDateOnly().Year)
            .Distinct()
            .OrderBy(y => y)
            .ToList();

        var annualSummaries = new List<object>();
        foreach (var year in years)
        {
            var yearSummaries = summaries.Where(s => s.ToDateOnly().Year == year).ToList();
            if (yearSummaries.Count == 0) continue;

            var temps = yearSummaries.Where(s => s.TemperatureAvg.HasValue).Select(s => s.TemperatureAvg!.Value).ToList();
            var maxTemps = yearSummaries.Where(s => s.TemperatureHigh.HasValue).ToList();
            var minTemps = yearSummaries.Where(s => s.TemperatureLow.HasValue).ToList();
            var maxRain = yearSummaries.OrderByDescending(s => s.BestPrecipitationMm).FirstOrDefault();
            var maxGust = yearSummaries.Where(s => s.WindGust.HasValue).OrderByDescending(s => s.WindGust!.Value).FirstOrDefault();
            var rainTotalMm = yearSummaries.Sum(s => s.BestPrecipitationMm);
            var rainyDays = yearSummaries.Count(s => s.BestPrecipitationMm >= 0.254); // >= 0.01 inch

            var hottest = maxTemps.OrderByDescending(s => s.TemperatureHigh!.Value).FirstOrDefault();
            var coldest = minTemps.OrderBy(s => s.TemperatureLow!.Value).FirstOrDefault();

            var windAvgs = yearSummaries.Where(s => s.WindAvg.HasValue).Select(s => s.WindAvg!.Value).ToList();
            var solars = yearSummaries.Where(s => s.SolarRadiationAvg.HasValue).Select(s => s.SolarRadiationAvg!.Value).ToList();

            annualSummaries.Add(new
            {
                year,
                daysRecorded = yearSummaries.Count,
                temperature = new
                {
                    avgC = temps.Count > 0 ? UnitConversions.Round1(temps.Average()) : (double?)null,
                    avgF = temps.Count > 0 ? UnitConversions.Round1(UnitConversions.CelsiusToFahrenheit(temps.Average())) : (double?)null,
                    maxC = hottest?.TemperatureHigh,
                    maxF = hottest != null ? UnitConversions.Round1(hottest.TemperatureHighF) : null,
                    maxDate = hottest?.Date,
                    minC = coldest?.TemperatureLow,
                    minF = coldest != null ? UnitConversions.Round1(coldest.TemperatureLowF) : null,
                    minDate = coldest?.Date
                },
                precipitation = new
                {
                    totalMm = UnitConversions.Round2(rainTotalMm),
                    totalInches = UnitConversions.Round2(UnitConversions.MillimetersToInches(rainTotalMm)),
                    rainyDaysCount = rainyDays,
                    maxDailyMm = maxRain != null ? UnitConversions.Round2(maxRain.BestPrecipitationMm) : 0,
                    maxDailyInches = maxRain != null ? UnitConversions.Round2(maxRain.BestPrecipitationInches) : 0,
                    maxDailyDate = maxRain?.Date
                },
                wind = new
                {
                    avgMps = windAvgs.Count > 0 ? UnitConversions.Round1(windAvgs.Average()) : (double?)null,
                    avgMph = windAvgs.Count > 0 ? UnitConversions.Round1(UnitConversions.MetersPerSecondToMph(windAvgs.Average())) : (double?)null,
                    maxGustMps = maxGust?.WindGust,
                    maxGustMph = maxGust != null ? UnitConversions.Round1(maxGust.WindGustMph) : null,
                    maxGustDate = maxGust?.Date
                },
                solar = new
                {
                    avgRadiationWM2 = solars.Count > 0 ? UnitConversions.Round1(solars.Average()) : (double?)null
                }
            });
        }

        // Monthly summaries
        var monthlySummaries = new List<object>();
        var yearMonthGroups = summaries
            .GroupBy(s => new { s.ToDateOnly().Year, s.ToDateOnly().Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month);

        foreach (var g in yearMonthGroups)
        {
            var mList = g.ToList();
            var monthName = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(g.Key.Month);
            var temps = mList.Where(s => s.TemperatureAvg.HasValue).Select(s => s.TemperatureAvg!.Value).ToList();
            var highs = mList.Where(s => s.TemperatureHigh.HasValue).Select(s => s.TemperatureHigh!.Value).ToList();
            var lows = mList.Where(s => s.TemperatureLow.HasValue).Select(s => s.TemperatureLow!.Value).ToList();
            var rainMm = mList.Sum(s => s.BestPrecipitationMm);
            var rainyDays = mList.Count(s => s.BestPrecipitationMm >= 0.254);
            var maxRain = mList.OrderByDescending(s => s.BestPrecipitationMm).FirstOrDefault();
            var maxGust = mList.Where(s => s.WindGust.HasValue).OrderByDescending(s => s.WindGust!.Value).FirstOrDefault();
            var windAvgs = mList.Where(s => s.WindAvg.HasValue).Select(s => s.WindAvg!.Value).ToList();

            monthlySummaries.Add(new
            {
                year = g.Key.Year,
                month = g.Key.Month,
                monthName,
                daysCount = mList.Count,
                temperature = new
                {
                    avgC = temps.Count > 0 ? UnitConversions.Round1(temps.Average()) : (double?)null,
                    avgF = temps.Count > 0 ? UnitConversions.Round1(UnitConversions.CelsiusToFahrenheit(temps.Average())) : (double?)null,
                    meanHighC = highs.Count > 0 ? UnitConversions.Round1(highs.Average()) : (double?)null,
                    meanHighF = highs.Count > 0 ? UnitConversions.Round1(UnitConversions.CelsiusToFahrenheit(highs.Average())) : (double?)null,
                    meanLowC = lows.Count > 0 ? UnitConversions.Round1(lows.Average()) : (double?)null,
                    meanLowF = lows.Count > 0 ? UnitConversions.Round1(UnitConversions.CelsiusToFahrenheit(lows.Average())) : (double?)null,
                    maxC = highs.Count > 0 ? UnitConversions.Round1(highs.Max()) : (double?)null,
                    maxF = highs.Count > 0 ? UnitConversions.Round1(UnitConversions.CelsiusToFahrenheit(highs.Max())) : (double?)null,
                    minC = lows.Count > 0 ? UnitConversions.Round1(lows.Min()) : (double?)null,
                    minF = lows.Count > 0 ? UnitConversions.Round1(UnitConversions.CelsiusToFahrenheit(lows.Min())) : (double?)null
                },
                precipitation = new
                {
                    totalMm = UnitConversions.Round2(rainMm),
                    totalInches = UnitConversions.Round2(UnitConversions.MillimetersToInches(rainMm)),
                    rainyDays,
                    maxDailyMm = maxRain != null ? UnitConversions.Round2(maxRain.BestPrecipitationMm) : 0,
                    maxDailyInches = maxRain != null ? UnitConversions.Round2(maxRain.BestPrecipitationInches) : 0,
                    maxDailyDate = maxRain?.Date
                },
                wind = new
                {
                    avgMph = windAvgs.Count > 0 ? UnitConversions.Round1(UnitConversions.MetersPerSecondToMph(windAvgs.Average())) : (double?)null,
                    maxGustMph = maxGust != null ? UnitConversions.Round1(maxGust.WindGustMph) : null
                }
            });
        }

        // Multi-year comparison matrix for each calendar month (1..12)
        var monthMatrix = new Dictionary<string, object>();
        for (int m = 1; m <= 12; m++)
        {
            var monthName = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(m);
            var allForMonth = summaries.Where(s => s.ToDateOnly().Month == m).ToList();

            var yearRows = new List<object>();
            foreach (var y in years)
            {
                var ymList = allForMonth.Where(s => s.ToDateOnly().Year == y).ToList();
                if (ymList.Count == 0) continue;

                var ymTemps = ymList.Where(s => s.TemperatureAvg.HasValue).Select(s => s.TemperatureAvg!.Value).ToList();
                var ymHighs = ymList.Where(s => s.TemperatureHighF.HasValue).Select(s => s.TemperatureHighF!.Value).ToList();
                var ymLows = ymList.Where(s => s.TemperatureLowF.HasValue).Select(s => s.TemperatureLowF!.Value).ToList();
                var ymRain = ymList.Sum(s => s.BestPrecipitationMm);
                var ymGust = ymList.Where(s => s.WindGust.HasValue).OrderByDescending(s => s.WindGust!.Value).FirstOrDefault();

                yearRows.Add(new
                {
                    year = y,
                    daysRecorded = ymList.Count,
                    avgTempF = ymTemps.Count > 0 ? UnitConversions.Round1(UnitConversions.CelsiusToFahrenheit(ymTemps.Average())) : (double?)null,
                    avgTempC = ymTemps.Count > 0 ? UnitConversions.Round1(ymTemps.Average()) : (double?)null,
                    maxTempF = ymHighs.Count > 0 ? (double?)UnitConversions.Round1(ymHighs.Max()) : null,
                    minTempF = ymLows.Count > 0 ? (double?)UnitConversions.Round1(ymLows.Min()) : null,
                    totalPrecipInches = UnitConversions.Round2(UnitConversions.MillimetersToInches(ymRain)),
                    totalPrecipMm = UnitConversions.Round2(ymRain),
                    rainyDays = ymList.Count(s => s.BestPrecipitationMm >= 0.254),
                    maxGustMph = ymGust != null ? UnitConversions.Round1(ymGust.WindGustMph) : null
                });
            }

            var allTemps = allForMonth.Where(s => s.TemperatureAvg.HasValue).Select(s => s.TemperatureAvg!.Value).ToList();
            var allRain = allForMonth.Sum(s => s.BestPrecipitationMm);
            double avgMonthlyRainMm = yearRows.Count > 0 ? allRain / yearRows.Count : 0.0;

            monthMatrix[m.ToString("D2")] = new
            {
                monthNumber = m,
                monthName,
                historicalAverage = new
                {
                    avgTempF = allTemps.Count > 0 ? UnitConversions.Round1(UnitConversions.CelsiusToFahrenheit(allTemps.Average())) : (double?)null,
                    avgTempC = allTemps.Count > 0 ? UnitConversions.Round1(allTemps.Average()) : (double?)null,
                    avgPrecipInches = UnitConversions.Round2(UnitConversions.MillimetersToInches(avgMonthlyRainMm)),
                    avgPrecipMm = UnitConversions.Round2(avgMonthlyRainMm)
                },
                years = yearRows
            };
        }

        return new
        {
            station = new
            {
                meta.StationId,
                meta.DeviceId,
                meta.StationName,
                meta.Timezone,
                meta.ElevationMeters,
                elevationFeet = UnitConversions.Round1(meta.ElevationFeet),
                meta.Latitude,
                meta.Longitude
            },
            recordDateRange = new
            {
                firstDate = summaries.FirstOrDefault()?.Date,
                lastDate = summaries.LastOrDefault()?.Date,
                totalDays = summaries.Count
            },
            annualSummaries,
            monthlySummaries,
            monthByMonthMultiYearComparison = monthMatrix
        };
    }

    private static object BuildRecentDaysReport(
        StationMetadata meta, List<DailyWeatherSummary> summaries, DateOnly todayLocal)
    {
        // Take recent completed days (up to 14 days ending at yesterday)
        var yesterday = todayLocal.AddDays(-1);
        var recentDaysList = new List<object>();

        // Look back 14 days
        for (int offset = 0; offset < 14; offset++)
        {
            var targetDate = yesterday.AddDays(-offset);
            var currentDaySummary = summaries.FirstOrDefault(s => s.Date == targetDate.ToString("yyyy-MM-dd"));
            if (currentDaySummary == null) continue;

            int targetMonth = targetDate.Month;
            int targetDay = targetDate.Day;
            var displayDate = targetDate.ToString("MMMM d", CultureInfo.InvariantCulture);

            // Previous years on the exact same calendar month and day
            var matchingPreviousYears = summaries
                .Where(s =>
                {
                    var d = s.ToDateOnly();
                    return d.Month == targetMonth && d.Day == targetDay && d.Year < targetDate.Year;
                })
                .OrderByDescending(s => s.ToDateOnly().Year)
                .ToList();

            var prevYearItems = new List<object>();
            foreach (var prev in matchingPreviousYears)
            {
                double? diffHighF = (currentDaySummary.TemperatureHighF.HasValue && prev.TemperatureHighF.HasValue)
                    ? UnitConversions.Round1(currentDaySummary.TemperatureHighF.Value - prev.TemperatureHighF.Value)
                    : null;

                prevYearItems.Add(new
                {
                    year = prev.ToDateOnly().Year,
                    date = prev.Date,
                    temperatureHighF = UnitConversions.Round1(prev.TemperatureHighF),
                    temperatureHighC = prev.TemperatureHigh,
                    temperatureLowF = UnitConversions.Round1(prev.TemperatureLowF),
                    temperatureLowC = prev.TemperatureLow,
                    temperatureAvgF = UnitConversions.Round1(prev.TemperatureAvgF),
                    temperatureAvgC = prev.TemperatureAvg,
                    precipInches = UnitConversions.Round2(prev.BestPrecipitationInches),
                    precipMm = UnitConversions.Round2(prev.BestPrecipitationMm),
                    windGustMph = UnitConversions.Round1(prev.WindGustMph),
                    humidityAvg = UnitConversions.Round1(prev.HumidityAvg),
                    diffHighF
                });
            }

            // Historical stats on this calendar day across all years
            var allYearsForDay = summaries
                .Where(s =>
                {
                    var d = s.ToDateOnly();
                    return d.Month == targetMonth && d.Day == targetDay;
                })
                .ToList();

            var highs = allYearsForDay.Where(s => s.TemperatureHighF.HasValue).Select(s => s.TemperatureHighF!.Value).ToList();
            var lows = allYearsForDay.Where(s => s.TemperatureLowF.HasValue).Select(s => s.TemperatureLowF!.Value).ToList();
            var hottest = allYearsForDay.OrderByDescending(s => s.TemperatureHigh ?? double.MinValue).FirstOrDefault();
            var coldest = allYearsForDay.OrderBy(s => s.TemperatureLow ?? double.MaxValue).FirstOrDefault();

            recentDaysList.Add(new
            {
                date = targetDate.ToString("yyyy-MM-dd"),
                month = targetMonth,
                day = targetDay,
                calendarDay = targetDate.ToString("MM-dd"),
                dayOfWeek = targetDate.DayOfWeek.ToString(),
                displayDate,
                currentYear = new
                {
                    year = targetDate.Year,
                    date = currentDaySummary.Date,
                    temperatureHighF = UnitConversions.Round1(currentDaySummary.TemperatureHighF),
                    temperatureHighC = currentDaySummary.TemperatureHigh,
                    temperatureLowF = UnitConversions.Round1(currentDaySummary.TemperatureLowF),
                    temperatureLowC = currentDaySummary.TemperatureLow,
                    temperatureAvgF = UnitConversions.Round1(currentDaySummary.TemperatureAvgF),
                    temperatureAvgC = currentDaySummary.TemperatureAvg,
                    precipInches = UnitConversions.Round2(currentDaySummary.BestPrecipitationInches),
                    precipMm = UnitConversions.Round2(currentDaySummary.BestPrecipitationMm),
                    windGustMph = UnitConversions.Round1(currentDaySummary.WindGustMph),
                    windAvgMph = UnitConversions.Round1(currentDaySummary.WindAvgMph),
                    humidityAvg = UnitConversions.Round1(currentDaySummary.HumidityAvg)
                },
                previousYears = prevYearItems,
                historicalNormal = new
                {
                    normalHighF = highs.Count > 0 ? UnitConversions.Round1(highs.Average()) : (double?)null,
                    normalLowF = lows.Count > 0 ? UnitConversions.Round1(lows.Average()) : (double?)null,
                    recordHighF = hottest != null ? UnitConversions.Round1(hottest.TemperatureHighF) : null,
                    recordHighYear = hottest?.ToDateOnly().Year,
                    recordLowF = coldest != null ? UnitConversions.Round1(coldest.TemperatureLowF) : null,
                    recordLowYear = coldest?.ToDateOnly().Year,
                    yearsObserved = allYearsForDay.Count,
                    hadRainCount = allYearsForDay.Count(s => s.BestPrecipitationMm >= 0.254)
                }
            });
        }

        return new
        {
            station = new
            {
                meta.StationId,
                meta.DeviceId,
                meta.StationName,
                meta.Timezone
            },
            asOfDate = yesterday.ToString("yyyy-MM-dd"),
            recentDays = recentDaysList
        };
    }

    private static object BuildNextWeekReport(
        StationMetadata meta, List<DailyWeatherSummary> summaries, DateOnly todayLocal)
    {
        // Next 7 days starting tomorrow (today + 1 through today + 7)
        var daysList = new List<object>();

        for (int i = 1; i <= 7; i++)
        {
            var upcomingDate = todayLocal.AddDays(i);
            int month = upcomingDate.Month;
            int day = upcomingDate.Day;
            var displayDate = upcomingDate.ToString("MMMM d", CultureInfo.InvariantCulture);

            // Previous year (1 year ago)
            var prevYearDate = upcomingDate.AddYears(-1);
            var prevYearSummary = summaries.FirstOrDefault(s => s.Date == prevYearDate.ToString("yyyy-MM-dd"));

            // All previous years on this same calendar day (month/day)
            var allPrevYearsOnDay = summaries
                .Where(s =>
                {
                    var d = s.ToDateOnly();
                    return d.Month == month && d.Day == day && d.Year < upcomingDate.Year;
                })
                .OrderByDescending(s => s.ToDateOnly().Year)
                .ToList();

            var prevYearDetails = prevYearSummary != null ? new
            {
                year = prevYearDate.Year,
                date = prevYearSummary.Date,
                temperatureHighF = UnitConversions.Round1(prevYearSummary.TemperatureHighF),
                temperatureHighC = prevYearSummary.TemperatureHigh,
                temperatureLowF = UnitConversions.Round1(prevYearSummary.TemperatureLowF),
                temperatureLowC = prevYearSummary.TemperatureLow,
                temperatureAvgF = UnitConversions.Round1(prevYearSummary.TemperatureAvgF),
                temperatureAvgC = prevYearSummary.TemperatureAvg,
                precipInches = UnitConversions.Round2(prevYearSummary.BestPrecipitationInches),
                precipMm = UnitConversions.Round2(prevYearSummary.BestPrecipitationMm),
                windGustMph = UnitConversions.Round1(prevYearSummary.WindGustMph),
                windAvgMph = UnitConversions.Round1(prevYearSummary.WindAvgMph),
                humidityAvg = UnitConversions.Round1(prevYearSummary.HumidityAvg),
                weatherDescription = FormatWeatherDescription(prevYearSummary)
            } : null;

            var historicalYearsList = allPrevYearsOnDay.Select(p => new
            {
                year = p.ToDateOnly().Year,
                date = p.Date,
                temperatureHighF = UnitConversions.Round1(p.TemperatureHighF),
                temperatureHighC = p.TemperatureHigh,
                temperatureLowF = UnitConversions.Round1(p.TemperatureLowF),
                temperatureLowC = p.TemperatureLow,
                temperatureAvgF = UnitConversions.Round1(p.TemperatureAvgF),
                precipInches = UnitConversions.Round2(p.BestPrecipitationInches),
                precipMm = UnitConversions.Round2(p.BestPrecipitationMm),
                windGustMph = UnitConversions.Round1(p.WindGustMph),
                weatherDescription = FormatWeatherDescription(p)
            }).ToList();

            var highs = allPrevYearsOnDay.Where(p => p.TemperatureHighF.HasValue).Select(p => p.TemperatureHighF!.Value).ToList();
            var lows = allPrevYearsOnDay.Where(p => p.TemperatureLowF.HasValue).Select(p => p.TemperatureLowF!.Value).ToList();
            var hottest = allPrevYearsOnDay.OrderByDescending(p => p.TemperatureHigh ?? double.MinValue).FirstOrDefault();
            var coldest = allPrevYearsOnDay.OrderBy(p => p.TemperatureLow ?? double.MaxValue).FirstOrDefault();
            var rainCount = allPrevYearsOnDay.Count(p => p.BestPrecipitationMm >= 0.254);

            daysList.Add(new
            {
                dayOffset = i,
                upcomingDate = upcomingDate.ToString("yyyy-MM-dd"),
                calendarDay = upcomingDate.ToString("MM-dd"),
                dayOfWeek = upcomingDate.DayOfWeek.ToString(),
                displayDate,
                previousYearWeather = prevYearDetails,
                allHistoricalYears = historicalYearsList,
                historicalNormal = new
                {
                    normalHighF = highs.Count > 0 ? UnitConversions.Round1(highs.Average()) : (double?)null,
                    normalLowF = lows.Count > 0 ? UnitConversions.Round1(lows.Average()) : (double?)null,
                    recordHighF = hottest != null ? UnitConversions.Round1(hottest.TemperatureHighF) : null,
                    recordHighYear = hottest?.ToDateOnly().Year,
                    recordLowF = coldest != null ? UnitConversions.Round1(coldest.TemperatureLowF) : null,
                    recordLowYear = coldest?.ToDateOnly().Year,
                    rainChancePercent = allPrevYearsOnDay.Count > 0 ? Math.Round((double)rainCount / allPrevYearsOnDay.Count * 100.0, 0) : 0,
                    yearsObserved = allPrevYearsOnDay.Count
                }
            });
        }

        return new
        {
            station = new
            {
                meta.StationId,
                meta.DeviceId,
                meta.StationName,
                meta.Timezone
            },
            generatedFromReferenceDate = todayLocal.ToString("yyyy-MM-dd"),
            upcomingWeek = daysList
        };
    }

    private static object BuildOverviewReport(
        StationMetadata meta, List<DailyWeatherSummary> summaries, DateOnly todayLocal)
    {
        var hottest = summaries.Where(s => s.TemperatureHigh.HasValue).OrderByDescending(s => s.TemperatureHigh!.Value).FirstOrDefault();
        var coldest = summaries.Where(s => s.TemperatureLow.HasValue).OrderBy(s => s.TemperatureLow!.Value).FirstOrDefault();
        var rainiest = summaries.OrderByDescending(s => s.BestPrecipitationMm).FirstOrDefault();
        var windiest = summaries.Where(s => s.WindGust.HasValue).OrderByDescending(s => s.WindGust!.Value).FirstOrDefault();

        double totalRainMm = summaries.Sum(s => s.BestPrecipitationMm);

        return new
        {
            station = new
            {
                meta.StationId,
                meta.DeviceId,
                meta.StationName,
                meta.Timezone,
                meta.Latitude,
                meta.Longitude,
                elevationMeters = meta.ElevationMeters,
                elevationFeet = UnitConversions.Round1(meta.ElevationFeet)
            },
            timeRange = new
            {
                startDate = summaries.FirstOrDefault()?.Date,
                endDate = summaries.LastOrDefault()?.Date,
                totalDaysRecorded = summaries.Count,
                asOfLocal = todayLocal.ToString("yyyy-MM-dd")
            },
            allTimeRecords = new
            {
                hottestDay = hottest != null ? new
                {
                    date = hottest.Date,
                    tempF = UnitConversions.Round1(hottest.TemperatureHighF),
                    tempC = hottest.TemperatureHigh
                } : null,
                coldestDay = coldest != null ? new
                {
                    date = coldest.Date,
                    tempF = UnitConversions.Round1(coldest.TemperatureLowF),
                    tempC = coldest.TemperatureLow
                } : null,
                rainiestDay = rainiest != null ? new
                {
                    date = rainiest.Date,
                    precipInches = UnitConversions.Round2(rainiest.BestPrecipitationInches),
                    precipMm = UnitConversions.Round2(rainiest.BestPrecipitationMm)
                } : null,
                windiestDay = windiest != null ? new
                {
                    date = windiest.Date,
                    gustMph = UnitConversions.Round1(windiest.WindGustMph),
                    gustMps = windiest.WindGust
                } : null,
                totalPrecipitation = new
                {
                    totalInches = UnitConversions.Round2(UnitConversions.MillimetersToInches(totalRainMm)),
                    totalMm = UnitConversions.Round2(totalRainMm),
                    rainyDaysCount = summaries.Count(s => s.BestPrecipitationMm >= 0.254)
                }
            },
            availableReports = new[]
            {
                "long_term_comparisons.json",
                "recent_day_comparisons.json",
                "next_week_historical_comparisons.json",
                "all_daily_summaries.json"
            },
            generatedUtc = DateTime.UtcNow.ToString("o")
        };
    }

    private static object BuildAllDailySummariesReport(StationMetadata meta, List<DailyWeatherSummary> summaries)
    {
        return new
        {
            station = new
            {
                meta.StationId,
                meta.DeviceId,
                meta.StationName,
                meta.Timezone
            },
            count = summaries.Count,
            dailySummaries = summaries.Select(s => new
            {
                s.Date,
                tempAvgF = UnitConversions.Round1(s.TemperatureAvgF),
                tempHighF = UnitConversions.Round1(s.TemperatureHighF),
                tempLowF = UnitConversions.Round1(s.TemperatureLowF),
                tempAvgC = s.TemperatureAvg,
                tempHighC = s.TemperatureHigh,
                tempLowC = s.TemperatureLow,
                precipInches = UnitConversions.Round2(s.BestPrecipitationInches),
                precipMm = UnitConversions.Round2(s.BestPrecipitationMm),
                windAvgMph = UnitConversions.Round1(s.WindAvgMph),
                windGustMph = UnitConversions.Round1(s.WindGustMph),
                windDirDeg = s.WindDirectionAvg,
                pressureMb = s.PressureAvg,
                humidityAvg = UnitConversions.Round1(s.HumidityAvg),
                solarRadiationAvg = s.SolarRadiationAvg,
                uvAvg = s.UvAvg,
                lightningStrikes = s.LightningStrikeCount ?? 0
            })
        };
    }

    private static string FormatWeatherDescription(DailyWeatherSummary s)
    {
        var rainIn = s.BestPrecipitationInches;
        var highF = s.TemperatureHighF.HasValue ? $"{Math.Round(s.TemperatureHighF.Value):F0}°F" : "--";
        var lowF = s.TemperatureLowF.HasValue ? $"{Math.Round(s.TemperatureLowF.Value):F0}°F" : "--";

        string rainDesc = rainIn >= 0.01 ? $"{rainIn:F2} in rain" : "No rain";
        string windDesc = s.WindGustMph.HasValue ? $", Peak gust {Math.Round(s.WindGustMph.Value):F0} mph" : "";

        return $"High {highF} / Low {lowF}, {rainDesc}{windDesc}";
    }

    private static async Task WriteJsonAsync(string path, object data)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        await File.WriteAllTextAsync(path, json);
    }
}
