using CakeBoss.Backend.Features.Availability;
using CakeBoss.Backend.Features.Orders;
using CakeBoss.Backend.Features.Ordering;
using CakeBoss.Backend.Infrastructure.Data;
using CakeBoss.Backend.Shared;
using CakeBoss.Backend.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace CakeBoss.Backend.Tests.Ordering;

/// <summary>
/// Rodam só com um Postgres de teste configurado: o serviço grava no banco e consulta a disponibilidade.
/// O banco novo já vem com o expediente e as configurações iniciais (60 min, 3 dias de antecedência e 20 kg por dia).
/// </summary>
public class OrderServiceTests
{
    // "Agora": sexta-feira 2026-11-13, 12:00 em Brasília (15:00 UTC).
    private static readonly DateTimeOffset FridayNoon = new(2026, 11, 13, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Saturday = new(2026, 11, 14);
    private static readonly DateOnly Sunday = new(2026, 11, 15);
    private static readonly DateOnly Tuesday = new(2026, 11, 17);
    private static readonly DateOnly Wednesday = new(2026, 11, 18);

    private static OrderService CreateService(AppDbContext db)
    {
        var clock = new FixedTimeProvider(FridayNoon);
        return new OrderService(db, new AvailabilityService(db, clock), clock);
    }

    private static PlaceOrderItem NewItem(decimal weightKg = 2m) =>
        new("Chocolate", ["Brigadeiro", "Ninho com morango"], "Chantilly", weightKg, "Tema unicórnio, tons de rosa e lilás");

    private static PlaceOrderCommand NewCommand(
        string phone = "(11) 91234-5678",
        string name = "Maria Silva",
        string? email = null,
        DateOnly? date = null,
        int hour = 15,
        decimal weightKg = 2m,
        DeliveryType deliveryType = DeliveryType.Pickup,
        string? address = null,
        IReadOnlyList<PlaceOrderItem>? items = null,
        IReadOnlyList<PlaceOrderPhoto>? photos = null) =>
        new(
            name,
            phone,
            email,
            deliveryType,
            address,
            date ?? Tuesday,
            new TimeOnly(hour, 0),
            PaymentMethod.Pix,
            "Sem nozes, por favor",
            items ?? [NewItem(weightKg)],
            photos ?? []);

    private static async Task ApproveOrderAsync(TestDatabase database, DateOnly date, int hour, decimal weightKg)
    {
        var order = TestData.NewOrder(date: date, time: new TimeOnly(hour, 0), items: [TestData.NewItem(weightKg)]);
        order.Approve(100m, 0m, 0m, false, TestData.NowUtc);
        database.Db.Orders.Add(order);
        await database.Db.SaveChangesAsync();
    }

    // ---------- Fazer pedido ----------

    [PostgresFact]
    public async Task PlaceOrder_CreatesAPendingOrderWithItsItemsAndANewCustomer()
    {
        await using var database = await TestDatabase.CreateAsync();

        var placed = await CreateService(database.Db).PlaceOrderAsync(NewCommand(email: "maria@example.com"));

        Assert.Equal(Order.CodeLength, placed.Code.Length);
        Assert.Equal(Tuesday, placed.Date);
        Assert.Equal(new TimeOnly(15, 0), placed.Time);

        var order = await database.Db.Orders.AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .SingleAsync(o => o.Id == placed.Id);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal("Sem nozes, por favor", order.Notes);
        Assert.Equal("Maria Silva", order.Customer.Name);
        Assert.Equal("11912345678", order.Customer.Phone);
        Assert.Equal("maria@example.com", order.Customer.Email);
        var item = Assert.Single(order.Items);
        Assert.Equal(["Brigadeiro", "Ninho com morango"], item.Fillings);
    }

    [PostgresFact]
    public async Task PlaceOrder_SavesDeliveryAddressAndPhotosMetadata()
    {
        await using var database = await TestDatabase.CreateAsync();
        var command = NewCommand(
            deliveryType: DeliveryType.Delivery,
            address: "Rua das Flores, 10",
            photos: [new PlaceOrderPhoto("orders/a.jpg", "a.jpg", "image/jpeg", 2048)]);

        var placed = await CreateService(database.Db).PlaceOrderAsync(command);

        var order = await database.Db.Orders.AsNoTracking().Include(o => o.Photos).SingleAsync(o => o.Id == placed.Id);
        Assert.Equal("Rua das Flores, 10", order.DeliveryAddress);
        var photo = Assert.Single(order.Photos);
        Assert.Equal("orders/a.jpg", photo.StorageKey);
    }

    [PostgresFact]
    public async Task PlaceOrder_ReusesTheCustomerFoundByPhone_UpdatingTheNameAndKeepingTheEmail()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database.Db);
        await service.PlaceOrderAsync(NewCommand(phone: "(11) 91234-5678", name: "Maria", email: "maria@example.com", hour: 14));

        await service.PlaceOrderAsync(NewCommand(phone: "+55 11 91234-5678", name: "Maria Silva Souza", email: null, hour: 15));

        var customer = await database.Db.Customers.AsNoTracking().SingleAsync();
        Assert.Equal("Maria Silva Souza", customer.Name);
        Assert.Equal("maria@example.com", customer.Email);
        Assert.Equal(2, await database.Db.Orders.CountAsync());
    }

    [PostgresFact]
    public async Task PlaceOrder_UpdatesTheEmailWhenANewOneIsGiven()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database.Db);
        await service.PlaceOrderAsync(NewCommand(email: "antigo@example.com", hour: 14));

        await service.PlaceOrderAsync(NewCommand(email: "novo@example.com", hour: 15));

        var customer = await database.Db.Customers.AsNoTracking().SingleAsync();
        Assert.Equal("novo@example.com", customer.Email);
    }

    [PostgresFact]
    public async Task PlaceOrder_PendingOrdersDoNotBlockEachOther()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database.Db);

        await service.PlaceOrderAsync(NewCommand(phone: "11911111111"));
        await service.PlaceOrderAsync(NewCommand(phone: "11922222222"));

        Assert.Equal(2, await database.Db.Orders.CountAsync());
    }

    [PostgresFact]
    public async Task PlaceOrder_ConcurrentSubmissionsFromTheSameNewPhone_CreateASingleCustomer()
    {
        await using var database = await TestDatabase.CreateAsync();
        var connectionString = database.Db.Database.GetConnectionString()!;

        async Task SubmitAsync(int hour)
        {
            await using var db = new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);
            await CreateService(db).PlaceOrderAsync(NewCommand(phone: "11933333333", hour: hour));
        }

        await Task.WhenAll(SubmitAsync(14), SubmitAsync(15), SubmitAsync(16));

        Assert.Equal(1, await database.Db.Customers.CountAsync());
        Assert.Equal(3, await database.Db.Orders.CountAsync());
    }

    // ---------- Fazer pedido: o que deve ser recusado ----------

    [PostgresFact]
    public async Task PlaceOrder_ForAnOccupiedSlot_Throws()
    {
        await using var database = await TestDatabase.CreateAsync();
        await ApproveOrderAsync(database, Tuesday, 15, 2m);

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            CreateService(database.Db).PlaceOrderAsync(NewCommand(phone: "11944444444", hour: 15)));

        Assert.Contains("horário", exception.Message);
        Assert.Equal(1, await database.Db.Orders.CountAsync());
    }

    [PostgresFact]
    public async Task PlaceOrder_OnAClosedDay_Throws()
    {
        await using var database = await TestDatabase.CreateAsync();

        await Assert.ThrowsAsync<DomainException>(() =>
            CreateService(database.Db).PlaceOrderAsync(NewCommand(date: Sunday)));
    }

    [PostgresFact]
    public async Task PlaceOrder_BeforeTheMinimumLeadTime_Throws()
    {
        await using var database = await TestDatabase.CreateAsync();

        await Assert.ThrowsAsync<DomainException>(() =>
            CreateService(database.Db).PlaceOrderAsync(NewCommand(date: Saturday)));
    }

    [PostgresFact]
    public async Task PlaceOrder_ThatDoesNotFitTheDailyCapacity_ThrowsWithTheCapacityMessage()
    {
        await using var database = await TestDatabase.CreateAsync();
        await ApproveOrderAsync(database, Wednesday, 14, 19m);

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            CreateService(database.Db).PlaceOrderAsync(NewCommand(phone: "11955555555", date: Wednesday, hour: 16, weightKg: 2m)));

        Assert.Contains("tamanho", exception.Message);
    }

    [PostgresFact]
    public async Task PlaceOrder_ThatFitsTheRemainingCapacity_Succeeds()
    {
        await using var database = await TestDatabase.CreateAsync();
        await ApproveOrderAsync(database, Wednesday, 14, 19m);

        var placed = await CreateService(database.Db)
            .PlaceOrderAsync(NewCommand(phone: "11966666666", date: Wednesday, hour: 16, weightKg: 1m));

        Assert.Equal(Wednesday, placed.Date);
    }

    [PostgresFact]
    public async Task PlaceOrder_WithInvalidData_SavesNothing()
    {
        await using var database = await TestDatabase.CreateAsync();
        var noFilling = new PlaceOrderItem("Chocolate", [], "Chantilly", 2m, "Tema unicórnio");

        await Assert.ThrowsAsync<DomainException>(() =>
            CreateService(database.Db).PlaceOrderAsync(NewCommand(items: [noFilling])));

        Assert.Equal(0, await database.Db.Orders.CountAsync());
        Assert.Equal(0, await database.Db.Customers.CountAsync());
    }

    [PostgresFact]
    public async Task PlaceOrder_WithInvalidPhone_Throws()
    {
        await using var database = await TestDatabase.CreateAsync();

        await Assert.ThrowsAsync<DomainException>(() =>
            CreateService(database.Db).PlaceOrderAsync(NewCommand(phone: "123")));
    }

    // ---------- Acompanhar pelo código ----------

    [PostgresFact]
    public async Task GetByCode_ShowsTheOrderWithoutValuesWhilePending()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database.Db);
        var placed = await service.PlaceOrderAsync(NewCommand(email: "maria@example.com"));

        var view = await service.GetByCodeAsync(placed.Code);

        Assert.NotNull(view);
        Assert.Equal(placed.Code, view.Code);
        Assert.Equal(OrderStatus.Pending, view.Status);
        Assert.Equal("Maria Silva", view.CustomerName);
        Assert.Equal(Tuesday, view.Date);
        var item = Assert.Single(view.Items);
        Assert.Equal(["Brigadeiro", "Ninho com morango"], item.Fillings);
        Assert.Null(view.Price);
        Assert.Null(view.Total);
        Assert.Null(view.RemainingAmount);
        Assert.False(view.DepositPaid);
    }

    [PostgresFact]
    public async Task GetByCode_ShowsTheAgreedValuesAfterApproval()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database.Db);
        var placed = await service.PlaceOrderAsync(NewCommand());
        var order = await database.Db.Orders.SingleAsync(o => o.Id == placed.Id);
        order.Approve(150m, 0m, 50m, false, TestData.NowUtc);
        await database.Db.SaveChangesAsync();

        var view = await service.GetByCodeAsync(placed.Code);

        Assert.NotNull(view);
        Assert.Equal(OrderStatus.Approved, view.Status);
        Assert.Equal(150m, view.Price);
        Assert.Equal(50m, view.DepositAmount);
        Assert.Equal(150m, view.Total);
        Assert.Equal(150m, view.RemainingAmount);
        Assert.False(view.DepositPaid);
    }

    [PostgresFact]
    public async Task GetByCode_AcceptsLowercaseAndSurroundingSpaces()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database.Db);
        var placed = await service.PlaceOrderAsync(NewCommand());

        var view = await service.GetByCodeAsync($"  {placed.Code.ToLowerInvariant()}  ");

        Assert.NotNull(view);
        Assert.Equal(placed.Code, view.Code);
    }

    [PostgresTheory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ABC")]
    [InlineData("0OIL1111")]
    [InlineData("ZZZZZZZZ")]
    public async Task GetByCode_WithUnknownOrMalformedCode_ReturnsNull(string? code)
    {
        await using var database = await TestDatabase.CreateAsync();

        Assert.Null(await CreateService(database.Db).GetByCodeAsync(code));
    }
}
