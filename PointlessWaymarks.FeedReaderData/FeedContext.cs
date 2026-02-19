using System.Data.Common;
using FluentMigrator.Runner;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PointlessWaymarks.FeedReaderData.Models;
using Serilog;
using SQLitePCL;

namespace PointlessWaymarks.FeedReaderData;

public class FeedContext(DbContextOptions<FeedContext> options) : DbContext(options)
{
    public static readonly string FeedReaderDbIdKeyValueKey = "FeedReaderDbIdBasicAuthKey";
    private static readonly SqlitePragmaConnectionInterceptor SqlitePragmaInterceptorInstance = new();

    public DbSet<ReaderFeedItem> FeedItems { get; set; } = null!;
    public DbSet<ReaderFeed> Feeds { get; set; } = null!;
    public DbSet<HistoricReaderFeedItem> HistoricFeedItems { get; set; } = null!;
    public DbSet<HistoricReaderFeed> HistoricFeeds { get; set; } = null!;
    public DbSet<HistoricSavedFeedItem> HistoricSavedFeedItems { get; set; } = null!;
    public DbSet<ReaderKeyValue> KeyValues { get; set; } = null!;
    public DbSet<SavedFeedItem> SavedFeedItems { get; set; } = null!;

    public static Task<FeedContext> CreateInstance(string fileName)
    {
        // https://github.com/aspnet/EntityFrameworkCore/issues/9994#issuecomment-508588678
        Batteries_V2.Init();
        raw.sqlite3_config(2 /*SQLITE_CONFIG_MULTITHREAD*/);
        var optionsBuilder = new DbContextOptionsBuilder<FeedContext>();
        optionsBuilder.AddInterceptors(SqlitePragmaInterceptorInstance);

        return Task.FromResult(new FeedContext(optionsBuilder
            .UseSqlite($"Data Source={fileName}").Options));
    }

    public static async Task<FeedContext> CreateInstanceWithEnsureCreated(string fileName)
    {
        if (File.Exists(fileName))
        {
            await using var sc = new ServiceCollection().AddFluentMigratorCore().ConfigureRunner(rb =>
                    rb.AddSQLite()
                        .WithGlobalConnectionString(
                            $"Data Source={fileName}")
                        .ScanIn(typeof(FeedContext).Assembly).For.Migrations())
                .AddLogging(lb => lb.AddSerilog()).BuildServiceProvider(false);

            using var scope = sc.CreateScope();
            // Instantiate the runner
            var runner = sc.GetRequiredService<IMigrationRunner>();

            // Execute the migrations
            runner.MigrateUp();
        }

        var context = await CreateInstance(fileName);
        await context.Database.EnsureCreatedAsync();

        await FeedReaderGuidInitialValueAsNeeded(context);

        return context;
    }

    public static async Task<string> FeedReaderGuid(string fileName)
    {
        var db = await CreateInstanceWithEnsureCreated(fileName);

        return (await db.KeyValues.SingleAsync(x => x.Key == FeedReaderDbIdKeyValueKey)).Value;
    }

    public static async Task<string> FeedReaderGuidIdString(string fileName)
    {
        var db = await CreateInstanceWithEnsureCreated(fileName);

        return $"FeedReaderBasicAuth-{(await db.KeyValues.SingleAsync(x => x.Key == FeedReaderDbIdKeyValueKey)).Value}";
    }

    private static async Task FeedReaderGuidInitialValueAsNeeded(FeedContext db)
    {
        var feedReaderGuids = await db.KeyValues.Where(x => x.Key == FeedReaderDbIdKeyValueKey).ToListAsync();

        if (feedReaderGuids.Count == 0)
        {
            db.KeyValues.Add(new ReaderKeyValue { Key = FeedReaderDbIdKeyValueKey, Value = Guid.NewGuid().ToString() });
            await db.SaveChangesAsync();
        }

        if (feedReaderGuids.Count > 1)
        {
            var toDelete = feedReaderGuids.OrderByDescending(x => x.Id).Skip(1).ToList();
            db.KeyValues.RemoveRange(toDelete);
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    ///     Use TryCreateInstance to test whether an input file is a valid db.
    /// </summary>
    /// <param name="fileName"></param>
    /// <returns></returns>
    public static async Task<(bool success, string message, FeedContext? context)> TryCreateInstance(string fileName)
    {
        var newFileInfo = new FileInfo(fileName);

        if (!newFileInfo.Exists)
            return (false, "File does not exist?", null);

        try
        {
            var sc = new ServiceCollection().AddFluentMigratorCore().ConfigureRunner(rb =>
                    rb.AddSQLite()
                        .WithGlobalConnectionString(
                            $"Data Source={fileName}")
                        .ScanIn(typeof(FeedContext).Assembly).For.Migrations())
                .AddLogging(lb => lb.AddSerilog()).BuildServiceProvider(false);

            // Instantiate the runner
            var runner = sc.GetRequiredService<IMigrationRunner>();

            // Execute the migrations
            runner.MigrateUp();
        }
        catch (Exception e)
        {
            return (false, e.Message, null);
        }

        // https://github.com/aspnet/EntityFrameworkCore/issues/9994#issuecomment-508588678
        Batteries_V2.Init();
        raw.sqlite3_config(2 /*SQLITE_CONFIG_MULTITHREAD*/);
        var optionsBuilder = new DbContextOptionsBuilder<FeedContext>();
        optionsBuilder.AddInterceptors(SqlitePragmaInterceptorInstance);

        FeedContext? db;

        try
        {
            db = new FeedContext(optionsBuilder.UseSqlite($"Data Source={fileName}")
                .Options);

            await FeedReaderGuidInitialValueAsNeeded(db);
        }
        catch (Exception e)
        {
            return (false, e.Message, null);
        }

        return (true, string.Empty, db);
    }

    private sealed class SqlitePragmaConnectionInterceptor : DbConnectionInterceptor
    {
        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        {
            ExecutePragmas(connection);
        }

        public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            ExecutePragmas(connection);
            return Task.CompletedTask;
        }

        private static void ExecutePragmas(DbConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                                  PRAGMA journal_mode=WAL;
                                  PRAGMA busy_timeout=1000;
                                  """;
            command.ExecuteNonQuery();
        }
    }
}