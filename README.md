# MomCare - Clean Architecture

A modern, scalable ASP.NET Core 9.0 application built with **Clean Architecture** principles to provide nursing and postpartum care services.

## Architecture

This solution follows Clean Architecture with clear separation of concerns:

```
src/
├── MomCare.Api              # Presentation Layer (ASP.NET Core Web API)
│   ├── Controllers/         # API endpoints
│   ├── Hubs/                # SignalR real-time communication
│   ├── Program.cs           # Application entry point
│   └── appsettings*.json    # Configuration
├── MomCare.Application      # Application Layer (Business Logic)
│   ├── Dto/                 # Data Transfer Objects
│   ├── Interfaces/          # Service abstractions
│   ├── Exceptions/          # Custom exceptions
│   ├── Validator/           # Input validation
│   └── DependencyInjection.cs
├── MomCare.Domain           # Domain Layer (Core Business Rules)
│   ├── Models/              # Entity models & aggregates
│   └── Enums/               # Domain enumerations
└── MomCare.Infrastructure   # Infrastructure Layer (Data & External Services)
    ├── Data/                # DbContext & seeding
    ├── Repositories/        # Repository implementations
    ├── Services/            # Infrastructure services (Auth, JWT, etc.)
    ├── Mapper/              # AutoMapper configurations
    ├── Migrations/          # EF Core migrations
    └── DependencyInjection.cs
```

### Layer Responsibilities

- **Domain**: Business entities, rules, and value objects (framework-independent)
- **Application**: Use cases, DTOs, interfaces, and validators
- **Infrastructure**: Database access, external service integrations, implementations
- **Api**: HTTP routing, request/response handling, dependency injection wiring

## Prerequisites

- **.NET 9.0 SDK** or later
- **SQL Server** (LocalDB or Express)
- **Visual Studio 2022** or **VS Code with C# extension**

## Getting Started

### 1. Clone the Repository

```bash
git clone <repository-url>
cd MomCare
```

### 2. Configure Environment

Copy the `.env.example` template to `.env` in the API project:

```bash
cp src/MomCare.Api/.env.example src/MomCare.Api/.env
```

Update values for your environment:
```
ASPNETCORE_ENVIRONMENT=Development
ConnectionStrings__DefaultConnection=Server=localhost;Database=MomCareDb;Trusted_Connection=true;
JwtSettings__SecretKey=<your-secret-key-here>
JwtSettings__Issuer=MomCareApi
JwtSettings__Audience=MomCareClient
JwtSettings__ExpirationMinutes=60
SeedData__Enabled=true
```

### 3. Restore & Build

```bash
dotnet restore MomCare.sln
dotnet build MomCare.sln
```

### 4. Apply Database Migrations

```bash
dotnet ef database update -p src/MomCare.Infrastructure -s src/MomCare.Api
```

Or run EF Core migrations from Visual Studio Package Manager Console.

### 5. Run the Application

```bash
dotnet run --project src/MomCare.Api
```

The API will be available at `https://localhost:5001` with Swagger UI at `/swagger`.

## Project Structure Details

### MomCare.Api
- **Type**: ASP.NET Core Web API
- **Purpose**: Presentation layer - handles HTTP requests/responses, routing, CORS, authentication
- **Key Files**:
  - `Program.cs` - Startup configuration & dependency injection
  - `Controllers/` - RESTful endpoints
  - `Hubs/` - SignalR real-time messaging
  - `appsettings.json` - Non-sensitive configuration
  - `.env` - Environment-specific sensitive configuration

### MomCare.Application
- **Type**: Class Library
- **Purpose**: Application logic & business rule enforcement
- **Key Items**:
  - `Interfaces/` - Service contracts (loosely coupled design)
  - `Dto/` - Transfer objects for API contracts
  - `Validators/` - Input validation logic
  - `Exceptions/` - Custom business exceptions
  - `DependencyInjection.cs` - Extension methods for registering application services

### MomCare.Domain
- **Type**: Class Library
- **Purpose**: Core business entities & domain logic (framework-independent)
- **Key Items**:
  - `Models/` - Entity aggregates (User, NurseProfile, Booking, etc.)
  - `Enums/` - Domain enumerations (BookingStatus, PaymentStatus, etc.)
  - **No dependencies** on external frameworks (clean core)

### MomCare.Infrastructure
- **Type**: Class Library
- **Purpose**: Data access, external service implementations, persistence
- **Key Items**:
  - `Data/MomCareContext.cs` - EF Core DbContext
  - `Repositories/` - Data access implementations
  - `Services/` - Infrastructure implementations (JWT, OAuth, notifications, etc.)
  - `Migrations/` - EF Core migration scripts
  - `DependencyInjection.cs` - Infrastructure service registration

## Development Commands

### Build
```bash
dotnet build MomCare.sln
```

### Run Tests (when added)
```bash
dotnet test MomCare.sln
```

### Create Database Migration
```bash
dotnet ef migrations add <MigrationName> -p src/MomCare.Infrastructure -s src/MomCare.Api
```

### Update Database
```bash
dotnet ef database update -p src/MomCare.Infrastructure -s src/MomCare.Api
```

### Reset Database
```bash
dotnet ef database drop -p src/MomCare.Infrastructure -s src/MomCare.Api --force
dotnet ef database update -p src/MomCare.Infrastructure -s src/MomCare.Api
```

## Technology Stack

- **Framework**: ASP.NET Core 9.0
- **ORM**: Entity Framework Core 9.0.1
- **Database**: SQL Server
- **Authentication**: JWT Bearer Tokens, OAuth2 (Google, Facebook, etc.)
- **Validation**: FluentValidation
- **Mapping**: AutoMapper
- **Security**: BCrypt password hashing
- **Real-time**: SignalR
- **Documentation**: OpenAPI/Swagger with Scalar

## Authentication & Authorization

- JWT-based authentication with refresh token support
- Role-based access control (RBAC)
- OAuth 2.0 integration for third-party providers
- Secure password hashing with BCrypt

## Key Services

- **AuthService**: User authentication, registration, token refresh
- **JwtService**: JWT token generation and validation
- **NurseService**: Nurse profile management
- **BookingService**: Booking lifecycle management
- **PaymentService**: Payment processing & status tracking
- **NotificationService**: In-app & push notifications

## CORS Configuration

The API is configured to accept requests from:
- `http://localhost:3000` (React dev server)
- `http://localhost:5173` (Vite dev server)

Update `src/MomCare.Api/Program.cs` for production domains.

## API Documentation

OpenAPI documentation is available at:
- **Development**: `https://localhost:5001/swagger`
- **Scalar UI**: `https://localhost:5001/scalar`

## Troubleshooting

### Database Connection Issues
- Verify SQL Server is running
- Check connection string in `appsettings.json` or `.env`
- Ensure database permissions for your user

### JWT Configuration
- Ensure `JwtSettings:SecretKey` is configured in `.env` or `appsettings.json`
- Key should be long enough (recommended: 256+ bits when converted to bytes)

### Port Already in Use
- Change `https_port` in `launchSettings.json` (Properties/)
- Or: `dotnet run --project src/MomCare.Api -- --urls="https://localhost:5002"`

## 📝 Git Workflow

Before pushing, ensure:
```bash
dotnet clean MomCare.sln

dotnet build MomCare.sln

git status
```

## 📄 License

[Specify your license here]

## 👥 Contributors

[Team member names]

## 📞 Support

For questions or issues, please contact the development team.

---

**Last Updated**: March 2026  
**Framework Version**: .NET 9.0  
**Architecture Pattern**: Clean Architecture
