FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore Metin2.sln \
    && dotnet publish src/Metin2.Server/Metin2.Server.csproj \
      --configuration Release \
      --no-restore \
      --output /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install --yes --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

USER $APP_UID
ENTRYPOINT ["dotnet", "Metin2.Server.dll"]
