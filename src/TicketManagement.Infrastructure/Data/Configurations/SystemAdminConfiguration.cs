using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketManagement.Core.Entities;

namespace TicketManagement.Infrastructure.Data.Configurations;

public class SystemAdminConfiguration : IEntityTypeConfiguration<SystemAdmin>
{
    public void Configure(EntityTypeBuilder<SystemAdmin> builder)
    {
        builder.HasKey(sa => sa.Id);

        builder.Property(sa => sa.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(sa => sa.UserName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(sa => sa.UserEmail)
            .HasMaxLength(256);

        builder.Property(sa => sa.GrantedBy)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(sa => sa.Reason)
            .HasMaxLength(500);

        builder.Property(sa => sa.GrantedAt)
            .IsRequired();

        builder.Property(sa => sa.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // Create unique index on UserId to ensure one SystemAdmin per user
        builder.HasIndex(sa => sa.UserId)
            .IsUnique()
            .HasDatabaseName("IX_SystemAdmins_UserId");

        builder.ToTable("SystemAdmins");
    }
}