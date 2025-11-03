# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app


# -------- build/publish ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# 1) Sadece gerekli .csproj'ları doğru path'lerden kopyala (repo yapına göre!)
COPY ["src/Event/EventBus.Base/EventBus.Base.csproj", "src/Event/EventBus.Base/"]
COPY ["src/Event/EventBus.Factory/EventBus.Factory.csproj", "src/Event/EventBus.Factory/"]
COPY ["src/Event/EventBus.AzureServiceBus/EventBus.AzureServiceBus.csproj", "src/Event/EventBus.AzureServiceBus/"]
COPY ["src/Event/EventBus.RabbitMQ/EventBus.RabbitMQ.csproj", "src/Event/EventBus.RabbitMQ/"]
COPY ["Services/NotificationService/NotificationService.Console/NotificationService.Console.csproj", "Services/NotificationService/NotificationService.Console/"]

# 2) Proje bazlı restore (solution adıyla uğraşma)
RUN dotnet restore "Services/NotificationService/NotificationService.Console/NotificationService.Console.csproj"

# 3) Tüm kaynakları al
COPY . .

# 4) Build & publish
WORKDIR "/src/Services/NotificationService/NotificationService.Console"
RUN dotnet publish "NotificationService.Console.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# -------- final ----------
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet","NotificationService.Console.dll"]
