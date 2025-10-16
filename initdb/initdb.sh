#!/bin/bash
echo "Running initdb.sh."
#export ODBC_TLS_VER=TLSv1.2
/opt/mssql/bin/sqlservr &
SQL_PID=$!
echo "SQL Server is starting."
sleep 10
echo "Connecting to service with setup.sql."
/opt/mssql-tools18/bin/sqlcmd -N -C \
    -S 127.0.0.1,1433 \
    -U SA \
    -P "$SA_PASSWORD" \
    -d master \
    -i /usr/config/setup.sql

echo "Initialization complete. Exiting script."

wait $SQL_PID