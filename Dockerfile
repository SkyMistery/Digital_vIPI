# Build multi-stage del modulo ospitato dall'host di esempio Vipi.Host.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
# Restore del solo Host (+ i suoi ProjectReference): l'immagine ora leggerebbe anche Vipi.slnx, ma
# restorare la soluzione intera tirerebbe dentro i pacchetti dei progetti di test, inutili nell'immagine.
RUN dotnet restore src/Vipi.Host/Vipi.Host.csproj
RUN dotnet publish src/Vipi.Host/Vipi.Host.csproj -c Release -o /app --no-restore

# aspnet:8.0 e non 10.0: Vipi.Host è passato a net8 col provider Pomelo (ADR-0007 §D4-ter), e un'immagine
# col solo runtime 10 fa morire il container all'avvio con «Microsoft.NETCore.App version 8.0.0 not found»
# — build e publish riescono lo stesso, quindi il guasto si vede solo eseguendolo. Lo stage di build resta
# su sdk:10.0, che compila net8 senza problemi.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app .
# Il DB SQLite di default è relativo: per la persistenza montare un volume su /app/data e impostare
# ConnectionStrings__Vipi="Data Source=/app/data/vipi.db". Segreti IVAO via env: Ivao__ClientId/Secret.
ENV ASPNETCORE_URLS=http://+:8080
# Niente FileSystemWatcher sulle config: su host con limite inotify basso (es. Render) i watcher
# di appsettings*.json esauriscono le istanze inotify e l'avvio crasha (IOException in CreateBuilder).
ENV DOTNET_hostBuilder__reloadConfigOnChange=false
# 🔴 T-062 (revisione del 13 settembre 2026): il processo NON gira come root. L'immagine aspnet porta l'utente
# `app` ($APP_UID), ma senza questa riga lo si ignorava — mentre ci.yml diceva il contrario. /app resta di root
# e in sola lettura; /app/data è l'unica cartella dell'applicazione scrivibile, quella del volume SQLite. La
# diagnostica ripiega da sé sulla temporanea, e il key-ring su Render sta nel database.
RUN mkdir -p /app/data && chown "$APP_UID" /app/data
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Vipi.Host.dll"]
