using System.Net.Mail;
using CakeBoss.Backend.Shared;

namespace CakeBoss.Backend.Features.Customers;

/// <summary>Quem faz o pedido. Não tem login: é identificado pelo telefone.</summary>
public class Customer
{
    private Customer() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";

    /// <summary>Somente dígitos, com DDD (10 ou 11 dígitos), sem o +55.</summary>
    public string Phone { get; private set; } = "";

    public string? Email { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static Customer Create(string name, string phone, string? email, DateTime nowUtc)
    {
        var customer = new Customer
        {
            Id = Guid.CreateVersion7(),
            Phone = NormalizePhone(phone),
            CreatedAt = nowUtc
        };
        customer.Update(name, email);
        return customer;
    }

    public void Update(string name, string? email)
    {
        name = name?.Trim() ?? "";
        if (name.Length is < 2 or > 120)
            throw new DomainException("Informe o nome (de 2 a 120 caracteres).");

        Name = name;
        Email = NormalizeEmail(email);
    }

    public static string NormalizePhone(string? phone)
    {
        var digits = new string((phone ?? "").Where(char.IsDigit).ToArray());

        if (digits.StartsWith("55") && digits.Length is 12 or 13)
            digits = digits[2..];

        if (digits.Length is not (10 or 11))
            throw new DomainException("Informe um telefone válido com DDD.");

        return digits;
    }

    private static string? NormalizeEmail(string? email)
    {
        email = email?.Trim();
        if (string.IsNullOrEmpty(email)) return null;

        if (email.Length > 200 || !MailAddress.TryCreate(email, out var address) || address.Address != email)
            throw new DomainException("Informe um e-mail válido.");

        return email;
    }
}
