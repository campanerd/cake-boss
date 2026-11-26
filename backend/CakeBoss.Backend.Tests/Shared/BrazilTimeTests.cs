using CakeBoss.Backend.Shared;
using CakeBoss.Backend.Tests.Support;

namespace CakeBoss.Backend.Tests.Shared;

public class BrazilTimeTests
{
    // O Brasil não tem mais horário de verão: Brasília é sempre UTC-3.
    [Theory]
    [InlineData(2026, 11, 18, 15, 0, 2026, 11, 18, 12, 0)]
    [InlineData(2026, 1, 10, 15, 0, 2026, 1, 10, 12, 0)]
    [InlineData(2026, 7, 10, 15, 0, 2026, 7, 10, 12, 0)]
    public void FromUtc_SubtractsThreeHours_AllYear(
        int y, int mo, int d, int h, int mi, int ey, int emo, int ed, int eh, int emi)
    {
        var result = BrazilTime.FromUtc(new DateTime(y, mo, d, h, mi, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(ey, emo, ed, eh, emi, 0), result);
    }

    [Fact]
    public void FromUtc_CrossesMidnightBackwards()
    {
        var result = BrazilTime.FromUtc(new DateTime(2026, 11, 26, 1, 30, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 11, 25, 22, 30, 0), result);
    }

    // O banco devolve o instante com Kind Utc, mas um valor sem Kind (Unspecified) também é tratado como UTC.
    [Fact]
    public void FromUtc_TreatsAnUnspecifiedKindAsUtc()
    {
        var result = BrazilTime.FromUtc(new DateTime(2026, 11, 18, 15, 0, 0, DateTimeKind.Unspecified));

        Assert.Equal(new DateTime(2026, 11, 18, 12, 0, 0), result);
    }

    [Fact]
    public void Now_UsesTheGivenClock()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 18, 15, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateTime(2026, 11, 18, 12, 0, 0), BrazilTime.Now(clock));
    }
}
