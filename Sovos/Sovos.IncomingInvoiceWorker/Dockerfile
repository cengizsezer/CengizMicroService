# See Sovos.InvoiceWorker/Dockerfile for the rationale (Playwright + .NET).
# Keep the playwright/dotnet image tag in lockstep with the Microsoft.Playwright
# NuGet version in Sovos.IncomingInvoiceWorker.csproj.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Sovos/Sovos.IncomingInvoiceWorker/Sovos.IncomingInvoiceWorker.csproj", "Sovos/Sovos.IncomingInvoiceWorker/"]
COPY ["Sovos/Sovos.InvoiceWorker.Core/Sovos.InvoiceWorker.Core.csproj", "Sovos/Sovos.InvoiceWorker.Core/"]
COPY ["Services/CatalogService/CatalogService.Api/CatalogService.Api.csproj", "Services/CatalogService/CatalogService.Api/"]

RUN dotnet restore "./Sovos/Sovos.IncomingInvoiceWorker/Sovos.IncomingInvoiceWorker.csproj"
COPY . .
WORKDIR "/src/Sovos/Sovos.IncomingInvoiceWorker"
RUN dotnet build "./Sovos.IncomingInvoiceWorker.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Sovos.IncomingInvoiceWorker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/playwright/dotnet:v1.47.0-jammy AS final
WORKDIR /app
EXPOSE 5021
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Sovos.IncomingInvoiceWorker.dll"]
