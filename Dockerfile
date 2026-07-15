# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy everything
COPY src/ src/
COPY Logo/ Logo/
COPY frontend/ frontend/
COPY package.json yarn.lock ./
COPY tsconfig.json ./

# Install Node.js for frontend build
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    && curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
    && apt-get install -y nodejs \
    && npm install -g yarn \
    && rm -rf /var/lib/apt/lists/*

# Build frontend
RUN yarn install --frozen-lockfile --network-timeout 120000
RUN yarn build

# Build backend
WORKDIR /src
RUN dotnet publish src/NzbDrone.Host/Readarr.Host.csproj \
    -c Release -f net6.0 -o /app/publish --no-restore \
    -p:TreatWarningsAsErrors=false \
    -p:EnableSourceControlManagerQueries=false

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

# Copy build output
COPY --from=build /app/publish/. /app/

# Copy frontend UI
COPY --from=build /src/_output/UI/. /app/UI/

# Expose port (8192 to avoid conflict with Sonarr on 8989)
EXPOSE 8192

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8192/api/v1/health || exit 1

# Run
ENTRYPOINT ["dotnet", "Readarr.dll"]
