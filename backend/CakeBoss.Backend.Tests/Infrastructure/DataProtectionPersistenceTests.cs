using CakeBoss.Backend.Tests.Support;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CakeBoss.Backend.Tests.Infrastructure;

public class DataProtectionPersistenceTests
{
    private const string Purpose = "CakeBoss.Tests";

    private static ServiceProvider BuildProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddBackend(connectionString);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddBackend_RegistersDataProtectionWithAFixedApplicationName()
    {
        // Não abre conexão: as chaves só são lidas quando alguém protege ou desprotege algo.
        using var provider = BuildProvider("Host=127.0.0.1;Port=1;Database=x;Timeout=1");

        var options = provider.GetRequiredService<IOptions<DataProtectionOptions>>().Value;

        Assert.Equal("CakeBoss", options.ApplicationDiscriminator);
        Assert.NotNull(provider.GetRequiredService<IDataProtectionProvider>());
    }

    [PostgresFact]
    public async Task Keys_AreSavedInTheDatabase()
    {
        await using var database = await TestDatabase.CreateAsync();
        var connectionString = database.Db.Database.GetConnectionString()!;

        using (var provider = BuildProvider(connectionString))
        {
            provider.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose).Protect("segredo");
        }

        var key = await database.Db.DataProtectionKeys.AsNoTracking().FirstAsync();
        Assert.False(string.IsNullOrWhiteSpace(key.Xml));
    }

    [PostgresFact]
    public async Task ProtectedData_SurvivesARestart()
    {
        await using var database = await TestDatabase.CreateAsync();
        var connectionString = database.Db.Database.GetConnectionString()!;

        string protectedValue;
        using (var firstRun = BuildProvider(connectionString))
        {
            protectedValue = firstRun.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose).Protect("segredo");
        }

        // "Reinício": um provedor novo, sem nada em memória, lendo só o que está no banco.
        using var secondRun = BuildProvider(connectionString);
        var unprotected = secondRun.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose).Unprotect(protectedValue);

        Assert.Equal("segredo", unprotected);
    }

    [PostgresFact]
    public async Task TwoRunningInstances_ShareTheSameKeys()
    {
        await using var database = await TestDatabase.CreateAsync();
        var connectionString = database.Db.Database.GetConnectionString()!;
        using var instanceA = BuildProvider(connectionString);
        using var instanceB = BuildProvider(connectionString);

        var protectedByA = instanceA.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose).Protect("segredo");
        var unprotectedByB = instanceB.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose).Unprotect(protectedByA);

        Assert.Equal("segredo", unprotectedByB);
    }
}
