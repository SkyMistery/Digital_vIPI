# Build multi-stage del modulo ospitato dall'host di esempio Vipi.Host.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
# Restore del solo Host (+ i suoi ProjectReference): l'immagine ora leggerebbe anche Vipi.slnx, ma
# restorare la soluzione intera tirerebbe dentro i pacchetti dei progetti di test, inutili nell'immagine.
RUN dotnet restore src/Vipi.Host/Vipi.Host.csproj
RUN dotnet publish src/Vipi.Host/Vipi.Host.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
# Il DB SQLite di default è relativo: per la persistenza montare un volume su /app/data e impostare
# ConnectionStrings__Vipi="Data Source=/app/data/vipi.db". Segreti IVAO via env: Ivao__ClientId/Secret.
ENV ASPNETCORE_URLS=http://+:8080
# Niente FileSystemWatcher sulle config: su host con limite inotify basso (es. Render) i watcher
# di appsettings*.json esauriscono le istanze inotify e l'avvio crasha (IOException in CreateBuilder).
ENV DOTNET_hostBuilder__reloadConfigOnChange=false
EXPOSE 8080
ENTRYPOINT ["dotnet", "Vipi.Host.dll"]
