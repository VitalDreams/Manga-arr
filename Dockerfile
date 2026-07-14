# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy everything
COPY src/ src/
COPY Logo/ Logo/
COPY build.sh ./

# Restore and build using Readarr's actual build system
WORKDIR /src
RUN chmod +x build.sh && \
    export READARRVERSION="0.1.0" && \
    export RID="linux-x64" && \
    export FRAMEWORK="net6.0" && \
    bash build.sh

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
COPY --from=build /src/_output/Readarr/. /app/

# Expose port (8192 to avoid conflict with Sonarr on 8989)
EXPOSE 8192

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8192/api/v3/health || exit 1

# Run
ENTRYPOINT ["dotnet", "Readarr.dll"]
