namespace CakeBoss.Frontend.Site;

/// <summary>
/// Dados da confeitaria que aparecem no site (nome, frase, horário e contatos).
/// Vêm da seção "Site" do appsettings: trocar um valor ali não exige mexer em nenhuma página.
/// </summary>
public sealed class SiteOptions
{
    public const string Section = "Site";

    public string Name { get; set; } = "Confeitaria";

    /// <summary>Frase curta que acompanha o nome no cabeçalho.</summary>
    public string Tagline { get; set; } = "";

    /// <summary>Texto livre do horário de atendimento, exibido no rodapé.</summary>
    public string OpeningHours { get; set; } = "";

    /// <summary>Só dígitos, com DDI e DDD (ex.: 5511912345678). Vazio esconde os botões de WhatsApp.</summary>
    public string? WhatsAppNumber { get; set; }

    /// <summary>Usuário do Instagram, sem o @. Vazio esconde o link.</summary>
    public string? InstagramUser { get; set; }

    /// <summary>
    /// Liga o formulário de pedido. Em produção fica desligado até existir o painel da dona:
    /// sem ele, um pedido de verdade cairia no banco sem ninguém para vê-lo.
    /// </summary>
    public bool OrdersEnabled { get; set; }

    public string? WhatsAppLink =>
        string.IsNullOrWhiteSpace(WhatsAppNumber) ? null : $"https://wa.me/{WhatsAppNumber}";

    public string? InstagramLink =>
        string.IsNullOrWhiteSpace(InstagramUser) ? null : $"https://www.instagram.com/{InstagramUser.TrimStart('@')}/";
}
