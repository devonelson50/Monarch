FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app

USER app
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG configuration=Release
WORKDIR /src
COPY ["app/src/Monapi.Worker/Monapi.Worker.csproj", "app/src/Monapi.Worker/"]
RUN dotnet restore "app/src/Monapi.Worker/Monapi.Worker.csproj"
COPY . .
WORKDIR "/src/app/src/Monapi.Worker"
RUN dotnet build "Monapi.Worker.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "Monapi.Worker.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .


# --- FOR SIMULATOR ---
#Can be deleted in production

#Copy the scripts from your local project root to the container
COPY app/src/Monapi.Worker/start.sh .
COPY app/src/Monapi.Worker/NewRelic/simulator.sh .

#Switch to root briefly to ensure the scripts are executable, fix line endings, and install curl
USER root

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

RUN sed -i 's/\r$//' start.sh simulator.sh && \
    chmod +x start.sh simulator.sh

#Switch back to the app user
USER app

#Changed the ENTRYPOINT to use the manager script, change back to Monapi for production
ENTRYPOINT ["./start.sh"]
