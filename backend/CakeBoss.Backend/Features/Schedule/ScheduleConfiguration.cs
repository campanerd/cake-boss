using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CakeBoss.Backend.Features.Schedule;

internal class OpeningHourConfiguration : IEntityTypeConfiguration<OpeningHour>
{
    public void Configure(EntityTypeBuilder<OpeningHour> b)
    {
        b.ToTable("OpeningHours");
        b.HasKey(h => h.Id);

        // Uma janela por dia da semana.
        b.HasIndex(h => h.DayOfWeek).IsUnique();

        // Horário inicial: terça a sexta 14h–20h, sábado 8h–18h. A dona ajusta pelo painel.
        b.HasData(
            new { Id = 1, DayOfWeek = DayOfWeek.Tuesday, OpensAt = new TimeOnly(14, 0), ClosesAt = new TimeOnly(20, 0) },
            new { Id = 2, DayOfWeek = DayOfWeek.Wednesday, OpensAt = new TimeOnly(14, 0), ClosesAt = new TimeOnly(20, 0) },
            new { Id = 3, DayOfWeek = DayOfWeek.Thursday, OpensAt = new TimeOnly(14, 0), ClosesAt = new TimeOnly(20, 0) },
            new { Id = 4, DayOfWeek = DayOfWeek.Friday, OpensAt = new TimeOnly(14, 0), ClosesAt = new TimeOnly(20, 0) },
            new { Id = 5, DayOfWeek = DayOfWeek.Saturday, OpensAt = new TimeOnly(8, 0), ClosesAt = new TimeOnly(18, 0) });
    }
}

internal class ScheduleBlockConfiguration : IEntityTypeConfiguration<ScheduleBlock>
{
    public void Configure(EntityTypeBuilder<ScheduleBlock> b)
    {
        b.ToTable("ScheduleBlocks");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Reason).HasMaxLength(200);
        b.HasIndex(x => new { x.StartDate, x.EndDate });
    }
}

internal class ScheduleSettingsConfiguration : IEntityTypeConfiguration<ScheduleSettings>
{
    public void Configure(EntityTypeBuilder<ScheduleSettings> b)
    {
        b.ToTable("ScheduleSettings");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).ValueGeneratedNever();
        b.Property(s => s.MaxKgPerDay).HasPrecision(6, 2);

        b.HasData(new
        {
            Id = ScheduleSettings.SingletonId,
            SlotMinutes = ScheduleSettings.DefaultSlotMinutes,
            MinLeadDays = ScheduleSettings.DefaultMinLeadDays,
            MaxKgPerDay = ScheduleSettings.DefaultMaxKgPerDay
        });
    }
}
