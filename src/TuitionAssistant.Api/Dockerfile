# Build context: repository root
#   docker build -f src/TuitionAssistant.Api/Dockerfile -t tuition-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/TuitionAssistant.Api/TuitionAssistant.Api.csproj src/TuitionAssistant.Api/
RUN dotnet restore src/TuitionAssistant.Api/TuitionAssistant.Api.csproj
COPY src/TuitionAssistant.Api/ src/TuitionAssistant.Api/
RUN dotnet publish src/TuitionAssistant.Api/TuitionAssistant.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
# Built-in non-root user of the .NET images
USER $APP_UID
HEALTHCHECK --interval=15s --timeout=3s --start-period=10s --retries=3 CMD curl -fsS http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "TuitionAssistant.Api.dll"]
