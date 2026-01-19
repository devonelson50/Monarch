FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

USER app
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG configuration=Release
WORKDIR /src
COPY ["app/src/Monarch/Monarch.csproj", "app/src/Monarch/"]
RUN dotnet restore "app/src/Monarch/Monarch.csproj"
COPY . .
WORKDIR "/src/app/src/Monarch"
RUN dotnet build "Monarch.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "Monarch.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .



# --- FOR SIMULATOR ---
#Can be deleted in production

#Copy the scripts from your local project root to the container
COPY app/src/Monarch/start.sh .
COPY app/src/Monarch/simulator.sh .

#Switch to root briefly to ensure the scripts are executable, fix line endings, and install curl
USER root

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

RUN sed -i 's/\r$//' start.sh simulator.sh && \
    chmod +x start.sh simulator.sh

#Switch back to the app user
USER app

#hange the ENTRYPOINT to use the manager script
ENTRYPOINT ["./start.sh"]
