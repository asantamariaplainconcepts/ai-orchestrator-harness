using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using AiOrchestrator.BuildingBlocks.Folders;

namespace AiOrchestrator.Modules.Pbis.FunctionalTests;

/// <summary>
/// <c>.aio/aio</c> run for real, with <c>node</c>, against this checkout's own file and a
/// test host, reached over HTTP through a relay on a loopback port because the test server has no
/// socket of its own. What the script prints is what an agent reads.
/// </summary>
static class AioScript
{
    /// <summary>
    /// The script with <paramref name="host"/> behind <c>AIO_API</c>, or with no <c>AIO_API</c> at all
    /// when it is null. Its working directory is not a repository, so its <c>.aio/</c> is the
    /// directory's own.
    /// </summary>
    public static async Task<(int Code, string Said)> Run(HttpClient? host, string workingDirectory, string repository, string[] arguments)
    {
        await using var server = host is null ? null : Relay.Start(host);
        var start = new ProcessStartInfo("node")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.Environment.Remove("AIO_API");
        start.Environment["AIO_REPOSITORY"] = repository;
        if (server is not null)
        {
            start.Environment["AIO_API"] = server.Address;
        }

        start.ArgumentList.Add(Path.Combine(SelfCheckout.Root()!, ".aio", "aio"));
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var said = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        (await errors).ShouldBeEmpty();
        return (process.ExitCode, (await said).TrimEnd('\n'));
    }

    /// <summary>A loopback listener that hands every request to the test host and its answer back.</summary>
    sealed class Relay : IAsyncDisposable
    {
        readonly HttpListener _listener = new();
        readonly HttpClient _host;
        readonly Task _serving;

        Relay(HttpClient host, int port)
        {
            _host = host;
            Address = $"http://127.0.0.1:{port}";
            _listener.Prefixes.Add($"{Address}/");
            _listener.Start();
            _serving = Serve();
        }

        public string Address { get; }

        public static Relay Start(HttpClient host)
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return new Relay(host, port);
        }

        async Task Serve()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (Exception stopped) when (stopped is HttpListenerException or ObjectDisposedException)
                {
                    return;
                }

                using var request = new HttpRequestMessage(new HttpMethod(context.Request.HttpMethod), context.Request.Url!.PathAndQuery);
                if (context.Request.HasEntityBody)
                {
                    using var reader = new StreamReader(context.Request.InputStream);
                    request.Content = new StringContent(await reader.ReadToEndAsync(), System.Text.Encoding.UTF8, "application/json");
                }

                using var response = await _host.SendAsync(request);
                var bytes = await response.Content.ReadAsByteArrayAsync();
                context.Response.StatusCode = (int)response.StatusCode;
                context.Response.ContentType = response.Content.Headers.ContentType?.ToString();
                await context.Response.OutputStream.WriteAsync(bytes);
                context.Response.Close();
            }
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            _listener.Close();
            await _serving;
            _host.Dispose();
        }
    }
}
