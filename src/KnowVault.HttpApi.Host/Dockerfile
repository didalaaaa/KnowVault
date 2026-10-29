FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG AUTH_CERT_PASSPHRASE
WORKDIR /src

COPY . .
RUN dotnet restore "src/KnowVault.HttpApi.Host/KnowVault.HttpApi.Host.csproj"
RUN dotnet publish "src/KnowVault.HttpApi.Host/KnowVault.HttpApi.Host.csproj" \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# 非 Development 环境由该 pfx 提供 OpenIddict 签名/加密证书；口令必须与运行时
# AuthServer__CertificatePassPhrase 一致，改口令需要重新构建镜像
RUN test -n "$AUTH_CERT_PASSPHRASE" || (echo "必须通过 --build-arg AUTH_CERT_PASSPHRASE=<证书口令> 提供口令" >&2; exit 1)
RUN dotnet dev-certs https -v -ep /app/publish/openiddict.pfx -p "$AUTH_CERT_PASSPHRASE"
RUN chmod 644 /app/publish/openiddict.pfx

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# 健康检查需要 curl；aspnet 基础镜像默认不带
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build --chown=$APP_UID /app/publish .
RUN mkdir -p /app/Logs && chown -R $APP_UID /app/Logs

USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "KnowVault.HttpApi.Host.dll"]
