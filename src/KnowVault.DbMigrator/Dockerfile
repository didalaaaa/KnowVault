FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore "src/KnowVault.DbMigrator/KnowVault.DbMigrator.csproj"
RUN dotnet publish "src/KnowVault.DbMigrator/KnowVault.DbMigrator.csproj" \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build --chown=$APP_UID /app/publish .
RUN mkdir -p /app/Logs && chown -R $APP_UID /app/Logs

USER $APP_UID
ENTRYPOINT ["dotnet", "KnowVault.DbMigrator.dll"]
