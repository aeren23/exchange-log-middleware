# ============================================================
# Stage 1: Build — SDK image ile derleme yapılır
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore için önce .csproj dosyaları kopyalanır (layer cache optimizasyonu)
COPY src/ExchangeLogMiddleware.Shared/ExchangeLogMiddleware.Shared.csproj \
     src/ExchangeLogMiddleware.Shared/
COPY src/ExchangeLogMiddleware.Producer/ExchangeLogMiddleware.Producer.csproj \
     src/ExchangeLogMiddleware.Producer/

# NuGet paketleri geri yüklenir
RUN dotnet restore src/ExchangeLogMiddleware.Producer/ExchangeLogMiddleware.Producer.csproj

# Kaynak kod kopyalanır
COPY src/ExchangeLogMiddleware.Shared/ src/ExchangeLogMiddleware.Shared/
COPY src/ExchangeLogMiddleware.Producer/ src/ExchangeLogMiddleware.Producer/

# Release modunda yayınlanır
WORKDIR /src/src/ExchangeLogMiddleware.Producer
RUN dotnet publish -c Release -o /app/publish --no-restore

# ============================================================
# Stage 2: Runtime — Minimal runtime image
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Output dizini oluşturulur (shared volume mount noktası)
RUN mkdir -p /app/output

# Non-root kullanıcı oluşturulur (güvenlik best practice)
RUN adduser --disabled-password --gecos "" appuser && \
    chown -R appuser:appuser /app
USER appuser

# Yayımlanan dosyalar kopyalanır
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ExchangeLogMiddleware.Producer.dll"]
