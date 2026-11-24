namespace CakeBoss.Frontend.Site;

/// <summary>
/// Qual versão (commit) do código está no ar. O Render informa o commit na variável RENDER_GIT_COMMIT, e a rota pública
/// <c>/version</c> o devolve para o workflow de deploy confirmar que a versão nova subiu.
/// </summary>
public static class BuildInfo
{
    public const string Unknown = "unknown";

    /// <summary>
    /// Só aceita o que parece um commit do Git (de 7 a 40 caracteres hexadecimais). O valor vem do ambiente e sai numa
    /// rota pública, então qualquer outra coisa vira <see cref="Unknown"/> em vez de ser repetida na resposta.
    /// </summary>
    public static string NormalizeCommit(string? value)
    {
        var commit = value?.Trim().ToLowerInvariant();

        return commit is { Length: >= 7 and <= 40 } && commit.All(Uri.IsHexDigit) ? commit : Unknown;
    }
}
