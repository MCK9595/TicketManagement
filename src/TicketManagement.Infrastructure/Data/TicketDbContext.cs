using Microsoft.EntityFrameworkCore;
using TicketManagement.Core.Entities;

namespace TicketManagement.Infrastructure.Data;

public class TicketDbContext : DbContext
{
    public TicketDbContext(DbContextOptions<TicketDbContext> options) : base(options)
    {
    }

    public DbSet<Organization> Organizations { get; set; } = null!;
    public DbSet<OrganizationMember> OrganizationMembers { get; set; } = null!;
    public DbSet<Project> Projects { get; set; } = null!;
    public DbSet<ProjectMember> ProjectMembers { get; set; } = null!;
    public DbSet<Ticket> Tickets { get; set; } = null!;
    public DbSet<Comment> Comments { get; set; } = null!;
    public DbSet<TicketAssignment> TicketAssignments { get; set; } = null!;
    public DbSet<TicketHistory> TicketHistories { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TicketDbContext).Assembly);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        
        // Only configure if options are not already configured (for migrations)
        if (!optionsBuilder.IsConfigured)
        {
            // Suppress PendingModelChangesWarning for DbInitializer
            optionsBuilder.ConfigureWarnings(warnings =>
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Configure string properties to have a default max length
        configurationBuilder.Properties<string>()
            .HaveMaxLength(256);
    }
}