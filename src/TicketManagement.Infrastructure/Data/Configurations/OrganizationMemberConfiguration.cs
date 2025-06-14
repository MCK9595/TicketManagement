using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketManagement.Core.Entities;

namespace TicketManagement.Infrastructure.Data.Configurations;

public class OrganizationMemberConfiguration : IEntityTypeConfiguration<OrganizationMember>
{
    public void Configure(EntityTypeBuilder<OrganizationMember> builder)
    {
        builder.HasKey(om => om.Id);
        
        builder.Property(om => om.UserId)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(om => om.UserName)
            .IsRequired()
            .HasMaxLength(200);
            
        builder.Property(om => om.UserEmail)
            .HasMaxLength(200);
            
        builder.Property(om => om.InvitedBy)
            .HasMaxLength(100);
            
        builder.Property(om => om.Role)
            .IsRequired();
            
        // 同じユーザーが同じ組織に複数回参加することを防ぐ
        builder.HasIndex(om => new { om.OrganizationId, om.UserId })
            .IsUnique();
            
        builder.HasIndex(om => om.UserId);
        builder.HasIndex(om => om.IsActive);
        
        // Relationships
        builder.HasOne(om => om.Organization)
            .WithMany(o => o.Members)
            .HasForeignKey(om => om.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}