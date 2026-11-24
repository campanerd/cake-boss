using CakeBoss.Backend.Features.Admin;
using CakeBoss.Backend.Features.Availability;
using CakeBoss.Backend.Features.Management;
using CakeBoss.Backend.Features.Ordering;
using CakeBoss.Backend.Infrastructure.Data;
using CakeBoss.Backend.Infrastructure.Health;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CakeBoss.Backend;

public static class DependencyInjection
{
    /// <summary>
    /// Registra tudo o que o backend precisa. O frontend só chama este método e nunca usa o DbContext direto.
    /// </summary>
    /// <param name="connectionString">Connection string do Postgres (no Supabase, use a do pooler).</param>
    public static IServiceCollection AddBackend(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton(TimeProvider.System);

        // No Render free o disco do container é apagado a cada reinício: sem isto, as chaves de proteção de dados
        // (tokens antiforgery dos formulários e cookies de login) se perdem e quem está com a página aberta toma erro.
        // O nome da aplicação é fixo para qualquer instância ler as mesmas chaves.
        services.AddDataProtection()
            .SetApplicationName("CakeBoss")
            .PersistKeysToDbContext<AppDbContext>();

        // Login da dona. O frontend liga a seção "Admin" da configuração a AdminOptions (como faz com SiteOptions);
        // sem ela, ninguém entra. O atraso por tentativa fica na memória, então precisa ser um só (singleton).
        services.AddOptions<AdminOptions>();
        services.AddSingleton<LoginThrottle>();
        services.AddSingleton<AdminLoginService>();

        services.AddScoped<DatabaseHealth>();
        services.AddScoped<AvailabilityService>();
        services.AddScoped<OrderService>();
        services.AddScoped<OrderManagementService>();

        return services;
    }
}
