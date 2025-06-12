# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a task-based ticket management system built with .NET Aspire (9.3.0) using Clean Architecture principles. The system provides project-based task ticket management with team collaboration features.

## Technology Stack

- **.NET 9.0** with C#
- **.NET Aspire** for distributed application orchestration
- **Entity Framework Core 9.0.5** with SQL Server
- **Blazor Server** for the frontend
- **Keycloak** for authentication
- **Redis** for caching and session management
- **NUnit** for testing

## Architecture

The solution follows Clean Architecture with Domain-Driven Design:

- **TicketManagement.AppHost**: Aspire orchestrator managing all services (SQL Server, Redis, Keycloak)
- **TicketManagement.ServiceDefaults**: Shared configuration for OpenTelemetry, health checks, service discovery
- **TicketManagement.Web**: Blazor Server frontend
- **TicketManagement.ApiService**: Web API backend
- **src/TicketManagement.Core**: Domain entities, enums, business logic
- **src/TicketManagement.Infrastructure**: Data access layer with EF Core, DbContext configurations
- **src/TicketManagement.Contracts**: Shared interfaces and DTOs
- **tests/**: Unit tests (NUnit) and Aspire integration tests

## Essential Commands

### Building and Running
```bash
# Build entire solution
dotnet build

# Run the Aspire application (starts all services)
dotnet run --project TicketManagement.AppHost

# Build specific project
dotnet build src/TicketManagement.Core
```

### Testing
```bash
# Run all tests
dotnet test

# Run unit tests only
dotnet test tests/TicketManagement.Tests

# Run integration tests only (requires Aspire services)
dotnet test tests/TicketManagement.IntegrationTests

# Run specific test with filter
dotnet test --filter "TestName"

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Database Management
```bash
# Add new migration (from Infrastructure project directory)
cd src/TicketManagement.Infrastructure
dotnet ef migrations add <MigrationName> --startup-project ../../TicketManagement.AppHost

# Update database
dotnet ef database update --startup-project ../../TicketManagement.AppHost

# Generate SQL script
dotnet ef migrations script --startup-project ../../TicketManagement.AppHost
```

## Key Architectural Patterns

### Domain Model
- **Project**: Container for tickets with member management and role-based access
- **Ticket**: Core entity with status workflow, priority levels, assignments, and history tracking
- **Comments**: Threaded discussions on tickets
- **Notifications**: Real-time notifications via SignalR
- **History Tracking**: Automatic change tracking for all ticket modifications

### Data Access
- Entity Framework Core with Code-First approach
- Repository pattern implemented in Infrastructure layer
- Entity configurations in `Data/Configurations/` for complex mappings
- String arrays stored as comma-separated values in database
- Optimized indexes for queries on status, priority, and project relationships

### Authentication & Authorization
- Keycloak integration for unified authentication
- JWT token-based authorization
- Role-based access control (Viewer, Member, Admin) at project level
- User context available throughout the application via Keycloak claims

### Aspire Service Orchestration
- AppHost configures SQL Server with data volumes
- Redis with persistence for caching and sessions
- Keycloak with data volumes and realm configuration
- Service discovery and health checks on `/health` endpoints
- All services referenced and connected automatically

### Real-time Features
- SignalR hubs for live notifications
- Real-time ticket updates and comments
- User presence and activity tracking

## Development Workflow

When adding new features:
1. Define domain entities in `Core/Entities/`
2. Add EF Core configurations in `Infrastructure/Data/Configurations/`
3. Create DTOs in `Contracts/`
4. Implement API endpoints in `ApiService/`
5. Add Blazor components in `Web/`
6. Write unit tests in `Tests/` and integration tests in `IntegrationTests/`

## Task Management

When working with TODO.md:
- **MUST mark tasks as completed**: When completing tasks listed in TODO.md, update the task status by adding a checkmark (`[x]`) or marking it as "completed"
- **MUST track progress systematically**: Use the TodoWrite and TodoRead tools to maintain an accurate task status throughout development
- **MUST update TODO.md regularly**: Keep the todo list current by marking completed items and adding new tasks as they arise
- **MUST reference completed tasks**: When finishing implementation work, explicitly note which TODO.md items have been completed in your summary

## Mandatory Testing Requirements

**CRITICAL**: Always follow these testing requirements:

### After Source Code Modifications
- **MUST run tests after any source code change**: Execute `dotnet test` immediately after completing any task that modifies source code
- **MUST verify all tests pass**: Ensure both unit tests and integration tests complete successfully before considering a task complete
- **MUST fix failing tests**: If tests fail after code changes, fix the issues before proceeding to other tasks

### When Adding or Modifying Source Code
- **MUST add corresponding test code**: Every new feature, bug fix, or code modification requires accompanying test code
- **MUST update existing tests**: Modify existing test cases when changing behavior of existing functionality
- **MUST write tests BEFORE or ALONGSIDE implementation**: Test-driven development approach is mandatory
- **MUST achieve adequate test coverage**: Aim for comprehensive coverage of new functionality

### Test Types Required
- **Unit Tests**: For business logic, domain entities, and service methods in `tests/TicketManagement.Tests/`
- **Integration Tests**: For API endpoints, database interactions, and cross-service communication in `tests/TicketManagement.IntegrationTests/`
- **Aspire Integration Tests**: For testing the complete application stack with all Aspire services

### Testing Commands to Use
```bash
# Always run after code changes
dotnet test

# For specific test suites
dotnet test tests/TicketManagement.Tests
dotnet test tests/TicketManagement.IntegrationTests
```

## Project Dependencies

Infrastructure depends on Core for domain models. API and Web projects reference Infrastructure for data access and Core for domain logic. The AppHost orchestrates all services and manages their lifecycle.