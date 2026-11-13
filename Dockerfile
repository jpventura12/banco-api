# syntax=docker/dockerfile:1

# =============================================================================
# Etapa 1: build. Se restaura primero solo con los .csproj para aprovechar
# el cache de capas: mientras no cambien las dependencias, no se vuelve a
# ejecutar el restore.
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/Banco.Domain/Banco.Domain.csproj                 src/Banco.Domain/
COPY src/Banco.Application/Banco.Application.csproj       src/Banco.Application/
COPY src/Banco.Infrastructure/Banco.Infrastructure.csproj src/Banco.Infrastructure/
COPY src/Banco.Api/Banco.Api.csproj                       src/Banco.Api/
RUN dotnet restore src/Banco.Api/Banco.Api.csproj

COPY src/ src/
RUN dotnet publish src/Banco.Api/Banco.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# =============================================================================
# Etapa 2: runtime. Imagen ASP.NET (sin SDK) + sqlite3 para aplicar los scripts.
# Corre con el usuario no root "app" que trae la imagen oficial ($APP_UID).
# =============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

RUN apt-get update \
    && apt-get install -y --no-install-recommends sqlite3 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .
COPY database/masterSQL.sql database/seed.sql ./database/
COPY docker/entrypoint.sh ./entrypoint.sh

RUN chmod 755 ./entrypoint.sh \
    && mkdir -p /app/data /app/logs \
    && chown -R $APP_UID:$APP_UID /app/data /app/logs

USER $APP_UID

# La imagen de .NET 8 escucha en 8080 por defecto (ASPNETCORE_HTTP_PORTS=8080).
EXPOSE 8080

# La configuración (connection string, BCRA, cache) NO se hornea en la imagen:
# se inyecta con variables de entorno, ej. -e BcraOptions__BaseAddress=...
ENTRYPOINT ["./entrypoint.sh"]
