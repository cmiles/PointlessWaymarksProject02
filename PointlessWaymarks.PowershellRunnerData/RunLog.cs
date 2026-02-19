using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using PointlessWaymarks.CommonTools;
using Serilog;

namespace PointlessWaymarks.PowerShellRunnerData;

/// <summary>
///     A thread-safe, batched log writer that periodically persists log entries
///     to the ScriptJobRun.Output field. Uses an unbounded Channel for ordered,
///     lock-free ingestion from any thread and a single background consumer that
///     batches writes to the database. Only unwritten entries are held in memory;
///     after each DB write the batch is released.
/// </summary>
internal sealed class RunLog : IAsyncDisposable
{
    private readonly int _batchSize;
    private readonly Channel<string> _channel;
    private readonly Task _consumerTask;
    private readonly string _databaseFile;
    private readonly TimeSpan _flushInterval;
    private readonly string _obfuscationKey;
    private readonly Guid _runPersistentId;
    private bool _hasErrors;

    internal RunLog(string databaseFile, string obfuscationKey, Guid runPersistentId,
        TimeSpan? flushInterval = null, int batchSize = 100)
    {
        _databaseFile = databaseFile;
        _obfuscationKey = obfuscationKey;
        _runPersistentId = runPersistentId;
        _batchSize = batchSize;
        _flushInterval = flushInterval ?? TimeSpan.FromSeconds(30);
        _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
        _consumerTask = Task.Run(ConsumeAsync);
    }

    internal bool HasErrors => Volatile.Read(ref _hasErrors);

    public async ValueTask DisposeAsync() => await FlushAsync();

    /// <summary>
    ///     Adds a pre-formatted log entry. Safe to call from any thread.
    /// </summary>
    internal void Add(string message) => _channel.Writer.TryWrite(message);

    /// <summary>
    ///     Marks the run as having errors. Safe to call from any thread.
    /// </summary>
    internal void SetErrored() => Volatile.Write(ref _hasErrors, true);

    /// <summary>
    ///     Signals that no more entries will be added, waits for the background
    ///     consumer to drain all remaining entries, and performs a final DB write.
    /// </summary>
    internal async Task FlushAsync()
    {
        _channel.Writer.TryComplete();
        await _consumerTask;
    }

    private async Task ConsumeAsync()
    {
        var pending = new List<string>();
        var lastPersist = DateTime.UtcNow;

        while (await WaitForDataOrTimeout())
        {
            while (_channel.Reader.TryRead(out var msg))
                pending.Add(msg);

            if (pending.Count == 0) continue;

            if (pending.Count >= _batchSize || DateTime.UtcNow - lastPersist >= _flushInterval)
            {
                try
                {
                    await PersistBatchAsync(pending);
                    pending.Clear();
                    lastPersist = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    // Keep pending entries for next attempt
                    Log.ForContext("RunPersistentId", _runPersistentId)
                        .Error(ex, "RunLog failed to persist batch, will retry next cycle");
                }
            }
        }

        // Channel completed — drain anything remaining
        while (_channel.Reader.TryRead(out var msg))
            pending.Add(msg);

        if (pending.Count > 0)
            await PersistBatchAsync(pending);
    }

    /// <summary>
    ///     Returns true when there may be data to read (or on timeout so the
    ///     consumer can check whether a time-based flush is needed). Returns
    ///     false only when the channel has been completed.
    /// </summary>
    private async Task<bool> WaitForDataOrTimeout()
    {
        using var cts = new CancellationTokenSource(_flushInterval);
        try
        {
            return await _channel.Reader.WaitToReadAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Timer expired — return true so the loop can flush if needed
            return true;
        }
    }

    /// <summary>
    ///     Reads the current output from the DB, appends the new entries,
    ///     encrypts, and saves. After this returns the entries can be released
    ///     from memory — they are persisted in the DB.
    /// </summary>
    private async Task PersistBatchAsync(List<string> entries)
    {
        var db = await PowerShellRunnerDbContext.CreateInstance(_databaseFile);
        var run = await db.ScriptJobRuns.FirstAsync(x => x.PersistentId == _runPersistentId);

        var existingOutput = string.IsNullOrWhiteSpace(run.Output)
            ? string.Empty
            : run.Output.Decrypt(_obfuscationKey);

        var newBlock = string.Join(Environment.NewLine, entries);
        var combined = string.IsNullOrEmpty(existingOutput)
            ? newBlock
            : string.Concat(existingOutput, Environment.NewLine, newBlock);

        run.Output = combined.Encrypt(_obfuscationKey);
        run.Errors = HasErrors;
        await db.SaveChangesAsync();
    }
}
