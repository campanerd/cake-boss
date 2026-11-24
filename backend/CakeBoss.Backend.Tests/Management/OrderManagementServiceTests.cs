using CakeBoss.Backend.Features.Management;
using CakeBoss.Backend.Features.Orders;
using CakeBoss.Backend.Tests.Support;

namespace CakeBoss.Backend.Tests.Management;

public class OrderManagementServiceTests
{
    private static Order NewOrder(
        DateOnly date,
        int hour,
        OrderStatus status = OrderStatus.Pending,
        params decimal[] weights)
    {
        var order = TestData.NewOrder(
            date: date,
            time: new TimeOnly(hour, 0),
            items: (weights.Length > 0 ? weights : [2m]).Select(w => TestData.NewItem(w)).ToArray());

        switch (status)
        {
            case OrderStatus.Approved:
                order.Approve(150m, 0m, 50m, false, TestData.NowUtc);
                break;
            case OrderStatus.Confirmed:
                order.Approve(150m, 0m, 50m, false, TestData.NowUtc);
                order.ConfirmDeposit(TestData.NowUtc);
                break;
            case OrderStatus.Completed:
                order.Approve(150m, 0m, 50m, false, TestData.NowUtc);
                order.ConfirmDeposit(TestData.NowUtc);
                order.Complete(TestData.NowUtc);
                break;
            case OrderStatus.Rejected:
                order.Reject("Agenda cheia", TestData.NowUtc);
                break;
            case OrderStatus.Cancelled:
                order.Cancel("Desistiu", false, TestData.NowUtc);
                break;
        }

        return order;
    }

    private static async Task Save(TestDatabase database, params Order[] orders)
    {
        database.Db.Orders.AddRange(orders);
        await database.Db.SaveChangesAsync();
    }

    private static DateOnly Day(int day) => new(2026, 11, day);

    // ---------- Lista ----------

    [PostgresFact]
    public async Task List_WithNoOrders_IsEmpty()
    {
        await using var database = await TestDatabase.CreateAsync();

        Assert.Empty(await new OrderManagementService(database.Db).ListAsync());
    }

    [PostgresFact]
    public async Task List_PutsPendingFirst_ThenOpenByDate_ThenClosedNewestFirst()
    {
        await using var database = await TestDatabase.CreateAsync();
        var pendingLate = NewOrder(new DateOnly(2026, 12, 5), 15);
        var pendingSoon = NewOrder(Day(26), 15);
        var approved = NewOrder(Day(25), 14, OrderStatus.Approved);
        var confirmed = NewOrder(Day(28), 10, OrderStatus.Confirmed);
        var completedOld = NewOrder(Day(10), 10, OrderStatus.Completed);
        var completedNew = NewOrder(Day(20), 10, OrderStatus.Completed);
        var rejected = NewOrder(Day(22), 15, OrderStatus.Rejected);
        var cancelled = NewOrder(Day(21), 15, OrderStatus.Cancelled);
        await Save(database, completedOld, pendingLate, cancelled, confirmed, rejected, approved, completedNew, pendingSoon);

        var list = await new OrderManagementService(database.Db).ListAsync();

        Assert.Equal(
            new[] { pendingSoon, pendingLate, approved, confirmed, rejected, cancelled, completedNew, completedOld }
                .Select(o => o.Id),
            list.Select(o => o.Id));
    }

    [PostgresFact]
    public async Task List_SameDay_OrdersByTime()
    {
        await using var database = await TestDatabase.CreateAsync();
        var late = NewOrder(Day(26), 18);
        var early = NewOrder(Day(26), 9);
        await Save(database, late, early);

        var list = await new OrderManagementService(database.Db).ListAsync();

        Assert.Equal(new[] { early.Id, late.Id }, list.Select(o => o.Id));
    }

    [PostgresFact]
    public async Task List_LimitsOnlyTheClosedOrders_OpenOnesAlwaysComeInFull()
    {
        await using var database = await TestDatabase.CreateAsync();
        var open = Enumerable.Range(1, 3).Select(i => NewOrder(Day(25 + i), 14)).ToArray();
        var closed = Enumerable.Range(1, 4).Select(i => NewOrder(Day(i), 10, OrderStatus.Completed)).ToArray();
        await Save(database, [.. open, .. closed]);

        var list = await new OrderManagementService(database.Db).ListAsync(closedLimit: 2);

        Assert.Equal(5, list.Count);
        Assert.Equal(open.Select(o => o.Id), list.Take(3).Select(o => o.Id));
        // Dos encerrados ficam os 2 mais recentes (dias 4 e 3).
        Assert.Equal(new[] { closed[3].Id, closed[2].Id }, list.Skip(3).Select(o => o.Id));
    }

    [PostgresFact]
    public async Task List_WithZeroClosedLimit_ReturnsOnlyOpenOrders()
    {
        await using var database = await TestDatabase.CreateAsync();
        await Save(database, NewOrder(Day(26), 14), NewOrder(Day(10), 10, OrderStatus.Completed));

        var list = await new OrderManagementService(database.Db).ListAsync(closedLimit: 0);

        Assert.Equal(OrderStatus.Pending, Assert.Single(list).Status);
    }

    [PostgresFact]
    public async Task List_WithANegativeLimit_Throws()
    {
        await using var database = await TestDatabase.CreateAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new OrderManagementService(database.Db).ListAsync(closedLimit: -1));
    }

    [PostgresFact]
    public async Task List_ShowsTheCustomerNameAndTheTotalWeightOfAllCakes()
    {
        await using var database = await TestDatabase.CreateAsync();
        var order = NewOrder(Day(26), 14, OrderStatus.Pending, 2m, 3.5m);
        await Save(database, order);

        var item = Assert.Single(await new OrderManagementService(database.Db).ListAsync());

        Assert.Equal("Maria Silva", item.CustomerName);
        Assert.Equal(5.5m, item.TotalWeightKg);
        Assert.Equal(order.Code, item.Code);
        Assert.Equal(DeliveryType.Pickup, item.DeliveryType);
        Assert.Equal(new TimeOnly(14, 0), item.Time);
    }

    // ---------- Detalhe ----------

    [PostgresFact]
    public async Task Get_ReturnsTheContactTheItemsAndTheWhatsAppMessage()
    {
        await using var database = await TestDatabase.CreateAsync();
        var order = NewOrder(Day(26), 14);
        await Save(database, order);

        var details = await new OrderManagementService(database.Db).GetAsync(order.Id);

        Assert.NotNull(details);
        Assert.Equal(order.Code, details.Code);
        Assert.Equal(OrderStatus.Pending, details.Status);
        Assert.Equal("Maria Silva", details.Customer.Name);
        Assert.Equal(order.Customer.Phone, details.Customer.Phone);
        Assert.Null(details.Customer.Email);

        var item = Assert.Single(details.Items);
        Assert.Equal("Chocolate", item.Batter);
        Assert.Equal(new[] { "Brigadeiro", "Ninho com morango" }, item.Fillings);
        Assert.Equal("Chantilly", item.Frosting);
        Assert.Equal(2m, item.WeightKg);
        Assert.Equal(2m, details.TotalWeightKg);

        Assert.StartsWith($"https://wa.me/55{order.Customer.Phone}?text=", details.WhatsApp.Link);
        Assert.Contains(order.Code, details.WhatsApp.Text);
    }

    [PostgresFact]
    public async Task Get_BeforeApproval_HasNoMoneyYet()
    {
        await using var database = await TestDatabase.CreateAsync();
        var order = NewOrder(Day(26), 14);
        await Save(database, order);

        var details = await new OrderManagementService(database.Db).GetAsync(order.Id);

        Assert.NotNull(details);
        Assert.Null(details.Price);
        Assert.Null(details.Total);
        Assert.Null(details.RemainingAmount);
        Assert.False(details.DepositPaid);
        Assert.False(details.BlocksWholeDay);
    }

    [PostgresFact]
    public async Task Get_AfterApproval_ShowsWhatWasAgreed_AndTheRemainingChangesWhenTheDepositIsPaid()
    {
        await using var database = await TestDatabase.CreateAsync();
        // Taxa de entrega só existe em pedido com entrega.
        var order = TestData.NewOrder(DeliveryType.Delivery, "Rua das Flores, 10", Day(26), new TimeOnly(14, 0));
        order.Approve(150m, 20m, 50m, true, TestData.NowUtc);
        await Save(database, order);
        var service = new OrderManagementService(database.Db);

        var approved = await service.GetAsync(order.Id);

        Assert.NotNull(approved);
        Assert.Equal("Rua das Flores, 10", approved.DeliveryAddress);
        Assert.Equal(150m, approved.Price);
        Assert.Equal(20m, approved.DeliveryFee);
        Assert.Equal(50m, approved.DepositAmount);
        Assert.Equal(170m, approved.Total);
        Assert.Equal(170m, approved.RemainingAmount); // o sinal ainda não foi pago
        Assert.False(approved.DepositPaid);
        Assert.True(approved.BlocksWholeDay);

        order.ConfirmDeposit(TestData.NowUtc);
        await database.Db.SaveChangesAsync();

        var confirmed = await service.GetAsync(order.Id);

        Assert.NotNull(confirmed);
        Assert.True(confirmed.DepositPaid);
        Assert.Equal(120m, confirmed.RemainingAmount);
    }

    [PostgresFact]
    public async Task Get_OfARejectedOrder_ShowsTheReason()
    {
        await using var database = await TestDatabase.CreateAsync();
        var order = NewOrder(Day(26), 14, OrderStatus.Rejected);
        await Save(database, order);

        var details = await new OrderManagementService(database.Db).GetAsync(order.Id);

        Assert.NotNull(details);
        Assert.Equal(OrderStatus.Rejected, details.Status);
        Assert.Equal("Agenda cheia", details.RejectionReason);
    }

    [PostgresFact]
    public async Task Get_TheWhatsAppMessageNeverCarriesTheAgreedMoney()
    {
        await using var database = await TestDatabase.CreateAsync();
        var order = NewOrder(Day(26), 14, OrderStatus.Approved);
        await Save(database, order);

        var details = await new OrderManagementService(database.Db).GetAsync(order.Id);

        Assert.NotNull(details);
        Assert.DoesNotContain("R$", details.WhatsApp.Text);
        Assert.DoesNotContain("150", details.WhatsApp.Text);
    }

    [PostgresFact]
    public async Task Get_WithAnUnknownId_ReturnsNull()
    {
        await using var database = await TestDatabase.CreateAsync();

        Assert.Null(await new OrderManagementService(database.Db).GetAsync(Guid.NewGuid()));
    }
}
