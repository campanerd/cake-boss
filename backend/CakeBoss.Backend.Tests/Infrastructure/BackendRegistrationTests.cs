using CakeBoss.Backend.Features.Availability;
using CakeBoss.Backend.Features.Ordering;
using CakeBoss.Backend.Infrastructure.Data;
using CakeBoss.Backend.Infrastructure.Health;
using CakeBoss.Backend.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CakeBoss.Backend.Tests.Infrastructure;

public class BackendRegistrationTests
{
    // Não precisa de banco para montar os serviços: a conexão só abre quando alguém consulta.
    private const string UnreachableConnection = "Host=127.0.0.1;Port=1;Database=x;Timeout=1;Command Timeout=1";

    [Fact]
    public void AddBackend_RegistersTheServices()
    {
        var services = new ServiceCollection();
        services.AddBackend(UnreachableConnection);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<AvailabilityService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<OrderService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<DatabaseHealth>());
        Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddBackend_WithoutConnectionString_Throws(string? connectionString)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ServiceCollection().AddBackend(connectionString!));
    }

    [Fact]
    public async Task DatabaseHealth_WithUnreachableDatabase_Throws()
    {
        var services = new ServiceCollection();
        services.AddBackend(UnreachableConnection);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var health = scope.ServiceProvider.GetRequiredService<DatabaseHealth>();

        await Assert.ThrowsAnyAsync<Exception>(() => health.CheckAsync());
    }

    [PostgresFact]
    public async Task DatabaseHealth_WithRunningDatabase_Succeeds()
    {
        await using var database = await TestDatabase.CreateAsync();

        await new DatabaseHealth(database.Db).CheckAsync();
    }
}
