# Этап 1: сборка проекта
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY *.csproj ./
RUN dotnet restore PhotoStudioCRM.Backend.csproj

COPY . ./
RUN dotnet publish PhotoStudioCRM.Backend.csproj -c Release -o /app/publish

# Этап 2: финальный образ для запуска
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=build /app/publish ./

RUN mkdir -p /app/wwwroot/uploads/photos && \
    mkdir -p /app/wwwroot/uploads/receipts && \
    mkdir -p /app/wwwroot/uploads/reports

EXPOSE 7042
ENV ASPNETCORE_URLS=http://+:7042

ENTRYPOINT ["dotnet", "PhotoStudioCRM.Backend.dll"]
