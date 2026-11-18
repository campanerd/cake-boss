using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CakeBoss.Backend.Features.Customers;

internal class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("Customers");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).ValueGeneratedNever();

        b.Property(c => c.Name).HasMaxLength(120).IsRequired();
        b.Property(c => c.Phone).HasMaxLength(11).IsRequired();
        b.Property(c => c.Email).HasMaxLength(200);

        // O telefone identifica o cliente: um novo pedido com o mesmo número reaproveita o cadastro.
        b.HasIndex(c => c.Phone).IsUnique();
    }
}
