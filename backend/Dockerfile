FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY Directory.Build.props PocketLense.slnx ./
COPY backend/ backend/
RUN dotnet restore backend/PocketLense.Api/PocketLense.Api.csproj --locked-mode
RUN dotnet publish backend/PocketLense.Api/PocketLense.Api.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "PocketLense.Api.dll"]
