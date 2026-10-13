FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props NuGet.Config ./
COPY Docnaut.FranceConnector.Sdk/Docnaut.FranceConnector.Sdk.csproj Docnaut.FranceConnector.Sdk/
COPY Docnaut.FranceConnector.TestTool/Docnaut.FranceConnector.TestTool.csproj Docnaut.FranceConnector.TestTool/
RUN dotnet restore Docnaut.FranceConnector.TestTool/Docnaut.FranceConnector.TestTool.csproj
COPY Docnaut.FranceConnector.Sdk/ Docnaut.FranceConnector.Sdk/
COPY Docnaut.FranceConnector.TestTool/ Docnaut.FranceConnector.TestTool/
RUN dotnet publish Docnaut.FranceConnector.TestTool/Docnaut.FranceConnector.TestTool.csproj \
    --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
LABEL org.opencontainers.image.title="Docnaut France Integration Tool C#" \
      org.opencontainers.image.description="ASP.NET Core Client API integration console" \
      org.opencontainers.image.source="https://github.com/Eaglessoft/docnaut-france-integration-tool-csharp" \
      org.opencontainers.image.licenses="MIT"
COPY --from=build /app/publish .
COPY LICENSE THIRD-PARTY-NOTICES.md ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Docnaut.FranceConnector.TestTool.dll", "--urls", "http://0.0.0.0:8080"]
