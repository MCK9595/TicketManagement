using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketManagement.Core.Entities;

namespace TicketManagement.Infrastructure.Data.Configurations;

public class TicketAssignmentConfiguration : IEntityTypeConfiguration<TicketAssignment>
{
    public void Configure(EntityTypeBuilder<TicketAssignment> builder)
    {
        builder.ToTable("TicketAssignments");

        builder.HasKey(ta => ta.Id);

        builder.Property(ta => ta.AssigneeId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(ta => ta.AssignedBy)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(ta => ta.AssignedAt)
            .IsRequired();

        builder.HasOne(ta => ta.Ticket)
            .WithMany(t => t.Assignments)
            .HasForeignKey(ta => ta.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for performance
        builder.HasIndex(ta => ta.AssigneeId);
        builder.HasIndex(ta => ta.TicketId);
        builder.HasIndex(ta => new { ta.TicketId, ta.AssigneeId });
    }
}