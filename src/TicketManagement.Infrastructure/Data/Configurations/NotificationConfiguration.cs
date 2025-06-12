using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketManagement.Core.Entities;

namespace TicketManagement.Infrastructure.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.UserId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(n => n.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(n => n.Message)
            .IsRequired()
            .HasMaxLength(1000);

        // Indexes for performance
        builder.HasIndex(n => new { n.UserId, n.IsRead }).HasDatabaseName("IX_Notifications_UserId_IsRead");
        builder.HasIndex(n => n.CreatedAt).HasDatabaseName("IX_Notifications_CreatedAt");
        builder.HasIndex(n => n.UserId).HasDatabaseName("IX_Notifications_UserId");
        builder.HasIndex(n => n.Type).HasDatabaseName("IX_Notifications_Type");
        builder.HasIndex(n => new { n.UserId, n.CreatedAt }).HasDatabaseName("IX_Notifications_UserId_CreatedAt");
    }
}