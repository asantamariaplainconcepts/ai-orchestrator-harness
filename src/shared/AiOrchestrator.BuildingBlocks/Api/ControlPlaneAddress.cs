using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace AiOrchestrator.BuildingBlocks.Api;

/// <summary>
/// Where an agent on this machine reaches this control plane: the address <c>aio issue</c> gets as
/// <c>AIO_API</c> (#634). The server's own first <c>http</c> address, with a wildcard host such as
/// <c>0.0.0.0</c>, <c>[::]</c>, <c>+</c> or <c>*</c> read as <c>127.0.0.1</c>, because a client
/// cannot connect to a wildcard. Null when the server lists no address, as the test server does.
/// </summary>
public static class ControlPlaneAddress
{
    public static Uri? Of(IServer? server)
    {
        var addresses = server?.Features.Get<IServerAddressesFeature>()?.Addresses ?? [];
        var chosen =
            addresses.FirstOrDefault(address => address.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            ?? addresses.FirstOrDefault();
        if (chosen is null)
        {
            return null;
        }

        // Uri refuses `+` and `*` as hosts, so the wildcard is replaced before it is parsed.
        var schemeEnd = chosen.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd < 0)
        {
            return null;
        }

        var rest = chosen[(schemeEnd + 3)..];
        var portAt = rest.LastIndexOf(':');
        var host = portAt < 0 ? rest.TrimEnd('/') : rest[..portAt];
        if (host is "0.0.0.0" or "[::]" or "+" or "*")
        {
            chosen = $"{chosen[..(schemeEnd + 3)]}127.0.0.1{(portAt < 0 ? "" : rest[portAt..])}";
        }

        return Uri.TryCreate(chosen.TrimEnd('/'), UriKind.Absolute, out var address) ? address : null;
    }
}
