using System.Globalization;
using CakeBoss.Backend.Features.Ordering;
using CakeBoss.Backend.Features.Orders;
using CakeBoss.Backend.Shared;

namespace CakeBoss.Frontend.Pages.Orders;

/// <summary>
/// O que o formulário público envia. Não valida regra de negócio: só converte os campos para o comando do backend
/// (o domínio recusa o que for inválido, com mensagem em português).
/// </summary>
public sealed class OrderForm
{
    // Contato
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }

    // O bolo
    public string? Batter { get; set; }
    public string? Filling1 { get; set; }
    public string? Filling2 { get; set; }
    public string? Filling3 { get; set; }
    public string? Frosting { get; set; }

    /// <summary>Texto, porque o navegador manda "2.5" e quem digita à mão escreve "2,5".</summary>
    public string? WeightKg { get; set; }

    public string? Decoration { get; set; }

    // Retirada ou entrega, data e horário
    public DeliveryType DeliveryType { get; set; } = DeliveryType.Pickup;
    public string? DeliveryAddress { get; set; }
    public DateOnly? Date { get; set; }
    public TimeOnly? Time { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Pix;
    public string? Notes { get; set; }

    /// <summary>Campo escondido que gente não vê: se vier preenchido, quem enviou foi um robô.</summary>
    public string? Website { get; set; }

    /// <exception cref="DomainException">Quando falta a data, o horário ou o peso não é um número.</exception>
    public PlaceOrderCommand ToCommand()
    {
        var date = Date ?? throw new DomainException("Escolha a data do pedido.");
        var time = Time ?? throw new DomainException("Escolha o horário do pedido.");

        var fillings = new[] { Filling1, Filling2, Filling3 }
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .ToList();

        var item = new PlaceOrderItem(Batter, fillings, Frosting, ParseWeight(), Decoration);

        return new PlaceOrderCommand(
            CustomerName,
            CustomerPhone,
            CustomerEmail,
            DeliveryType,
            DeliveryAddress,
            date,
            time,
            PaymentMethod,
            Notes,
            [item],
            []);
    }

    /// <summary>Lê o peso como o cliente digita: "2,5" ou "2.5".</summary>
    public static bool TryParseWeight(string? text, out decimal weight) =>
        decimal.TryParse(
            text?.Trim().Replace(',', '.'),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out weight);

    private decimal ParseWeight()
    {
        if (!TryParseWeight(WeightKg, out var weight))
            throw new DomainException("Informe o tamanho do bolo em kg (por exemplo, 2,5).");

        return weight;
    }
}
