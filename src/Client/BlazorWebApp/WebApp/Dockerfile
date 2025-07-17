FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS build-env
WORKDIR /app
COPY . ./
RUN dotnet publish "src/Client/BlazorWebApp/WebApp/WebApp.csproj" -c Release -o /app/build

FROM nginx:alpine
WORKDIR /var/www/web
COPY --from=build-env /app/build/wwwroot .
RUN rm /etc/nginx/conf.d/*
COPY src/Clients/BlazorWebApp/WebApp/default.conf /etc/nginx/conf.d/default.conf
#EXPOSE 80