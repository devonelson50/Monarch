#!/bin/bash

# Devon Nelson
# 
# Microsoft SQL Server container entrypoint script.
#
# This script will:
#       - retrieve any credentials passed via Docker Secrets.
#       - Call the sqlservr service in the background
#       - await the sqlservr service
#       - Connect to SQL server, and execute setup.sql
#       - wait the sqlservr service's PID to keep the container running

echo "Running initdb.sh."

SA_PATH="/run/secrets/monarch_sql_sa_password"
MONARCH_PATH="/run/secrets/monarch_sql_monarch_password"
MONAPI_PATH="/run/secrets/monarch_sql_monapi_password"
KEYCLOAK_PATH="/run/secrets/monarch_sql_keycloak_password"
if [ -f "$SA_PATH" ]; then
    SA_PASSWORD=$(cat $SA_PATH)
else
    echo "SA secret file not found. Exiting."
    exit 2
fi

if [ -f "$MONARCH_PATH" ]; then
    MONARCH_PASSWORD=$(cat $MONARCH_PATH)
else
    echo "Monarch secret file not found. Exiting."
    exit 3
fi

if [ -f "$MONAPI_PATH" ]; then
    MONAPI_PASSWORD=$(cat $MONAPI_PATH)
else
    echo "Monapi file not found. Exiting."
    exit 4
fi

if [ -f "$KEYCLOAK_PATH" ]; then
    KEYCLOAK_PASSWORD=$(cat $KEYCLOAK_PATH)
else
    echo "Keycloak file not found. Exiting."
    exit 5
fi

#export ODBC_TLS_VER=TLSv1.2
/opt/mssql/bin/sqlservr &
SQL_PID=$!
echo "SQL Server is starting."

# Retry basic query until a connection succeeds
# Robust retry logic provided as example in mssql-docker repo:
# https://github.com/microsoft/mssql-docker/blob/master/linux/preview/examples/mssql-customize/configure-db.sh

DBSTATUS=1
i=0

while [[ $DBSTATUS -ne 0 ]] && [[ $i -lt 60 ]]; do
	i=$i+1
	DBSTATUS=$(/opt/mssql-tools18/bin/sqlcmd -N -C -h -1 -t 1 -U SA -P "$SA_PASSWORD" -Q "SET NOCOUNT ON; Select SUM(state) from sys.databases")
	sleep 1
    echo "SQL Server is starting..."
done

if [[ $DBSTATUS -ne 0 ]]; then
	echo "SQL Server took more than 60 seconds to start up or one or more databases are not in an ONLINE state."
    echo $DBSTATUS 
	exit 1
fi

echo "SQL Server is started. Running setup.sql."
/opt/mssql-tools18/bin/sqlcmd -N -C \
    -S 127.0.0.1,1433 \
    -U SA \
    -P "$SA_PASSWORD" \
    -d master \
    -i /usr/config/setup.sql \
    -v MONARCH_PASSWORD="$MONARCH_PASSWORD" \
    -v MONAPI_PASSWORD="$MONAPI_PASSWORD" \
    -v KEYCLOAK_PASSWORD="$KEYCLOAK_PASSWORD"

echo "Initialization complete. Exiting script."
wait $SQL_PID