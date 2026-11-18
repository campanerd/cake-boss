using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CakeBoss.Backend.Infrastructure.Data;

/// <summary>
/// Usada somente pelas ferramentas do EF (dotnet ef migrations ...) para criar o contexto sem subir o app.
/// Gerar migrations não precisa de banco; para aplicar, defina CAKEBOSS_CONNECTION com a connection string real.
/// </summary>
internal class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CAKEBOSS_CONNECTION")
                               ?? "Host=localhost;Database=cakeboss_design";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
