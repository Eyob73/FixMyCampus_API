using FixMyCampus.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FixMyCampus.Infrastructure.Persistence.Configurations;

public class TicketImageConfiguration : IEntityTypeConfiguration<TicketImage>
{
    public void Configure(EntityTypeBuilder<TicketImage> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.StoragePath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(i => i.OriginalFileName)
            .IsRequired()
            .HasMaxLength(260);

        builder.Property(i => i.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(i => i.Category)
            .IsRequired();

        builder.Property(i => i.FileSize)
            .IsRequired();

        builder.Property(i => i.UploadedAt)
            .IsRequired();

        builder.HasOne(i => i.Ticket)
            .WithMany(t => t.Images)
            .HasForeignKey(i => i.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Uploader)
            .WithMany(u => u.UploadedImages)
            .HasForeignKey(i => i.UploaderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.TicketId);
        builder.HasIndex(i => new { i.TicketId, i.Category });
    }
}
