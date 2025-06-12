using Microsoft.EntityFrameworkCore;
using TicketManagement.Core.Entities;

namespace TicketManagement.Infrastructure.Data;

public class TicketDbContext : DbContext
{
    public TicketDbContext(DbContextOptions<TicketDbContext> options) : base(options)
    {
    }

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
        
        // DbContext pooling使用時はOnConfiguringでオプションを変更できないため、
        // Program.csでDbContextを登録する際に設定する
        if (!optionsBuilder.IsConfigured)
        {
            // マイグレーション時のみ実行される設定
            // 本番環境ではProgram.csでの設定が使用される
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Configure string properties to have a default max length
        configurationBuilder.Properties<string>()
            .HaveMaxLength(256);
    }
}