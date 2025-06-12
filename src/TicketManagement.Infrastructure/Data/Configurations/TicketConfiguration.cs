using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketManagement.Core.Entities;

namespace TicketManagement.Infrastructure.Data.Configurations;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Description)
            .HasMaxLength(2000);

        builder.Property(t => t.Category)
            .HasMaxLength(50);

        builder.Property(t => t.Tags)
            .HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .HasMaxLength(500);

        builder.Property(t => t.CreatedBy)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.UpdatedBy)
            .HasMaxLength(100);

        builder.HasMany(t => t.Comments)
            .WithOne(c => c.Ticket)
            .HasForeignKey(c => c.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Assignments)
            .WithOne(a => a.Ticket)
            .HasForeignKey(a => a.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Histories)
            .WithOne(h => h.Ticket)
            .HasForeignKey(h => h.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for performance
        builder.HasIndex(t => t.Status).HasDatabaseName("IX_Tickets_Status");
        builder.HasIndex(t => t.Priority).HasDatabaseName("IX_Tickets_Priority");
        builder.HasIndex(t => t.CreatedAt).HasDatabaseName("IX_Tickets_CreatedAt");
        builder.HasIndex(t => t.UpdatedAt).HasDatabaseName("IX_Tickets_UpdatedAt");
        builder.HasIndex(t => t.DueDate).HasDatabaseName("IX_Tickets_DueDate");
        builder.HasIndex(t => t.CreatedBy).HasDatabaseName("IX_Tickets_CreatedBy");
        builder.HasIndex(t => new { t.ProjectId, t.Status }).HasDatabaseName("IX_Tickets_ProjectId_Status");
        builder.HasIndex(t => new { t.Status, t.Priority }).HasDatabaseName("IX_Tickets_Status_Priority");
        builder.HasIndex(t => new { t.ProjectId, t.CreatedAt }).HasDatabaseName("IX_Tickets_ProjectId_CreatedAt");
        builder.HasIndex(t => new { t.ProjectId, t.Priority }).HasDatabaseName("IX_Tickets_ProjectId_Priority");
    }
}