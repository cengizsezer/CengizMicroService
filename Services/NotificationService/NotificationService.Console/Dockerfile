# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app


# ---- BUILD ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Sadece kendi projesini kopyala
COPY ["Services/NotificationService/NotificationService.Console/NotificationService.Console.csproj", "Services/NotificationService/NotificationService.Console/"]

# Restore
RUN dotnet restore "Services/NotificationService/NotificationService.Console/NotificationService.Console.csproj"

# Tüm kaynakları kopyala
COPY . .

# Publish et
WORKDIR /src/Services/NotificationService/NotificationService.Console
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# ---- FINAL ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Yayınlanan dosyaları kopyala
COPY --from=build /app/publish .

# Config dosyalarını dahil et (appsettings.json, serilog.json vs)
COPY Services/NotificationService/NotificationService.Console/Configurations /app/Configurations

ENTRYPOINT ["dotnet", "NotificationService.Console.dll"]
