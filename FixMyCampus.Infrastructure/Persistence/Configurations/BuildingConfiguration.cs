using FixMyCampus.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FixMyCampus.Infrastructure.Persistence.Configurations;

public class BuildingConfiguration : IEntityTypeConfiguration<Building>
{
    public void Configure(EntityTypeBuilder<Building> builder)
    {
        builder.ToTable("Buildings");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(b => b.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(b => b.Zone)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(b => b.Floors)
            .IsRequired();

        builder.Property(b => b.TotalRooms)
            .IsRequired();

        builder.Property(b => b.ManagerName)
            .IsRequired(false)
            .HasMaxLength(100);

        builder.Property(b => b.ManagerContact)
            .IsRequired(false)
            .HasMaxLength(150);

        builder.Property(b => b.Status)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("OPERATIONAL");

        builder.Property(b => b.CreatedAt)
            .IsRequired();

        builder.Property(b => b.UpdatedAt)
            .IsRequired();

        builder.HasIndex(b => b.Code)
            .IsUnique();
    }
}
