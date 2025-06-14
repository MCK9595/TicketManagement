using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketManagement.Core.Entities;

namespace TicketManagement.Infrastructure.Data.Configurations;

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.HasKey(o => o.Id);
        
        builder.Property(o => o.Name)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(o => o.DisplayName)
            .HasMaxLength(200);
            
        builder.Property(o => o.Description)
            .HasMaxLength(500);
            
        builder.Property(o => o.CreatedBy)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(o => o.UpdatedBy)
            .HasMaxLength(100);
            
        builder.Property(o => o.BillingPlan)
            .HasMaxLength(50);
            
        builder.HasIndex(o => o.Name)
            .IsUnique();
            
        builder.HasIndex(o => o.IsActive);
        
        // Relationships
        builder.HasMany(o => o.Members)
            .WithOne(m => m.Organization)
            .HasForeignKey(m => m.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasMany(o => o.Projects)
            .WithOne(p => p.Organization)
            .HasForeignKey(p => p.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}