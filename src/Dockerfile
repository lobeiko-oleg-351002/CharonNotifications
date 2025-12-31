FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy NuGet config and restore packages
COPY ["nuget-packages/", "/nuget-packages/"]
RUN if [ -d "/nuget-packages" ] && [ "$(ls -A /nuget-packages/*.nupkg 2>/dev/null)" ]; then \
        dotnet nuget add source /nuget-packages --name local-packages; \
    fi

# Copy project files
COPY ["CharonNotifications/src/CharonNotifications/CharonNotifications.csproj", "CharonNotifications/src/CharonNotifications/"]
COPY ["CharonDbContext/src/CharonDbContext/CharonDbContext.csproj", "CharonDbContext/src/CharonDbContext/"]

# Restore dependencies
WORKDIR "/src/CharonNotifications/src/CharonNotifications"
RUN dotnet restore "CharonNotifications.csproj"

# Copy all source files
WORKDIR /src
COPY ["CharonNotifications/src/CharonNotifications/", "CharonNotifications/src/CharonNotifications/"]
COPY ["CharonDbContext/src/CharonDbContext/", "CharonDbContext/src/CharonDbContext/"]

# Build
WORKDIR "/src/CharonNotifications/src/CharonNotifications"
RUN dotnet build "CharonNotifications.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CharonNotifications.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create logs directory
RUN mkdir -p /app/logs

ENTRYPOINT ["dotnet", "CharonNotifications.dll"]

