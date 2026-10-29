FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "tools/KnowVault.DemoSeed/KnowVault.DemoSeed.csproj"
RUN dotnet publish "tools/KnowVault.DemoSeed/KnowVault.DemoSeed.csproj" \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build --chown=$APP_UID /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "KnowVault.DemoSeed.dll"]
