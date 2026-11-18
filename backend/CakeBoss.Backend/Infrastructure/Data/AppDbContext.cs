using CakeBoss.Backend.Features.Customers;
using Microsoft.EntityFrameworkCore;

namespace CakeBoss.Backend.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Cada feature mantém o próprio mapeamento (…Configuration.cs ao lado das entidades).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
