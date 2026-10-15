namespace HistoricTempestsDataDownloader;

public class CommandLineOptions
{
    public string AccessKey { get; set; } = string.Empty;
    public long DeviceId { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public string DataDirectory { get; set; } = string.Empty;

    public string FullDatabasePath
    {
        get
        {
            var dbFileName = DatabaseName;
            if (!dbFileName.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase) &&
                !dbFileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
            {
                dbFileName += ".sqlite";
            }

            if (Path.IsPathRooted(dbFileName))
            {
                return dbFileName;
            }

            var dir = string.IsNullOrWhiteSpace(DataDirectory) ? Directory.GetCurrentDirectory() : DataDirectory;
            return Path.Combine(dir, dbFileName);
        }
    }

    public string ResolvedDataDirectory
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(DataDirectory))
            {
                return DataDirectory;
            }

            var dir = Path.GetDirectoryName(FullDatabasePath);
            return string.IsNullOrWhiteSpace(dir) ? Directory.GetCurrentDirectory() : dir;
        }
    }

    public static bool TryParse(string[] args, out CommandLineOptions options, out string errorMessage)
    {
        options = new CommandLineOptions();
        errorMessage = string.Empty;

        if (args.Length == 0 || args.Any(a => a is "-h" or "--help" or "-?"))
        {
            errorMessage = GetHelpText();
            return false;
        }

        var positionalArgs = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg is "-k" or "--key" or "--access-key" or "--token" or "-t")
            {
                if (i + 1 < args.Length)
                {
                    options.AccessKey = args[++i].Trim();
                }
                else
                {
                    errorMessage = "Missing value for access key option.";
                    return false;
                }
            }
            else if (arg is "-d" or "--device-id" or "--device" or "--dev")
            {
                if (i + 1 < args.Length && long.TryParse(args[++i], out var devId))
                {
                    options.DeviceId = devId;
                }
                else
                {
                    errorMessage = "Missing or invalid numeric value for device id option.";
                    return false;
                }
            }
            else if (arg is "-n" or "--database" or "--database-name" or "--db")
            {
                if (i + 1 < args.Length)
                {
                    options.DatabaseName = args[++i].Trim();
                }
                else
                {
                    errorMessage = "Missing value for database option.";
                    return false;
                }
            }
            else if (arg is "-o" or "--data-dir" or "--dir" or "--directory" or "--output-dir")
            {
                if (i + 1 < args.Length)
                {
                    options.DataDirectory = args[++i].Trim();
                }
                else
                {
                    errorMessage = "Missing value for data directory option.";
                    return false;
                }
            }
            else if (arg.StartsWith("--access-key=", StringComparison.OrdinalIgnoreCase) ||
                     arg.StartsWith("--key=", StringComparison.OrdinalIgnoreCase) ||
                     arg.StartsWith("--token=", StringComparison.OrdinalIgnoreCase))
            {
                options.AccessKey = arg[(arg.IndexOf('=') + 1)..].Trim();
            }
            else if (arg.StartsWith("--device-id=", StringComparison.OrdinalIgnoreCase) ||
                     arg.StartsWith("--device=", StringComparison.OrdinalIgnoreCase))
            {
                var val = arg[(arg.IndexOf('=') + 1)..].Trim();
                if (long.TryParse(val, out var devId))
                {
                    options.DeviceId = devId;
                }
                else
                {
                    errorMessage = $"Invalid numeric device id: '{val}'";
                    return false;
                }
            }
            else if (arg.StartsWith("--database=", StringComparison.OrdinalIgnoreCase) ||
                     arg.StartsWith("--database-name=", StringComparison.OrdinalIgnoreCase) ||
                     arg.StartsWith("--db=", StringComparison.OrdinalIgnoreCase))
            {
                options.DatabaseName = arg[(arg.IndexOf('=') + 1)..].Trim();
            }
            else if (arg.StartsWith("--data-dir=", StringComparison.OrdinalIgnoreCase) ||
                     arg.StartsWith("--dir=", StringComparison.OrdinalIgnoreCase) ||
                     arg.StartsWith("--output-dir=", StringComparison.OrdinalIgnoreCase))
            {
                options.DataDirectory = arg[(arg.IndexOf('=') + 1)..].Trim();
            }
            else if (!arg.StartsWith("-"))
            {
                positionalArgs.Add(arg);
            }
            else
            {
                errorMessage = $"Unrecognized option: '{arg}'";
                return false;
            }
        }

        // Fill unset properties from positional arguments if provided
        int posIndex = 0;
        if (string.IsNullOrWhiteSpace(options.AccessKey) && posIndex < positionalArgs.Count)
        {
            // If positional argument looks like a device id (pure number), don't assign it to access key if another arg is a guid
            if (long.TryParse(positionalArgs[posIndex], out var numId) && options.DeviceId == 0)
            {
                options.DeviceId = numId;
                posIndex++;
            }
            else
            {
                options.AccessKey = positionalArgs[posIndex++];
            }
        }

        if (options.DeviceId == 0 && posIndex < positionalArgs.Count)
        {
            if (long.TryParse(positionalArgs[posIndex], out var numId))
            {
                options.DeviceId = numId;
                posIndex++;
            }
        }

        if (string.IsNullOrWhiteSpace(options.DatabaseName) && posIndex < positionalArgs.Count)
        {
            options.DatabaseName = positionalArgs[posIndex++];
        }

        if (string.IsNullOrWhiteSpace(options.DataDirectory) && posIndex < positionalArgs.Count)
        {
            options.DataDirectory = positionalArgs[posIndex++];
        }

        // Validation
        if (string.IsNullOrWhiteSpace(options.AccessKey))
        {
            errorMessage = "WeatherFlow Tempest access key / token is required. Use --access-key <key>.";
            return false;
        }

        if (options.DeviceId <= 0)
        {
            errorMessage = "Valid numeric device id is required. Use --device-id <id>.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(options.DatabaseName))
        {
            errorMessage = "Database name or path is required. Use --database <name>.";
            return false;
        }

        return true;
    }

    public static string GetHelpText()
    {
        return """
HistoricTempestsDataDownloader - Downloads WeatherFlow Tempest station history and generates comparison datasets.

Usage:
  HistoricTempestsDataDownloader --access-key <key> --device-id <id> --database <name> [--data-dir <path>]
  HistoricTempestsDataDownloader -k <key> -d <id> -n <name> [-o <path>]
  HistoricTempestsDataDownloader <access-key> <device-id> <database> [<data-dir>]

Options:
  -k, --access-key, --token <string>    WeatherFlow Tempest API Personal Access Token (Required)
  -d, --device-id <long>                Device ID (Required)
  -n, --database, --db <string>         Database name or full path to SQLite file (Required)
  -o, --data-dir, --dir <string>        Directory where the database and output JSON files are saved
  -h, --help                            Show this help message
""";
    }
}
