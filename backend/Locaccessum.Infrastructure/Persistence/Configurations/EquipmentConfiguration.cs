using Locaccessum.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Locaccessum.Infrastructure.Persistence.Configurations;

public sealed class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Reference).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Informations).HasColumnType("text");
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasIndex(e => new { e.InventoryId, e.Name, e.Reference });

        builder.HasOne(e => e.Inventory)
            .WithMany(i => i.Equipment)
            .HasForeignKey(e => e.InventoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
