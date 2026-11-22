namespace CakeBoss.Backend.Tests.Support;

/// <summary>Relógio parado em um instante, para testes que dependem de "agora".</summary>
internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
