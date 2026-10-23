using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.SharedFunctionalTests;

/// <summary>
/// Records the SQL a request actually issued.
/// <para>
/// <b>Why the response is not enough.</b> The most expensive bug found in this codebase was an
/// owned collection being eagerly loaded: every slice that materialised a <c>Run</c> fetched every
/// transcript line of every run it touched. The <i>response</i> was correct throughout — the read
/// model never carried a log — so a test that asserts on the payload passes against the bug. That
/// was confirmed by writing those assertions, reverting the fix, and watching them stay green.
/// </para>
/// <para>
/// The defect lives in the query, so the query is what a test has to see. Asserting on a timing
/// would be the other option and a worse one: a threshold meaningful on a loaded CI machine is a
/// flake, and one that is not is not a test.
/// </para>
/// <para>
/// Reads EF's command log rather than registering a <c>DbCommandInterceptor</c>. The interceptor
/// route looks tidier and silently did nothing here — an <c>IInterceptor</c> registered in the
/// application container was never invoked, and the first version of these tests passed while
/// capturing zero statements. A vacuous test is worse than no test, so this uses the channel that
/// is observably on: every executed command is logged under
/// <c>Microsoft.EntityFrameworkCore.Database.Command</c>.
/// </para>
/// </summary>
public sealed class SqlRecorder : ILoggerProvider
{
    /// <summary>The category EF logs executed commands under.</summary>
    public const string Category = "Microsoft.EntityFrameworkCore.Database.Command";

    /// <summary>
    /// The category EF logs transaction lifecycle under, recorded since ADR-0003. SQLite writes a
    /// 40-row batch as 40 INSERT commands where Postgres wrote one, so "one flush, one call" is no
    /// longer visible in the statement count — but a multi-statement SaveChanges still opens
    /// exactly one transaction, and forty single-statement saves open none.
    /// </summary>
    public const string TransactionCategory = "Microsoft.EntityFrameworkCore.Database.Transaction";

    readonly ConcurrentQueue<string> _statements = new();
    readonly ConcurrentQueue<string> _transactions = new();

    public IReadOnlyList<string> Statements => [.. _statements];

    /// <summary>Transaction lifecycle messages; count "Began transaction" for begins.</summary>
    public IReadOnlyList<string> Transactions => [.. _transactions];

    public void Clear()
    {
        _statements.Clear();
        _transactions.Clear();
    }

    /// <summary>Statements naming a table — the question is almost always "did it touch X".</summary>
    public IReadOnlyList<string> Touching(string table) =>
        [.. _statements.Where(sql => sql.Contains(table, StringComparison.OrdinalIgnoreCase))];

    public ILogger CreateLogger(string categoryName) =>
        categoryName switch
        {
            Category => new Sink(_statements, LogLevel.Information),
            // Transaction begin/commit log at Debug; the fixture raises the category's level.
            TransactionCategory => new Sink(_transactions, LogLevel.Debug),
            _ => Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
        };

    public void Dispose() { }

    sealed class Sink(ConcurrentQueue<string> statements, LogLevel threshold) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= threshold;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            if (IsEnabled(logLevel))
            {
                statements.Enqueue(formatter(state, exception));
            }
        }
    }
}
