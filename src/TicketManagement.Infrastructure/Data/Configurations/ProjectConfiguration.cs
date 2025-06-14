using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketManagement.Core.Entities;

namespace TicketManagement.Infrastructure.Data.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Description)
            .HasMaxLength(500);

        builder.Property(p => p.CreatedBy)
            .IsRequired()
            .HasMaxLength(100);

        // Organization relationship
        builder.HasOne(p => p.Organization)
            .WithMany(o => o.Projects)
            .HasForeignKey(p => p.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Members)
            .WithOne(m => m.Project)
            .HasForeignKey(m => m.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Tickets)
            .WithOne(t => t.Project)
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for performance
        builder.HasIndex(p => p.CreatedAt).HasDatabaseName("IX_Projects_CreatedAt");
        builder.HasIndex(p => p.IsActive).HasDatabaseName("IX_Projects_IsActive");
        builder.HasIndex(p => p.CreatedBy).HasDatabaseName("IX_Projects_CreatedBy");
        builder.HasIndex(p => p.Name).HasDatabaseName("IX_Projects_Name");
        builder.HasIndex(p => p.OrganizationId).HasDatabaseName("IX_Projects_OrganizationId");
        builder.HasIndex(p => new { p.IsActive, p.CreatedAt }).HasDatabaseName("IX_Projects_IsActive_CreatedAt");
        builder.HasIndex(p => new { p.OrganizationId, p.IsActive }).HasDatabaseName("IX_Projects_OrganizationId_IsActive");
    }
}