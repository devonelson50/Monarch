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
ENTRYPOINT ["dotnet", "Monapi.Worker.dll"]
