#!/bin/bash

#Brady Brown
#This is a simulator that will send generated data to New Relic
#This is only needed for testing purposes, should be deleted when used in production

#!/bin/bash

#Finds secret files
ACCOUNT_FILE="/run/secrets/monarch_account_number"
LICENSE_FILE="/run/secrets/monarch_simulator_api_key"

#Reads secret files
ACCOUNT_ID=$(cat "$ACCOUNT_FILE" | tr -d '[:space:]')
LICENSE_KEY=$(cat "$LICENSE_FILE" | tr -d '[:space:]')

#Dummy app names
APPS=("EC2-WEB-01" "EC2-WEB-02" "LOAD-BAL" "EC2-CDN-01" "ZTA-APP" "SQL-PROD-01" "SQL-PROD-02"
      "SQL-TEST-01" "SQL-TEST-02" "API-CONT" "DOCK-RUN-01" "DOCK-RUN-02" "POS-01" "POS-02" 
      "POS-TEST" "PSA-01" "IS-DC1" "IS-DC2" "IS-DC3" "WAN-UP-01" "WAN-UP-02" "WAN-UP-03"
      "NFS-01" "iSCSI-01" "DNS-01" "DNS-02" "MONARCH" "HV1" "HV2" "HV3" "HV4" "HV-LAB"
      "CRM-01" "CRM-02" "ATERA-01" "VPN-01" "VPN-02" "WSUS-01" "WSUS-02" "WINS-01" "WINS-02"
      "EDR-01" "EDR-02" "EDR-TEST" "RMM-01" "RMM-02" "MDM-01" "MDM-02" "RSNAP-01" "RSNAP-02")

while true; do #Send data as long as container is running
  NOW=$(date +%s)
  ISO_TIME=$(date -u +"%Y-%m-%dT%H:%M:%SZ")

  for i in "${!APPS[@]}"; do
    APPNAME=${APPS[$i]}
    HOST_ID=$(( 1000 + i ))
    IP_ADDR="10.0.1.$(( 10 + i ))"

    #Sim Logic
    #Base CPU Usage (0-100)
    CPU=$(( RANDOM % 100 ))
    
    #Latency increases as CPU increases
    BASE_LATENCY=$(( ( RANDOM % 100 ) + 20 ))
    LATENCY=$(( BASE_LATENCY + (CPU * 2) )) 
    
    #Determine State (0=OK, 1=Warning, 2=Critical)
    if [ $CPU -gt 90 ]; then
        STATE=2; STATUS_MSG="CRITICAL: CPU Load $CPU%"
    elif [ $CPU -gt 75 ] || [ $LATENCY -gt 250 ]; then
        STATE=1; STATUS_MSG="WARNING: High Latency ${LATENCY}ms"
    else
        STATE=0; STATUS_MSG="OK: System healthy"
    fi

    #Throughput & Error Rate
    THROUGHPUT=$(( RANDOM % 1000 ))
    ERROR_RATE=$(( RANDOM % 3 ))

    #Json Data Sent
    JSON_PAYLOAD=$(cat <<EOF
    [{
      "common": {
        "attributes": {
          "hostObjectId": $HOST_ID,
          "hostName": "$APPNAME",
          "ipAddress": "$IP_ADDR"
        }
      },
      "metrics": [
        {
          "name": "newRelic.host.health",
          "type": "gauge",
          "value": $STATE,
          "timestamp": $NOW,
          "attributes": {
            "currentState": $STATE,
            "output": "$STATUS_MSG",
            "latency": "${LATENCY}",
            "cpuUsage": $CPU,
            "throughput": $THROUGHPUT,
            "errorRate": $ERROR_RATE,
            "statusUpdateTime": "$ISO_TIME",
            "lastCheck": "$ISO_TIME"
          }
        }
      ]
    }]
EOF
)

    #Send to New Relic
    curl -s -X POST "https://metric-api.newrelic.com/metric/v1" \
      -H "Api-Key: $LICENSE_KEY" \
      -H "Content-Type: application/json" \
      -d "$JSON_PAYLOAD" > /dev/null
    
    sleep 0.1 #Throttle to prevent network congestion
  done

  echo "Sent real-time metrics for all 50 apps"
  sleep 1m #Wait 1 minute to send more data
done