var builder = DistributedApplication.CreateBuilder(args);

// Add databases
var sql = builder.AddSqlServer("sql")
    .WithDataVolume()
    .AddDatabase("mauiappdb");

// Add Redis for caching and session management
var redis = builder.AddRedis("redis")
    .WithDataVolume();

// Add Azure Services
var appInsights = builder.AddAzureApplicationInsights("appinsights");
var keyVault = builder.AddAzureKeyVault("keyvault");
var serviceBus = builder.AddAzureServiceBus("servicebus");

// Add Azure Storage for file storage
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator()
    .AddBlobs("blobs")
    .AddQueues("queues")
    .AddTables("tables");

// Add Identity Service
var identityService = builder.AddProject<Projects.MauiApp_IdentityService>("identityservice")
    .WithReference(sql)
    .WithReference(redis)
    .WithReference(appInsights)
    .WithReference(keyVault)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

// Add Projects Service
var projectsService = builder.AddProject<Projects.MauiApp_ProjectsService>("projectsservice")
    .WithReference(sql)
    .WithReference(redis)
    .WithReference(appInsights)
    .WithReference(serviceBus)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

// Add Tasks Service
var tasksService = builder.AddProject<Projects.MauiApp_TasksService>("tasksservice")
    .WithReference(sql)
    .WithReference(redis)
    .WithReference(appInsights)
    .WithReference(serviceBus)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

// Add Collaboration Service
var collaborationService = builder.AddProject<Projects.MauiApp_CollaborationService>("collaborationservice")
    .WithReference(sql)
    .WithReference(redis)
    .WithReference(appInsights)
    .WithReference(serviceBus)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

// Add Files Service
var filesService = builder.AddProject<Projects.MauiApp_FilesService>("filesservice")
    .WithReference(sql)
    .WithReference(redis)
    .WithReference(storage)
    .WithReference(appInsights)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

// Add Analytics Service
var analyticsService = builder.AddProject<Projects.MauiApp_AnalyticsService>("analyticsservice")
    .WithReference(sql)
    .WithReference(redis)
    .WithReference(appInsights)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

// Add Sync Service
var syncService = builder.AddProject<Projects.MauiApp_SyncService>("syncservice")
    .WithReference(sql)
    .WithReference(redis)
    .WithReference(appInsights)
    .WithReference(serviceBus)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

// Add Notification Service
var notificationService = builder.AddProject<Projects.MauiApp_NotificationService>("notificationservice")
    .WithReference(redis)
    .WithReference(appInsights)
    .WithReference(serviceBus)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

// Add API Gateway (Enhanced existing API service)
var apiGateway = builder.AddProject<Projects.MauiApp_ApiService>("apigateway")
    .WithReference(identityService)
    .WithReference(projectsService)
    .WithReference(tasksService)
    .WithReference(collaborationService)
    .WithReference(filesService)
    .WithReference(analyticsService)
    .WithReference(syncService)
    .WithReference(notificationService)
    .WithReference(redis)
    .WithReference(appInsights)
    .WithReference(keyVault)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

// Add health checks for all services
identityService.WithHealthCheck("/health");
projectsService.WithHealthCheck("/health");
tasksService.WithHealthCheck("/health");
collaborationService.WithHealthCheck("/health");
filesService.WithHealthCheck("/health");
analyticsService.WithHealthCheck("/health");
syncService.WithHealthCheck("/health");
notificationService.WithHealthCheck("/health");
apiGateway.WithHealthCheck("/health");

// Build and run the app
var app = builder.Build();

app.Run();