using CakeBoss.Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CakeBoss.Backend.Tests.Support;

/// <summary>
/// Pula o teste quando não há um Postgres de teste configurado. Para rodar, defina CAKEBOSS_TEST_CONNECTION
/// sem o nome do banco, por exemplo: "Host=localhost;Port=5433;Username=postgres;Password=dev".
/// </summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public const string EnvironmentVariable = "CAKEBOSS_TEST_CONNECTION";

    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariable)))
            Skip = $"Defina {EnvironmentVariable} para rodar testes contra um Postgres real.";
    }
}

/// <summary>Versão de [Theory] que também é pulada quando não há Postgres de teste.</summary>
public sealed class PostgresTheoryAttribute : TheoryAttribute
{
    public PostgresTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PostgresFactAttribute.EnvironmentVariable)))
            Skip = $"Defina {PostgresFactAttribute.EnvironmentVariable} para rodar testes contra um Postgres real.";
    }
}

/// <summary>Cria um banco novo e isolado para cada teste (com os dados iniciais) e o apaga no final.</summary>
internal sealed class TestDatabase : IAsyncDisposable
{
    private TestDatabase(AppDbContext db) => Db = db;

    public AppDbContext Db { get; }

    public static async Task<TestDatabase> CreateAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable(PostgresFactAttribute.EnvironmentVariable))
        {
            Database = $"cakeboss_test_{Guid.NewGuid():N}"
        };

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(builder.ConnectionString).Options;
        var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return new TestDatabase(db);
    }

    public async ValueTask DisposeAsync()
    {
        await Db.Database.EnsureDeletedAsync();
        await Db.DisposeAsync();
    }
}
