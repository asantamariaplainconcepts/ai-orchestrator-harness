using Xunit;
using Npgsql;
using Testcontainers.PostgreSql;

public sealed class PostgresTests : IAsyncLifetime
{
    readonly PostgreSqlContainer _pg = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();

    public Task InitializeAsync() => _pg.StartAsync();
    public Task DisposeAsync() => _pg.DisposeAsync().AsTask();

    [Fact]
    public async Task A_real_postgres_answers()
    {
        await using var conn = new NpgsqlConnection(_pg.GetConnectionString());
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("select version()", conn);
        var v = (string)(await cmd.ExecuteScalarAsync())!;
        Console.WriteLine($"PG: {v} via {_pg.Hostname}:{_pg.GetMappedPublicPort(5432)}");
        Assert.StartsWith("PostgreSQL 16", v);
    }
}
