# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

# Copy solution & project files first (layer-cache friendly)
COPY CqrsPoC.sln ./
COPY src/CqrsPoC.Contracts/CqrsPoC.Contracts.csproj         src/CqrsPoC.Contracts/
COPY src/CqrsPoC.Domain/CqrsPoC.Domain.csproj               src/CqrsPoC.Domain/
COPY src/CqrsPoC.Application/CqrsPoC.Application.csproj     src/CqrsPoC.Application/
COPY src/CqrsPoC.Infrastructure/CqrsPoC.Infrastructure.csproj src/CqrsPoC.Infrastructure/
COPY src/CqrsPoC.API/CqrsPoC.API.csproj                     src/CqrsPoC.API/

RUN dotnet restore

# Copy everything else and publish
COPY . .
RUN dotnet publish src/CqrsPoC.API/CqrsPoC.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Stage 2: Runtime ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Non-root user for security
RUN adduser --disabled-password --no-create-home appuser
USER appuser

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "CqrsPoC.API.dll"]
