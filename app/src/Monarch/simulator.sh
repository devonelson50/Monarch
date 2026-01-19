#!/bin/bash

#Brady Brown
#This is a simulator that will send generated data to New Relic
#This is only needed for testing purposes, should be deleted when used in production

while true; do

  LATENCY=$(( ( RANDOM % 500 )  + 50 ))
  
  STATUSES=("Healthy" "Healthy" "Healthy" "Warning" "Critical")
  STATUS=${STATUSES[$(( RANDOM % 5 ))]}

  curl -s -v -X POST "https://insights-collector.newrelic.com/v1/accounts/____/events" \
    -H "Api-Key: ____" \
    -H "Content-Type: application/json" \
    -d "[{\"eventType\":\"AppHealth\",\"appName\":\"Docker-Sim-App\",\"status\":\"$STATUS\",\"latency\":$LATENCY}]"
  
  echo ">>> Simulator sent health check for Docker-Sim-App"
  sleep 60
done