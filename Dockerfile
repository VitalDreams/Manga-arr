# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy everything
COPY src/ src/
COPY Logo/ Logo/

# Restore (ignore NuGet vulnerability warnings)
WORKDIR /src/src
RUN dotnet restore Readarr.sln /p:TreatWarningsAsErrors=false -nowarn:NU1902,NU1903

# Build the Console project (actual entry point)
RUN dotnet publish NzbDrone.Console/Readarr.Console.csproj -c Release -f net6.0 --self-contained false -o /app/publish --no-restore /p:TreatWarningsAsErrors=false -nowarn:NU1902,NU1903

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime
WORKDIR /app

# Install dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    sqlite3 \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Create directories
RUN mkdir -p /config /config/logs /manga /tmp/manga-arr

# Copy build
COPY --from=build /app/publish .

# Expose port (8192 to avoid conflict with Sonarr on 8989)
EXPOSE 8192

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8192/api/v3/health || exit 1

# Run
ENTRYPOINT ["dotnet", "Readarr.dll"]
