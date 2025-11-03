# Environment Variables Setup Guide

This document describes the required environment variables for deploying the SafeSignal Cloud API to production.

## Required Environment Variables

### 1. JWT_SECRET_KEY
**Purpose:** Secret key for signing and validating JWT tokens
**Security:** CRITICAL - Must be kept secret

**Generate a secure key:**
```bash
# Linux/macOS
openssl rand -base64 64

# PowerShell (Windows)
[Convert]::ToBase64String((1..64 | ForEach-Object {Get-Random -Minimum 0 -Maximum 256}))
```

**Set the variable:**
```bash
# Linux/macOS
export JWT_SECRET_KEY="your-generated-key-here"

# Windows PowerShell
$env:JWT_SECRET_KEY="your-generated-key-here"

# Docker
-e JWT_SECRET_KEY="your-generated-key-here"

# Azure App Service
az webapp config appsettings set --name your-app --resource-group your-rg \
  --settings JWT_SECRET_KEY="your-generated-key-here"

# AWS Elastic Beanstalk
# Set via AWS Console: Configuration > Software > Environment properties
```

### 2. DATABASE_CONNECTION_STRING
**Purpose:** PostgreSQL database connection string
**Security:** SENSITIVE - Contains database credentials

**Format:**
```
Host=your-db-host.postgres.database.azure.com;Port=5432;Database=safesignal;Username=your-user@your-db;Password=your-secure-password;SSL Mode=Require;Trust Server Certificate=true
```

**Set the variable:**
```bash
# Linux/macOS
export DATABASE_CONNECTION_STRING="Host=...;Port=5432;Database=safesignal;..."

# Windows PowerShell
$env:DATABASE_CONNECTION_STRING="Host=...;Port=5432;Database=safesignal;..."

# Docker
-e DATABASE_CONNECTION_STRING="Host=...;Port=5432;..."

# Azure App Service
az webapp config appsettings set --name your-app --resource-group your-rg \
  --settings DATABASE_CONNECTION_STRING="Host=...;Port=5432;..."
```

## Configuration Fallback

The application uses a fallback mechanism:
1. **First Priority:** Environment variables (recommended for production)
2. **Fallback:** appsettings.json values (for local development only)

If neither is configured, the application will throw an exception and fail to start.

## Development vs Production

### Development (.NET Local)
```bash
# Use appsettings.Development.json
# No environment variables needed
dotnet run
```

### Production (Docker Example)
```dockerfile
# Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY published/ .

# Environment variables set at runtime
ENTRYPOINT ["dotnet", "SafeSignal.Cloud.Api.dll"]
```

```bash
# docker run with environment variables
docker run -p 5118:8080 \
  -e JWT_SECRET_KEY="your-secure-key" \
  -e DATABASE_CONNECTION_STRING="Host=..." \
  -e ASPNETCORE_ENVIRONMENT="Production" \
  safesignal-api
```

### Production (Azure App Service)
```bash
# Set all environment variables via Azure CLI
az webapp config appsettings set \
  --name safesignal-api \
  --resource-group safesignal-rg \
  --settings \
    JWT_SECRET_KEY="your-secure-key" \
    DATABASE_CONNECTION_STRING="Host=..." \
    ASPNETCORE_ENVIRONMENT="Production"
```

## Verification

After setting environment variables, verify they're loaded:

```bash
# Check if variables are set
echo $JWT_SECRET_KEY
echo $DATABASE_CONNECTION_STRING

# Start the application and check logs
dotnet run
# Look for startup messages confirming configuration loaded
```

## Security Best Practices

1. **Never commit** environment variables to source control
2. **Never log** sensitive environment variables
3. **Rotate secrets** regularly (at least every 90 days)
4. **Use Azure Key Vault** or AWS Secrets Manager for production
5. **Restrict access** to environment configuration to authorized personnel only
6. **Use different secrets** for each environment (dev, staging, production)

## Troubleshooting

### Error: "JWT SecretKey not configured"
- Ensure `JWT_SECRET_KEY` environment variable is set
- Check that the variable is exported in the current shell session
- Verify the application has permission to read environment variables

### Error: "Database connection string not configured"
- Ensure `DATABASE_CONNECTION_STRING` environment variable is set
- Test the connection string format is correct
- Verify database server is accessible from the application

### Connection Refused / SSL Issues
- Ensure PostgreSQL server allows connections from application IP
- Check SSL Mode in connection string matches server requirements
- Verify firewall rules allow traffic on port 5432
