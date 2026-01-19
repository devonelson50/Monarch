#!/bin/bash

#Brady Brown
#This is a simulator that will send generated data to New Relic
#This is only needed for testing purposes, should be deleted when used in production

while true; do

  LATENCY=$(( ( RANDOM % 500 )  + 50 ))
  
  STATUSES=("Healthy" "Healthy" "Healthy" "Warning" "Critical")
  STATUS=${STATUSES[$(( RANDOM % 5 ))]}

  curl -s -v -X POST "https://insights-collector.newrelic.com/v1/accounts/7596990/events" \
    -H "Api-Key: 54a5fb14a53029f8ba9945387b115ea7FFFFNRAL" \
    -H "Content-Type: application/json" \
    -d "[{\"eventType\":\"AppHealth\",\"appName\":\"Docker-Sim-App\",\"status\":\"$STATUS\",\"latency\":$LATENCY}]"
  
  echo ">>> Simulator sent health check for Docker-Sim-App"
  sleep 60
done