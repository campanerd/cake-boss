namespace CakeBoss.Backend.Tests.Support;

/// <summary>Relógio parado em um instante, para testes que dependem de "agora". Só anda se o teste mandar (<see cref="Advance"/>).</summary>
internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private DateTimeOffset now = utcNow;

    public override DateTimeOffset GetUtcNow() => now;

    public void Advance(TimeSpan time) => now += time;
}
