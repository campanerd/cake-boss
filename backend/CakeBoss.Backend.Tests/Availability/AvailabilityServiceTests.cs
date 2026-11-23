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
}
