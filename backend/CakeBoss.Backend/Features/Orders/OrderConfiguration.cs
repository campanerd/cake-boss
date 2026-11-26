using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CakeBoss.Backend.Features.Orders;

internal class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.ToTable("Orders");
        b.HasKey(o => o.Id);
        b.Property(o => o.Id).ValueGeneratedNever();

        b.Property(o => o.Code).HasMaxLength(12).IsRequired();
        b.HasIndex(o => o.Code).IsUnique();

        // Enums gravados como texto: o banco fica legível e a ordem do enum pode mudar sem quebrar dados.
        b.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(o => o.DeliveryType).HasConversion<string>().HasMaxLength(20);
        b.Property(o => o.PaymentMethod).HasConversion<string>().HasMaxLength(20);

        b.Property(o => o.DeliveryAddress).HasMaxLength(300);
        b.Property(o => o.Notes).HasMaxLength(1000);
        b.Property(o => o.RejectionReason).HasMaxLength(500);
        b.Property(o => o.CancellationReason).HasMaxLength(500);
        b.Property(o => o.GoogleEventId).HasMaxLength(200);

        b.Property(o => o.Price).HasPrecision(10, 2);
        b.Property(o => o.DeliveryFee).HasPrecision(10, 2);
        b.Property(o => o.DepositAmount).HasPrecision(10, 2);

        b.Ignore(o => o.TotalWeightKg);
        b.Ignore(o => o.Total);
        b.Ignore(o => o.RemainingAmount);

        b.HasOne(o => o.Customer)
            .WithMany()
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Navigation(o => o.Items).UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasMany(o => o.Photos)
            .WithOne()
            .HasForeignKey(p => p.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Navigation(o => o.Photos).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Garante no BANCO que dois pedidos ativos nunca ocupem o mesmo horário, mesmo com
        // duas aprovações simultâneas. Mantenha a lista em sincronia com Order.SlotBlockingStatuses.
        // Expired fica de fora de propósito: é assim que o horário de um pedido sem sinal volta a ficar livre.
        b.HasIndex(o => new { o.Date, o.Time })
            .IsUnique()
            .HasFilter("\"Status\" IN ('Approved', 'Confirmed', 'Completed')")
            .HasDatabaseName("UX_Orders_Occupied_Slot");

        b.HasIndex(o => new { o.Status, o.Date });
    }
}

internal class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> b)
    {
        b.ToTable("OrderItems");
        b.HasKey(i => i.Id);
        b.Property(i => i.Id).ValueGeneratedNever();

        b.Property(i => i.Batter).HasMaxLength(100).IsRequired();
        b.Property(i => i.Frosting).HasMaxLength(100).IsRequired();
        b.Property(i => i.Decoration).HasMaxLength(1000).IsRequired();
        b.Property(i => i.WeightKg).HasPrecision(5, 2);

        // Lista de recheios: coluna text[] no Postgres.
        b.PrimitiveCollection(i => i.Fillings).HasField("_fillings").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal class OrderPhotoConfiguration : IEntityTypeConfiguration<OrderPhoto>
{
    public void Configure(EntityTypeBuilder<OrderPhoto> b)
    {
        b.ToTable("OrderPhotos");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).ValueGeneratedNever();

        b.Property(p => p.StorageKey).HasMaxLength(300).IsRequired();
        b.Property(p => p.OriginalName).HasMaxLength(200).IsRequired();
        b.Property(p => p.ContentType).HasMaxLength(100).IsRequired();
    }
}
