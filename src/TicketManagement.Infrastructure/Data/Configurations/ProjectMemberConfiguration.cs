using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketManagement.Core.Entities;

namespace TicketManagement.Infrastructure.Data.Configurations;

public class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.ToTable("ProjectMembers");

        builder.HasKey(pm => pm.Id);

        builder.Property(pm => pm.UserId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(pm => pm.Role)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(pm => pm.JoinedAt)
            .IsRequired();

        builder.HasOne(pm => pm.Project)
            .WithMany(p => p.Members)
            .HasForeignKey(pm => pm.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ensure unique user per project
        builder.HasIndex(pm => new { pm.ProjectId, pm.UserId })
            .IsUnique();

        // Indexes for performance
        builder.HasIndex(pm => pm.UserId);
        builder.HasIndex(pm => pm.ProjectId);
    }
}