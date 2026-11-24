using CakeBoss.Backend.Features.Customers;
using CakeBoss.Backend.Features.Notifications;
using CakeBoss.Backend.Features.Orders;
using CakeBoss.Backend.Tests.Support;

namespace CakeBoss.Backend.Tests.Notifications;

public class WhatsAppMessageBuilderTests
{
    // TestData: Maria Silva, terça-feira 2026-11-24 às 15:00, retirada, 1 bolo de 2 kg.

    private static Order OrderFor(
        Customer customer,
        DeliveryType deliveryType = DeliveryType.Pickup,
        string? address = null,
        params OrderItem[] items) =>
        Order.Create(
            customer,
            deliveryType,
            address,
            new DateOnly(2026, 11, 24),
            new TimeOnly(15, 0),
            PaymentMethod.Pix,
            "Sem nozes",
            items.Length > 0 ? items : [TestData.NewItem()],
            [],
            TestData.NowUtc);

    private static OrderItem Item(decimal kg = 2m, string decoration = "Tema unicórnio", params string[] fillings) =>
        OrderItem.Create("Chocolate", fillings.Length > 0 ? fillings : ["Brigadeiro"], "Chantilly", kg, decoration);

    // ---------- Conteúdo ----------

    [Fact]
    public void Greets_TheCustomerByFirstName_AndMentionsTheOrderCode()
    {
        var order = TestData.NewOrder();

        var message = WhatsAppMessageBuilder.ForOrder(order);

        Assert.StartsWith("Olá, Maria!", message.Text);
        Assert.DoesNotContain("Silva", message.Text);
        Assert.Contains($"(código {order.Code})", message.Text);
    }

    [Fact]
    public void WithOnlyOneName_UsesIt()
    {
        var order = OrderFor(Customer.Create("Ana", "11912345678", null, TestData.NowUtc));

        Assert.StartsWith("Olá, Ana!", WhatsAppMessageBuilder.ForOrder(order).Text);
    }

    [Fact]
    public void ForPickup_SaysTheDateWithTheWeekDay_AndThatItIsPickup()
    {
        var text = WhatsAppMessageBuilder.ForOrder(TestData.NewOrder()).Text;

        Assert.Contains("*Quando:* terça-feira, 24/11/2026, às 15:00", text);
        Assert.Contains("*Retirada* na confeitaria", text);
        Assert.DoesNotContain("Entrega em", text);
    }

    [Fact]
    public void ForDelivery_IncludesTheAddress()
    {
        var order = TestData.NewOrder(DeliveryType.Delivery, "Rua das Flores, 10, apto 3");

        var text = WhatsAppMessageBuilder.ForOrder(order).Text;

        Assert.Contains("*Entrega em:* Rua das Flores, 10, apto 3", text);
        Assert.DoesNotContain("Retirada", text);
    }

    [Fact]
    public void DescribesTheCake_WithBatterFillingsFrostingSizeAndDecoration()
    {
        var text = WhatsAppMessageBuilder.ForOrder(TestData.NewOrder()).Text;

        Assert.Contains("*Bolo*", text);
        Assert.Contains("• Massa: Chocolate", text);
        Assert.Contains("• Recheio: Brigadeiro e Ninho com morango", text);
        Assert.Contains("• Cobertura: Chantilly", text);
        Assert.Contains("• Tamanho: 2 kg", text);
        Assert.Contains("• Decoração: Tema unicórnio, tons de rosa e lilás, escrita: Parabéns Ana", text);
    }

    [Theory]
    [InlineData(2.5, "2,5 kg")]
    [InlineData(10, "10 kg")]
    [InlineData(0.75, "0,75 kg")]
    public void TheSize_UsesCommaAndKg(double kg, string expected)
    {
        var order = OrderFor(TestData.NewCustomer(), items: Item((decimal)kg));

        Assert.Contains($"• Tamanho: {expected}", WhatsAppMessageBuilder.ForOrder(order).Text);
    }

    [Theory]
    [InlineData(1, "Brigadeiro")]
    [InlineData(2, "Brigadeiro e Ninho")]
    [InlineData(3, "Brigadeiro, Ninho e Maracujá")]
    public void TheFillings_AreJoinedInPortuguese(int count, string expected)
    {
        var fillings = new[] { "Brigadeiro", "Ninho", "Maracujá" }.Take(count).ToArray();
        var order = OrderFor(TestData.NewCustomer(), items: Item(2m, "Tema", fillings));

        Assert.Contains($"• Recheio: {expected}", WhatsAppMessageBuilder.ForOrder(order).Text);
    }

    [Fact]
    public void WithSeveralCakes_NumbersThem()
    {
        var order = OrderFor(TestData.NewCustomer(), items: [Item(2m, "Primeiro"), Item(3m, "Segundo")]);

        var text = WhatsAppMessageBuilder.ForOrder(order).Text;

        Assert.Contains("*Bolo 1*", text);
        Assert.Contains("*Bolo 2*", text);
        Assert.Contains("• Decoração: Primeiro", text);
        Assert.Contains("• Decoração: Segundo", text);
    }

    // ---------- O que NUNCA entra ----------

    [Fact]
    public void NeverMentionsPricesDepositOrPix_EvenAfterTheOrderIsApproved()
    {
        // Aprovado com valor, sinal e Pix: nada disso pode vazar na mensagem. Isso se combina na conversa.
        var order = TestData.NewApprovedOrder();

        var text = WhatsAppMessageBuilder.ForOrder(order).Text.ToLowerInvariant();

        Assert.DoesNotContain("r$", text);
        Assert.DoesNotContain("150", text);
        Assert.DoesNotContain("pix", text);
        Assert.DoesNotContain("sinal", text);
        Assert.DoesNotContain("valor", text);
        Assert.DoesNotContain("preço", text);
    }

    [Fact]
    public void DoesNotRepeatTheCustomersPhoneOrEmailInTheText()
    {
        var customer = Customer.Create("Maria Silva", "11912345678", "maria@example.com", TestData.NowUtc);

        var text = WhatsAppMessageBuilder.ForOrder(OrderFor(customer)).Text;

        Assert.DoesNotContain("11912345678", text);
        Assert.DoesNotContain("maria@example.com", text);
    }

    // ---------- Decoração longa ----------

    [Fact]
    public void ALongDecoration_IsCutWithAnEllipsis_SoTheLinkStaysShort()
    {
        var order = OrderFor(TestData.NewCustomer(), items: Item(2m, new string('x', 900)));

        var line = WhatsAppMessageBuilder.ForOrder(order).Text
            .Split('\n').Single(l => l.StartsWith("• Decoração: "));
        var decoration = line["• Decoração: ".Length..];

        Assert.Equal(WhatsAppMessageBuilder.MaxDecorationLength, decoration.Length);
        Assert.EndsWith("…", decoration);
    }

    [Fact]
    public void ADecorationWithLineBreaks_StaysOnOneLine()
    {
        var order = OrderFor(TestData.NewCustomer(), items: Item(2m, "Tema: flores\r\n\r\nCores: rosa\nEscrita: Ana"));

        var text = WhatsAppMessageBuilder.ForOrder(order).Text;

        Assert.Contains("• Decoração: Tema: flores Cores: rosa Escrita: Ana", text);
    }

    // ---------- Link ----------

    [Fact]
    public void TheLink_OpensTheCustomersChat_WithTheCountryCode()
    {
        var customer = Customer.Create("Maria Silva", "(11) 91234-5678", null, TestData.NowUtc);

        var message = WhatsAppMessageBuilder.ForOrder(OrderFor(customer));

        Assert.StartsWith("https://wa.me/5511912345678?text=", message.Link);
    }

    [Fact]
    public void TheLink_WorksForALandlineNumberToo()
    {
        var customer = Customer.Create("Maria Silva", "1133334444", null, TestData.NowUtc);

        Assert.StartsWith("https://wa.me/551133334444?text=", WhatsAppMessageBuilder.ForOrder(OrderFor(customer)).Link);
    }

    [Fact]
    public void TheLinkText_IsUrlEncoded_AndDecodesBackToTheMessage()
    {
        var order = TestData.NewOrder(DeliveryType.Delivery, "Rua A, 10 & Rua B?");

        var message = WhatsAppMessageBuilder.ForOrder(order);
        var encoded = message.Link[(message.Link.IndexOf("?text=", StringComparison.Ordinal) + "?text=".Length)..];

        Assert.DoesNotContain(' ', message.Link);
        Assert.DoesNotContain('\n', message.Link);
        Assert.DoesNotContain('&', encoded); // o "&" do endereço não pode abrir outro parâmetro
        Assert.Equal(message.Text, Uri.UnescapeDataString(encoded));
    }

    [Fact]
    public void WithoutAnOrder_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => WhatsAppMessageBuilder.ForOrder(null!));
    }
}
