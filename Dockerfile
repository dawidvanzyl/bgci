# ── Frontend build stage ───────────────────────────────────
FROM node:22-alpine AS frontend
WORKDIR /frontend

COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci

COPY frontend/ .
RUN npm run build

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
COPY --from=frontend /frontend/dist ./wwwroot

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "Api.dll"]
