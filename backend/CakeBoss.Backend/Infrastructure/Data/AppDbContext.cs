using CakeBoss.Backend.Features.Customers;
using CakeBoss.Backend.Features.Orders;
using CakeBoss.Backend.Features.Schedule;
using Microsoft.EntityFrameworkCore;

namespace CakeBoss.Backend.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OpeningHour> OpeningHours => Set<OpeningHour>();
    public DbSet<ScheduleBlock> ScheduleBlocks => Set<ScheduleBlock>();
    public DbSet<ScheduleSettings> Settings => Set<ScheduleSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Cada feature mantém o próprio mapeamento (…Configuration.cs ao lado das entidades).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
