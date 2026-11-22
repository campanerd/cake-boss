using CakeBoss.Backend.Features.Orders;
using CakeBoss.Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CakeBoss.Backend.Features.Availability;

/// <summary>Carrega do banco o que o <see cref="AvailabilityCalculator"/> precisa e delega o cálculo a ele.</summary>
public class AvailabilityService(AppDbContext db, TimeProvider clock)
{
    private const int MaxDaysPerQuery = 366;

    private static readonly Lazy<TimeZoneInfo> BrazilTimeZone = new(ResolveBrazilTimeZone);

    /// <summary>Data de hoje no horário de Brasília.</summary>
    public DateOnly Today => DateOnly.FromDateTime(LocalNow());

    public async Task<IReadOnlyList<TimeOnly>> GetFreeTimesAsync(
        DateOnly date,
        decimal requestedKg = 0,
        CancellationToken ct = default)
    {
        var input = await LoadAsync(date, date, ct);
        return AvailabilityCalculator.FreeTimes(input, date, requestedKg);
    }

    public async Task<bool> IsAvailableAsync(
        DateOnly date,
        TimeOnly time,
        decimal requestedKg = 0,
        CancellationToken ct = default)
    {
        var freeTimes = await GetFreeTimesAsync(date, requestedKg, ct);
        return freeTimes.Contains(time);
    }

    public async Task<IReadOnlyList<DateOnly>> GetDaysWithFreeTimesAsync(
        DateOnly from,
        DateOnly to,
        decimal requestedKg = 0,
        CancellationToken ct = default)
    {
        if (to < from) return [];
        if (to.DayNumber - from.DayNumber > MaxDaysPerQuery)
            throw new ArgumentOutOfRangeException(nameof(to), $"Consulte no máximo {MaxDaysPerQuery} dias por vez.");

        var input = await LoadAsync(from, to, ct);
        return AvailabilityCalculator.DaysWithFreeTimes(input, from, to, requestedKg);
    }

    private DateTime LocalNow() => TimeZoneInfo.ConvertTime(clock.GetUtcNow(), BrazilTimeZone.Value).DateTime;

    private async Task<AvailabilityInput> LoadAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var openingHours = await db.OpeningHours.AsNoTracking().ToListAsync(ct);
        var settings = await db.Settings.AsNoTracking().SingleAsync(ct);

        var blocks = await db.ScheduleBlocks.AsNoTracking()
            .Where(b => b.StartDate <= to && b.EndDate >= from)
            .ToListAsync(ct);

        var blockingStatuses = Order.SlotBlockingStatuses;
        var occupied = await db.Orders.AsNoTracking()
            .Where(o => o.Date >= from && o.Date <= to && blockingStatuses.Contains(o.Status))
            .Select(o => new OccupiedSlot(o.Date, o.Time, o.Items.Sum(i => i.WeightKg), o.BlocksWholeDay))
            .ToListAsync(ct);

        return new AvailabilityInput(LocalNow(), settings, openingHours, blocks, occupied);
    }

    private static TimeZoneInfo ResolveBrazilTimeZone()
    {
        foreach (var id in new[] { "America/Sao_Paulo", "E. South America Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        return TimeZoneInfo.Local;
    }
}
