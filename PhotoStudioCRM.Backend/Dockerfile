# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY *.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Создаём папки для загружаемых файлов (фото, чеки, отчёты)
# Для SQL Server папка /app/data НЕ НУЖНА!
RUN mkdir -p /app/wwwroot/uploads/photos && \
    mkdir -p /app/wwwroot/uploads/receipts && \
    mkdir -p /app/wwwroot/uploads/reports

EXPOSE 7042

ENTRYPOINT ["dotnet", "PhotoStudioCRM.Backend.dll"]