using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketManagement.Core.Entities;

namespace TicketManagement.Infrastructure.Data.Configurations;

public class TicketHistoryConfiguration : IEntityTypeConfiguration<TicketHistory>
{
    public void Configure(EntityTypeBuilder<TicketHistory> builder)
    {
        builder.ToTable("TicketHistories");

        builder.HasKey(th => th.Id);

        builder.Property(th => th.ActionType)
            .IsRequired();

        builder.Property(th => th.FieldName)
            .HasMaxLength(100);

        builder.Property(th => th.OldValue)
            .HasMaxLength(500);

        builder.Property(th => th.NewValue)
            .HasMaxLength(500);

        builder.Property(th => th.ChangedBy)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(th => th.ChangedAt)
            .IsRequired();

        builder.HasOne(th => th.Ticket)
            .WithMany(t => t.Histories)
            .HasForeignKey(th => th.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for performance
        builder.HasIndex(th => th.TicketId);
        builder.HasIndex(th => th.ChangedAt);
        builder.HasIndex(th => new { th.TicketId, th.ChangedAt });
    }
}