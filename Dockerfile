# ── Build stage ────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /build

COPY src/ ./
RUN dotnet restore bgci.slnx
RUN dotnet publish Api/Api.csproj -c Release -o /app/publish --no-restore

# ── Runtime stage ──────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ARG DISPLAY_VERSION=dev
ENV APP_VERSION=$DISPLAY_VERSION

COPY --from=build /app/publish .

# Data directory for SQLite
RUN mkdir -p /data

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "Api.dll"]
