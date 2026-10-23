using System.Collections.Concurrent;
using System.Diagnostics;
using AiOrchestrator.BuildingBlocks.Folders;
using Microsoft.Playwright;

namespace AiOrchestrator.EndToEndTests;

/// <summary>
/// The real SPA, booted out of this checkout by one Vite with the API stubbed at the route level, and
/// no Server started. The classes that use it share the "review-frontend" collection, because two
/// Vites at once kill each other. The port is its own (#432): the checkout's vite.config asks for
/// 5173 with <c>strictPort</c>, and the AppHost's own Vite asks for the same number when
/// <see cref="AppFixture"/>'s Aspire starts beside this collection.
/// </summary>
public sealed class ReviewFrontendFixture : IAsyncLifetime
{
    readonly ConcurrentQueue<string> _logs = new();
    Process? _frontend;
    IPlaywright? _playwright;
    IBrowser? _browser;
    string _url = "";
    string _media = "";

    public async Task InitializeAsync()
    {
        var root = SelfCheckout.Root() ?? throw new DirectoryNotFoundException("No checkout for the frontend test.");
        _media = Path.Combine(root, "src", "artifacts", "pull-request-drafts", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_media);
        var start = new ProcessStartInfo("node")
        {
            WorkingDirectory = Path.Combine(root, "src", "frontend"),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.Environment["API_PROXY_TARGET"] = "http://127.0.0.1:1";
        // A port of its own, asked from the OS and passed as a number: the checkout's
        // vite.config asks for 5173 with `strictPort`, and an inline `port: 0` loses that merge —
        // zero reads unset, so the file's 5173 wins (measured: the fixture listened on 5173 with
        // `port: 0` inline and died beside the AppHost's own Vite, "Port 5173 is already in use").
        // A concrete number is honoured (#432), and `strictPort: false` is not: the port was
        // reserved by the OS a line above, so another taker is a race already lost either way.
        using var reserved = System.Net.Sockets.TcpListener.Create(0);
        reserved.Start();
        var port = ((System.Net.IPEndPoint)reserved.LocalEndpoint).Port;
        reserved.Stop();
        start.Environment["AIO_FRONTEND_FIXTURE_PORT"] = port.ToString();
        start.ArgumentList.Add("--input-type=module");
        start.ArgumentList.Add("--eval");
        start.ArgumentList.Add("""
            import { createServer } from 'vite';
            const server = await createServer({ server: { host: '127.0.0.1', port: Number(process.env.AIO_FRONTEND_FIXTURE_PORT) } });
            await server.listen();
            console.log('AIO_FRONTEND_TEST_URL=http://127.0.0.1:' + server.httpServer.address().port);
            """);
        _frontend = new Process { StartInfo = start, EnableRaisingEvents = true };
        var ready = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _frontend.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not { } line) return;
            _logs.Enqueue(line);
            const string prefix = "AIO_FRONTEND_TEST_URL=";
            if (line.StartsWith(prefix, StringComparison.Ordinal)) ready.TrySetResult(line[prefix.Length..]);
        };
        _frontend.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is { } line) _logs.Enqueue(line);
        };
        _frontend.Exited += (_, _) => ready.TrySetException(
            new InvalidOperationException($"Frontend exited: {string.Join(Environment.NewLine, _logs)}")
        );
        _frontend.Start();
        _frontend.BeginOutputReadLine();
        _frontend.BeginErrorReadLine();
        _url = await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Microsoft.Playwright.Program.Main(["install", "chromium"])
            .ShouldBe(0, "Playwright could not install its browser");
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync();
    }

    /// <param name="phone">A touch phone: its primary pointer is coarse, as <c>(pointer: coarse)</c> reads it.</param>
    public Task<IBrowserContext> Open(string theme, bool phone = false) => _browser!.NewContextAsync(new()
    {
        BaseURL = _url,
        ViewportSize = AppFixture.Phone,
        ColorScheme = theme == "dark" ? ColorScheme.Dark : ColorScheme.Light,
        RecordVideoDir = Path.Combine(_media, "videos"),
        HasTouch = phone,
        IsMobile = phone,
    });

    public string CapturePath(string name) => Path.Combine(_media, $"{name}.png");

    public async Task DisposeAsync()
    {
        if (_browser is not null) await _browser.CloseAsync();
        _playwright?.Dispose();
        if (_frontend is not null)
        {
            if (!_frontend.HasExited)
            {
                _frontend.Kill(entireProcessTree: true);
                await _frontend.WaitForExitAsync();
            }
            _frontend.Dispose();
        }
    }
}
