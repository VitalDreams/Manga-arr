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

# Build backend - nuclear option: strip Sentry before build
WORKDIR /src/src
RUN sed -i '/PackageReference.*Sentry/d' NzbDrone.Common/Readarr.Common.csproj

# Remove Sentry source files (they reference types from the stripped NuGet package)
RUN rm -f NzbDrone.Common/Instrumentation/Sentry/SentryCleanser.cs \
          NzbDrone.Common/Instrumentation/Sentry/SentryTarget.cs \
          NzbDrone.Common/Instrumentation/Sentry/SentryDebounce.cs \
          NzbDrone.Core/Instrumentation/ReconfigureSentry.cs \
          NzbDrone.Common.Test/InstrumentationTests/SentryTargetFixture.cs

# Clean up NzbDroneLogger.cs - remove Sentry references
RUN sed -i '/using NzbDrone.Common.Instrumentation.Sentry;/d' NzbDrone.Common/Instrumentation/NzbDroneLogger.cs && \
    sed -i '/RegisterSentry(updateApp, appFolderInfo);/d' NzbDrone.Common/Instrumentation/NzbDroneLogger.cs && \
    sed -i '/private static void RegisterSentry/,/^        private/ { /^        private/!d; }' NzbDrone.Common/Instrumentation/NzbDroneLogger.cs

# Clean up InitializeLogger.cs - remove Sentry references
RUN sed -i '/using NzbDrone.Common.Instrumentation.Sentry;/d' NzbDrone.Common/Instrumentation/InitializeLogger.cs && \
    sed -i '/var sentryTarget = LogManager.Configuration.AllTargets.OfType<SentryTarget>().FirstOrDefault();/,/}/d' NzbDrone.Common/Instrumentation/InitializeLogger.cs

# Clean up ReconfigureLogging.cs - remove Sentry references
RUN sed -i '/using NzbDrone.Common.Instrumentation.Sentry;/d' NzbDrone.Core/Instrumentation/ReconfigureLogging.cs && \
    sed -i '/ReconfigureSentry();/d' NzbDrone.Core/Instrumentation/ReconfigureLogging.cs && \
    sed -i '/private void ReconfigureSentry()/,/^        private void SetSyslogParameters/ { /^        private void SetSyslogParameters/!d; }' NzbDrone.Core/Instrumentation/ReconfigureLogging.cs

RUN dotnet nuget locals all --clear
RUN dotnet msbuild -restore Readarr.sln \
    -p:Configuration=Release \
    -p:Platform=Posix \
    -p:RuntimeIdentifiers=linux-x64 \
    -t:PublishAllRids \
    -p:TreatWarningsAsErrors=false \
    -nowarn:NU1902,NU1903 > /tmp/build.log 2>&1 || (cat /tmp/build.log | grep -i 'error' | head -30; exit 1)

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
