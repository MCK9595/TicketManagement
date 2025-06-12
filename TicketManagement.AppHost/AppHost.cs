var builder = DistributedApplication.CreateBuilder(args);

// Redis Cache - temporarily disabled due to connection issues
// var redis = builder.AddRedis("redis")
//     .WithDataVolume("redis-data");

// SQL Server
var sqlServer = builder.AddSqlServer("sql")
    .WithDataVolume("sql-data");

var ticketDb = sqlServer.AddDatabase("TicketDB");

// Keycloak with fixed port for development
var keycloak = builder.AddKeycloak("keycloak", 8080)
    .WithDataVolume()  // Add data persistence
    .WithRealmImport("./Realms");

// API Service
var apiService = builder.AddProject<Projects.TicketManagement_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithReference(ticketDb)
    // .WithReference(redis)  // Temporarily disabled
    .WithReference(keycloak)
    .WaitFor(keycloak);

// Web Frontend
builder.AddProject<Projects.TicketManagement_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    // .WithReference(redis)  // Temporarily disabled
    .WithReference(keycloak)
    .WaitFor(apiService);

builder.Build().Run();
