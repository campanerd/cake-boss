using System.Globalization;
using CakeBoss.Backend.Features.Orders;

namespace CakeBoss.Frontend.Site;

/// <summary>
/// Sugestões e rótulos do formulário e das páginas do pedido. As sugestões só ajudam a preencher: o cliente pode escrever outro sabor.
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

    /// <summary>Nome do status para o cliente.</summary>
    public static string StatusLabel(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "Aguardando a confeiteira",
        OrderStatus.Approved => "Aprovado, aguardando o sinal",
        OrderStatus.Confirmed => "Confirmado",
        OrderStatus.Completed => "Concluído",
        OrderStatus.Rejected => "Recusado",
        OrderStatus.Cancelled => "Cancelado",
        OrderStatus.Expired => "Prazo do sinal encerrado",
        _ => status.ToString()
    };

    /// <summary>O que o cliente deve esperar em cada status.</summary>
    public static string StatusDescription(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "Recebemos o seu pedido. A confeiteira vai chamar você no WhatsApp para combinar o valor e o sinal.",
        OrderStatus.Approved => "O pedido foi aprovado. Pague o sinal por Pix, como combinado, para garantir a data.",
        OrderStatus.Confirmed => "Sinal recebido: a data está garantida. Agora é com a gente!",
        OrderStatus.Completed => "Pedido entregue. Obrigada pela preferência!",
        OrderStatus.Rejected => "Infelizmente a confeiteira não pôde aceitar este pedido.",
        OrderStatus.Cancelled => "Este pedido foi cancelado.",
        OrderStatus.Expired => "O prazo para pagar o sinal acabou e o horário foi liberado. Se ainda quiser este bolo, fale com a confeiteira pelo WhatsApp.",
        _ => ""
    };

    /// <summary>Ex.: "sexta-feira, 27/11/2026". Feito à mão para não depender dos dados de cultura do servidor.</summary>
    public static string FormatDate(DateOnly date) => $"{WeekDays[(int)date.DayOfWeek]}, {date:dd/MM/yyyy}";

    /// <summary>Ex.: "R$ 1.234,50". Também feito à mão, pelo mesmo motivo.</summary>
    public static string FormatMoney(decimal value)
    {
        var text = value.ToString("#,##0.00", CultureInfo.InvariantCulture);
        return "R$ " + text.Replace(',', '_').Replace('.', ',').Replace('_', '.');
    }

    /// <summary>Nome do status para a dona, no painel. Curto, para caber no cartão da lista.</summary>
    public static string PanelStatusLabel(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "Pendente",
        OrderStatus.Approved => "Aguardando sinal",
        OrderStatus.Confirmed => "Confirmado",
        OrderStatus.Completed => "Concluído",
        OrderStatus.Rejected => "Recusado",
        OrderStatus.Cancelled => "Cancelado",
        OrderStatus.Expired => "Expirado",
        _ => status.ToString()
    };

    /// <summary>Ex.: "(11) 91234-5678". O telefone vem só com os dígitos (DDD e número); fora do padrão, devolve como veio.</summary>
    public static string FormatPhone(string digits) => digits switch
    {
        { Length: 11 } when digits.All(char.IsDigit) => $"({digits[..2]}) {digits[2..7]}-{digits[7..]}",
        { Length: 10 } when digits.All(char.IsDigit) => $"({digits[..2]}) {digits[2..6]}-{digits[6..]}",
        _ => digits
    };

    /// <summary>Ex.: "2,5 kg".</summary>
    public static string FormatWeight(decimal kg) =>
        kg.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',') + " kg";
}
