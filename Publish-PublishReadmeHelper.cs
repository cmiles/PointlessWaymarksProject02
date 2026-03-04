var searchDirectory = new DirectoryInfo(Environment.CurrentDirectory);

Console.Write(
    $"Pointless Waymarks README.md -> Project Specific README_[project] running in {searchDirectory.FullName}");

var mainReadme = new FileInfo(Path.Combine(searchDirectory.FullName, "README-Fossil.md"));

if (mainReadme.Exists && File.Exists(Path.Combine(searchDirectory.FullName, "PointlessWaymarks.sln")))
{
    var gitMirrorInformation =
        $"""
         ## Fossil Repository Mirror - This is a Read Only View ##

         *This file is auto-generated - do not edit this directly, changes will be overwritten.*
         """;

    await File.WriteAllTextAsync(Path.Combine(searchDirectory.FullName, "README.md"), gitMirrorInformation);

    Console.WriteLine("Found the main README-Fossil.md - prepended mirror message and wrote to README.md");
    Console.WriteLine();
}

var subDirectories = searchDirectory.GetDirectories("*", SearchOption.AllDirectories)
    .Where(d => !d.FullName.Contains(Path.DirectorySeparatorChar + "bin", StringComparison.OrdinalIgnoreCase) &&
                !d.FullName.Contains(Path.DirectorySeparatorChar + "obj", StringComparison.OrdinalIgnoreCase) &&
                !d.FullName.Contains(Path.DirectorySeparatorChar + "debug", StringComparison.OrdinalIgnoreCase) &&
                !d.FullName.Contains(Path.DirectorySeparatorChar + "release", StringComparison.OrdinalIgnoreCase) &&
                !d.FullName.Contains(Path.DirectorySeparatorChar + ".", StringComparison.OrdinalIgnoreCase))
    .ToArray();

Console.WriteLine($"Scanning {subDirectories.Length} SubDirectories.");

foreach (var subDirectory in subDirectories)
    try
    {
        var possibleReadme = new FileInfo(Path.Combine(subDirectory.FullName, "README.md"));

        if (subDirectory.Name.Equals("PointlessWaymarksTools", StringComparison.OrdinalIgnoreCase))
        {
            var toolsMainReadme = Path.Combine(subDirectory.FullName, "README-Fossil.md");

            if (File.Exists(toolsMainReadme))
            {
                var gitMirrorInformation =
                    $"""
                     ## Fossil Repository Mirror - This is a Read Only View ##

                     *This file is auto-generated - do not edit this directly, changes will be overwritten.*

                     {await File.ReadAllTextAsync(toolsMainReadme)}
                     """;

                await File.WriteAllTextAsync(Path.Combine(subDirectory.FullName, "README.md"), gitMirrorInformation);
				
				Console.WriteLine(subDirectory.FullName);
                Console.WriteLine(
                    "  Found the main README-Fossil.md - prepended mirror message and wrote to README.md");
                Console.WriteLine();
            }

            continue;
        }


        if (!possibleReadme.Exists) continue;

        var readmeName = string.Join("-", subDirectory.Name.Split(".")[1..]);

        if (string.IsNullOrWhiteSpace(readmeName))
        {
            var possibleSolutionFile = subDirectory.EnumerateFiles("*.sln", SearchOption.TopDirectoryOnly).ToList();

            readmeName = possibleSolutionFile.Any()
                ? possibleSolutionFile.First().Name.Split(".")[0]
                : subDirectory.Name;
        }

        var targetReadme = new FileInfo(Path.Combine(subDirectory.FullName, $"README_{readmeName}.md"));

        possibleReadme.CopyTo(targetReadme.FullName, true);

        Console.WriteLine($"  Copied {possibleReadme.FullName} to {targetReadme.FullName}");
    }
    catch (Exception e)
    {
        Console.WriteLine($"!!! Error - continuing...{Environment.NewLine}{e}");
    }