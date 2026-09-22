FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:e1ffd2a92ae84c1291bc1b6887501f8af98e6331e7af6d4c8d37168c5e87a64c AS build
WORKDIR /source
COPY Directory.Build.props ./
COPY src/ src/
RUN dotnet publish src/SecureFlow.Api/SecureFlow.Api.csproj -c Release -o /publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:2d584d8147faddb0d678c5748d47953e5b8e18621ed4fb7049a91381d9d7746f AS runtime
WORKDIR /app
COPY --from=build /publish/ ./
RUN mkdir -p /var/lib/secureflow/keys && chown -R 1654:1654 /var/lib/secureflow
ENV ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_HTTP_PORTS=8080
USER 1654:1654
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=5 CMD ["dotnet", "SecureFlow.Api.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "SecureFlow.Api.dll"]
