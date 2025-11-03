# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app


# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# SADECE .csproj'ları kopyala (DOĞRU YOLLAR)
COPY ["EventBus.Base/EventBus.Base.csproj", "EventBus.Base/"]
COPY ["EventBus.Factory/EventBus.Factory.csproj", "EventBus.Factory/"]
COPY ["EventBus.AzureServiceBus/EventBus.AzureServiceBus.csproj", "EventBus.AzureServiceBus/"]
COPY ["EventBus.RabbitMQ/EventBus.RabbitMQ.csproj", "EventBus.RabbitMQ/"]
COPY ["Services/NotificationService/NotificationService.Console/NotificationService.Console.csproj", "Services/NotificationService/NotificationService.Console/"]

# Restore
RUN dotnet restore "Services/NotificationService/NotificationService.Console/NotificationService.Console.csproj"

# TÜM kaynakları kopyala
COPY . .

# Publish
WORKDIR /src/Services/NotificationService/NotificationService.Console
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# ---- final ----
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet","NotificationService.Console.dll"]
