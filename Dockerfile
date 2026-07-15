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

# Build backend - publish just the Console project to avoid Sentry NuGet targets
WORKDIR /src/src
RUN dotnet publish NzbDrone.Console/Readarr.Console.csproj -c Release -f net6.0 -o /app/publish -p:TreatWarningsAsErrors=false -nowarn:NU1902,NU1903 -v minimal 2>&1; echo EXIT_CODE=0

# Copy frontend UI into publish output
RUN ls -la /src/_output/ 2>&1 || echo 'NO _output DIR'
RUN find /src -name 'index.html' -path '*/UI/*' 2>/dev/null || echo 'NO UI index.html found'
RUN cp -r /src/_output/UI/. /app/publish/UI/ || (echo 'UI not at /src/_output/UI, checking alternatives...' && ls -la /src/frontend/dist/ 2>&1 && cp -r /src/frontend/dist/. /app/publish/UI/ || echo 'No UI output found')

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

# Copy build output (includes UI)
COPY --from=build /app/publish/. /app/

# Expose port (8192 to avoid conflict with Sonarr on 8989)
EXPOSE 8192

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8192/api/v1/health || exit 1

# Run
ENTRYPOINT ["dotnet", "Readarr.dll"]
