# 1. Aşama: Blazor WebAssembly'yi publish et (.NET SDK kullanarak)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app
COPY . ./
RUN dotnet publish "src/Client/BlazorWebApp/WebApp/WebApp.csproj" -c Release -o /app/build

# 2. Aşama: Yayınlanan wwwroot klasörünü Nginx ile servis et
FROM nginx:alpine
WORKDIR /var/www/web

# Blazor wwwroot dosyalarını kopyala
COPY --from=build-env /app/build/wwwroot .

# Varsayılan Nginx ayarını kaldır, kendi ayarımızı kopyala
RUN rm /etc/nginx/conf.d/default.conf
COPY src/Client/BlazorWebApp/WebApp/default.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]