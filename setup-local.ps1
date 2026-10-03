$ErrorActionPreference = 'Stop'

Write-Host 'Checking .NET 10...' -ForegroundColor Cyan
dotnet --version

Write-Host 'Starting SQL Server container...' -ForegroundColor Cyan
docker compose -f docker-compose.sqlserver.yml up -d

Write-Host 'Restoring backend...' -ForegroundColor Cyan
dotnet restore backend/HealthApp.sln

Write-Host 'Building backend...' -ForegroundColor Cyan
dotnet build backend/HealthApp.sln --configuration Debug

Write-Host 'Starting API. EF Core will create/update the database and seed demo data...' -ForegroundColor Cyan
dotnet run --project backend/src/HealthApp.Api --configuration Debug
