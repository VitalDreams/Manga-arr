# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy everything
COPY src/ src/
COPY Logo/ Logo/
COPY frontend/ frontend/
COPY package.json yarn.lock ./

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

# Build backend using Readarr's msbuild approach
WORKDIR /src/src
ENV SENTRY_SKIP_RELEASE=true
ENV SENTRY_AUTH_TOKEN=
RUN dotnet msbuild -restore Readarr.sln \
    -p:Configuration=Release \
    -p:Platform=Posix \
    -p:RuntimeIdentifiers=linux-x64 \
    -t:PublishAllRids \
    -p:TreatWarningsAsErrors=false \
    -p:EnableSentryRelease=false \
    -p:SentryUploadSymbols=false \
    -nowarn:NU1902,NU1903,SA1600,SA1309,SA1101,SA1202,SA1633

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
COPY --from=build /src/_output/net6.0/linux-x64/. /app/

# Copy frontend UI
COPY --from=build /src/_output/UI/. /app/UI/

# Expose port (8192 to avoid conflict with Sonarr on 8989)
EXPOSE 8192

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8192/api/v1/health || exit 1

# Run
ENTRYPOINT ["dotnet", "Readarr.dll"]
