using CakeBoss.Backend.Features.Schedule;
using CakeBoss.Backend.Shared;

namespace CakeBoss.Backend.Tests.Schedule;

public class OpeningHourTests
{
    [Fact]
    public void Create_WithValidWindow_Succeeds()
    {
        var hour = OpeningHour.Create(DayOfWeek.Tuesday, new TimeOnly(14, 0), new TimeOnly(20, 0));

        Assert.Equal(DayOfWeek.Tuesday, hour.DayOfWeek);
        Assert.Equal(new TimeOnly(14, 0), hour.OpensAt);
        Assert.Equal(new TimeOnly(20, 0), hour.ClosesAt);
    }

    [Theory]
    [InlineData(14, 14)]
    [InlineData(20, 14)]
    public void Create_WithOpeningNotBeforeClosing_Throws(int opens, int closes)
    {
        Assert.Throws<DomainException>(() =>
            OpeningHour.Create(DayOfWeek.Tuesday, new TimeOnly(opens, 0), new TimeOnly(closes, 0)));
    }

    [Fact]
    public void Update_WithInvalidWindow_KeepsPreviousValues()
    {
        var hour = OpeningHour.Create(DayOfWeek.Friday, new TimeOnly(14, 0), new TimeOnly(20, 0));

        Assert.Throws<DomainException>(() => hour.Update(new TimeOnly(21, 0), new TimeOnly(20, 0)));

        Assert.Equal(new TimeOnly(14, 0), hour.OpensAt);
        Assert.Equal(new TimeOnly(20, 0), hour.ClosesAt);
    }

    [Fact]
    public void Update_WithValidWindow_ChangesTimes()
    {
        var hour = OpeningHour.Create(DayOfWeek.Saturday, new TimeOnly(8, 0), new TimeOnly(18, 0));

        hour.Update(new TimeOnly(9, 0), new TimeOnly(16, 0));

        Assert.Equal(new TimeOnly(9, 0), hour.OpensAt);
        Assert.Equal(new TimeOnly(16, 0), hour.ClosesAt);
    }
}

public class ScheduleBlockTests
{
    private static readonly DateOnly Start = new(2026, 12, 24);
    private static readonly DateOnly End = new(2026, 12, 26);

    [Fact]
    public void Create_WithEndDateBeforeStartDate_Throws()
    {
        Assert.Throws<DomainException>(() => ScheduleBlock.Create(End, Start));
    }

    [Fact]
    public void Create_WithOnlyOneTime_Throws()
    {
        Assert.Throws<DomainException>(() => ScheduleBlock.Create(Start, End, new TimeOnly(10, 0), null));
        Assert.Throws<DomainException>(() => ScheduleBlock.Create(Start, End, null, new TimeOnly(10, 0)));
    }

    [Theory]
    [InlineData(12, 12)]
    [InlineData(14, 12)]
    public void Create_WithStartTimeNotBeforeEndTime_Throws(int start, int end)
    {
        Assert.Throws<DomainException>(() =>
            ScheduleBlock.Create(Start, End, new TimeOnly(start, 0), new TimeOnly(end, 0)));
    }

    [Fact]
    public void Create_TrimsReason()
    {
        var block = ScheduleBlock.Create(Start, End, reason: "  Férias  ");

        Assert.Equal("Férias", block.Reason);
    }

    [Fact]
    public void Create_WithSingleDay_Succeeds()
    {
        var block = ScheduleBlock.Create(Start, Start);

        Assert.Equal(Start, block.StartDate);
        Assert.Equal(Start, block.EndDate);
    }

    [Theory]
    [InlineData(2026, 12, 23, false)]
    [InlineData(2026, 12, 24, true)]
    [InlineData(2026, 12, 25, true)]
    [InlineData(2026, 12, 26, true)]
    [InlineData(2026, 12, 27, false)]
    public void Covers_WholeDayBlock_CoversEveryHourOfEveryDayInRange(int year, int month, int day, bool expected)
    {
        var block = ScheduleBlock.Create(Start, End);

        Assert.Equal(expected, block.Covers(new DateOnly(year, month, day), new TimeOnly(15, 0)));
    }

    [Theory]
    [InlineData(13, 59, false)]
    [InlineData(14, 0, true)]
    [InlineData(15, 30, true)]
    [InlineData(16, 0, false)]
    public void Covers_TimeRangeBlock_IncludesStartAndExcludesEnd(int hour, int minute, bool expected)
    {
        var block = ScheduleBlock.Create(Start, Start, new TimeOnly(14, 0), new TimeOnly(16, 0));

        Assert.Equal(expected, block.Covers(Start, new TimeOnly(hour, minute)));
    }

    [Fact]
    public void Covers_TimeRangeBlock_AppliesToEveryDayInRange()
    {
        var block = ScheduleBlock.Create(Start, End, new TimeOnly(14, 0), new TimeOnly(16, 0));

        Assert.True(block.Covers(End, new TimeOnly(15, 0)));
        Assert.False(block.Covers(End, new TimeOnly(17, 0)));
    }
}

public class ScheduleSettingsTests
{
    [Fact]
    public void CreateDefault_UsesAgreedValues()
    {
        var settings = ScheduleSettings.CreateDefault();

        Assert.Equal(ScheduleSettings.SingletonId, settings.Id);
        Assert.Equal(60, settings.SlotMinutes);
        Assert.Equal(3, settings.MinLeadDays);
        Assert.Equal(20m, settings.MaxKgPerDay);
    }

    [Fact]
    public void Update_WithValidValues_ChangesSettings()
    {
        var settings = ScheduleSettings.CreateDefault();

        settings.Update(30, 5, 12.5m);

        Assert.Equal(30, settings.SlotMinutes);
        Assert.Equal(5, settings.MinLeadDays);
        Assert.Equal(12.5m, settings.MaxKgPerDay);
    }

    [Theory]
    [InlineData(14)]
    [InlineData(481)]
    public void Update_WithInvalidSlotMinutes_Throws(int slotMinutes)
    {
        var settings = ScheduleSettings.CreateDefault();

        Assert.Throws<DomainException>(() => settings.Update(slotMinutes, 3, 20m));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(366)]
    public void Update_WithInvalidLeadDays_Throws(int leadDays)
    {
        var settings = ScheduleSettings.CreateDefault();

        Assert.Throws<DomainException>(() => settings.Update(60, leadDays, 20m));
    }

    [Fact]
    public void Update_WithZeroLeadDays_AllowsSameDayOrders()
    {
        var settings = ScheduleSettings.CreateDefault();

        settings.Update(60, 0, 20m);

        Assert.Equal(0, settings.MinLeadDays);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1000.01)]
    public void Update_WithInvalidMaxKg_Throws(double maxKg)
    {
        var settings = ScheduleSettings.CreateDefault();

        Assert.Throws<DomainException>(() => settings.Update(60, 3, (decimal)maxKg));
    }

    [Fact]
    public void Update_WithInvalidValues_KeepsPreviousValues()
    {
        var settings = ScheduleSettings.CreateDefault();

        Assert.Throws<DomainException>(() => settings.Update(5, 3, 20m));

        Assert.Equal(60, settings.SlotMinutes);
        Assert.Equal(20m, settings.MaxKgPerDay);
    }
}
