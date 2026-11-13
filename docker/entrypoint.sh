#!/bin/sh
# Inicializa la base con los scripts versionados (idempotentes) y arranca la API.
#
# Variables:
#   DB_PATH   ruta del archivo SQLite            (default /app/data/banco.db)
#   DB_INIT   aplicar masterSQL.sql al arrancar  (default true)
#   DB_SEED   aplicar seed.sql al arrancar       (default true)
#   DatabaseOptions__ConnectionString  si no viene, se arma a partir de DB_PATH
set -eu

DB_PATH="${DB_PATH:-/app/data/banco.db}"

if [ "${DB_INIT:-true}" = "true" ]; then
    echo "[entrypoint] Aplicando database/masterSQL.sql sobre $DB_PATH (idempotente)"
    sqlite3 -bail "$DB_PATH" < /app/database/masterSQL.sql

    if [ "${DB_SEED:-true}" = "true" ]; then
        echo "[entrypoint] Aplicando database/seed.sql (idempotente)"
        sqlite3 -bail "$DB_PATH" < /app/database/seed.sql
    fi
fi

: "${DatabaseOptions__ConnectionString:=Data Source=$DB_PATH;Mode=ReadWrite;Foreign Keys=True}"
export DatabaseOptions__ConnectionString

exec dotnet Banco.Api.dll
