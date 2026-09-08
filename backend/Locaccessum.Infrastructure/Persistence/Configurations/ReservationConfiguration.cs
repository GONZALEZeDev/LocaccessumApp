using Locaccessum.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Locaccessum.Infrastructure.Persistence.Configurations;

public sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasIndex(r => new { r.EquipmentId, r.StartsAt, r.EndsAt });

        builder.ToTable(t => t.HasCheckConstraint("reservations_time_order", "ends_at > starts_at"));

        builder.HasOne(r => r.Equipment)
            .WithMany(e => e.Reservations)
            .HasForeignKey(r => r.EquipmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
