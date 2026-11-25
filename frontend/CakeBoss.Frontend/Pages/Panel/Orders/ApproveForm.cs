using System.Globalization;
using System.Text.RegularExpressions;
using CakeBoss.Backend.Features.Management;

namespace CakeBoss.Frontend.Pages.Panel.Orders;

/// <summary>
/// O que a dona digita ao aprovar. Só converte o texto do formulário em comando: as regras (sinal até o total, taxa só na entrega,
/// horário livre...) ficam no backend.
/// </summary>
public sealed partial class ApproveForm
{
    /// <summary>Maior valor que cabe na coluna do banco (10 dígitos, 2 casas).</summary>
    private const decimal MaxMoney = 99_999_999.99m;

    public string? Price { get; set; }
    public string? DeliveryFee { get; set; }
    public string? Deposit { get; set; }
    public bool BlocksWholeDay { get; set; }

    /// <summary>
    /// Monta o comando. Devolve false, com a mensagem em <paramref name="error"/>, se algum valor não for um número.
    /// Campo vazio vale zero; o backend recusa o valor do pedido zerado com a mensagem certa.
    /// </summary>
    public bool TryToCommand(Guid orderId, out ApproveOrderCommand? command, out string? error)
    {
        command = null;

        if (!TryParseMoney(Price, out var price))
            return Fail("o valor do bolo", out error);
        if (!TryParseMoney(DeliveryFee, out var deliveryFee))
            return Fail("a taxa de entrega", out error);
        if (!TryParseMoney(Deposit, out var deposit))
            return Fail("o sinal", out error);

        command = new ApproveOrderCommand(orderId, price, deliveryFee, deposit, BlocksWholeDay);
        error = null;
        return true;
    }

    /// <summary>
    /// Lê um valor como se digita no Brasil: "150", "150,50", "1.500,00", "R$ 150". O ponto sozinho com 1 ou 2 dígitos depois
    /// ("150.50") é decimal; com 3 dígitos ("1.500") é separador de milhar. Em branco vale zero. Não aceita sinal, mais de
    /// 2 casas decimais nem valor acima do que o banco guarda.
    /// </summary>
    public static bool TryParseMoney(string? text, out decimal value)
    {
        value = 0m;

        var cleaned = (text ?? "").Replace("R$", "", StringComparison.OrdinalIgnoreCase).Replace(" ", "").Trim();
        if (cleaned.Length == 0) return true;

        if (cleaned.Contains(','))
            cleaned = cleaned.Replace(".", "").Replace(',', '.');
        else if (!DecimalPoint().IsMatch(cleaned))
            cleaned = cleaned.Replace(".", "");

        if (!decimal.TryParse(cleaned, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed))
            return false;

        if (decimal.Round(parsed, 2) != parsed || parsed > MaxMoney) return false;

        value = parsed;
        return true;
    }

    private static bool Fail(string what, out string? error)
    {
        error = $"Não entendi {what}. Use só números, como 150 ou 150,50.";
        return false;
    }

    // "150.5" ou "150.50": um único ponto seguido de 1 ou 2 dígitos no fim.
    [GeneratedRegex(@"^\d*\.\d{1,2}$")]
    private static partial Regex DecimalPoint();
}
