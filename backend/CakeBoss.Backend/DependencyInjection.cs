using CakeBoss.Backend.Features.Availability;
using CakeBoss.Backend.Features.Ordering;
using CakeBoss.Backend.Infrastructure.Data;
using CakeBoss.Backend.Infrastructure.Health;
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

        services.AddScoped<DatabaseHealth>();
        services.AddScoped<AvailabilityService>();
        services.AddScoped<OrderService>();

        return services;
    }
}
