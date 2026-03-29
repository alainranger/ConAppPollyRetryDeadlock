#!/bin/bash
set -e

# Démarre SQL Server en arrière-plan
/opt/mssql/bin/sqlservr &
SQL_PID=$!

echo "Attente du démarrage de SQL Server..."
for i in $(seq 1 30); do
    /opt/mssql-tools18/bin/sqlcmd \
        -S localhost -U sa -P "$SA_PASSWORD" \
        -Q "SELECT 1" -No > /dev/null 2>&1 \
    && break
    echo "  Tentative $i/30..."
    sleep 2
done

echo "SQL Server prêt. Exécution du script d'init..."
/opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$SA_PASSWORD" \
    -i /var/opt/mssql/init.sql -No

echo "Init terminée."

# Garde le processus SQL Server au premier plan
wait $SQL_PID