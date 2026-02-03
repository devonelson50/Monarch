FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8443

ENV ASPNETCORE_URLS=https://+:8443

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
ENTRYPOINT ["dotnet", "Monarch.dll"]
