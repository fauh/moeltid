# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore in a separate layer so package cache survives source changes
COPY src/Moeltid/Moeltid.csproj src/Moeltid/
RUN dotnet restore src/Moeltid/Moeltid.csproj

COPY src/ src/
RUN dotnet publish src/Moeltid/Moeltid.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

# su-exec: cleanly drops root privileges to appuser at container startup.
# wget:    used by Docker HEALTHCHECK.
RUN apk add --no-cache su-exec wget

# Create a non-root user for the app process
RUN adduser -u 5678 -D -H appuser

WORKDIR /app
COPY --from=build /app/publish .

# /data is the mount point for the Render persistent disk.
# Ownership is fixed at startup (not here) because the mounted volume overwrites
# this directory's metadata — see entrypoint.sh.
RUN mkdir -p /data

COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

HEALTHCHECK --interval=30s --timeout=10s --retries=3 \
    CMD wget -qO- http://localhost:8080/health || exit 1

ENTRYPOINT ["/entrypoint.sh"]
