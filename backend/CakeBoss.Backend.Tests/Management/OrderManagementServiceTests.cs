using CakeBoss.Backend.Features.Availability;
using CakeBoss.Backend.Features.Management;
using CakeBoss.Backend.Features.Orders;
using CakeBoss.Backend.Infrastructure.Data;
using CakeBoss.Backend.Shared;
using CakeBoss.Backend.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace CakeBoss.Backend.Tests.Management;

/// <summary>
/// Rodam só com um Postgres de teste configurado. "Agora" nos testes é quarta-feira 2026-11-18, 12:00 em Brasília
/// (<see cref="TestData.NowUtc"/>); o banco novo já vem com o expediente e as configurações iniciais.
/// </summary>
public class OrderManagementServiceTests
{
    /// <param name="later">Quanto o relógio anda depois de <see cref="TestData.NowUtc"/>, quando os pedidos foram aprovados.</param>
    private static OrderManagementService CreateService(TestDatabase database, TimeSpan? later = null)
    {
        var clock = new FixedTimeProvider(TestData.NowUtc + (later ?? TimeSpan.Zero));
        return new OrderManagementService(database.Db, new AvailabilityService(database.Db, clock), clock);
    }

    // Os pedidos aprovados dos testes têm 50 reais de sinal e foram aprovados em TestData.NowUtc: o prazo termina 5 horas depois.
    private static readonly TimeSpan PastTheDeadline = TimeSpan.FromHours(6);

    // Simula a corrida de duas aprovações: a conferência da agenda passa, mas outro pedido toma o horário antes de gravar.
    private sealed class BlindAvailability(AppDbContext db, TimeProvider clock) : AvailabilityService(db, clock)
    {
        public override Task<string?> FindApprovalConflictAsync(
            Guid orderId, DateOnly date, TimeOnly time, decimal weightKg, bool blocksWholeDay, CancellationToken ct = default) =>
            Task.FromResult<string?>(null);
    }

    private static async Task<Order> Reload(TestDatabase database, Guid id)
    {
        database.Db.ChangeTracker.Clear();
        return await database.Db.Orders.AsNoTracking().SingleAsync(o => o.Id == id);
    }

    private static ApproveOrderCommand Approval(
        Order order,
        decimal price = 150m,
        decimal deliveryFee = 0m,
        decimal deposit = 50m,
        bool wholeDay = false) =>
        new(order.Id, price, deliveryFee, deposit, wholeDay);

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
            case OrderStatus.Expired:
                order.Approve(150m, 0m, 50m, false, TestData.NowUtc);
                order.Expire(TestData.NowUtc + PastTheDeadline);
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

        Assert.Empty(await CreateService(database).ListAsync());
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

        var list = await CreateService(database).ListAsync();

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

        var list = await CreateService(database).ListAsync();

        Assert.Equal(new[] { early.Id, late.Id }, list.Select(o => o.Id));
    }

    [PostgresFact]
    public async Task List_LimitsOnlyTheClosedOrders_OpenOnesAlwaysComeInFull()
    {
        await using var database = await TestDatabase.CreateAsync();
        var open = Enumerable.Range(1, 3).Select(i => NewOrder(Day(25 + i), 14)).ToArray();
        var closed = Enumerable.Range(1, 4).Select(i => NewOrder(Day(i), 10, OrderStatus.Completed)).ToArray();
        await Save(database, [.. open, .. closed]);

        var list = await CreateService(database).ListAsync(closedLimit: 2);

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

        var list = await CreateService(database).ListAsync(closedLimit: 0);

        Assert.Equal(OrderStatus.Pending, Assert.Single(list).Status);
    }

    [PostgresFact]
    public async Task List_WithANegativeLimit_Throws()
    {
        await using var database = await TestDatabase.CreateAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            CreateService(database).ListAsync(closedLimit: -1));
    }

    [PostgresFact]
    public async Task List_ShowsTheCustomerNameAndTheTotalWeightOfAllCakes()
    {
        await using var database = await TestDatabase.CreateAsync();
        var order = NewOrder(Day(26), 14, OrderStatus.Pending, 2m, 3.5m);
        await Save(database, order);

        var item = Assert.Single(await CreateService(database).ListAsync());

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

        var details = await CreateService(database).GetAsync(order.Id);

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

        var details = await CreateService(database).GetAsync(order.Id);

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
        var service = CreateService(database);

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

        var details = await CreateService(database).GetAsync(order.Id);

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

        var details = await CreateService(database).GetAsync(order.Id);

        Assert.NotNull(details);
        Assert.DoesNotContain("R$", details.WhatsApp.Text);
        Assert.DoesNotContain("150", details.WhatsApp.Text);
    }

    [PostgresFact]
    public async Task Get_WithAnUnknownId_ReturnsNull()
    {
        await using var database = await TestDatabase.CreateAsync();

        Assert.Null(await CreateService(database).GetAsync(Guid.NewGuid()));
    }

    // ---------- Aprovar ----------

    [PostgresFact]
    public async Task Approve_SavesWhatWasAgreed_AndOccupiesTheSlot()
    {
        await using var database = await TestDatabase.CreateAsync();
        var order = NewOrder(Day(26), 15);
        await Save(database, order);

        await CreateService(database).ApproveAsync(Approval(order, price: 150m, deposit: 60m, wholeDay: true));

        var saved = await Reload(database, order.Id);
        Assert.Equal(OrderStatus.Approved, saved.Status);
        Assert.Equal(150m, saved.Price);
        Assert.Equal(60m, saved.DepositAmount);
        Assert.True(saved.BlocksWholeDay);
        Assert.Equal(TestData.NowUtc, saved.UpdatedAt);
    }

    [PostgresFact]
    public async Task Approve_AcceptsAnOrderCloserThanTheMinimumLeadTime()
    {
        await using var database = await TestDatabase.CreateAsync();
        var order = NewOrder(Day(19), 15); // amanhã: o cliente não conseguiria pedir (3 dias), mas a dona pode aprovar
        await Save(database, order);

        await CreateService(database).ApproveAsync(Approval(order));

        Assert.Equal(OrderStatus.Approved, (await Reload(database, order.Id)).Status);
    }

    [PostgresFact]
    public async Task Approve_WithTheSlotTakenByAnotherApprovedOrder_ThrowsAndKeepsPending()
    {
        await using var database = await TestDatabase.CreateAsync();
        var other = NewOrder(Day(26), 15, OrderStatus.Approved);
        var order = NewOrder(Day(26), 15);
        await Save(database, other, order);

        var exception = await Assert.ThrowsAsync<DomainException>(() => CreateService(database).ApproveAsync(Approval(order)));

        Assert.Equal("Já existe um pedido aprovado nesse horário.", exception.Message);
        Assert.Equal(OrderStatus.Pending, (await Reload(database, order.Id)).Status);
    }

    [PostgresFact]
    public async Task Approve_WhenTheDayCapacityIsExceeded_ThrowsAndKeepsPending()
    {
        await using var database = await TestDatabase.CreateAsync();
        var other = NewOrder(Day(26), 14, OrderStatus.Approved, 19m);
        var order = NewOrder(Day(26), 16, OrderStatus.Pending, 2m);
        await Save(database, other, order);

        var exception = await Assert.ThrowsAsync<DomainException>(() => CreateService(database).ApproveAsync(Approval(order)));

        Assert.Contains("não cabe no limite do dia", exception.Message);
        Assert.Equal(OrderStatus.Pending, (await Reload(database, order.Id)).Status);
    }

    [PostgresFact]
    public async Task Approve_WithInvalidValues_ThrowsAndKeepsPending()
    {
        await using var database = await TestDatabase.CreateAsync();
        var order = NewOrder(Day(26), 15);
        await Save(database, order);

        await Assert.ThrowsAsync<DomainException>(() => CreateService(database).ApproveAsync(Approval(order, price: 0m)));
        await Assert.ThrowsAsync<DomainException>(() => CreateService(database).ApproveAsync(Approval(order, deposit: 500m)));

        Assert.Equal(OrderStatus.Pending, (await Reload(database, order.Id)).Status);
    }

    [PostgresFact]
    public async Task Approve_AnOrderThatIsNotPending_Throws()
    {
        await using var database = await TestDatabase.CreateAsync();
        var order = NewOrder(Day(26), 15, OrderStatus.Approved);
        await Save(database, order);

        // Sem a ordem certa (entidade antes da agenda), o pedido apareceria como conflito com ele mesmo.
        var exception = await Assert.ThrowsAsync<DomainException>(() => CreateService(database).ApproveAsync(Approval(order)));

        Assert.DoesNotContain("Já existe um pedido aprovado", exception.Message);
    }

    [PostgresFact]
    public async Task Approve_WhenAnotherApprovalWinsTheRace_ThrowsInPortugueseAndKeepsPending()
    {
        await using var database = await TestDatabase.CreateAsync();
        var other = NewOrder(Day(26), 15, OrderStatus.Approved);
        var order = NewOrder(Day(26), 15);
        await Save(database, other, order);
        var clock = new FixedTimeProvider(TestData.NowUtc);
        var service = new OrderManagementService(database.Db, new BlindAvailability(database.Db, clock), clock);

        var exception = await Assert.ThrowsAsync<DomainException>(() => service.ApproveAsync(Approval(order)));

        Assert.Equal("Outro pedido acabou de ocupar esse horário. Atualize a página e confira a agenda.", exception.Message);
        Assert.Equal(OrderStatus.Pending, (await Reload(database, order.Id)).Status);
    }

    // ---------- Recusar ----------

    [PostgresFact]
    public async Task Reject_SavesTheReason()
    {
        await using var database = await TestDatabase.CreateAsync();
        var order = NewOrder(Day(26), 15);
        await Save(database, order);

        await CreateService(database).RejectAsync(order.Id, "  Agenda cheia nesse dia  ");

        var saved = await Reload(database, order.Id);
        Assert.Equal(OrderStatus.Rejected, saved.Status);
        Assert.Equal("Agenda cheia nesse dia", saved.RejectionReason);
    }

    [PostgresFact]
    public async Task Reject_AnApprovedOrder_Throws()
    {
        await using var database = await TestDatabase.CreateAsync();
        var order = NewOrder(Day(26), 15, OrderStatus.Approved);
        await Save(database, order);

        await Assert.ThrowsAsync<DomainException>(() => CreateService(database).RejectAsync(order.Id, "Desculpe"));
    }

    // ---------- Sinal e conclusão ----------

    [PostgresFact]
    public async Task ConfirmDeposit_MovesToConfirmed_AndRecordsWhenItWasPaid()
    {
        await using var database = await TestDatabase.CreateAsync();
        var order = NewOrder(Day(26), 15, OrderStatus.Approved);
        await Save(database, order);

        await CreateService(database).ConfirmDepositAsync(order.Id);

        var saved = await Reload(database, order.Id);
        Assert.Equal(OrderStatus.Confirmed, saved.Status);
        Assert.Equal(TestData.NowUtc, saved.DepositPaidAt);
    }

    [PostgresFact]
    public async Task ConfirmDeposit_OnAPendingOrder_Throws()
    {
        await using var database = await TestDatabase.CreateAsync();
        var order = NewOrder(Day(26), 15);
        await Save(database, order);

        await Assert.ThrowsAsync<DomainException>(() => CreateService(database).ConfirmDepositAsync(order.Id));
    }

    [PostgresFact]
    public async Task Complete_OnlyAfterTheDepositIsConfirmed()
    {
        await using var database = await TestDatabase.CreateAsync();
        var waiting = NewOrder(Day(26), 15, OrderStatus.Approved);
        var paid = NewOrder(Day(26), 16, OrderStatus.Confirmed);
        await Save(database, waiting, paid);
        var service = CreateService(database);

        await Assert.ThrowsAsync<DomainException>(() => service.CompleteAsync(waiting.Id));
        await service.CompleteAsync(paid.Id);

        Assert.Equal(OrderStatus.Completed, (await Reload(database, paid.Id)).Status);
    }

    // ---------- Cancelar ----------

    [PostgresFact]
    public async Task Cancel_AConfirmedOrder_CanKeepTheDeposit()
    {
        await using var database = await TestDatabase.CreateAsync();
        var order = NewOrder(Day(26), 15, OrderStatus.Confirmed);
        await Save(database, order);

        await CreateService(database).CancelAsync(order.Id, "Cliente desistiu", keepDeposit: true);

        var saved = await Reload(database, order.Id);
        Assert.Equal(OrderStatus.Cancelled, saved.Status);
        Assert.Equal("Cliente desistiu", saved.CancellationReason);
        Assert.True(saved.DepositKept);
    }

    [PostgresFact]
    public async Task Cancel_BeforeTheDepositIsPaid_NeverKeepsIt()
    {
        await using var database = await TestDatabase.CreateAsync();
        var order = NewOrder(Day(26), 15, OrderStatus.Approved);
        await Save(database, order);

        await CreateService(database).CancelAsync(order.Id, null, keepDeposit: true);

        Assert.False((await Reload(database, order.Id)).DepositKept);
    }

    [PostgresFact]
    public async Task Cancel_ACompletedOrder_Throws()
    {
        await using var database = await TestDatabase.CreateAsync();
        var order = NewOrder(Day(26), 15, OrderStatus.Completed);
        await Save(database, order);

        await Assert.ThrowsAsync<DomainException>(() => CreateService(database).CancelAsync(order.Id, null, false));
    }

    [PostgresFact]
    public async Task Cancel_FreesTheSlotForAnotherApproval()
    {
        await using var database = await TestDatabase.CreateAsync();
        var first = NewOrder(Day(26), 15, OrderStatus.Approved);
        var second = NewOrder(Day(26), 15);
        await Save(database, first, second);
        var service = CreateService(database);

        await Assert.ThrowsAsync<DomainException>(() => service.ApproveAsync(Approval(second)));
        await service.CancelAsync(first.Id, "Desistiu", keepDeposit: false);
        await service.ApproveAsync(Approval(second));

        Assert.Equal(OrderStatus.Approved, (await Reload(database, second.Id)).Status);
    }

    // ---------- Prazo do sinal ----------

    [PostgresFact]
    public async Task Approve_StartsTheFiveHourDeadline_WhenThereIsADeposit()
    {
        await using var database = await TestDatabase.CreateAsync();
        var order = NewOrder(Day(26), 15);
        await Save(database, order);

        await CreateService(database).ApproveAsync(Approval(order, deposit: 50m));

        Assert.Equal(TestData.NowUtc.AddHours(5), (await Reload(database, order.Id)).DepositDueAt);
    }

    [PostgresFact]
    public async Task Approve_WithoutDeposit_HasNoDeadline()
    {
        await using var database = await TestDatabase.CreateAsync();
        var order = NewOrder(Day(26), 15);
        await Save(database, order);

        await CreateService(database).ApproveAsync(Approval(order, deposit: 0m));

        Assert.Null((await Reload(database, order.Id)).DepositDueAt);
    }

    [PostgresFact]
    public async Task ExpireOverdue_ClosesOnlyApprovedOrdersPastTheDeadline()
    {
        await using var database = await TestDatabase.CreateAsync();
        var overdue = NewOrder(Day(26), 14, OrderStatus.Approved);
        var pending = NewOrder(Day(26), 15);
        var confirmed = NewOrder(Day(26), 16, OrderStatus.Confirmed);
        var noDeposit = TestData.NewOrder(date: Day(26), time: new TimeOnly(17, 0));
        noDeposit.Approve(150m, 0m, 0m, false, TestData.NowUtc);
        await Save(database, overdue, pending, confirmed, noDeposit);

        var closed = await CreateService(database, PastTheDeadline).ExpireOverdueAsync();

        Assert.Equal(1, closed);
        Assert.Equal(OrderStatus.Expired, (await Reload(database, overdue.Id)).Status);
        Assert.Equal(OrderStatus.Pending, (await Reload(database, pending.Id)).Status);
        Assert.Equal(OrderStatus.Confirmed, (await Reload(database, confirmed.Id)).Status);
        Assert.Equal(OrderStatus.Approved, (await Reload(database, noDeposit.Id)).Status);
    }

    [PostgresFact]
    public async Task ExpireOverdue_InsideTheDeadline_DoesNothing()
    {
        await using var database = await TestDatabase.CreateAsync();
        var order = NewOrder(Day(26), 14, OrderStatus.Approved);
        await Save(database, order);

        var closed = await CreateService(database, TimeSpan.FromHours(4)).ExpireOverdueAsync();

        Assert.Equal(0, closed);
        Assert.Equal(OrderStatus.Approved, (await Reload(database, order.Id)).Status);
    }

    [PostgresFact]
    public async Task List_ClosesOverdueOrders_AndShowsThemAmongTheClosedOnes()
    {
        await using var database = await TestDatabase.CreateAsync();
        var overdue = NewOrder(Day(26), 14, OrderStatus.Approved);
        var pending = NewOrder(Day(26), 15);
        await Save(database, overdue, pending);

        var list = await CreateService(database, PastTheDeadline).ListAsync();

        Assert.Equal([pending.Id, overdue.Id], list.Select(o => o.Id));
        Assert.Equal(OrderStatus.Expired, list.Single(o => o.Id == overdue.Id).Status);
    }

    // O índice único do banco ainda enxerga o aprovado vencido como ocupante: aprovar fecha os vencidos antes.
    [PostgresFact]
    public async Task Approve_CanTakeTheSlotOfAnOrderWhoseDepositDeadlinePassed()
    {
        await using var database = await TestDatabase.CreateAsync();
        var overdue = NewOrder(Day(26), 15, OrderStatus.Approved);
        var order = NewOrder(Day(26), 15);
        await Save(database, overdue, order);

        await CreateService(database, PastTheDeadline).ApproveAsync(Approval(order));

        Assert.Equal(OrderStatus.Approved, (await Reload(database, order.Id)).Status);
        Assert.Equal(OrderStatus.Expired, (await Reload(database, overdue.Id)).Status);
    }

    // Decisão da dona: a palavra dela sobre o Pix recebido vale mais que o relógio, enquanto ninguém fechou o pedido.
    [PostgresFact]
    public async Task ConfirmDeposit_AfterTheDeadline_StillWorksWhileTheOrderIsNotClosed()
    {
        await using var database = await TestDatabase.CreateAsync();
        var order = NewOrder(Day(26), 15, OrderStatus.Approved);
        await Save(database, order);

        await CreateService(database, PastTheDeadline).ConfirmDepositAsync(order.Id);

        Assert.Equal(OrderStatus.Confirmed, (await Reload(database, order.Id)).Status);
    }

    [PostgresFact]
    public async Task Reopen_BringsAnExpiredOrderBack_WithANewDeadline()
    {
        await using var database = await TestDatabase.CreateAsync();
        var order = NewOrder(Day(26), 15, OrderStatus.Expired);
        await Save(database, order);
        var later = TimeSpan.FromHours(8);

        await CreateService(database, later).ReopenAsync(order.Id);

        var saved = await Reload(database, order.Id);
        Assert.Equal(OrderStatus.Approved, saved.Status);
        Assert.Equal(TestData.NowUtc + later + Order.DepositWindow, saved.DepositDueAt);
    }

    [PostgresFact]
    public async Task Reopen_WhenTheSlotWasTakenInTheMeantime_ThrowsAndStaysExpired()
    {
        await using var database = await TestDatabase.CreateAsync();
        var expired = NewOrder(Day(26), 15, OrderStatus.Expired);
        var other = NewOrder(Day(26), 15, OrderStatus.Confirmed);
        await Save(database, expired, other);

        var exception = await Assert.ThrowsAsync<DomainException>(() => CreateService(database).ReopenAsync(expired.Id));

        Assert.Equal("Já existe um pedido aprovado nesse horário.", exception.Message);
        Assert.Equal(OrderStatus.Expired, (await Reload(database, expired.Id)).Status);
    }

    [PostgresFact]
    public async Task Reopen_AnOrderThatIsNotExpired_Throws()
    {
        await using var database = await TestDatabase.CreateAsync();
        var order = NewOrder(Day(26), 15, OrderStatus.Approved);
        await Save(database, order);

        await Assert.ThrowsAsync<DomainException>(() => CreateService(database).ReopenAsync(order.Id));
    }

    [PostgresFact]
    public async Task Get_ShowsTheDepositDeadline()
    {
        await using var database = await TestDatabase.CreateAsync();
        var order = NewOrder(Day(26), 15, OrderStatus.Approved);
        await Save(database, order);

        var details = await CreateService(database).GetAsync(order.Id);

        Assert.Equal(TestData.NowUtc.AddHours(5), details!.DepositDueAt);
    }

    // A lista do painel mostra "Sinal até ..." nos pedidos que aguardam o sinal.
    [PostgresFact]
    public async Task List_BringsTheDepositDeadline()
    {
        await using var database = await TestDatabase.CreateAsync();
        var approved = NewOrder(Day(26), 15, OrderStatus.Approved);
        var pending = NewOrder(Day(26), 16);
        await Save(database, approved, pending);

        var list = await CreateService(database).ListAsync();

        Assert.Equal(TestData.NowUtc + Order.DepositWindow, list.Single(o => o.Id == approved.Id).DepositDueAt);
        Assert.Null(list.Single(o => o.Id == pending.Id).DepositDueAt);
    }

    // ---------- Pedido inexistente ----------

    [PostgresFact]
    public async Task Actions_OnAnUnknownOrder_ThrowNotFound()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);
        var id = Guid.NewGuid();

        var exceptions = new[]
        {
            await Assert.ThrowsAsync<DomainException>(() => service.ApproveAsync(new ApproveOrderCommand(id, 100m, 0m, 0m, false))),
            await Assert.ThrowsAsync<DomainException>(() => service.RejectAsync(id, null)),
            await Assert.ThrowsAsync<DomainException>(() => service.ConfirmDepositAsync(id)),
            await Assert.ThrowsAsync<DomainException>(() => service.CompleteAsync(id)),
            await Assert.ThrowsAsync<DomainException>(() => service.CancelAsync(id, null, false)),
            await Assert.ThrowsAsync<DomainException>(() => service.ReopenAsync(id))
        };

        Assert.All(exceptions, e => Assert.Equal("Pedido não encontrado.", e.Message));
    }
}
