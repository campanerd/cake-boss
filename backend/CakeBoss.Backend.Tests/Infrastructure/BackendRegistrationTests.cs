using CakeBoss.Backend.Features.Admin;
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

    [Fact]
    public void AddBackend_RegistersTheAdminLogin_AsSingletons_AndNobodyGetsInWithoutConfiguration()
    {
        var services = new ServiceCollection();
        services.AddBackend(UnreachableConnection);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var login = provider.GetRequiredService<AdminLoginService>();

        // Um só controle de tentativas para o app inteiro: se fosse um por requisição, o atraso nunca valeria.
        Assert.Same(login, provider.GetRequiredService<AdminLoginService>());
        Assert.Same(provider.GetRequiredService<LoginThrottle>(), provider.GetRequiredService<LoginThrottle>());
        Assert.False(login.SignIn("1.2.3.4", "dona@confeitaria.com", "qualquer senha longa").Succeeded);
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
