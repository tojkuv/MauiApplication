# Database Setup Guide

## Overview
This guide explains how to set up the comprehensive database schema for the Enterprise Project Management Platform. The schema supports all 8 microservices with proper relationships and data integrity.

## Database Architecture

### Single Database vs Microservice Databases
While microservices typically use separate databases, our implementation uses a **shared database approach** for the following reasons:

1. **Data Consistency**: Ensures ACID transactions across related entities
2. **Simplified Development**: Easier to maintain relationships and referential integrity
3. **MVP Approach**: Faster development and deployment for initial release
4. **Cost Effectiveness**: Single database instance reduces infrastructure costs

## Schema Components

### Core Tables
- **Users & Identity**: ASP.NET Identity integration with user management
- **Projects**: Project lifecycle management with team assignments
- **Tasks**: Comprehensive task management with dependencies
- **Files**: File storage with versioning and sharing
- **Collaboration**: Chat messages and real-time communication
- **Notifications**: Multi-channel notification system
- **Sync**: Offline synchronization and conflict resolution
- **Analytics**: Data aggregation and performance metrics

### Key Features
- **Audit Logging**: Comprehensive change tracking
- **Performance Optimization**: Strategic indexes for common queries
- **Security Functions**: Role-based access control helpers
- **Data Retention**: Automated cleanup procedures
- **Business Logic**: Constraints and triggers for data integrity

## Implementation Steps

### 1. Database Creation
```sql
-- Create the main database
CREATE DATABASE MauiAppEnterprise;
USE MauiAppEnterprise;

-- Run the comprehensive schema script
-- Execute: ComprehensiveSchema.sql
```

### 2. Connection Strings
Update all microservice `appsettings.json` files:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=MauiAppEnterprise;Trusted_Connection=true;MultipleActiveResultSets=true"
  }
}
```

### 3. Service Configuration
Each microservice will connect to the same database but focus on its domain entities:

- **Identity Service**: Users, Roles, Claims, RefreshTokens
- **Projects Service**: Projects, ProjectMembers, Milestones
- **Tasks Service**: Tasks, TaskComments, TaskAttachments, TimeEntries
- **Files Service**: ProjectFiles, FileVersions, FileShares, FileComments
- **Collaboration Service**: ChatMessages, MessageReactions
- **Notification Service**: Notifications, DeviceTokens, NotificationPreferences
- **Sync Service**: SyncClients, SyncItems
- **Analytics Service**: AnalyticsCache, DailyMetrics, UserActivityLogs

## Entity Framework Setup

### 1. Update DbContext Classes
Each service's DbContext should inherit from a base context:

```csharp
public abstract class BaseDbContext : DbContext
{
    protected BaseDbContext(DbContextOptions options) : base(options) { }
    
    // Common entities that all services might need to read
    public DbSet<ApplicationUser> Users { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<ProjectTask> Tasks { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Common configuration
        base.OnModelCreating(modelBuilder);
    }
}
```

### 2. Migration Strategy
Since we're using a shared database, we need a coordinated migration approach:

1. **Primary Service**: Identity Service manages core schema
2. **Secondary Services**: Add their specific tables/indexes
3. **Coordination**: Use migration naming conventions to avoid conflicts

## Security Considerations

### 1. Database Access
- Each service uses the same connection but with appropriate table permissions
- Read-only access for cross-service data queries
- Write access only to owned entities

### 2. Data Protection
- Sensitive data encryption at rest
- Connection string encryption
- Audit logging for all changes

### 3. Performance
- Strategic indexing for common query patterns
- Query optimization for cross-service operations
- Connection pooling configuration

## Monitoring and Maintenance

### 1. Performance Monitoring
- Query execution time tracking
- Index usage analysis
- Connection pool monitoring

### 2. Data Retention
- Automated cleanup procedures
- Archive old audit logs
- Purge expired tokens and cache

### 3. Backup Strategy
- Daily full backups
- Transaction log backups every 15 minutes
- Point-in-time recovery capability

## Development Workflow

### 1. Local Development
```bash
# Setup local database
sqlcmd -S "(localdb)\mssqllocaldb" -i ComprehensiveSchema.sql

# Run all services
dotnet run --project MauiApp.IdentityService
dotnet run --project MauiApp.ProjectsService
# ... etc
```

### 2. Testing
- Integration tests with shared test database
- Cleanup procedures between test runs
- Mock external dependencies

### 3. Deployment
- Database schema versioning
- Migration coordination
- Zero-downtime deployment strategies

## Best Practices

### 1. Code Organization
- Keep domain-specific logic in respective services
- Use shared DTOs for cross-service communication
- Implement proper error handling and retry logic

### 2. Performance
- Use async/await for all database operations
- Implement caching strategies
- Monitor and optimize slow queries

### 3. Maintainability
- Document all schema changes
- Use consistent naming conventions
- Implement proper logging and monitoring

## Troubleshooting

### Common Issues
1. **Connection Timeouts**: Increase connection timeout in connection string
2. **Lock Timeouts**: Optimize queries and use appropriate isolation levels
3. **Migration Conflicts**: Coordinate changes across services
4. **Performance Issues**: Analyze query execution plans and add indexes

### Monitoring Queries
```sql
-- Check active connections
SELECT * FROM sys.dm_exec_sessions WHERE is_user_process = 1;

-- Monitor slow queries
SELECT * FROM sys.dm_exec_query_stats 
ORDER BY total_elapsed_time DESC;

-- Check index usage
SELECT * FROM sys.dm_db_index_usage_stats 
WHERE database_id = DB_ID('MauiAppEnterprise');
```

## Next Steps

After database setup:
1. Update all microservice DbContext classes
2. Run database migrations
3. Test cross-service data access
4. Implement proper monitoring
5. Set up backup procedures
6. Begin MAUI client implementation

This shared database approach provides a solid foundation for our MVP while maintaining the flexibility to split into separate databases in the future if needed.