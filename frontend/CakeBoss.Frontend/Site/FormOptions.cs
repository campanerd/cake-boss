using CakeBoss.Backend.Features.Orders;

namespace CakeBoss.Frontend.Site;

/// <summary>
/// Sugestões e rótulos do formulário de pedido. As sugestões só ajudam a preencher: o cliente pode escrever outro sabor.
/// Quando existir um cardápio editável pela dona, as listas vêm dele.
/// </summary>
public static class FormOptions
{
    public static readonly string[] Batters = ["Chocolate", "Baunilha", "Red velvet", "Cenoura", "Coco"];
    public static readonly string[] Fillings = ["Brigadeiro", "Ninho", "Doce de leite", "Frutas vermelhas", "Maracujá"];
    public static readonly string[] Frostings = ["Chantilly", "Ganache", "Buttercream", "Pasta americana"];

    private static readonly string[] WeekDays =
        ["domingo", "segunda-feira", "terça-feira", "quarta-feira", "quinta-feira", "sexta-feira", "sábado"];

    public static string DeliveryLabel(DeliveryType type) => type switch
    {
        DeliveryType.Pickup => "Retirada na confeitaria",
        DeliveryType.Delivery => "Entrega",
        _ => type.ToString()
    };

    public static string PaymentLabel(PaymentMethod method) => method switch
    {
        PaymentMethod.Pix => "Pix",
        PaymentMethod.Cash => "Dinheiro",
        PaymentMethod.Card => "Cartão",
        _ => method.ToString()
    };

    /// <summary>Ex.: "sexta-feira, 27/11/2026". Feito à mão para não depender dos dados de cultura do servidor.</summary>
    public static string FormatDate(DateOnly date) => $"{WeekDays[(int)date.DayOfWeek]}, {date:dd/MM/yyyy}";
}
