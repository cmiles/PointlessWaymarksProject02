namespace HistoricTempestsDataDownloader;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("================================================================================");
        Console.WriteLine("       WeatherFlow Tempest Historic Data Downloader & Aggregator");
        Console.WriteLine("================================================================================");

        if (!CommandLineOptions.TryParse(args, out var options, out var errorMessage))
        {
            if (args.Any(a => a is "-h" or "--help" or "-?"))
            {
                Console.WriteLine(errorMessage);
                return 0;
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {errorMessage}");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Run with --help for usage information.");
            return 1;
        }

        // Mask the key for logging
        string maskedKey = options.AccessKey.Length > 8
            ? $"{options.AccessKey[..4]}...{options.AccessKey[^4..]}"
            : "****";

        Console.WriteLine($"Device ID       : {options.DeviceId}");
        Console.WriteLine($"Access Key      : {maskedKey}");
        Console.WriteLine($"Database Path   : {options.FullDatabasePath}");
        Console.WriteLine($"Output Directory: {options.ResolvedDataDirectory}");
        Console.WriteLine();

        try
        {
            await using var db = new WeatherDatabase(options.FullDatabasePath);
            Console.WriteLine("Initializing SQLite database...");
            await db.InitializeAsync();

            var apiClient = new TempestApiClient();

            Console.WriteLine("Retrieving station metadata from WeatherFlow API...");
            var meta = await apiClient.GetStationForDeviceAsync(options.DeviceId, options.AccessKey);
            if (meta == null)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Warning: Could not find station containing device ID in /stations. Checking cached database metadata...");
                Console.ResetColor();

                meta = await db.GetStationMetadataAsync();
                if (meta == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error: Device ID {options.DeviceId} was not found on your WeatherFlow account, and no local metadata is available.");
                    Console.ResetColor();
                    return 1;
                }
            }

            // Determine Station TimeZone
            TimeZoneInfo stationTz;
            try
            {
                stationTz = TimeZoneInfo.FindSystemTimeZoneById(meta.Timezone);
            }
            catch (TimeZoneNotFoundException)
            {
                if (TimeZoneInfo.TryConvertIanaIdToWindowsId(meta.Timezone, out var windowsTzId))
                {
                    stationTz = TimeZoneInfo.FindSystemTimeZoneById(windowsTzId);
                }
                else
                {
                    var offset = TimeSpan.FromMinutes(meta.TimezoneOffsetMinutes);
                    stationTz = TimeZoneInfo.CreateCustomTimeZone(meta.Timezone, offset, meta.Timezone, meta.Timezone);
                }
            }

            // Station local day calculation
            var nowUtc = DateTime.UtcNow;
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, stationTz);
            var todayLocal = DateOnly.FromDateTime(nowLocal);
            var yesterdayLocal = todayLocal.AddDays(-1);

            DateOnly stationOnlineDate;
            if (meta.CreatedEpoch > 0)
            {
                var createdUtc = DateTimeOffset.FromUnixTimeSeconds(meta.CreatedEpoch).UtcDateTime;
                var createdLocal = TimeZoneInfo.ConvertTimeFromUtc(createdUtc, stationTz);
                stationOnlineDate = DateOnly.FromDateTime(createdLocal);
            }
            else
            {
                stationOnlineDate = new DateOnly(2020, 1, 1);
            }

            Console.WriteLine($"Station Name    : {meta.StationName} (Station ID: {meta.StationId})");
            var offsetPrefix = stationTz.BaseUtcOffset >= TimeSpan.Zero ? "+" : "-";
            var offsetStr = $"{offsetPrefix}{Math.Abs(stationTz.BaseUtcOffset.Hours):D2}:{Math.Abs(stationTz.BaseUtcOffset.Minutes):D2}";
            Console.WriteLine($"Timezone        : {meta.Timezone} (UTC {offsetStr})");
            Console.WriteLine($"Current Local   : {nowLocal:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"Station Online  : {stationOnlineDate:yyyy-MM-dd}");
            Console.WriteLine($"Target Range    : {stationOnlineDate:yyyy-MM-dd} to {yesterdayLocal:yyyy-MM-dd} (Previous Day)");
            Console.WriteLine();

            // Examine SQLite data to determine missing dates
            Console.WriteLine("Examining existing SQLite summaries...");
            var existingDates = await db.GetExistingDatesAsync(stationOnlineDate, yesterdayLocal);
            Console.WriteLine($"Found {existingDates.Count} existing daily summaries in SQLite.");

            var missingDates = new HashSet<DateOnly>();
            for (var d = stationOnlineDate; d <= yesterdayLocal; d = d.AddDays(1))
            {
                if (!existingDates.Contains(d))
                {
                    missingDates.Add(d);
                }
            }

            if (missingDates.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Database is fully up to date through previous day ({yesterdayLocal:yyyy-MM-dd}). No downloads required.");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine($"Found {missingDates.Count} missing/new day(s) to download.");
                Console.WriteLine("Requesting daily summaries from WeatherFlow Tempest API...");

                int savedCount = 0;
                List<DailyWeatherSummary> summariesToSave = new();

                try
                {
                    // Attempt fast, calibrated daily stats retrieval
                    var (statsSummaries, firstLocalDay, lastLocalDay) =
                        await apiClient.GetDeviceDailyStatsAsync(options.DeviceId, options.AccessKey, meta.StationId);

                    if (!string.IsNullOrWhiteSpace(firstLocalDay))
                    {
                        meta.FirstObservationDateLocal = firstLocalDay;
                    }
                    if (!string.IsNullOrWhiteSpace(lastLocalDay))
                    {
                        meta.LastObservationDateLocal = lastLocalDay;
                    }

                    // Filter only the summaries that were missing and <= yesterdayLocal
                    foreach (var s in statsSummaries)
                    {
                        if (DateOnly.TryParse(s.Date, out var sDate) &&
                            missingDates.Contains(sDate) &&
                            sDate <= yesterdayLocal)
                        {
                            summariesToSave.Add(s);
                            missingDates.Remove(sDate);
                        }
                    }

                    if (summariesToSave.Count > 0)
                    {
                        Console.WriteLine($"Saving {summariesToSave.Count} daily summaries to SQLite...");
                        savedCount += await db.SaveDailySummariesAsync(summariesToSave);
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Note: /stats/device endpoint returned: {ex.Message}. Falling back to /observations endpoint.");
                    Console.ResetColor();
                }

                // If any missing dates remain, download via observations API
                if (missingDates.Count > 0)
                {
                    Console.WriteLine($"Retrieving observations for remaining {missingDates.Count} missing date(s)...");
                    var sortedMissing = missingDates.OrderBy(d => d).ToList();
                    var observationSummaries = new List<DailyWeatherSummary>();

                    int processed = 0;
                    foreach (var missingDate in sortedMissing)
                    {
                        processed++;
                        Console.Write($"\rDownloading day {processed}/{sortedMissing.Count}: {missingDate:yyyy-MM-dd}...");

                        var summary = await apiClient.GetObservationsDailySummaryAsync(
                            options.DeviceId, missingDate, stationTz, options.AccessKey, meta.StationId);

                        if (summary != null)
                        {
                            observationSummaries.Add(summary);
                        }

                        // Gentle pause between requests to respect rate limits
                        await Task.Delay(300);
                    }
                    Console.WriteLine();

                    if (observationSummaries.Count > 0)
                    {
                        Console.WriteLine($"Saving {observationSummaries.Count} observation summaries to SQLite...");
                        savedCount += await db.SaveDailySummariesAsync(observationSummaries);
                    }
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Successfully persisted {savedCount} summaries to SQLite database.");
                Console.ResetColor();
            }

            // Save updated station metadata
            await db.SaveStationMetadataAsync(meta);

            // Query database and generate JSON comparison files
            Console.WriteLine();
            Console.WriteLine("Querying database to generate web comparison datasets...");
            var allSummaries = await db.GetAllDailySummariesAsync();
            Console.WriteLine($"Loaded {allSummaries.Count} total daily summaries from database.");

            Console.WriteLine($"Exporting JSON files to '{options.ResolvedDataDirectory}'...");
            await JsonReportGenerator.GenerateAllReportsAsync(meta, allSummaries, options.ResolvedDataDirectory, todayLocal);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("All JSON datasets created successfully:");
            Console.ResetColor();
            Console.WriteLine($"  - long_term_comparisons.json          (Monthly & yearly comparisons across all years)");
            Console.WriteLine($"  - monthly_yearly_comparisons.json     (Alias for long_term_comparisons.json)");
            Console.WriteLine($"  - recent_day_comparisons.json         (Recent calendar days vs previous years)");
            Console.WriteLine($"  - next_week_historical_comparisons.json (Next 7 days compared to prior years)");
            Console.WriteLine($"  - weather_summary_overview.json       (Station overview & all-time records)");
            Console.WriteLine($"  - all_daily_summaries.json            (Complete chronological time series)");

            Console.WriteLine();
            Console.WriteLine("Process completed successfully!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Fatal Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Console.ResetColor();
            return 1;
        }
    }
}
