# syntax=docker/dockerfile:1
ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0

FROM ${SDK_IMAGE} AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/CoffeeShop.Api/CoffeeShop.Api.csproj src/CoffeeShop.Api/
COPY src/CoffeeShop.Application/CoffeeShop.Application.csproj src/CoffeeShop.Application/
COPY src/CoffeeShop.Contracts/CoffeeShop.Contracts.csproj src/CoffeeShop.Contracts/
COPY src/CoffeeShop.Domain/CoffeeShop.Domain.csproj src/CoffeeShop.Domain/
COPY src/CoffeeShop.Infrastructure.Postgres/CoffeeShop.Infrastructure.Postgres.csproj src/CoffeeShop.Infrastructure.Postgres/
RUN --mount=type=secret,id=proxy_ca,required=false \
    if [ -f /run/secrets/proxy_ca ]; then export SSL_CERT_FILE=/run/secrets/proxy_ca; fi; \
    dotnet restore src/CoffeeShop.Api/CoffeeShop.Api.csproj

COPY src/CoffeeShop.Api/ src/CoffeeShop.Api/
COPY src/CoffeeShop.Application/ src/CoffeeShop.Application/
COPY src/CoffeeShop.Contracts/ src/CoffeeShop.Contracts/
COPY src/CoffeeShop.Domain/ src/CoffeeShop.Domain/
COPY src/CoffeeShop.Infrastructure.Postgres/ src/CoffeeShop.Infrastructure.Postgres/
RUN dotnet publish src/CoffeeShop.Api/CoffeeShop.Api.csproj --configuration Release --no-restore \
    --output /app/publish -p:UseAppHost=false

FROM ${RUNTIME_IMAGE} AS final
WORKDIR /app
COPY --from=build /app/publish ./
COPY docker/healthcheck.sh ./healthcheck.sh
COPY docker/entrypoint.sh ./entrypoint.sh
RUN chmod -R a+rX /app
ENV ASPNETCORE_HTTP_PORTS=8080
USER 1654
ENTRYPOINT ["bash", "/app/entrypoint.sh"]
