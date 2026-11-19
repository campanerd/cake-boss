using CakeBoss.Backend.Features.Orders;
using CakeBoss.Backend.Infrastructure.Data;
using CakeBoss.Backend.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CakeBoss.Backend.Tests.Orders;

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

/// <summary>Cria um banco novo e isolado para cada teste e o apaga no final.</summary>
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

public class OrderPersistenceTests
{
    private static readonly DateOnly Day = new(2026, 11, 24);
    private static readonly TimeOnly Hour = new(15, 0);

    private static Order NewOrder(OrderStatus status, TimeOnly? time = null)
    {
        var order = TestData.NewOrder(date: Day, time: time ?? Hour);

        switch (status)
        {
            case OrderStatus.Approved:
                order.Approve(100m, 0m, 0m, false, TestData.NowUtc);
                break;
            case OrderStatus.Confirmed:
                order.Approve(100m, 0m, 0m, false, TestData.NowUtc);
                order.ConfirmDeposit(TestData.NowUtc);
                break;
            case OrderStatus.Completed:
                order.Approve(100m, 0m, 0m, false, TestData.NowUtc);
                order.ConfirmDeposit(TestData.NowUtc);
                order.Complete(TestData.NowUtc);
                break;
            case OrderStatus.Rejected:
                order.Reject(null, TestData.NowUtc);
                break;
            case OrderStatus.Cancelled:
                order.Cancel(null, false, TestData.NowUtc);
                break;
        }

        return order;
    }

    [PostgresFact]
    public async Task SaveAndLoad_RoundTripsTheWholeAggregate()
    {
        await using var database = await TestDatabase.CreateAsync();
        var order = TestData.NewOrder(
            DeliveryType.Delivery,
            "Rua das Flores, 10",
            Day,
            Hour,
            [TestData.NewItem(2.5m)],
            [TestData.NewPhoto("a.jpg")]);
        order.Approve(250m, 15m, 100m, true, TestData.NowUtc);
        database.Db.Orders.Add(order);
        await database.Db.SaveChangesAsync();

        await using var readDb = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(database.Db.Database.GetConnectionString()).Options);
        var loaded = await readDb.Orders
            .Include(o => o.Items)
            .Include(o => o.Photos)
            .Include(o => o.Customer)
            .SingleAsync(o => o.Id == order.Id);

        Assert.Equal(order.Code, loaded.Code);
        Assert.Equal(OrderStatus.Approved, loaded.Status);
        Assert.Equal(DeliveryType.Delivery, loaded.DeliveryType);
        Assert.Equal("Rua das Flores, 10", loaded.DeliveryAddress);
        Assert.Equal(Day, loaded.Date);
        Assert.Equal(Hour, loaded.Time);
        Assert.Equal(250m, loaded.Price);
        Assert.Equal(15m, loaded.DeliveryFee);
        Assert.Equal(100m, loaded.DepositAmount);
        Assert.True(loaded.BlocksWholeDay);
        Assert.Equal(order.Customer.Phone, loaded.Customer.Phone);

        var item = Assert.Single(loaded.Items);
        Assert.Equal("Chocolate", item.Batter);
        Assert.Equal(["Brigadeiro", "Ninho com morango"], item.Fillings);
        Assert.Equal("Chantilly", item.Frosting);
        Assert.Equal(2.5m, item.WeightKg);

        var photo = Assert.Single(loaded.Photos);
        Assert.Equal("orders/a.jpg", photo.StorageKey);
    }

    [PostgresFact]
    public async Task TwoApprovedOrdersInTheSameSlot_AreRejectedByTheDatabase()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.Db.Orders.Add(NewOrder(OrderStatus.Approved));
        await database.Db.SaveChangesAsync();

        database.Db.Orders.Add(NewOrder(OrderStatus.Approved));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => database.Db.SaveChangesAsync());

        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal("UX_Orders_Occupied_Slot", postgres.ConstraintName);
    }

    [PostgresTheory]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Completed)]
    public async Task ApprovedAndAnotherSlotBlockingOrder_CannotShareTheSlot(OrderStatus otherStatus)
    {
        await using var database = await TestDatabase.CreateAsync();
        database.Db.Orders.Add(NewOrder(OrderStatus.Approved));
        await database.Db.SaveChangesAsync();

        database.Db.Orders.Add(NewOrder(otherStatus));

        await Assert.ThrowsAsync<DbUpdateException>(() => database.Db.SaveChangesAsync());
    }

    [PostgresTheory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Rejected)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task OrdersThatDoNotBlockTheSlot_CanSitNextToAnApprovedOne(OrderStatus otherStatus)
    {
        await using var database = await TestDatabase.CreateAsync();
        database.Db.Orders.Add(NewOrder(OrderStatus.Approved));
        database.Db.Orders.Add(NewOrder(otherStatus));

        await database.Db.SaveChangesAsync();

        Assert.Equal(2, await database.Db.Orders.CountAsync());
    }

    [PostgresFact]
    public async Task TwoPendingOrdersInTheSameSlot_AreAllowed()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.Db.Orders.Add(NewOrder(OrderStatus.Pending));
        database.Db.Orders.Add(NewOrder(OrderStatus.Pending));

        await database.Db.SaveChangesAsync();

        Assert.Equal(2, await database.Db.Orders.CountAsync());
    }

    [PostgresFact]
    public async Task ApprovedOrdersInDifferentSlots_AreAllowed()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.Db.Orders.Add(NewOrder(OrderStatus.Approved, new TimeOnly(15, 0)));
        database.Db.Orders.Add(NewOrder(OrderStatus.Approved, new TimeOnly(16, 0)));

        await database.Db.SaveChangesAsync();

        Assert.Equal(2, await database.Db.Orders.CountAsync());
    }
}
