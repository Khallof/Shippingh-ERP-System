# Build context = solution root. Build with:
#   docker build -f src/ShippingERP.API/Dockerfile -t shippingerp-api .

# ---------- Runtime base image ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

# ---------- Build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy only project files first, so "dotnet restore" is cached when only .cs files change.
COPY ["global.json", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["src/ShippingERP.Domain/ShippingERP.Domain.csproj", "src/ShippingERP.Domain/"]
COPY ["src/ShippingERP.Application/ShippingERP.Application.csproj", "src/ShippingERP.Application/"]
COPY ["src/ShippingERP.Infrastructure/ShippingERP.Infrastructure.csproj", "src/ShippingERP.Infrastructure/"]
COPY ["src/ShippingERP.API/ShippingERP.API.csproj", "src/ShippingERP.API/"]
RUN dotnet restore "src/ShippingERP.API/ShippingERP.API.csproj"

# Copy the rest of the source and publish.
COPY ["src/", "src/"]
RUN dotnet publish "src/ShippingERP.API/ShippingERP.API.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ---------- Final image ----------
FROM base AS final
WORKDIR /app

# libfontconfig1: native dependency of the PDF engine (QuestPDF / Skia) on Linux.
# curl: used only by the container HEALTHCHECK below.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 curl \
    && rm -rf /var/lib/apt/lists/*

# The .NET 10 image runs as a non-root user ($APP_UID). Give it writable folders for logs and uploaded files.
RUN mkdir -p /app/logs /app/App_Data/uploads && chown -R $APP_UID:$APP_UID /app/logs /app/App_Data

COPY --from=build /app/publish .

USER $APP_UID

# Liveness only (the process answers); readiness (/health/ready) also checks SQL Server.
HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
  CMD curl -fsS http://127.0.0.1:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "ShippingERP.API.dll"]
