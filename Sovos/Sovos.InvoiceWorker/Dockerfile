# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.
#
# Worker uses Microsoft.Playwright to drive a headless Chromium against the Sovos
# portal. The standard aspnet image lacks Chromium and its native dependencies, so
# the FINAL stage runs on the official Playwright .NET image (Ubuntu 22.04 jammy)
# which ships Chromium + Firefox + WebKit + .NET runtime + all native libs.
#
# IMPORTANT: keep the playwright image tag in lockstep with the Microsoft.Playwright
# NuGet version in Sovos.InvoiceWorker.csproj — version drift causes browser/CDP
# protocol mismatches at runtime.

# Build stage — standard .NET SDK (no browsers needed at build time)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Sovos/Sovos.InvoiceWorker/Sovos.InvoiceWorker.csproj", "Sovos/Sovos.InvoiceWorker/"]
COPY ["Sovos/Sovos.InvoiceWorker.Core/Sovos.InvoiceWorker.Core.csproj", "Sovos/Sovos.InvoiceWorker.Core/"]

RUN dotnet restore "./Sovos/Sovos.InvoiceWorker/Sovos.InvoiceWorker.csproj"
COPY . .
WORKDIR "/src/Sovos/Sovos.InvoiceWorker"
RUN dotnet build "./Sovos.InvoiceWorker.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Sovos.InvoiceWorker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage — Playwright .NET image (Ubuntu 22.04 jammy) with Chromium + .NET 8 runtime
FROM mcr.microsoft.com/playwright/dotnet:v1.47.0-jammy AS final
WORKDIR /app
EXPOSE 5011
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Sovos.InvoiceWorker.dll"]
