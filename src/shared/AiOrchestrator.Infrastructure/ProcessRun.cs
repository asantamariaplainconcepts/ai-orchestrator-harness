using System.ComponentModel;
using System.Text;
using CliWrap;

namespace AiOrchestrator.Infrastructure;

/// <summary>
/// One CLI asked one question: run to exit inside a deadline, both streams kept, the whole tree
/// killed when the deadline or the caller gives up. Every spawn here that is not a terminal goes
/// through this; the ones a person watches go through Porta.Pty.
/// <para>
/// <b>Returns rather than throws</b> for everything but the caller's own cancellation. A CLI that
/// is not installed, did not answer, or exited non-zero is an answer each caller renders in its own
/// words, and an exception would flatten the three into one.
/// </para>
/// </summary>
static class ProcessRun
{
    public static async Task<ProcessOutcome> Capture(
        Command command,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        int maxCharacters = int.MaxValue
    )
    {
        var output = new BoundedText(maxCharacters);
        var error = new BoundedText(maxCharacters);

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);

        try
        {
            var result = await command
                .WithValidation(CommandResultValidation.None)
                .WithStandardOutputPipe(output)
                .WithStandardErrorPipe(error)
                .ExecuteAsync(deadline.Token);

            return new ProcessOutcome(result.ExitCode, output.Text, error.Text)
            {
                Exceeded = output.Exceeded || error.Exceeded,
            };
        }
        catch (Win32Exception failure)
        {
            // Not installed, not on PATH, or a working directory that is gone. CliWrap wraps the
            // OS's sentence in its own; the inner one is the one that names the cause.
            return new ProcessOutcome(-1, string.Empty, string.Empty)
            {
                NotStarted = failure.InnerException?.Message ?? failure.Message,
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ProcessOutcome(-1, string.Empty, string.Empty) { TimedOut = true };
        }
    }

    /// <summary>
    /// Keeps the first <c>limit</c> characters and drains the rest, so a stream that says too much
    /// is reported as such rather than held in this process's memory or left to block its writer.
    /// </summary>
    sealed class BoundedText(int limit) : PipeTarget
    {
        readonly StringBuilder _text = new();

        public bool Exceeded { get; private set; }

        public string Text => _text.ToString();

        public override async Task CopyFromAsync(
            Stream origin,
            CancellationToken cancellationToken = default
        )
        {
            using var reader = new StreamReader(origin, Encoding.UTF8, false, 4096, leaveOpen: true);
            var buffer = new char[4096];
            int count;

            while ((count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken)) != 0)
            {
                var room = Math.Max(0, limit - _text.Length);
                _text.Append(buffer, 0, Math.Min(room, count));
                Exceeded |= count > room;
            }
        }
    }
}

/// <param name="ExitCode">-1 when it did not start or did not answer in time.</param>
sealed record ProcessOutcome(int ExitCode, string StandardOutput, string StandardError)
{
    /// <summary>The OS's reason it could not be started; null when it was.</summary>
    public string? NotStarted { get; init; }

    public bool TimedOut { get; init; }

    /// <summary>A stream said more than the caller's bound; what it kept is the first part.</summary>
    public bool Exceeded { get; init; }

    public bool Missing => NotStarted is not null;

    public bool Succeeded => !Missing && !TimedOut && ExitCode == 0;
}
