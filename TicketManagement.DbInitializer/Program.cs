using Microsoft.EntityFrameworkCore;
using TicketManagement.Infrastructure.Data;

var builder = Host.CreateApplicationBuilder(args);

// Add service defaults & configuration
builder.AddServiceDefaults();

// Add connection string for migration
var connectionString = builder.Configuration.GetConnectionString("TicketDB");

var host = builder.Build();

// マイグレーションを実行
try
{
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Starting database migration...");
    
    // Create DbContext with custom options to suppress warnings
    var optionsBuilder = new DbContextOptionsBuilder<TicketDbContext>();
    optionsBuilder.UseSqlServer(connectionString);
    optionsBuilder.ConfigureWarnings(warnings =>
        warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    
    using var dbContext = new TicketDbContext(optionsBuilder.Options);
    await dbContext.Database.MigrateAsync();
    
    logger.LogInformation("Database migration completed successfully.");
}
catch (Exception ex)
{
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An error occurred while migrating the database.");
    throw;
}