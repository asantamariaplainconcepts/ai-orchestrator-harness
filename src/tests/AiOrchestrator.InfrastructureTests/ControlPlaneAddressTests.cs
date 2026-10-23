using AiOrchestrator.BuildingBlocks.Api;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;

namespace AiOrchestrator.InfrastructureTests;

/// <summary>#634. The address <c>aio issue</c> gets as <c>AIO_API</c>, from what the server says it listens on.</summary>
public sealed class ControlPlaneAddressTests
{
    [Theory]
    [InlineData(new[] { "http://localhost:5080" }, "http://localhost:5080/")]
    [InlineData(new[] { "https://localhost:7080", "http://127.0.0.1:5080" }, "http://127.0.0.1:5080/")]
    [InlineData(new[] { "http://0.0.0.0:5080" }, "http://127.0.0.1:5080/")]
    [InlineData(new[] { "http://[::]:5080" }, "http://127.0.0.1:5080/")]
    [InlineData(new[] { "http://+:5080" }, "http://127.0.0.1:5080/")]
    public void The_first_http_address_with_a_wildcard_read_as_loopback(string[] addresses, string expected) =>
        ControlPlaneAddress.Of(new Listening(addresses))!.ToString().ShouldBe(expected);

    [Fact]
    public void A_server_that_lists_no_address_gives_none()
    {
        ControlPlaneAddress.Of(new Listening([])).ShouldBeNull();
        ControlPlaneAddress.Of(null).ShouldBeNull();
    }

    sealed class Listening : IServer
    {
        public Listening(IEnumerable<string> addresses)
        {
            var feature = new ServerAddressesFeature();
            foreach (var address in addresses)
            {
                feature.Addresses.Add(address);
            }

            Features.Set<IServerAddressesFeature>(feature);
        }

        public IFeatureCollection Features { get; } = new FeatureCollection();

        public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
            where TContext : notnull => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void Dispose() { }
    }
}
