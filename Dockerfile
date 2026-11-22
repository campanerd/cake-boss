# Imagem de produção do app (frontend + backend num só processo).
# Construir a partir da raiz do repositório:  docker build -t cakeboss .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Primeiro só os .csproj, para o restore ficar em cache quando só o código mudar.
COPY backend/CakeBoss.Backend/CakeBoss.Backend.csproj backend/CakeBoss.Backend/
COPY frontend/CakeBoss.Frontend/CakeBoss.Frontend.csproj frontend/CakeBoss.Frontend/
RUN dotnet restore frontend/CakeBoss.Frontend/CakeBoss.Frontend.csproj

COPY backend/CakeBoss.Backend backend/CakeBoss.Backend
COPY frontend/CakeBoss.Frontend frontend/CakeBoss.Frontend
RUN dotnet publish frontend/CakeBoss.Frontend/CakeBoss.Frontend.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Roda sem root. A connection string vem da variável de ambiente ConnectionStrings__Default.
USER $APP_UID

# O Render informa a porta pela variável PORT; sem ela, usa 8080.
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT:-8080} exec dotnet CakeBoss.Frontend.dll"]
