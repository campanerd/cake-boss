using System.Globalization;
using CakeBoss.Backend.Features.Orders;

namespace CakeBoss.Backend.Features.Notifications;

/// <summary>O texto pronto e o link do WhatsApp que já abre a conversa com ele preenchido.</summary>
public sealed record WhatsAppMessage(string Text, string Link);

/// <summary>
/// Monta a mensagem que a dona envia ao cliente para negociar o pedido pelo WhatsApp (botão do painel, sem API paga).
/// É lógica pura: só lê o pedido, que precisa vir com o cliente e os itens carregados.
///
/// A mensagem leva o nome do cliente e o pedido (código, data e hora, retirada ou entrega e, por bolo, massa, recheios,
/// cobertura, tamanho e decoração). NUNCA leva preço, sinal nem Pix: isso é combinado na conversa e só depois a dona
/// registra no sistema o que ficou combinado.
/// </summary>
public static class WhatsAppMessageBuilder
{
    /// <summary>A decoração é texto livre do cliente (até 1000 caracteres). Cortada aqui, a mensagem e o link não crescem sem limite.</summary>
    public const int MaxDecorationLength = 300;

    private static readonly string[] WeekDays =
        ["domingo", "segunda-feira", "terça-feira", "quarta-feira", "quinta-feira", "sexta-feira", "sábado"];

    public static WhatsAppMessage ForOrder(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var lines = new List<string>
        {
            $"Olá, {FirstName(order.Customer.Name)}! Recebi o seu pedido pelo site (código {order.Code}) e vamos combinar os detalhes por aqui.",
            "",
            $"*Quando:* {WeekDays[(int)order.Date.DayOfWeek]}, {order.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}, às {order.Time.ToString("HH:mm", CultureInfo.InvariantCulture)}",
            order.DeliveryType == DeliveryType.Delivery
                ? $"*Entrega em:* {order.DeliveryAddress}"
                : "*Retirada* na confeitaria"
        };

        var items = order.Items.ToList();
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            lines.Add("");
            lines.Add(items.Count > 1 ? $"*Bolo {i + 1}*" : "*Bolo*");
            lines.Add($"• Massa: {item.Batter}");
            lines.Add($"• Recheio: {JoinFillings(item.Fillings)}");
            lines.Add($"• Cobertura: {item.Frosting}");
            lines.Add($"• Tamanho: {FormatWeight(item.WeightKg)}");
            lines.Add($"• Decoração: {Truncate(item.Decoration, MaxDecorationLength)}");
        }

        lines.Add("");
        lines.Add("Está tudo certo? Se quiser mudar alguma coisa, é só me avisar.");

        var text = string.Join("\n", lines);

        // O telefone é guardado só com DDD e número; o wa.me pede o código do país (55).
        var link = $"https://wa.me/55{order.Customer.Phone}?text={Uri.EscapeDataString(text)}";

        return new WhatsAppMessage(text, link);
    }

    private static string FirstName(string name) =>
        name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? name;

    // "Brigadeiro", "Brigadeiro e Ninho", "Brigadeiro, Ninho e Maracujá".
    private static string JoinFillings(IReadOnlyList<string> fillings) => fillings.Count switch
    {
        0 => "",
        1 => fillings[0],
        _ => string.Join(", ", fillings.Take(fillings.Count - 1)) + " e " + fillings[^1]
    };

    private static string FormatWeight(decimal kg) =>
        kg.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',') + " kg";

    private static string Truncate(string text, int maxLength)
    {
        var oneLine = string.Join(' ', text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return oneLine.Length <= maxLength ? oneLine : oneLine[..(maxLength - 1)].TrimEnd() + "…";
    }
}
