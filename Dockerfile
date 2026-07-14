# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY *.sln ./
COPY src/NzbDrone.Core/*.csproj src/NzbDrone.Core/
COPY src/NzbDrone.Api/*.csproj src/NzbDrone.Api/
COPY src/NzbDrone.Common/*.csproj src/NzbDrone.Common/
COPY src/NzbDrone.Host/*.csproj src/NzbDrone.Host/

# Restore
RUN dotnet restore

# Copy source
COPY src/ src/

# Build
RUN dotnet publish src/NzbDrone.Api/NzbDrone.Api.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    sqlite3 \
    && rm -rf /var/lib/apt/lists/*

# Create directories
RUN mkdir -p /config /config/logs /manga /tmp/manga-arr

# Copy build
COPY --from=build /app/publish .

# Expose port
EXPOSE 8989

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8989/api/v3/health || exit 1

# Run
ENTRYPOINT ["dotnet", "NzbDrone.Api.dll"]
