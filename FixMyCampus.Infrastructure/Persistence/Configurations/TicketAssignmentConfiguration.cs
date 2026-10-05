using FixMyCampus.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FixMyCampus.Infrastructure.Persistence.Configurations;

public class TicketAssignmentConfiguration : IEntityTypeConfiguration<TicketAssignment>
{
    public void Configure(EntityTypeBuilder<TicketAssignment> builder)
    {
        builder.HasKey(ta => ta.Id);

        builder.HasOne(ta => ta.Ticket)
            .WithMany(t => t.Assignments)
            .HasForeignKey(ta => ta.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ta => ta.Technician)
            .WithMany(u => u.TechnicianAssignments)
            .HasForeignKey(ta => ta.TechnicianId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ta => ta.AssignedBy)
            .WithMany(u => u.CreatedAssignments)
            .HasForeignKey(ta => ta.AssignedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(ta => ta.TicketId);
        builder.HasIndex(ta => ta.TechnicianId);
        builder.HasIndex(ta => ta.AssignedById);
    }
}
