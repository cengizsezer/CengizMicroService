# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Services/OCRService/OCRService.Api/OCRService.Api.csproj", "Services/OCRService/OCRService.Api/"]
RUN dotnet restore "./Services/OCRService/OCRService.Api/OCRService.Api.csproj"
COPY . .
WORKDIR "/src/Services/OCRService/OCRService.Api"
RUN dotnet build "./OCRService.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./OCRService.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# İzin ayarlamak için root'a geç
USER root

# Vision key'i kopyala
COPY Services/OCRService/OCRService.Api/Config/vision-sa.json \
     /app/Services/OCRService/OCRService.Api/Config/vision-sa.json

# Dizin traverse + dosya okuma izinleri
RUN chmod a+rx /app \
 && chmod a+rx /app/Services \
 && chmod a+rx "/app/Services/OCRService" \
 && chmod a+rx "/app/Services/OCRService/OCRService.Api" \
 && chmod a+rx "/app/Services/OCRService/OCRService.Api/Config" \
 && chmod a+r  "/app/Services/OCRService/OCRService.Api/Config/vision-sa.json"

# Vision env
ENV GOOGLE_APPLICATION_CREDENTIALS=/app/Services/OCRService/OCRService.Api/Config/vision-sa.json
USER $APP_UID
ENTRYPOINT ["dotnet", "OCRService.Api.dll"]