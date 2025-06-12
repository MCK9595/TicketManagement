using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TicketManagement.Infrastructure.Data;

public class TicketDbContextFactory : IDesignTimeDbContextFactory<TicketDbContext>
{
    public TicketDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TicketDbContext>();
        
        // Use a temporary connection string for migrations
        // This will be replaced with the actual connection string at runtime
        optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=TicketManagementDb;Trusted_Connection=true;MultipleActiveResultSets=true");
        
        return new TicketDbContext(optionsBuilder.Options);
    }
}