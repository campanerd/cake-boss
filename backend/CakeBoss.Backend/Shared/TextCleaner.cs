namespace CakeBoss.Backend.Shared;

internal static class TextCleaner
{
    /// <summary>Remove espaços nas pontas; texto vazio vira null; acima do limite lança DomainException.</summary>
    public static string? Clean(string? value, int maxLength)
    {
        var cleaned = value?.Trim();
        if (string.IsNullOrEmpty(cleaned)) return null;
        if (cleaned.Length > maxLength)
            throw new DomainException($"Texto muito longo (máximo de {maxLength} caracteres).");
        return cleaned;
    }
}
