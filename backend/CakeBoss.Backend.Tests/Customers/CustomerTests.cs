using CakeBoss.Backend.Features.Customers;
using CakeBoss.Backend.Shared;

namespace CakeBoss.Backend.Tests.Customers;

public class CustomerTests
{
    private static readonly DateTime NowUtc = new(2026, 11, 18, 15, 0, 0, DateTimeKind.Utc);

    private static Customer NewCustomer() =>
        Customer.Create("Maria Silva", "(11) 91234-5678", null, NowUtc);

    [Theory]
    [InlineData("(11) 91234-5678", "11912345678")]
    [InlineData("11 1234-5678", "1112345678")]
    [InlineData("+55 11 91234-5678", "11912345678")]
    [InlineData("5511912345678", "11912345678")]
    [InlineData("55 91234-5678", "55912345678")]
    public void NormalizePhone_KeepsOnlyDigitsAndDropsCountryCode(string input, string expected)
    {
        Assert.Equal(expected, Customer.NormalizePhone(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234")]
    [InlineData("123456789012345")]
    public void NormalizePhone_WithInvalidNumber_Throws(string? input)
    {
        Assert.Throws<DomainException>(() => Customer.NormalizePhone(input));
    }

    [Fact]
    public void Create_TrimsNameAndStoresNormalizedPhone()
    {
        var customer = Customer.Create("  Maria Silva  ", "(11) 91234-5678", null, NowUtc);

        Assert.Equal("Maria Silva", customer.Name);
        Assert.Equal("11912345678", customer.Phone);
        Assert.Equal(NowUtc, customer.CreatedAt);
        Assert.NotEqual(Guid.Empty, customer.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("A")]
    public void Create_WithShortName_Throws(string name)
    {
        Assert.Throws<DomainException>(() => Customer.Create(name, "11912345678", null, NowUtc));
    }

    [Fact]
    public void Create_WithTooLongName_Throws()
    {
        Assert.Throws<DomainException>(() =>
            Customer.Create(new string('a', 121), "11912345678", null, NowUtc));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankEmail_StoresNull(string? email)
    {
        var customer = Customer.Create("Maria", "11912345678", email, NowUtc);

        Assert.Null(customer.Email);
    }

    [Fact]
    public void Create_WithValidEmail_KeepsIt()
    {
        var customer = Customer.Create("Maria", "11912345678", "maria@example.com", NowUtc);

        Assert.Equal("maria@example.com", customer.Email);
    }

    [Theory]
    [InlineData("maria")]
    [InlineData("maria@")]
    [InlineData("Maria <maria@example.com>")]
    public void Create_WithInvalidEmail_Throws(string email)
    {
        Assert.Throws<DomainException>(() => Customer.Create("Maria", "11912345678", email, NowUtc));
    }

    [Fact]
    public void Update_ChangesNameAndEmailButKeepsPhone()
    {
        var customer = NewCustomer();

        customer.Update("Maria Souza", "nova@example.com");

        Assert.Equal("Maria Souza", customer.Name);
        Assert.Equal("nova@example.com", customer.Email);
        Assert.Equal("11912345678", customer.Phone);
    }

    [Fact]
    public void Update_WithInvalidName_KeepsPreviousValues()
    {
        var customer = NewCustomer();

        Assert.Throws<DomainException>(() => customer.Update("A", "nova@example.com"));

        Assert.Equal("Maria Silva", customer.Name);
        Assert.Null(customer.Email);
    }
}
