using System.Net.WebSockets;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Runs.Features.PbiRuns.UseCases;

/// <summary>
/// A live run's terminal over a WebSocket, for xterm.js in the tray (ARCHITECTURE.md): the
/// browser reads what the session prints, types into it and resizes it. Closing the socket detaches
/// the viewer and leaves the run running; the run ends when its agent ends or a person stops it.
/// <para>
/// The shape is the first tree's <c>TerminalHub</c> (attach with a replay of the tail, keystrokes
/// straight through, resize), over a plain WebSocket instead of SignalR: a terminal is a byte stream
/// both ways, and the attach is a query through the pipeline, so it is logged and authorized like
/// any other read before the socket is accepted.
/// </para>
/// <para>
/// The wire: every frame from the server is binary, the session's bytes as printed, the replay
/// first. From the browser, a binary frame is keystrokes, written to the PTY exactly as sent, and a
/// text frame is a resize, <c>{"columns": 120, "rows": 30}</c>. The server closes the socket when
/// the session ends, with the reason as its description.
/// </para>
/// </summary>
sealed class AttachSession : IUseCase
{
    /// <summary>How long a closing socket waits for the browser's half of the close.</summary>
    static readonly TimeSpan CloseWait = TimeSpan.FromSeconds(2);

    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet("/api/sessions/{runId:guid}/attach", Attach)
            .WithName(nameof(AttachSession))
            .WithTags("Runs")
            // A WebSocket has no response body an OpenAPI document can describe; the wire is above.
            .ExcludeFromDescription();

    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Query(Guid RunId) : IQuery<ErrorOr<LiveRun>>;

    internal sealed class Handler(LiveRuns live) : IAppQueryHandler<Query, ErrorOr<LiveRun>>
    {
        public Task<ErrorOr<LiveRun>> Handle(Query query, CancellationToken cancellationToken) =>
            Task.FromResult<ErrorOr<LiveRun>>(
                live.Find(query.RunId) is { Session: not null } run
                    ? run
                    : Error.NotFound("Session.NotLive", $"Run {query.RunId} has no live session: it ended, or its terminal has not started yet.")
            );
    }

    static async Task Attach(Guid runId, HttpContext context, ISender sender, CancellationToken cancellationToken)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            await Results
                .Problem(statusCode: StatusCodes.Status400BadRequest, detail: "A session is attached over a WebSocket.")
                .ExecuteAsync(context);
            return;
        }

        var found = await sender.Send(new Query(runId), cancellationToken);
        if (found.IsError)
        {
            await ApiResults.Problem(found.Errors).ExecuteAsync(context);
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        await View(socket, found.Value, cancellationToken);
    }

    /// <summary>Pumps both ways until the session ends or the browser goes, then closes the socket.</summary>
    static async Task View(WebSocket socket, LiveRun run, CancellationToken aborted)
    {
        using var viewer = run.Output.Attach();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(aborted);

        var down = Down(socket, viewer, stop.Token);
        var up = Up(socket, run, stop.Token);

        if (await Task.WhenAny(down, up) == down && await Quietly(down) is { } ended && socket.State == WebSocketState.Open)
        {
            // The session ended: say so, and give the browser a moment to answer the close.
            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, ended, CancellationToken.None);
            await Task.WhenAny(up, Task.Delay(CloseWait, CancellationToken.None));
        }

        await stop.CancelAsync();
        await Quietly(down);
        await Quietly(up);

        if (socket.State == WebSocketState.CloseReceived)
        {
            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Detached.", CancellationToken.None);
        }
    }

    /// <summary>The replay, then every chunk. Answers why the stream ended, or null when it was stopped.</summary>
    static async Task<string?> Down(WebSocket socket, SessionViewer viewer, CancellationToken ct)
    {
        if (viewer.Replay.Length > 0)
        {
            await socket.SendAsync(viewer.Replay, WebSocketMessageType.Binary, endOfMessage: true, ct);
        }

        try
        {
            await foreach (var chunk in viewer.Output.ReadAllAsync(ct))
            {
                await socket.SendAsync(chunk, WebSocketMessageType.Binary, endOfMessage: true, ct);
            }
        }
        catch (Exception behind) when ((behind as SessionViewerBehindException ?? behind.InnerException) is SessionViewerBehindException cut)
        {
            return cut.Message;
        }

        return "The session ended.";
    }

    /// <summary>Keystrokes and resizes from the browser, until it closes the socket.</summary>
    static async Task Up(WebSocket socket, LiveRun run, CancellationToken ct)
    {
        var session = run.Session!;
        var buffer = new byte[16 * 1024];
        using var message = new MemoryStream();

        while (true)
        {
            var received = await socket.ReceiveAsync(buffer, ct);
            if (received.MessageType == WebSocketMessageType.Close)
            {
                return;
            }

            message.Write(buffer, 0, received.Count);
            if (!received.EndOfMessage)
            {
                continue;
            }

            var bytes = message.ToArray();
            message.SetLength(0);

            if (received.MessageType == WebSocketMessageType.Binary)
            {
                await session.WriteAsync(bytes, ct);
            }
            else if (Size(bytes) is var (columns, rows))
            {
                await session.ResizeAsync(columns, rows, ct);
            }
        }
    }

    /// <summary>A resize message's size, or null for a text frame that is not one.</summary>
    static (int Columns, int Rows)? Size(byte[] text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("columns", out var columns)
                && root.TryGetProperty("rows", out var rows)
                && columns.TryGetInt32(out var width)
                && rows.TryGetInt32(out var height)
                ? (width, height)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// A pump's end, whichever way it came: a socket the browser dropped, a session killed under a
    /// write, or the stop. None of them is the run's business, so none of them escapes.
    /// </summary>
    static async Task<T?> Quietly<T>(Task<T> pump)
        where T : class?
    {
        try
        {
            return await pump;
        }
        catch (Exception gone) when (gone is OperationCanceledException or WebSocketException or IOException or InvalidOperationException or ObjectDisposedException)
        {
            return null;
        }
    }

    static async Task Quietly(Task pump)
    {
        try
        {
            await pump;
        }
        catch (Exception gone) when (gone is OperationCanceledException or WebSocketException or IOException or InvalidOperationException or ObjectDisposedException)
        {
        }
    }
}
