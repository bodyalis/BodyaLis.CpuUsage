FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app

USER root
RUN apt-get update && \
    apt-get upgrade -y && \
    apt-get install -y --no-install-recommends htop && \
    rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["CpuUsage.LoadSimulations/CpuUsage.LoadSimulations.csproj", "CpuUsage.LoadSimulations/"]
COPY ["CpuUsage/CpuUsage.csproj", "CpuUsage/"]
RUN dotnet restore "CpuUsage.LoadSimulations/CpuUsage.LoadSimulations.csproj"
COPY . .
WORKDIR "/src/CpuUsage.LoadSimulations"
RUN dotnet build "CpuUsage.LoadSimulations.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "CpuUsage.LoadSimulations.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CpuUsage.LoadSimulations.dll"]
