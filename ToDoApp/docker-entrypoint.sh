#!/bin/bash
set -euo pipefail

DB_NAME="${DB_NAME:-ToDo}"
DB_USER="${DB_USER:-todoapp}"
DB_PASSWORD="${DB_PASSWORD:-local-db-password}"
INIT_MARKER="/var/lib/mysql/.todoapp-initialized"

clean_var() {
    local v="$1"
    v="${v#\'}"; v="${v%\'}"
    v="${v#\"}"; v="${v%\"}"
    printf '%s' "$v"
}

DB_NAME="$(clean_var "$DB_NAME")"
DB_USER="$(clean_var "$DB_USER")"
DB_PASSWORD="$(clean_var "$DB_PASSWORD")"
export ConnectionStrings__DefaultConnection="${ConnectionStrings__DefaultConnection:-Server=127.0.0.1;Port=3306;Database=${DB_NAME};User=${DB_USER};Password=${DB_PASSWORD};TreatTinyAsBoolean=true;AllowUserVariables=True;Connection Timeout=30}"

mkdir -p /run/mysqld
mkdir -p /var/log/mysql
chown -R mysql:mysql /run/mysqld /var/lib/mysql /var/log/mysql

if [ ! -d /var/lib/mysql/mysql ]; then
    mariadb-install-db --user=mysql --datadir=/var/lib/mysql --skip-test-db >/dev/null
fi

/usr/sbin/mariadbd \
    --user=mysql \
    --datadir=/var/lib/mysql \
    --bind-address=127.0.0.1 \
    --socket=/run/mysqld/mysqld.sock \
    --log-error=/var/log/mysql/error.log &
db_pid=$!

ready=0
for _ in $(seq 30); do
    if mariadb-admin --user=root --protocol=SOCKET --socket=/run/mysqld/mysqld.sock ping --silent; then
        ready=1
        break
    fi
    sleep 1
done

if [ $ready -ne 1 ]; then
    echo "MariaDB did not start in time" >&2
    kill "$db_pid"
    exit 1
fi

if [ ! -f "$INIT_MARKER" ]; then
    mariadb --protocol=SOCKET --socket=/run/mysqld/mysqld.sock -uroot <<SQL
CREATE DATABASE IF NOT EXISTS \`${DB_NAME}\` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS '${DB_USER}'@'127.0.0.1' IDENTIFIED BY '${DB_PASSWORD}';
GRANT ALL PRIVILEGES ON \`${DB_NAME}\`.* TO '${DB_USER}'@'127.0.0.1';
FLUSH PRIVILEGES;
SQL
    touch "$INIT_MARKER"
fi

dotnet ToDoApp.dll &
app_pid=$!

trap "kill -TERM $app_pid $db_pid 2>/dev/null || true" TERM INT

wait $app_pid
kill $db_pid
wait $db_pid
