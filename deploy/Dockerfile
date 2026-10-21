# Сборка из корня репозитория: docker build -f deploy/Dockerfile .

FROM node:24-alpine AS web
WORKDIR /src/web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
# Vite кладёт сборку в ../src/ShrekParty.Api/wwwroot.
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY src/ShrekParty.Api/ShrekParty.Api.csproj src/ShrekParty.Api/
RUN dotnet restore src/ShrekParty.Api/ShrekParty.Api.csproj
COPY src/ src/
COPY --from=web /src/src/ShrekParty.Api/wwwroot src/ShrekParty.Api/wwwroot
RUN dotnet publish src/ShrekParty.Api/ShrekParty.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=api /app .
ENV ASPNETCORE_HTTP_PORTS=8080 \
    Storage__DataDir=/data
VOLUME /data
EXPOSE 8080
ENTRYPOINT ["dotnet", "ShrekParty.Api.dll"]
