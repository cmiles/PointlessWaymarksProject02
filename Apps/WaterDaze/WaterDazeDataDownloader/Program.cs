using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WaterDaze.Models;
using WaterDazeDataDownloader.Services;

namespace WaterDazeDataDownloader;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        string? outputDirectory = null;
        var endDate = DateTime.Today.AddDays(-2);
        var delayMs = 1000;
        var maxRetries = 5;
        List<string>? requestedSiteCodes = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg is "-h" or "--help" or "-?" or "/?")
            {
                PrintHelp();
                return 0;
            }

            if (arg is "-o" or "--output" or "-d" or "--dir" or "-f" or "--folder")
            {
                if (i + 1 < args.Length)
                {
                    outputDirectory = args[++i];
                }
                else
                {
                    Console.Error.WriteLine($"Error: Missing value for option '{arg}'.");
                    PrintHelp();
                    return 1;
                }
            }
            else if (arg.StartsWith("--output=", StringComparison.OrdinalIgnoreCase))
            {
                outputDirectory = arg["--output=".Length..];
            }
            else if (arg.StartsWith("-o=", StringComparison.OrdinalIgnoreCase))
            {
                outputDirectory = arg["-o=".Length..];
            }
            else if (arg is "--end-date")
            {
                if (i + 1 < args.Length && DateTime.TryParse(args[++i], CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                {
                    endDate = parsedDate.Date;
                }
                else
                {
                    Console.Error.WriteLine("Error: Invalid or missing date format for --end-date. Use yyyy-MM-dd.");
                    return 1;
                }
            }
            else if (arg is "--delay")
            {
                if (i + 1 < args.Length && int.TryParse(args[++i], out var parsedDelay) && parsedDelay >= 0)
                {
                    delayMs = parsedDelay;
                }
                else
                {
                    Console.Error.WriteLine("Error: Invalid or missing integer value for --delay (in milliseconds).");
                    return 1;
                }
            }
            else if (arg is "--max-retries")
            {
                if (i + 1 < args.Length && int.TryParse(args[++i], out var parsedRetries) && parsedRetries >= 0)
                {
                    maxRetries = parsedRetries;
                }
                else
                {
                    Console.Error.WriteLine("Error: Invalid or missing integer value for --max-retries.");
                    return 1;
                }
            }
            else if (arg is "--sites")
            {
                if (i + 1 < args.Length)
                {
                    requestedSiteCodes = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                }
                else
                {
                    Console.Error.WriteLine("Error: Missing comma-separated sites list for --sites.");
                    return 1;
                }
            }
            else if (!arg.StartsWith('-') && string.IsNullOrEmpty(outputDirectory))
            {
                outputDirectory = arg;
            }
            else
            {
                Console.Error.WriteLine($"Error: Unrecognized argument '{arg}'.");
                PrintHelp();
                return 1;
            }
        }

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            Console.Error.WriteLine("Error: Output folder was not specified.");
            Console.WriteLine();
            PrintHelp();
            return 1;
        }

        var fullOutputDir = Path.GetFullPath(outputDirectory);

        // Determine sites to download
        var allSites = GaugeSite.TucsonAreaSites;
        List<GaugeSite> targetSites;

        if (requestedSiteCodes is { Count: > 0 })
        {
            targetSites = [];
            foreach (var code in requestedSiteCodes)
            {
                var site = allSites.FirstOrDefault(s => s.SiteCode.Equals(code, StringComparison.OrdinalIgnoreCase))
                           ?? new GaugeSite
                           {
                               SiteCode = code,
                               SiteName = $"Site {code}",
                               DisplayTitle = $"Site {code} -- {code}"
                           };
                targetSites.Add(site);
            }
        }
        else
        {
            targetSites = allSites.ToList();
        }

        Console.WriteLine("========================================================================");
        Console.WriteLine("WaterDaze Data Downloader");
        Console.WriteLine("========================================================================");
        Console.WriteLine($"Output Directory:     {fullOutputDir}");
        Console.WriteLine($"End Date:             {endDate:yyyy-MM-dd} ({(endDate == DateTime.Today.AddDays(-2) ? "two days ago" : "custom")})");
        Console.WriteLine($"Total Gauges:         {targetSites.Count}");
        Console.WriteLine($"Delay Between Gauges: {delayMs} ms");
        Console.WriteLine($"Max Retries on Error: {maxRetries}");
        Console.WriteLine("========================================================================");
        Console.WriteLine();

        var downloader = new GaugeDataDownloader();
        var options = new GaugeDataDownloaderOptions
        {
            OutputDirectory = fullOutputDir,
            EndDate = endDate,
            DelayBetweenRequests = TimeSpan.FromMilliseconds(delayMs),
            MaxRetries = maxRetries,
            Logger = Console.WriteLine
        };

        var summary = await downloader.DownloadAllGaugesAsync(targetSites, options);

        Console.WriteLine();
        Console.WriteLine("========================================================================");
        Console.WriteLine("Download Summary");
        Console.WriteLine("========================================================================");
        Console.WriteLine($"Total Gauges:         {summary.TotalSites}");
        Console.WriteLine($"Successful:           {summary.SuccessCount}");
        Console.WriteLine($"Failed:               {summary.FailureCount}");
        Console.WriteLine($"Total Daily Records:  {summary.TotalRecords:N0}");
        Console.WriteLine($"Total Data Written:   {summary.TotalBytesWritten / (1024.0 * 1024.0):F2} MB");
        Console.WriteLine($"Time Elapsed:         {summary.ElapsedTime.TotalSeconds:F1}s");
        Console.WriteLine("========================================================================");

        return summary.FailureCount == 0 ? 0 : 1;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("WaterDaze Data Downloader");
        Console.WriteLine("Downloads USGS streamflow daily data for gauges through two days ago and saves each gauge as JSON in a target folder.");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  WaterDazeDataDownloader <output-folder> [options]");
        Console.WriteLine("  WaterDazeDataDownloader -o <output-folder> [options]");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine("  <output-folder>           Target folder where {siteCode}.json files will be saved.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -o, --output <path>       Specify the output folder path.");
        Console.WriteLine("  --end-date <yyyy-MM-dd>   Override the end date (default: two days ago).");
        Console.WriteLine("  --sites <codes>           Comma-separated list of USGS site codes to download.");
        Console.WriteLine("  --delay <ms>              Delay between gauge downloads in ms (default: 1000).");
        Console.WriteLine("  --max-retries <n>         Max retries on 503 or transient network errors (default: 5).");
        Console.WriteLine("  -h, --help                Show this help information.");
        Console.WriteLine();
        Console.WriteLine("Default Tucson Area Gauges (15 sites):");
        foreach (var site in GaugeSite.TucsonAreaSites)
        {
            Console.WriteLine($"  {site.SiteCode} - {site.SiteName}");
        }
    }
}
