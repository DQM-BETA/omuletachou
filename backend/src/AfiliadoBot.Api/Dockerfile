FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/AfiliadoBot.Api/AfiliadoBot.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
# ISSUE-15 / Sub-A: curl necessario para o healthcheck do docker-compose.yml
# ("curl -f http://localhost:8080/health"). A imagem base nao garante curl instalado em
# todas as tags/atualizacoes da Microsoft — instalado explicitamente para nao depender disso.
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "AfiliadoBot.Api.dll"]
