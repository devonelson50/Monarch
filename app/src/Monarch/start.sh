#!/bin/bash
# 1. Start the simulator in the background
./simulator.sh &

# 2. Start the actual Blazor app (replaces the old ENTRYPOINT)
dotnet Monarch.dll