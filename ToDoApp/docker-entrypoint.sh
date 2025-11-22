#!/bin/bash
set -euo pipefail

DB_HOST="${DB_HOST:-db}"
DB_PORT="${DB_PORT:-3306}"
DB_NAME="${DB_NAME:-ToDo}"
DB_USER="${DB_USER:-todoapp}"
DB_PASSWORD="${DB_PASSWORD:-change_me}"

if [ -z "${ConnectionStrings__DefaultConnection:-}" ]; then
    export ConnectionStrings__DefaultConnection="Server=${DB_HOST};Port=${DB_PORT};Database=${DB_NAME};User=${DB_USER};Password=${DB_PASSWORD};TreatTinyAsBoolean=true;AllowUserVariables=True;Connection Timeout=30"
fi

export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://+:8080}"

exec dotnet ToDoApp.dll
