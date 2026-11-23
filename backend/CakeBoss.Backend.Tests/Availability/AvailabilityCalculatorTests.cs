using CakeBoss.Backend.Features.Availability;
using CakeBoss.Backend.Features.Schedule;

namespace CakeBoss.Backend.Tests.Availability;

public class AvailabilityCalculatorTests
{
    // Nos testes, "agora" é segunda-feira 2026-11-16, 12:00 em Brasília.
    // Expediente: terça a sexta 14h–20h, sábado 8h–18h. Por padrão do teste: janela de 1h, 1 dia de antecedência, 20 kg por dia.
    private static readonly DateTime MondayNoon = new(2026, 11, 16, 12, 0, 0);
    private static readonly DateOnly Monday = new(2026, 11, 16);
    private static readonly DateOnly Tuesday = new(2026, 11, 17);
    private static readonly DateOnly Wednesday = new(2026, 11, 18);
    private static readonly DateOnly Thursday = new(2026, 11, 19);
    private static readonly DateOnly Friday = new(2026, 11, 20);
    private static readonly DateOnly Saturday = new(2026, 11, 21);
    private static readonly DateOnly Sunday = new(2026, 11, 22);
    private static readonly DateOnly NextMonday = new(2026, 11, 23);

    private static readonly OpeningHour[] BusinessHours =
    [
        OpeningHour.Create(DayOfWeek.Tuesday, new TimeOnly(14, 0), new TimeOnly(20, 0)),
        OpeningHour.Create(DayOfWeek.Wednesday, new TimeOnly(14, 0), new TimeOnly(20, 0)),
        OpeningHour.Create(DayOfWeek.Thursday, new TimeOnly(14, 0), new TimeOnly(20, 0)),
        OpeningHour.Create(DayOfWeek.Friday, new TimeOnly(14, 0), new TimeOnly(20, 0)),
        OpeningHour.Create(DayOfWeek.Saturday, new TimeOnly(8, 0), new TimeOnly(18, 0))
    ];

    private static ScheduleSettings Settings(int slotMinutes = 60, int leadDays = 1, decimal maxKgPerDay = 20m)
    {
        var settings = ScheduleSettings.CreateDefault();
        settings.Update(slotMinutes, leadDays, maxKgPerDay);
        return settings;
    }

    private static AvailabilityInput Input(
        DateTime? now = null,
        ScheduleSettings? settings = null,
        ScheduleBlock[]? blocks = null,
        OccupiedSlot[]? occupied = null) =>
        new(
            now ?? MondayNoon,
            settings ?? Settings(),
            BusinessHours,
            blocks ?? Array.Empty<ScheduleBlock>(),
            occupied ?? Array.Empty<OccupiedSlot>());

    private static TimeOnly[] Hours(params int[] hours) => hours.Select(h => new TimeOnly(h, 0)).ToArray();

    private static OccupiedSlot Occupy(DateOnly date, int hour, decimal weightKg = 2m, bool wholeDay = false) =>
        new(date, new TimeOnly(hour, 0), weightKg, wholeDay);

    // ---------- Expediente ----------

    [Fact]
    public void FreeTimes_OnTuesday_ReturnsSixSlotsFrom14To19()
    {
        var times = AvailabilityCalculator.FreeTimes(Input(), Tuesday);

        Assert.Equal(Hours(14, 15, 16, 17, 18, 19), times);
    }

    [Fact]
    public void FreeTimes_OnSaturday_ReturnsTenSlotsFrom8To17()
    {
        var times = AvailabilityCalculator.FreeTimes(Input(), Saturday);

        Assert.Equal(Hours(8, 9, 10, 11, 12, 13, 14, 15, 16, 17), times);
    }

    [Theory]
    [InlineData(22)]
    [InlineData(23)]
    public void FreeTimes_OnClosedDays_ReturnsEmpty(int day)
    {
        Assert.Empty(AvailabilityCalculator.FreeTimes(Input(), new DateOnly(2026, 11, day)));
    }

    [Fact]
    public void FreeTimes_WithSlotOf90Minutes_OnlyKeepsSlotsThatFitBeforeClosing()
    {
        var times = AvailabilityCalculator.FreeTimes(Input(settings: Settings(slotMinutes: 90)), Tuesday);

        Assert.Equal(
            new[] { new TimeOnly(14, 0), new TimeOnly(15, 30), new TimeOnly(17, 0), new TimeOnly(18, 30) },
            times);
    }

    // ---------- Antecedência ----------

    [Fact]
    public void FreeTimes_WithDefaultSettings_NeedsThreeDaysOfLeadTime()
    {
        var input = Input(settings: ScheduleSettings.CreateDefault());

        Assert.Empty(AvailabilityCalculator.FreeTimes(input, Tuesday));
        Assert.Empty(AvailabilityCalculator.FreeTimes(input, Wednesday));
        Assert.NotEmpty(AvailabilityCalculator.FreeTimes(input, Thursday));
    }

    [Fact]
    public void FreeTimes_WithOneDayLeadTime_BlocksTodayAndAllowsTomorrow()
    {
        var tuesdayNoon = new DateTime(2026, 11, 17, 12, 0, 0);
        var input = Input(now: tuesdayNoon);

        Assert.Empty(AvailabilityCalculator.FreeTimes(input, Tuesday));
        Assert.NotEmpty(AvailabilityCalculator.FreeTimes(input, Wednesday));
    }

    [Fact]
    public void FreeTimes_WithZeroLeadDays_HidesTimesAlreadyPassedToday()
    {
        var tuesdayAfternoon = new DateTime(2026, 11, 17, 16, 30, 0);
        var input = Input(now: tuesdayAfternoon, settings: Settings(leadDays: 0));

        Assert.Equal(Hours(17, 18, 19), AvailabilityCalculator.FreeTimes(input, Tuesday));
    }

    [Fact]
    public void FreeTimes_ForPastDates_ReturnsEmpty()
    {
        var input = Input(settings: Settings(leadDays: 0));

        Assert.Empty(AvailabilityCalculator.FreeTimes(input, new DateOnly(2026, 11, 10)));
    }

    // ---------- Pedidos que ocupam horário ----------

    [Fact]
    public void FreeTimes_RemovesTheOccupiedTime()
    {
        var input = Input(occupied: [Occupy(Tuesday, 15)]);

        Assert.Equal(Hours(14, 16, 17, 18, 19), AvailabilityCalculator.FreeTimes(input, Tuesday));
    }

    [Fact]
    public void FreeTimes_OccupiedTimeOnAnotherDayDoesNotAffectThisDay()
    {
        var input = Input(occupied: [Occupy(Wednesday, 15)]);

        Assert.Equal(6, AvailabilityCalculator.FreeTimes(input, Tuesday).Count);
    }

    // ---------- Capacidade em kg ----------

    [Fact]
    public void FreeTimes_WhenRequestFitsInTheRemainingKg_ReturnsSlots()
    {
        var input = Input(occupied: [Occupy(Tuesday, 14, 18m)]);

        Assert.NotEmpty(AvailabilityCalculator.FreeTimes(input, Tuesday, requestedKg: 2m));
    }

    [Fact]
    public void FreeTimes_WhenRequestExceedsTheRemainingKg_ReturnsEmpty()
    {
        var input = Input(occupied: [Occupy(Tuesday, 14, 18m)]);

        Assert.Empty(AvailabilityCalculator.FreeTimes(input, Tuesday, requestedKg: 2.5m));
    }

    [Fact]
    public void FreeTimes_SumsTheWeightOfAllOccupyingOrders()
    {
        var input = Input(occupied: [Occupy(Tuesday, 14, 10m), Occupy(Tuesday, 16, 8m)]);

        Assert.NotEmpty(AvailabilityCalculator.FreeTimes(input, Tuesday, requestedKg: 2m));
        Assert.Empty(AvailabilityCalculator.FreeTimes(input, Tuesday, requestedKg: 2.01m));
    }

    [Fact]
    public void FreeTimes_WhenCapacityIsExhausted_ReturnsEmptyEvenWithoutAKnownWeight()
    {
        var input = Input(occupied: [Occupy(Tuesday, 14, 20m)]);

        Assert.Empty(AvailabilityCalculator.FreeTimes(input, Tuesday));
    }

    [Fact]
    public void FreeTimes_WithoutAKnownWeight_ShowsDaysThatStillHaveCapacity()
    {
        var input = Input(occupied: [Occupy(Tuesday, 14, 19m)]);

        Assert.NotEmpty(AvailabilityCalculator.FreeTimes(input, Tuesday));
    }

    [Fact]
    public void FreeTimes_UsesTheConfiguredDailyLimit()
    {
        var input = Input(settings: Settings(maxKgPerDay: 5m), occupied: [Occupy(Tuesday, 14, 4m)]);

        Assert.NotEmpty(AvailabilityCalculator.FreeTimes(input, Tuesday, requestedKg: 1m));
        Assert.Empty(AvailabilityCalculator.FreeTimes(input, Tuesday, requestedKg: 1.5m));
    }

    [Fact]
    public void FreeTimes_OrderThatBlocksTheWholeDay_ClosesTheDay()
    {
        var input = Input(occupied: [Occupy(Tuesday, 14, 3m, wholeDay: true)]);

        Assert.Empty(AvailabilityCalculator.FreeTimes(input, Tuesday));
        Assert.NotEmpty(AvailabilityCalculator.FreeTimes(input, Wednesday));
    }

    [Fact]
    public void FreeTimes_WithNegativeWeight_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AvailabilityCalculator.FreeTimes(Input(), Tuesday, -1m));
    }

    // ---------- Bloqueios ----------

    [Fact]
    public void FreeTimes_WithWholeDayBlock_ReturnsEmptyOnlyForBlockedDays()
    {
        var input = Input(blocks: [ScheduleBlock.Create(Tuesday, Wednesday, reason: "Folga")]);

        Assert.Empty(AvailabilityCalculator.FreeTimes(input, Tuesday));
        Assert.Empty(AvailabilityCalculator.FreeTimes(input, Wednesday));
        Assert.NotEmpty(AvailabilityCalculator.FreeTimes(input, Thursday));
    }

    [Fact]
    public void FreeTimes_WithTimeRangeBlock_RemovesOnlyThoseTimes()
    {
        var input = Input(blocks: [ScheduleBlock.Create(Tuesday, Tuesday, new TimeOnly(15, 0), new TimeOnly(17, 0))]);

        Assert.Equal(Hours(14, 17, 18, 19), AvailabilityCalculator.FreeTimes(input, Tuesday));
    }

    // ---------- Calendário ----------

    [Fact]
    public void DaysWithFreeTimes_ReturnsOnlyOpenFutureDays()
    {
        var days = AvailabilityCalculator.DaysWithFreeTimes(Input(), Monday, NextMonday);

        Assert.Equal(new[] { Tuesday, Wednesday, Thursday, Friday, Saturday }, days);
    }

    [Fact]
    public void DaysWithFreeTimes_ExcludesBlockedAndFullDays()
    {
        var input = Input(
            blocks: [ScheduleBlock.Create(Wednesday, Wednesday)],
            occupied: [Occupy(Friday, 14, 20m)]);

        var days = AvailabilityCalculator.DaysWithFreeTimes(input, Monday, Sunday);

        Assert.Equal(new[] { Tuesday, Thursday, Saturday }, days);
    }

    [Fact]
    public void DaysWithFreeTimes_ConsidersTheRequestedWeight()
    {
        var input = Input(occupied: [Occupy(Tuesday, 14, 15m), Occupy(Thursday, 14, 19m)]);

        var days = AvailabilityCalculator.DaysWithFreeTimes(input, Tuesday, Thursday, requestedKg: 4m);

        Assert.Equal(new[] { Tuesday, Wednesday }, days);
    }

    [Fact]
    public void DaysWithFreeTimes_WithInvertedRange_ReturnsEmpty()
    {
        Assert.Empty(AvailabilityCalculator.DaysWithFreeTimes(Input(), Sunday, Monday));
    }

    // ---------- Dias com os horários (calendário do formulário) ----------

    [Fact]
    public void FreeDays_ReturnsEachFreeDayWithItsTimes()
    {
        var input = Input(occupied: [Occupy(Tuesday, 15)]);

        var days = AvailabilityCalculator.FreeDays(input, Monday, Wednesday);

        Assert.Equal(new[] { Tuesday, Wednesday }, days.Select(d => d.Date));
        Assert.Equal(Hours(14, 16, 17, 18, 19), days[0].Times);
        Assert.Equal(Hours(14, 15, 16, 17, 18, 19), days[1].Times);
    }

    [Fact]
    public void FreeDays_LeavesOutDaysThatCannotHoldTheRequestedWeight()
    {
        var input = Input(occupied: [Occupy(Tuesday, 14, 18m)]);

        var days = AvailabilityCalculator.FreeDays(input, Tuesday, Wednesday, requestedKg: 3m);

        Assert.Equal(Wednesday, Assert.Single(days).Date);
    }

    [Fact]
    public void FreeDays_MatchesDaysWithFreeTimes()
    {
        var input = Input(blocks: [ScheduleBlock.Create(Thursday, Thursday)], occupied: [Occupy(Friday, 14, 20m)]);

        var days = AvailabilityCalculator.FreeDays(input, Monday, NextMonday);

        Assert.Equal(AvailabilityCalculator.DaysWithFreeTimes(input, Monday, NextMonday), days.Select(d => d.Date));
    }
}
