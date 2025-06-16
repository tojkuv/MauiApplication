var builder = DistributedApplication.CreateBuilder(args);

// Add SQL Server database
var sql = builder.AddSqlServer("sql")
    .WithDataVolume()
    .AddDatabase("mauiappdb");

// Add Azure Application Insights
var appInsights = builder.AddAzureApplicationInsights("appinsights");

// Add Azure Key Vault for secrets management
var keyVault = builder.AddAzureKeyVault("keyvault");

// Add Azure Storage for file storage
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator()
    .AddBlobs("blobs");

// Add the API service
var apiService = builder.AddProject<Projects.MauiApp_ApiService>("apiservice")
    .WithReference(sql)
    .WithReference(appInsights)
    .WithReference(keyVault)
    .WithReference(storage)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

// Add health checks endpoint
apiService.WithHealthCheck("/health");

// Build and run the app
var app = builder.Build();

app.Run();