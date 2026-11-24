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
    public void EveryStatus_HasAShortPortugueseLabelForThePanel(OrderStatus status)
    {
        var label = FormOptions.PanelStatusLabel(status);

        Assert.NotEqual(status.ToString(), label);
        Assert.InRange(label.Length, 1, 20); // cabe no cartão da lista
    }

    [Fact]
    public void StatusTests_CoverEveryStatus()
    {
        Assert.Equal(6, Enum.GetValues<OrderStatus>().Length);
    }
}
