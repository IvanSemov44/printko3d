# Build context is the repository root: docker build -f src/backend/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0.100-alpine3.22 AS build
WORKDIR /repo
COPY global.json Directory.Build.props ./
COPY src/backend/Storefront.Domain/*.csproj src/backend/Storefront.Domain/
COPY src/backend/Storefront.Application/*.csproj src/backend/Storefront.Application/
COPY src/backend/Storefront.Infrastructure/*.csproj src/backend/Storefront.Infrastructure/
COPY src/backend/Storefront.Api/*.csproj src/backend/Storefront.Api/
RUN dotnet restore src/backend/Storefront.Api/Storefront.Api.csproj
COPY src/backend src/backend
RUN dotnet publish src/backend/Storefront.Api/Storefront.Api.csproj \
    -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0.3-alpine3.22 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
HEALTHCHECK --interval=15s --timeout=5s --start-period=10s --retries=5 \
    CMD wget -q --spider http://127.0.0.1:8080/health || exit 1
ENTRYPOINT ["dotnet", "Storefront.Api.dll"]
