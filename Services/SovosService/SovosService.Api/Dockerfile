# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5010
# NOTE: USER $APP_UID intentionally NOT set here — it is set in the final stage
# AFTER /var/sovos-dp-keys is created and chowned, so the non-root user can write
# DataProtection keys to that volume mount.


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Services/SovosService/SovosService.Api/SovosService.Api.csproj", "Services/SovosService/SovosService.Api/"]
COPY ["Sovos/Sovos.InvoiceWorker.Core/Sovos.InvoiceWorker.Core.csproj", "Sovos/Sovos.InvoiceWorker.Core/"]

RUN dotnet restore "./Services/SovosService/SovosService.Api/SovosService.Api.csproj"
COPY . .
WORKDIR "/src/Services/SovosService/SovosService.Api"
RUN dotnet build "./SovosService.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SovosService.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# Create DataProtection keys folder and grant ownership to the non-root user
# BEFORE switching to USER $APP_UID. The folder is the target of a volume mount
# (shared with Sovos.InvoiceWorker) and must be writable by the runtime user.
RUN mkdir -p /var/sovos-dp-keys && chown -R $APP_UID:$APP_UID /var/sovos-dp-keys
USER $APP_UID
ENTRYPOINT ["dotnet", "SovosService.Api.dll"]
