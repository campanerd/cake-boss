using CakeBoss.Backend.Features.Admin;
using CakeBoss.Backend.Tests.Support;

namespace CakeBoss.Backend.Tests.Admin;

public class LoginThrottleTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 24, 15, 0, 0, TimeSpan.Zero);

    private readonly FixedTimeProvider clock = new(Start);
    private readonly LoginThrottle throttle;

    public LoginThrottleTests() => throttle = new LoginThrottle(clock);

    private void Fail(string key, int times)
    {
        for (var i = 0; i < times; i++) throttle.RecordFailure(key);
    }

    [Fact]
    public void ANewClient_CanTryRightAway()
    {
        Assert.Null(throttle.RetryAfter("1.2.3.4"));
    }

    [Fact]
    public void TypingMistakes_BelowTheLimit_DoNotMakeTheClientWait()
    {
        Fail("1.2.3.4", LoginThrottle.FreeFailures - 1);

        Assert.Null(throttle.RetryAfter("1.2.3.4"));
    }

    [Fact]
    public void FromTheThirdFailure_TheClientMustWaitOneSecond()
    {
        Fail("1.2.3.4", LoginThrottle.FreeFailures);

        Assert.Equal(TimeSpan.FromSeconds(1), throttle.RetryAfter("1.2.3.4"));
    }

    [Fact]
    public void TheWaitDoublesWithEachFailure()
    {
        Fail("1.2.3.4", 3);
        Assert.Equal(TimeSpan.FromSeconds(1), throttle.RetryAfter("1.2.3.4"));

        throttle.RecordFailure("1.2.3.4");
        Assert.Equal(TimeSpan.FromSeconds(2), throttle.RetryAfter("1.2.3.4"));

        throttle.RecordFailure("1.2.3.4");
        Assert.Equal(TimeSpan.FromSeconds(4), throttle.RetryAfter("1.2.3.4"));

        throttle.RecordFailure("1.2.3.4");
        Assert.Equal(TimeSpan.FromSeconds(8), throttle.RetryAfter("1.2.3.4"));
    }

    [Fact]
    public void TheWaitShrinksAsTimePasses_AndEndsAtZero()
    {
        Fail("1.2.3.4", 5); // 4 s de espera

        clock.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal(TimeSpan.FromSeconds(1), throttle.RetryAfter("1.2.3.4"));

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Null(throttle.RetryAfter("1.2.3.4"));
    }

    [Fact]
    public void TheWaitStopsGrowingAtTheCap()
    {
        Fail("1.2.3.4", 40);

        Assert.Equal(LoginThrottle.MaxDelay, throttle.RetryAfter("1.2.3.4"));
        Assert.Equal(TimeSpan.FromMinutes(15), LoginThrottle.MaxDelay);
    }

    [Fact]
    public void ASuccessfulLogin_GivesAllTheAttemptsBack()
    {
        Fail("1.2.3.4", 6);

        throttle.RecordSuccess("1.2.3.4");

        Assert.Null(throttle.RetryAfter("1.2.3.4"));
        Fail("1.2.3.4", LoginThrottle.FreeFailures - 1); // a contagem recomeçou do zero
        Assert.Null(throttle.RetryAfter("1.2.3.4"));
    }

    [Fact]
    public void EachClientHasItsOwnCount()
    {
        Fail("1.2.3.4", 6);

        Assert.NotNull(throttle.RetryAfter("1.2.3.4"));
        Assert.Null(throttle.RetryAfter("5.6.7.8"));
    }

    [Fact]
    public void AClientThatStopsFailingForAnHour_IsForgiven()
    {
        Fail("1.2.3.4", 10);

        clock.Advance(TimeSpan.FromMinutes(61));

        Assert.Null(throttle.RetryAfter("1.2.3.4"));

        // E a contagem recomeça: dois erros de digitação não esperam nada.
        Fail("1.2.3.4", 2);
        Assert.Null(throttle.RetryAfter("1.2.3.4"));
    }

    [Fact]
    public void WhileWaiting_NothingElseIsCounted_SoTheWaitDoesNotGrowByItself()
    {
        Fail("1.2.3.4", 3);

        var first = throttle.RetryAfter("1.2.3.4");
        var second = throttle.RetryAfter("1.2.3.4");

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankKey_SharesOneBucket_InsteadOfSkippingTheLimit(string? blank)
    {
        Fail(blank!, 3);

        Assert.NotNull(throttle.RetryAfter(null));
        Assert.NotNull(throttle.RetryAfter(""));
    }

    [Fact]
    public void Memory_IsBounded_AndTheOldestClientsAreDroppedFirst()
    {
        Fail("oldest", 6);

        clock.Advance(TimeSpan.FromSeconds(1));
        for (var i = 0; i < 10_000; i++) throttle.RecordFailure($"client-{i}");

        // Passou do limite de clientes guardados: sai o mais antigo.
        Assert.Null(throttle.RetryAfter("oldest"));
    }
}
