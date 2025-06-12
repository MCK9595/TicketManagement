using Microsoft.EntityFrameworkCore;
using TicketManagement.Infrastructure.Data;

var builder = Host.CreateApplicationBuilder(args);

// Add service defaults & SQL Server database
builder.AddServiceDefaults();
builder.AddSqlServerDbContext<TicketDbContext>("TicketDB");

var host = builder.Build();

// マイグレーションを実行
try
{
    using var scope = host.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<TicketDbContext>();
    
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Starting database migration...");
    
    await dbContext.Database.MigrateAsync();
    
    logger.LogInformation("Database migration completed successfully.");
}
catch (Exception ex)
{
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An error occurred while migrating the database.");
    throw;
}