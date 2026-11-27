using System.Globalization;
using CakeBoss.Backend.Features.Orders;
using CakeBoss.Frontend.Site;

namespace CakeBoss.Frontend.Tests.Site;

public class FormOptionsTests
{
    [Theory]
    [InlineData(0, "R$ 0,00")]
    [InlineData(80, "R$ 80,00")]
    [InlineData(150.5, "R$ 150,50")]
    [InlineData(1234.5, "R$ 1.234,50")]
    [InlineData(1000000, "R$ 1.000.000,00")]
    public void FormatMoney_UsesTheBrazilianFormat(double value, string expected)
    {
        Assert.Equal(expected, FormOptions.FormatMoney((decimal)value));
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en-US")]
    public void FormatMoney_DoesNotDependOnTheServerCulture(string culture)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            Assert.Equal("R$ 1.234,50", FormOptions.FormatMoney(1234.5m));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData(2.5, "2,5 kg")]
    [InlineData(3, "3 kg")]
    [InlineData(0.75, "0,75 kg")]
    public void FormatWeight_UsesCommaAndKg(double kg, string expected)
    {
        Assert.Equal(expected, FormOptions.FormatWeight((decimal)kg));
    }

    // Todo status precisa de nome e explicação em português (nunca o nome do enum em inglês).
    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Approved)]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.Rejected)]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Expired)]
    public void EveryStatus_HasAPortugueseLabelAndDescription(OrderStatus status)
    {
        Assert.NotEqual(status.ToString(), FormOptions.StatusLabel(status));
        Assert.False(string.IsNullOrWhiteSpace(FormOptions.StatusDescription(status)));
    }

    // Telefone guardado só com dígitos; o painel mostra no formato que a dona reconhece.
    [Theory]
    [InlineData("11912345678", "(11) 91234-5678")]
    [InlineData("1133334444", "(11) 3333-4444")]
    [InlineData("12345", "12345")]
    [InlineData("119123abc78", "119123abc78")]
    [InlineData("", "")]
    public void FormatPhone_UsesTheBrazilianFormat_AndLeavesOddValuesAlone(string digits, string expected)
    {
        Assert.Equal(expected, FormOptions.FormatPhone(digits));
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Approved)]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.Rejected)]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Expired)]
    public void EveryStatus_HasAShortPortugueseLabelForThePanel(OrderStatus status)
    {
        var label = FormOptions.PanelStatusLabel(status);

        Assert.NotEqual(status.ToString(), label);
        Assert.InRange(label.Length, 1, 20); // cabe no cartão da lista
    }

    [Fact]
    public void StatusTests_CoverEveryStatus()
    {
        Assert.Equal(7, Enum.GetValues<OrderStatus>().Length);
    }

    // Os instantes vêm em UTC; Brasília é UTC-3. "Agora" é quinta-feira, 26/11/2026, 12:00 em Brasília.
    private static readonly DateTime NowUtc = new(2026, 11, 26, 15, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(2026, 11, 26, 21, 30, "hoje às 18:30")]
    [InlineData(2026, 11, 27, 13, 0, "amanhã às 10:00")]
    [InlineData(2026, 11, 28, 13, 0, "sábado, 28/11 às 10:00")]
    public void FormatDeadline_UsesBrasiliaTime_AndSaysTodayOrTomorrow(int y, int mo, int d, int h, int mi, string expected)
    {
        var dueUtc = new DateTime(y, mo, d, h, mi, 0, DateTimeKind.Utc);

        Assert.Equal(expected, FormOptions.FormatDeadline(dueUtc, NowUtc));
    }

    // Já é dia 27 em UTC, mas ainda é dia 26 em Brasília: "hoje" segue o horário de Brasília.
    [Fact]
    public void FormatDeadline_CountsTodayInBrasilia_NotInUtc()
    {
        var nowUtc = new DateTime(2026, 11, 27, 1, 0, 0, DateTimeKind.Utc); // 26/11, 22:00 em Brasília
        var dueUtc = new DateTime(2026, 11, 27, 2, 30, 0, DateTimeKind.Utc); // 26/11, 23:30 em Brasília

        Assert.Equal("hoje às 23:30", FormOptions.FormatDeadline(dueUtc, nowUtc));
    }
}
