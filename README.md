# MAUI Enterprise Application with .NET Aspire

A comprehensive .NET MAUI cross-platform application with enterprise-grade architecture, comprehensive testing, and Azure deployment capabilities using .NET Aspire.

## 🏗️ Architecture Overview

This solution implements Clean Architecture principles with MVVM pattern for the mobile application and microservices architecture for the backend services.

### Project Structure

```
MauiApp.sln
├── src/
│   ├── MauiApp/                     # Main MAUI application (iOS/Android)
│   ├── MauiApp.Core/                # Shared business logic & entities
│   ├── MauiApp.Services/            # Service implementations
│   ├── MauiApp.Data/                # Data access layer with EF Core
│   ├── MauiApp.ApiService/          # ASP.NET Core Web API
│   └── MauiApp.AppHost/             # .NET Aspire orchestration
├── tests/
│   ├── MauiApp.UnitTests/           # Unit tests (90%+ coverage target)
│   └── MauiApp.IntegrationTests/    # API integration tests
└── tools/
    ├── scripts/                     # Build and deployment scripts
    └── coverage/                    # Test coverage reports
```

## 🚀 Features

### Mobile Application (MAUI)
- **Cross-platform**: iOS and Android support
- **MVVM Architecture**: Clean separation of concerns
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection
- **Navigation**: Shell-based navigation with service layer
- **Data Storage**: Secure storage for sensitive data
- **HTTP Client**: Service discovery integration with Aspire
- **Authentication**: JWT-based authentication with secure token storage

### Backend Services
- **ASP.NET Core Web API**: RESTful API with Swagger documentation
- **Entity Framework Core**: SQL Server database with code-first migrations
- **Repository Pattern**: Generic repository with Unit of Work
- **Authentication & Authorization**: JWT Bearer tokens
- **Health Checks**: Comprehensive health monitoring
- **Logging**: Structured logging with Application Insights

### .NET Aspire Integration
- **Service Orchestration**: Centralized service discovery and configuration
- **Azure Integration**: Application Insights, Key Vault, Storage
- **Database Management**: SQL Server with automatic provisioning
- **Health Monitoring**: Distributed health checks
- **Local Development**: Simplified local development experience

### Testing Strategy
- **Unit Tests**: xUnit with Moq and FluentAssertions
- **Integration Tests**: ASP.NET Core TestServer with in-memory database
- **Test Data Factories**: AutoFixture for test data generation
- **Code Coverage**: Coverlet integration with 90%+ target
- **CI/CD Ready**: GitHub Actions compatible

## 🛠️ Technology Stack

### Core Technologies
- **.NET 8.0**: Latest LTS version
- **.NET MAUI**: Multi-platform App UI
- **C# 12**: Latest language features
- **XAML**: Declarative UI markup

### Backend
- **ASP.NET Core 8.0**: Web API framework
- **Entity Framework Core 8**: Object-relational mapping
- **SQL Server**: Primary database
- **Swagger/OpenAPI**: API documentation

### Mobile Services
- **CommunityToolkit.Mvvm**: MVVM helpers and source generators
- **Microsoft.Extensions.Http**: HTTP client factory
- **Microsoft.Maui.Essentials**: Cross-platform APIs

### Testing
- **xUnit**: Testing framework
- **Moq**: Mocking framework
- **FluentAssertions**: Fluent assertion library
- **AutoFixture**: Test data generation
- **Coverlet**: Code coverage analysis

### DevOps & Deployment
- **.NET Aspire**: Application orchestration
- **Azure**: Cloud deployment platform
- **Application Insights**: Application monitoring
- **Azure Key Vault**: Secrets management

## 🚦 Getting Started

### Prerequisites

1. **.NET 8 SDK** or later
2. **Visual Studio 2022** (17.8+) or **Visual Studio Code**
3. **.NET Aspire workload**: `dotnet workload install aspire`
4. **MAUI workload**: `dotnet workload install maui`
5. **SQL Server** (LocalDB, Express, or full version)

### Initial Setup

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd MAUI_PROJECT
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Set up the database**
   ```bash
   cd src/MauiApp.ApiService
   dotnet ef database update
   ```

4. **Run with Aspire (Recommended)**
   ```bash
   cd src/MauiApp.AppHost
   dotnet run
   ```
   This will start the Aspire dashboard and launch all services.

### Development Workflow

#### Running Individual Projects

**API Service**
```bash
cd src/MauiApp.ApiService
dotnet run
```

**MAUI Application**
```bash
cd src/MauiApp
dotnet build -f net8.0-android    # For Android
dotnet build -f net8.0-ios        # For iOS
```

#### Running Tests

**Unit Tests**
```bash
dotnet test tests/MauiApp.UnitTests/
```

**Integration Tests**
```bash
dotnet test tests/MauiApp.IntegrationTests/
```

**All Tests with Coverage**
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## 📱 Mobile Application Features

### Core Features
- **User Management**: Create, read, update, delete users
- **Authentication**: Secure login with JWT tokens
- **Navigation**: Shell-based navigation between pages
- **Data Synchronization**: Online/offline data sync
- **Error Handling**: Comprehensive error handling and logging

### UI/UX
- **Material Design**: Android Material Design components
- **iOS Design**: iOS-native look and feel
- **Dark/Light Theme**: Automatic theme switching
- **Accessibility**: WCAG 2.1 AA compliance

## 🌐 API Endpoints

### Users API
- `GET /api/users` - Get all users
- `GET /api/users/{id}` - Get user by ID
- `POST /api/users` - Create new user
- `PUT /api/users/{id}` - Update user
- `DELETE /api/users/{id}` - Delete user

### Health Checks
- `GET /health` - Application health status

### Swagger Documentation
- Available at `/swagger` when running in development mode

## 🔧 Configuration

### App Settings
Key configuration files:
- `src/MauiApp.ApiService/appsettings.json` - API configuration
- `src/MauiApp.AppHost/appsettings.json` - Aspire configuration

### Environment Variables
- `ASPNETCORE_ENVIRONMENT` - Environment (Development/Staging/Production)
- `ConnectionStrings__DefaultConnection` - Database connection string

## 🧪 Testing

### Test Coverage Goals
- **Unit Tests**: 90%+ code coverage
- **Integration Tests**: All API endpoints covered
- **UI Tests**: Critical user journeys (planned)

### Test Categories
1. **Unit Tests**: Business logic, services, repositories
2. **Integration Tests**: API endpoints, database operations
3. **Performance Tests**: Load testing, memory usage (planned)
4. **Security Tests**: Authentication, authorization (planned)

## 📊 Monitoring & Observability

### Application Insights
- Request tracking
- Exception logging
- Performance counters
- Custom metrics

### Health Checks
- Database connectivity
- External service dependencies
- Application-specific health indicators

## 🚀 Deployment

### Azure Deployment
The application is designed for Azure deployment using .NET Aspire:

1. **Azure Resources**:
   - App Service for API
   - SQL Database
   - Application Insights
   - Key Vault for secrets

2. **CI/CD Pipeline** (planned):
   - GitHub Actions
   - Automated testing
   - Code coverage reporting
   - Automated deployment

### Local Development
Use the Aspire App Host for local development:
```bash
cd src/MauiApp.AppHost
dotnet run
```

## 📚 Additional Resources

- [.NET MAUI Documentation](https://docs.microsoft.com/en-us/dotnet/maui/)
- [.NET Aspire Documentation](https://docs.microsoft.com/en-us/dotnet/aspire/)
- [Clean Architecture Guide](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [MVVM Pattern](https://docs.microsoft.com/en-us/xamarin/xamarin-forms/enterprise-application-patterns/mvvm)

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Write tests for new functionality
4. Ensure all tests pass
5. Submit a pull request

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

---

**Note**: This is an enterprise-grade template designed for production use. Ensure you follow security best practices and conduct thorough testing before deploying to production environments.