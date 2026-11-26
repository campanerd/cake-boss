using CakeBoss.Backend.Features.Availability;
using CakeBoss.Backend.Features.Orders;
using CakeBoss.Backend.Features.Schedule;
using CakeBoss.Backend.Tests.Support;

namespace CakeBoss.Backend.Tests.Availability;

/// <summary>
/// Confere as consultas ao banco (o cálculo em si é testado em <see cref="AvailabilityCalculatorTests"/>).
/// Rodam só com um Postgres de teste configurado. O banco novo já vem com o expediente e as configurações iniciais
/// (60 min, 3 dias de antecedência e 20 kg por dia).
/// </summary>
public class AvailabilityServiceTests
{
    // "Agora": sexta-feira 2026-11-13, 12:00 em Brasília (15:00 UTC).
    private static readonly DateTimeOffset FridayNoon = new(2026, 11, 13, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Tuesday = new(2026, 11, 17);
    private static readonly DateOnly Wednesday = new(2026, 11, 18);
    private static readonly DateOnly Friday = new(2026, 11, 20);

    private static AvailabilityService CreateService(TestDatabase database, DateTimeOffset? utcNow = null) =>
        new(database.Db, new FixedTimeProvider(utcNow ?? FridayNoon));

    private static TimeOnly[] Hours(params int[] hours) => hours.Select(h => new TimeOnly(h, 0)).ToArray();

    private static Order NewOrder(DateOnly date, int hour, OrderStatus status, decimal[]? weights = null, bool wholeDay = false)
    {
        var order = TestData.NewOrder(
            date: date,
            time: new TimeOnly(hour, 0),
            items: (weights ?? [2m]).Select(w => TestData.NewItem(w)).ToArray());

        switch (status)
        {
            case OrderStatus.Approved:
                order.Approve(100m, 0m, 0m, wholeDay, TestData.NowUtc);
                break;
            case OrderStatus.Confirmed:
                order.Approve(100m, 0m, 0m, wholeDay, TestData.NowUtc);
                order.ConfirmDeposit(TestData.NowUtc);
                break;
            case OrderStatus.Completed:
                order.Approve(100m, 0m, 0m, wholeDay, TestData.NowUtc);
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
    public async Task FreeTimes_OnTuesday_ReturnsTheSixInitialSlots()
    {
        await using var database = await TestDatabase.CreateAsync();

        var times = await CreateService(database).GetFreeTimesAsync(Tuesday);

        Assert.Equal(Hours(14, 15, 16, 17, 18, 19), times);
    }

    [PostgresFact]
    public async Task FreeTimes_UsesTheInitialThreeDayLeadTime()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);

        Assert.Empty(await service.GetFreeTimesAsync(new DateOnly(2026, 11, 14))); // sábado, antes da antecedência
        Assert.NotEmpty(await service.GetFreeTimesAsync(Tuesday));
    }

    [PostgresTheory]
    [InlineData(OrderStatus.Approved, true)]
    [InlineData(OrderStatus.Confirmed, true)]
    [InlineData(OrderStatus.Completed, true)]
    [InlineData(OrderStatus.Pending, false)]
    [InlineData(OrderStatus.Rejected, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    public async Task FreeTimes_OnlySlotBlockingOrdersOccupyTheSlot(OrderStatus status, bool occupiesSlot)
    {
        await using var database = await TestDatabase.CreateAsync();
        database.Db.Orders.Add(NewOrder(Tuesday, 15, status));
        await database.Db.SaveChangesAsync();

        var times = await CreateService(database).GetFreeTimesAsync(Tuesday);

        Assert.Equal(!occupiesSlot, times.Contains(new TimeOnly(15, 0)));
    }

    [PostgresFact]
    public async Task FreeTimes_SumsTheCakesOfAnOrderAgainstTheDailyLimit()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.Db.Orders.Add(NewOrder(Wednesday, 14, OrderStatus.Approved, weights: [10m, 9m]));
        await database.Db.SaveChangesAsync();
        var service = CreateService(database);

        Assert.NotEmpty(await service.GetFreeTimesAsync(Wednesday, requestedKg: 1m));
        Assert.Empty(await service.GetFreeTimesAsync(Wednesday, requestedKg: 1.5m));
    }

    [PostgresFact]
    public async Task FreeTimes_OrderThatBlocksTheWholeDay_ClosesTheDay()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.Db.Orders.Add(NewOrder(Tuesday, 14, OrderStatus.Approved, wholeDay: true));
        await database.Db.SaveChangesAsync();
        var service = CreateService(database);

        Assert.Empty(await service.GetFreeTimesAsync(Tuesday));
        Assert.NotEmpty(await service.GetFreeTimesAsync(Wednesday));
    }

    [PostgresFact]
    public async Task FreeTimes_ScheduleBlocksFromTheDatabaseRemoveTimes()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.Db.ScheduleBlocks.Add(ScheduleBlock.Create(Tuesday, Tuesday, new TimeOnly(15, 0), new TimeOnly(17, 0)));
        await database.Db.SaveChangesAsync();

        var times = await CreateService(database).GetFreeTimesAsync(Tuesday);

        Assert.Equal(Hours(14, 17, 18, 19), times);
    }

    [PostgresFact]
    public async Task IsAvailable_ReturnsTrueOnlyForFreeTimesOnTheGrid()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.Db.Orders.Add(NewOrder(Tuesday, 15, OrderStatus.Approved));
        await database.Db.SaveChangesAsync();
        var service = CreateService(database);

        Assert.True(await service.IsAvailableAsync(Tuesday, new TimeOnly(14, 0)));
        Assert.False(await service.IsAvailableAsync(Tuesday, new TimeOnly(15, 0))); // ocupado
        Assert.False(await service.IsAvailableAsync(Tuesday, new TimeOnly(20, 0))); // fora do expediente
        Assert.False(await service.IsAvailableAsync(Tuesday, new TimeOnly(14, 30))); // fora da grade
    }

    [PostgresFact]
    public async Task DaysWithFreeTimes_ExcludesBlockedAndFullDays()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.Db.ScheduleBlocks.Add(ScheduleBlock.Create(Wednesday, Wednesday));
        database.Db.Orders.Add(NewOrder(Friday, 14, OrderStatus.Approved, weights: [20m]));
        await database.Db.SaveChangesAsync();

        var days = await CreateService(database).GetDaysWithFreeTimesAsync(new DateOnly(2026, 11, 13), new DateOnly(2026, 11, 22));

        Assert.Equal(new[] { Tuesday, new DateOnly(2026, 11, 19), new DateOnly(2026, 11, 21) }, days);
    }

    [PostgresFact]
    public async Task FreeDays_ReturnsTimesAndRespectsTheRequestedWeight()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.Db.Orders.Add(NewOrder(Tuesday, 15, OrderStatus.Approved, weights: [18m]));
        await database.Db.SaveChangesAsync();
        var service = CreateService(database);

        var anyWeight = await service.GetFreeDaysAsync(Tuesday, Wednesday);
        var heavy = await service.GetFreeDaysAsync(Tuesday, Wednesday, requestedKg: 5m);

        Assert.Equal(new[] { Tuesday, Wednesday }, anyWeight.Select(d => d.Date));
        Assert.Equal(Hours(14, 16, 17, 18, 19), anyWeight[0].Times);
        Assert.Equal(Wednesday, Assert.Single(heavy).Date);
    }

    [PostgresFact]
    public async Task Today_UsesBrasiliaTimeInsteadOfUtc()
    {
        await using var database = await TestDatabase.CreateAsync();
        // 01:30 UTC de sábado = 22:30 de sexta em Brasília.
        var service = CreateService(database, new DateTimeOffset(2026, 11, 14, 1, 30, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 11, 13), service.Today);
    }

    [PostgresFact]
    public async Task DaysWithFreeTimes_WithRangeLongerThanOneYear_Throws()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.GetDaysWithFreeTimesAsync(Tuesday, Tuesday.AddDays(400)));
    }

    [PostgresFact]
    public async Task FindApprovalConflict_WithTheSlotTakenByAnotherOrder_ReturnsReason()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.Db.Orders.Add(NewOrder(Wednesday, 15, OrderStatus.Approved));
        await database.Db.SaveChangesAsync();

        var reason = await CreateService(database)
            .FindApprovalConflictAsync(Guid.NewGuid(), Wednesday, new TimeOnly(15, 0), 2m, false);

        Assert.Equal("Já existe um pedido aprovado nesse horário.", reason);
    }

    [PostgresFact]
    public async Task FindApprovalConflict_NeverCountsTheOrderItself()
    {
        await using var database = await TestDatabase.CreateAsync();
        var order = NewOrder(Wednesday, 15, OrderStatus.Approved);
        database.Db.Orders.Add(order);
        await database.Db.SaveChangesAsync();

        var reason = await CreateService(database)
            .FindApprovalConflictAsync(order.Id, Wednesday, new TimeOnly(15, 0), 2m, false);

        Assert.Null(reason);
    }

    // ---------- Prazo do sinal ----------

    /// <summary>Pedido aprovado com sinal a pagar, <paramref name="hoursAgo"/> horas antes de "agora" (prazo de 5 horas).</summary>
    private static Order ApprovedWithDeposit(DateOnly date, int hour, double hoursAgo, decimal weightKg = 2m)
    {
        var order = TestData.NewOrder(date: date, time: new TimeOnly(hour, 0), items: [TestData.NewItem(weightKg)]);
        order.Approve(100m, 0m, 50m, false, FridayNoon.UtcDateTime.AddHours(-hoursAgo));
        return order;
    }

    [PostgresFact]
    public async Task FreeTimes_IgnoresAnApprovedOrderWhoseDepositDeadlinePassed()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.Db.Orders.Add(ApprovedWithDeposit(Wednesday, 15, hoursAgo: 6)); // venceu há 1 hora
        await database.Db.SaveChangesAsync();

        var times = await CreateService(database).GetFreeTimesAsync(Wednesday);

        Assert.Contains(new TimeOnly(15, 0), times);
    }

    [PostgresFact]
    public async Task FreeTimes_StillBlocksAnApprovedOrderInsideTheDeadline()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.Db.Orders.Add(ApprovedWithDeposit(Wednesday, 15, hoursAgo: 2)); // ainda faltam 3 horas
        await database.Db.SaveChangesAsync();

        var times = await CreateService(database).GetFreeTimesAsync(Wednesday);

        Assert.DoesNotContain(new TimeOnly(15, 0), times);
    }

    // O prazo só vale para quem ainda não pagou: com o sinal confirmado, o horário é dele para sempre.
    [PostgresFact]
    public async Task FreeTimes_StillBlocksAConfirmedOrderEvenAfterTheOriginalDeadline()
    {
        await using var database = await TestDatabase.CreateAsync();
        var order = ApprovedWithDeposit(Wednesday, 15, hoursAgo: 10);
        order.ConfirmDeposit(FridayNoon.UtcDateTime.AddHours(-9));
        database.Db.Orders.Add(order);
        await database.Db.SaveChangesAsync();

        var times = await CreateService(database).GetFreeTimesAsync(Wednesday);

        Assert.DoesNotContain(new TimeOnly(15, 0), times);
    }

    [PostgresFact]
    public async Task FreeTimes_AnOverdueOrderNoLongerCountsAgainstTheDailyCapacity()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.Db.Orders.Add(ApprovedWithDeposit(Wednesday, 14, hoursAgo: 6, weightKg: 19m)); // venceu: não conta os 19 kg
        await database.Db.SaveChangesAsync();

        var times = await CreateService(database).GetFreeTimesAsync(Wednesday, requestedKg: 5m);

        Assert.NotEmpty(times);
    }

    [PostgresFact]
    public async Task FreeTimes_AnExpiredOrderDoesNotBlock()
    {
        await using var database = await TestDatabase.CreateAsync();
        var order = ApprovedWithDeposit(Wednesday, 15, hoursAgo: 6);
        order.Expire(FridayNoon.UtcDateTime);
        database.Db.Orders.Add(order);
        await database.Db.SaveChangesAsync();

        Assert.Contains(new TimeOnly(15, 0), await CreateService(database).GetFreeTimesAsync(Wednesday));
    }

    [PostgresFact]
    public async Task FindApprovalConflict_IgnoresAnOverdueOrderInTheSameSlot()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.Db.Orders.Add(ApprovedWithDeposit(Wednesday, 15, hoursAgo: 6));
        await database.Db.SaveChangesAsync();

        var reason = await CreateService(database)
            .FindApprovalConflictAsync(Guid.NewGuid(), Wednesday, new TimeOnly(15, 0), 2m, false);

        Assert.Null(reason);
    }

    // O padrão exige 3 dias de antecedência: o sábado de amanhã não é oferecido ao cliente, mas a dona ainda pode aprovar.
    [PostgresFact]
    public async Task FindApprovalConflict_AllowsADateInsideTheMinimumLeadTime()
    {
        await using var database = await TestDatabase.CreateAsync();
        var saturday = new DateOnly(2026, 11, 14); // amanhã, aberto das 8h às 18h

        Assert.Empty(await CreateService(database).GetFreeTimesAsync(saturday));
        Assert.Null(await CreateService(database)
            .FindApprovalConflictAsync(Guid.NewGuid(), saturday, new TimeOnly(10, 0), 2m, false));
    }
}
