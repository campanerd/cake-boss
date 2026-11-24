namespace CakeBoss.Backend.Features.Admin;

/// <summary>
/// Freia quem tenta adivinhar a senha. Conta as falhas seguidas de cada cliente (o frontend passa o IP) e, a partir da
/// <see cref="FreeFailures"/>-ésima, o cliente precisa esperar antes de tentar de novo: 1 s, 2 s, 4 s... até
/// <see cref="MaxDelay"/>. Durante a espera a tentativa é recusada sem nem conferir a senha.
///
/// Por que por cliente e não global: com um único usuário, um bloqueio global deixaria qualquer pessoa trancar a dona
/// para fora só errando a senha de propósito. Assim, quem erra é quem espera.
///
/// Fica na memória do app: um reinício do Render zera as contagens. Aceitável, porque o objetivo é tornar a adivinhação
/// inviável, e a defesa principal continua sendo uma senha longa.
/// </summary>
public sealed class LoginThrottle(TimeProvider clock)
{
    /// <summary>Erros de digitação que passam sem espera.</summary>
    public const int FreeFailures = 3;

    public static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(15);

    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ForgetAfter = TimeSpan.FromHours(1);
    private const int MaxTrackedClients = 10_000;
    private const int MaxKeyLength = 64;

    private readonly object gate = new();
    private readonly Dictionary<string, Entry> entries = [];

    /// <summary>Quanto esse cliente ainda precisa esperar, ou null se pode tentar agora.</summary>
    public TimeSpan? RetryAfter(string? clientKey)
    {
        var key = Normalize(clientKey);
        var now = clock.GetUtcNow();

        lock (gate)
        {
            if (!entries.TryGetValue(key, out var entry)) return null;

            if (IsForgotten(entry, now))
            {
                entries.Remove(key);
                return null;
            }

            return entry.BlockedUntil > now ? entry.BlockedUntil - now : null;
        }
    }

    public void RecordFailure(string? clientKey)
    {
        var key = Normalize(clientKey);
        var now = clock.GetUtcNow();

        lock (gate)
        {
            if (!entries.TryGetValue(key, out var entry) || IsForgotten(entry, now))
            {
                entry = new Entry();
                entries[key] = entry;
            }

            entry.Failures++;
            entry.LastFailureAt = now;
            entry.BlockedUntil = entry.Failures >= FreeFailures
                ? now + DelayFor(entry.Failures)
                : DateTimeOffset.MinValue;

            if (entries.Count > MaxTrackedClients) Prune(now);
        }
    }

    /// <summary>Login certo: o cliente volta a ter todas as tentativas.</summary>
    public void RecordSuccess(string? clientKey)
    {
        lock (gate)
        {
            entries.Remove(Normalize(clientKey));
        }
    }

    /// <summary>Espera depois da N-ésima falha seguida: dobra a cada falha e para no teto.</summary>
    private static TimeSpan DelayFor(int failures)
    {
        var exponent = Math.Clamp(failures - FreeFailures, 0, 20);
        var delay = TimeSpan.FromTicks(BaseDelay.Ticks * (1L << exponent));
        return delay < MaxDelay ? delay : MaxDelay;
    }

    private static bool IsForgotten(Entry entry, DateTimeOffset now) => now - entry.LastFailureAt > ForgetAfter;

    // Limite de memória: primeiro descarta o que já foi esquecido; se ainda passar, os mais antigos.
    private void Prune(DateTimeOffset now)
    {
        foreach (var key in entries.Where(e => IsForgotten(e.Value, now)).Select(e => e.Key).ToList())
            entries.Remove(key);

        var excess = entries.Count - MaxTrackedClients;
        if (excess <= 0) return;

        foreach (var key in entries.OrderBy(e => e.Value.LastFailureAt).Take(excess).Select(e => e.Key).ToList())
            entries.Remove(key);
    }

    private static string Normalize(string? clientKey)
    {
        if (string.IsNullOrWhiteSpace(clientKey)) return "unknown";

        var key = clientKey.Trim();
        return key.Length <= MaxKeyLength ? key : key[..MaxKeyLength];
    }

    private sealed class Entry
    {
        public int Failures;
        public DateTimeOffset LastFailureAt;
        public DateTimeOffset BlockedUntil;
    }
}
