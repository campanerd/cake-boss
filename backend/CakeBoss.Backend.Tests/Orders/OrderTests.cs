using CakeBoss.Backend.Features.Orders;
using CakeBoss.Backend.Shared;
using CakeBoss.Backend.Tests.Support;

namespace CakeBoss.Backend.Tests.Orders;

public class OrderTests
{
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    // ---------- Criação ----------

    [Fact]
    public void Create_StartsPendingWithPublicCode()
    {
        var order = TestData.NewOrder();

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(8, order.Code.Length);
        Assert.All(order.Code, c => Assert.True(CodeAlphabet.Contains(c), $"Caractere inesperado '{c}' no código."));
        Assert.Equal(new DateOnly(2026, 11, 24), order.Date);
        Assert.Equal(new TimeOnly(15, 0), order.Time);
        Assert.Equal(TestData.NowUtc, order.CreatedAt);
        Assert.Equal(TestData.NowUtc, order.UpdatedAt);
        Assert.Null(order.Price);
        Assert.False(order.BlocksWholeDay);
    }

    [Fact]
    public void Create_GeneratesDifferentCodes()
    {
        var codes = Enumerable.Range(0, 200).Select(_ => TestData.NewOrder().Code).ToList();

        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    [Fact]
    public void Create_LinksCustomerAndKeepsItemsAndPhotos()
    {
        var order = TestData.NewOrder(photos: [TestData.NewPhoto("a.jpg"), TestData.NewPhoto("b.jpg")]);

        Assert.Equal(order.Customer.Id, order.CustomerId);
        Assert.Single(order.Items);
        Assert.Equal(2, order.Photos.Count);
    }

    [Fact]
    public void TotalWeightKg_SumsAllCakes()
    {
        var order = TestData.NewOrder(items: [TestData.NewItem(2m), TestData.NewItem(1.5m)]);

        Assert.Equal(3.5m, order.TotalWeightKg);
    }

    [Fact]
    public void Create_WithoutItems_Throws()
    {
        Assert.Throws<DomainException>(() => TestData.NewOrder(items: []));
    }

    [Fact]
    public void Create_WithMoreThanMaxPhotos_Throws()
    {
        var photos = Enumerable.Range(0, Order.MaxPhotos + 1).Select(i => TestData.NewPhoto($"{i}.jpg"));

        Assert.Throws<DomainException>(() => TestData.NewOrder(photos: photos));
    }

    [Fact]
    public void Create_WithMaxPhotos_Succeeds()
    {
        var photos = Enumerable.Range(0, Order.MaxPhotos).Select(i => TestData.NewPhoto($"{i}.jpg"));

        var order = TestData.NewOrder(photos: photos);

        Assert.Equal(Order.MaxPhotos, order.Photos.Count);
    }

    [Fact]
    public void Create_WithoutPhoto_Succeeds()
    {
        var order = TestData.NewOrder(photos: []);

        Assert.Empty(order.Photos);
    }

    [Fact]
    public void Create_DeliveryWithoutAddress_Throws()
    {
        Assert.Throws<DomainException>(() => TestData.NewOrder(DeliveryType.Delivery, address: "  "));
    }

    [Fact]
    public void Create_Delivery_KeepsTrimmedAddress()
    {
        var order = TestData.NewOrder(DeliveryType.Delivery, address: "  Rua das Flores, 10  ");

        Assert.Equal("Rua das Flores, 10", order.DeliveryAddress);
    }

    [Fact]
    public void Create_Pickup_DiscardsAddress()
    {
        var order = TestData.NewOrder(DeliveryType.Pickup, address: "Rua das Flores, 10");

        Assert.Null(order.DeliveryAddress);
    }

    [Fact]
    public void SlotBlockingStatuses_AreApprovedConfirmedAndCompleted()
    {
        Assert.Equivalent(
            new[] { OrderStatus.Approved, OrderStatus.Confirmed, OrderStatus.Completed },
            Order.SlotBlockingStatuses);
    }

    // ---------- Aprovar ----------

    [Fact]
    public void Approve_SetsValuesAndStatus()
    {
        var order = TestData.NewOrder(DeliveryType.Delivery, address: "Rua A, 1");
        var later = TestData.NowUtc.AddHours(2);

        order.Approve(150m, 10m, 50m, false, later);

        Assert.Equal(OrderStatus.Approved, order.Status);
        Assert.Equal(150m, order.Price);
        Assert.Equal(10m, order.DeliveryFee);
        Assert.Equal(50m, order.DepositAmount);
        Assert.Equal(160m, order.Total);
        Assert.Equal(later, order.UpdatedAt);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Approve_StoresWhetherTheOrderBlocksTheWholeDay(bool blocksWholeDay)
    {
        var order = TestData.NewOrder();

        order.Approve(150m, 0m, 50m, blocksWholeDay, TestData.NowUtc);

        Assert.Equal(blocksWholeDay, order.BlocksWholeDay);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(160)]
    public void Approve_WithDepositWithinTotal_Succeeds(double deposit)
    {
        var order = TestData.NewOrder(DeliveryType.Delivery, address: "Rua A, 1");

        order.Approve(150m, 10m, (decimal)deposit, false, TestData.NowUtc);

        Assert.Equal(OrderStatus.Approved, order.Status);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(160.01)]
    public void Approve_WithDepositOutsideTotal_Throws(double deposit)
    {
        var order = TestData.NewOrder(DeliveryType.Delivery, address: "Rua A, 1");

        Assert.Throws<DomainException>(() => order.Approve(150m, 10m, (decimal)deposit, false, TestData.NowUtc));
        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Approve_WithNonPositivePrice_Throws(double price)
    {
        var order = TestData.NewOrder();

        Assert.Throws<DomainException>(() => order.Approve((decimal)price, 0m, 0m, false, TestData.NowUtc));
    }

    [Fact]
    public void Approve_WithNegativeDeliveryFee_Throws()
    {
        var order = TestData.NewOrder(DeliveryType.Delivery, address: "Rua A, 1");

        Assert.Throws<DomainException>(() => order.Approve(150m, -5m, 0m, false, TestData.NowUtc));
    }

    [Fact]
    public void Approve_DeliveryFeeOnPickup_Throws()
    {
        var order = TestData.NewOrder(DeliveryType.Pickup);

        Assert.Throws<DomainException>(() => order.Approve(150m, 10m, 0m, false, TestData.NowUtc));
    }

    [Fact]
    public void Approve_Twice_ThrowsWithStatusInPortuguese()
    {
        var order = TestData.NewApprovedOrder();

        var exception = Assert.Throws<DomainException>(() => order.Approve(150m, 0m, 50m, false, TestData.NowUtc));

        Assert.Contains("aprovar", exception.Message);
        Assert.Contains("Aprovado", exception.Message);
    }

    [Fact]
    public void Approve_AfterReject_Throws()
    {
        var order = TestData.NewOrder();
        order.Reject("Sem agenda", TestData.NowUtc);

        Assert.Throws<DomainException>(() => order.Approve(150m, 0m, 50m, false, TestData.NowUtc));
    }

    // ---------- Recusar ----------

    [Fact]
    public void Reject_FromPending_StoresReason()
    {
        var order = TestData.NewOrder();

        order.Reject("  Sem agenda nesse dia  ", TestData.NowUtc.AddHours(1));

        Assert.Equal(OrderStatus.Rejected, order.Status);
        Assert.Equal("Sem agenda nesse dia", order.RejectionReason);
        Assert.Equal(TestData.NowUtc.AddHours(1), order.UpdatedAt);
    }

    [Fact]
    public void Reject_WithoutReason_Succeeds()
    {
        var order = TestData.NewOrder();

        order.Reject(null, TestData.NowUtc);

        Assert.Equal(OrderStatus.Rejected, order.Status);
        Assert.Null(order.RejectionReason);
    }

    [Fact]
    public void Reject_AfterApproval_Throws()
    {
        var order = TestData.NewApprovedOrder();

        Assert.Throws<DomainException>(() => order.Reject("x", TestData.NowUtc));
    }

    // ---------- Confirmar sinal ----------

    [Fact]
    public void ConfirmDeposit_FromApproved_MarksDepositAsPaid()
    {
        var order = TestData.NewApprovedOrder();
        var paidAt = TestData.NowUtc.AddDays(1);

        order.ConfirmDeposit(paidAt);

        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Equal(paidAt, order.DepositPaidAt);
    }

    [Fact]
    public void ConfirmDeposit_FromPending_Throws()
    {
        var order = TestData.NewOrder();

        Assert.Throws<DomainException>(() => order.ConfirmDeposit(TestData.NowUtc));
    }

    [Fact]
    public void ConfirmDeposit_Twice_Throws()
    {
        var order = TestData.NewConfirmedOrder();

        Assert.Throws<DomainException>(() => order.ConfirmDeposit(TestData.NowUtc));
    }

    // ---------- Concluir ----------

    [Fact]
    public void Complete_FromConfirmed_Succeeds()
    {
        var order = TestData.NewConfirmedOrder();

        order.Complete(TestData.NowUtc.AddDays(3));

        Assert.Equal(OrderStatus.Completed, order.Status);
    }

    [Fact]
    public void Complete_BeforeDepositIsConfirmed_Throws()
    {
        var order = TestData.NewApprovedOrder();

        Assert.Throws<DomainException>(() => order.Complete(TestData.NowUtc));
    }

    // ---------- Cancelar ----------

    [Fact]
    public void Cancel_FromPending_Succeeds()
    {
        var order = TestData.NewOrder();

        order.Cancel("Cliente desistiu", false, TestData.NowUtc);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal("Cliente desistiu", order.CancellationReason);
    }

    [Fact]
    public void Cancel_FromApprovedWithoutPaidDeposit_NeverKeepsDeposit()
    {
        var order = TestData.NewApprovedOrder();

        order.Cancel("Cliente desistiu", keepDeposit: true, TestData.NowUtc);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.False(order.DepositKept);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Cancel_FromConfirmed_KeepsDepositOnlyWhenRequested(bool keepDeposit, bool expectedKept)
    {
        var order = TestData.NewConfirmedOrder();

        order.Cancel("Cliente desistiu", keepDeposit, TestData.NowUtc);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(expectedKept, order.DepositKept);
    }

    [Fact]
    public void Cancel_AfterCompletion_Throws()
    {
        var order = TestData.NewConfirmedOrder();
        order.Complete(TestData.NowUtc);

        Assert.Throws<DomainException>(() => order.Cancel(null, false, TestData.NowUtc));
    }

    [Fact]
    public void Cancel_AfterRejection_Throws()
    {
        var order = TestData.NewOrder();
        order.Reject(null, TestData.NowUtc);

        Assert.Throws<DomainException>(() => order.Cancel(null, false, TestData.NowUtc));
    }

    [Fact]
    public void Cancel_Twice_Throws()
    {
        var order = TestData.NewOrder();
        order.Cancel(null, false, TestData.NowUtc);

        Assert.Throws<DomainException>(() => order.Cancel(null, false, TestData.NowUtc));
    }

    // ---------- Valores calculados e demais ----------

    [Fact]
    public void RemainingAmount_BeforeDepositIsPaid_EqualsTotal()
    {
        var order = TestData.NewApprovedOrder();

        Assert.Equal(150m, order.Total);
        Assert.Equal(150m, order.RemainingAmount);
    }

    [Fact]
    public void RemainingAmount_AfterDepositIsPaid_SubtractsDeposit()
    {
        var order = TestData.NewConfirmedOrder();

        Assert.Equal(100m, order.RemainingAmount);
    }

    [Fact]
    public void Total_BeforeApproval_IsZero()
    {
        var order = TestData.NewOrder();

        Assert.Equal(0m, order.Total);
    }

    [Fact]
    public void SetGoogleEventId_StoresTrimmedValueAndAllowsClearing()
    {
        var order = TestData.NewApprovedOrder();

        order.SetGoogleEventId("  abc123  ");
        Assert.Equal("abc123", order.GoogleEventId);

        order.SetGoogleEventId(null);
        Assert.Null(order.GoogleEventId);
    }

    // ---------- Código público ----------

    [Theory]
    [InlineData("ABCD2345", "ABCD2345")]
    [InlineData("abcd2345", "ABCD2345")]
    [InlineData("  abcd2345  ", "ABCD2345")]
    public void NormalizeCode_FixesCaseAndSpaces(string input, string expected)
    {
        Assert.Equal(expected, Order.NormalizeCode(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ABC")]
    [InlineData("ABCD234")]
    [InlineData("ABCD23456")]
    [InlineData("ABCD234O")] // O não existe no alfabeto dos códigos
    [InlineData("ABCD 345")]
    public void NormalizeCode_WithAnInvalidCode_ReturnsNull(string? input)
    {
        Assert.Null(Order.NormalizeCode(input));
    }

    [Fact]
    public void NormalizeCode_AcceptsEveryGeneratedCode()
    {
        var order = TestData.NewOrder();

        Assert.Equal(order.Code, Order.NormalizeCode(order.Code));
    }

    // ---------- Prazo do sinal ----------

    private static DateTime Due(Order order) => order.DepositDueAt!.Value;

    [Fact]
    public void DepositWindow_IsFiveHours()
    {
        Assert.Equal(TimeSpan.FromHours(5), Order.DepositWindow);
    }

    [Fact]
    public void Approve_WithDeposit_SetsTheDeadlineFiveHoursAfterTheApproval()
    {
        var order = TestData.NewOrder();
        var approvedAt = TestData.NowUtc.AddHours(1);

        order.Approve(150m, 0m, 50m, false, approvedAt);

        Assert.Equal(approvedAt.AddHours(5), order.DepositDueAt);
    }

    // Sem sinal a pagar não há prazo: senão um pedido sem sinal expiraria sozinho.
    [Fact]
    public void Approve_WithoutDeposit_HasNoDeadline()
    {
        var order = TestData.NewOrder();

        order.Approve(150m, 0m, 0m, false, TestData.NowUtc);

        Assert.Null(order.DepositDueAt);
        Assert.False(order.IsDepositOverdue(TestData.NowUtc.AddDays(30)));
    }

    [Fact]
    public void IsDepositOverdue_IsTrueOnlyFromTheDeadlineOn()
    {
        var order = TestData.NewApprovedOrder();

        Assert.False(order.IsDepositOverdue(Due(order).AddSeconds(-1)));
        Assert.True(order.IsDepositOverdue(Due(order)));
        Assert.True(order.IsDepositOverdue(Due(order).AddHours(1)));
    }

    [Fact]
    public void IsDepositOverdue_IsFalseOnceTheDepositIsPaid()
    {
        var order = TestData.NewApprovedOrder();
        order.ConfirmDeposit(Due(order).AddMinutes(-1));

        Assert.False(order.IsDepositOverdue(Due(order).AddDays(1)));
    }

    [Fact]
    public void Expire_AfterTheDeadline_MovesToExpired()
    {
        var order = TestData.NewApprovedOrder();
        var now = Due(order).AddMinutes(1);

        order.Expire(now);

        Assert.Equal(OrderStatus.Expired, order.Status);
        Assert.Equal(now, order.UpdatedAt);
    }

    [Fact]
    public void Expire_BeforeTheDeadline_Throws()
    {
        var order = TestData.NewApprovedOrder();

        var exception = Assert.Throws<DomainException>(() => order.Expire(Due(order).AddMinutes(-1)));

        Assert.Equal("O prazo para pagar o sinal ainda não venceu.", exception.Message);
        Assert.Equal(OrderStatus.Approved, order.Status);
    }

    [Fact]
    public void Expire_AnOrderWithoutDeadline_Throws()
    {
        var order = TestData.NewOrder();
        order.Approve(150m, 0m, 0m, false, TestData.NowUtc);

        Assert.Throws<DomainException>(() => order.Expire(TestData.NowUtc.AddDays(30)));
    }

    [Fact]
    public void Expire_WhenNotApproved_Throws()
    {
        var late = TestData.NowUtc.AddDays(30);

        Assert.Throws<DomainException>(() => TestData.NewOrder().Expire(late)); // pendente
        Assert.Throws<DomainException>(() => TestData.NewConfirmedOrder().Expire(late)); // sinal já pago
    }

    [Fact]
    public void Expired_DoesNotBlockTheSlot()
    {
        Assert.DoesNotContain(OrderStatus.Expired, Order.SlotBlockingStatuses);
    }

    [Fact]
    public void Reopen_FromExpired_GoesBackToApprovedWithANewDeadline()
    {
        var order = TestData.NewApprovedOrder();
        order.Expire(Due(order).AddMinutes(1));
        var reopenedAt = TestData.NowUtc.AddDays(1);

        order.Reopen(reopenedAt);

        Assert.Equal(OrderStatus.Approved, order.Status);
        Assert.Equal(reopenedAt.AddHours(5), order.DepositDueAt);
        Assert.Equal(reopenedAt, order.UpdatedAt);
        Assert.Equal(50m, order.DepositAmount); // o que foi combinado continua valendo
    }

    [Fact]
    public void Reopen_WhenNotExpired_Throws()
    {
        Assert.Throws<DomainException>(() => TestData.NewOrder().Reopen(TestData.NowUtc));
        Assert.Throws<DomainException>(() => TestData.NewApprovedOrder().Reopen(TestData.NowUtc));
    }

    // Um pedido que expirou está encerrado: só reabrir o tira daí.
    [Fact]
    public void Expired_CannotBeConfirmedCompletedCancelledOrRejected()
    {
        var order = TestData.NewApprovedOrder();
        order.Expire(Due(order).AddMinutes(1));
        var now = TestData.NowUtc.AddDays(1);

        Assert.Throws<DomainException>(() => order.ConfirmDeposit(now));
        Assert.Throws<DomainException>(() => order.Complete(now));
        Assert.Throws<DomainException>(() => order.Cancel("Desistiu", false, now));
        Assert.Throws<DomainException>(() => order.Reject("Não", now));
        Assert.Throws<DomainException>(() => order.Approve(100m, 0m, 0m, false, now));
    }

    // A palavra da dona sobre o Pix recebido vale mais que o relógio: enquanto o pedido não foi fechado, confirmar o sinal funciona.
    [Fact]
    public void ConfirmDeposit_AfterTheDeadlineButBeforeExpiring_StillWorks()
    {
        var order = TestData.NewApprovedOrder();

        order.ConfirmDeposit(Due(order).AddHours(2));

        Assert.Equal(OrderStatus.Confirmed, order.Status);
    }

    [Fact]
    public void ExpiredStatus_HasAMessageInPortuguese()
    {
        var order = TestData.NewApprovedOrder();
        order.Expire(Due(order).AddMinutes(1));

        var exception = Assert.Throws<DomainException>(() => order.Complete(TestData.NowUtc));

        Assert.Contains("Expirado", exception.Message);
    }
}
