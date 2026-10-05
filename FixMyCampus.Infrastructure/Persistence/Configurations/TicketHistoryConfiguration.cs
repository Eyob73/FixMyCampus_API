using FixMyCampus.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FixMyCampus.Infrastructure.Persistence.Configurations;

public class TicketHistoryConfiguration : IEntityTypeConfiguration<TicketHistory>
{
    public void Configure(EntityTypeBuilder<TicketHistory> builder)
    {
        builder.HasKey(th => th.Id);

        builder.HasOne(th => th.Ticket)
            .WithMany(t => t.Histories)
            .HasForeignKey(th => th.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(th => th.ChangedBy)
            .WithMany(u => u.TicketHistories)
            .HasForeignKey(th => th.ChangedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(th => th.TicketId);
        builder.HasIndex(th => th.ChangedById);
        builder.HasIndex(th => th.ChangedAt);
    }
}
