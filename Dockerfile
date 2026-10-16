# Build multi-stage del modulo ospitato dall'host di esempio Vipi.Host.
# 🔴 Versioni FISSE, le stesse di global.json (SDK 10.0.401 → runtime 10.0.12). Con `sdk:10.0` il 16 ottobre 2026
# l'immagine è passata da sola all'SDK 10.0.402 (runtime 10.0.13): il pacchetto implicito
# Microsoft.AspNetCore.App.Internal.Assets cambiava versione, i packages.lock.json non valevano più e il restore
# (allora senza --locked-mode) rifaceva tutto, prendendo EF 8.0.32 contro il SQLitePCLRaw fissato (NU1605).
# Quando si aggiorna l'SDK si cambiano INSIEME: global.json, queste due righe FROM e i lock (--force-evaluate).
FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /src
COPY . .
# Restore del solo Host (+ i suoi ProjectReference): l'immagine ora leggerebbe anche Vipi.slnx, ma
# restorare la soluzione intera tirerebbe dentro i pacchetti dei progetti di test, inutili nell'immagine.
# --locked-mode come nei job dei test: l'immagine usa le versioni dei packages.lock.json committati. Senza, il 16
# ottobre 2026 il restore ha preso Microsoft.EntityFrameworkCore.Sqlite 8.0.32 (uscita dopo il lock a 8.0.31), che
# vuole SQLitePCLRaw 2.1.13 contro il 2.1.12 fissato in Vipi.Infrastructure: NU1605, e il job docker-image rosso
# senza che il codice fosse cambiato. Se si ferma qui, il lock va rifatto (`dotnet restore --force-evaluate`).
RUN dotnet restore src/Vipi.Host/Vipi.Host.csproj --locked-mode
# Il publish lancia tools/Vipi.Assets (net8) per minificare gli asset, e sdk:10.0 porta il SOLO runtime 10:
# senza questa riga l'attrezzo muore con «Framework 'Microsoft.NETCore.App', version '8.0.0' not found»
# (codice 150) e il publish con lui. Vale solo in questo stadio.
ENV DOTNET_ROLL_FORWARD=Major
RUN dotnet publish src/Vipi.Host/Vipi.Host.csproj -c Release -o /app --no-restore

# aspnet:10.0, come il TFM di Vipi.Host (net10 dal 13 settembre 2026, L13). ⚠️ I due vanno cambiati INSIEME:
# ad agosto, con l'host net8 e l'immagine sul solo runtime 10, il container moriva all'avvio con
# «Microsoft.NETCore.App version 8.0.0 not found» — build e publish riuscivano lo stesso, quindi il guasto si
# vedeva solo eseguendolo. E fissata alla patch dell'SDK qui sopra (10.0.401 → 10.0.12): vedi la nota in cima.
FROM mcr.microsoft.com/dotnet/aspnet:10.0.12 AS final
WORKDIR /app
COPY --from=build /app .
# Il DB SQLite sta in /app/data/vipi.db (ENV qui sotto): per la persistenza montare un volume su /app/data.
# Segreti IVAO via env: Ivao__ClientId/Secret.
ENV ASPNETCORE_URLS=http://+:8080
# Il default relativo («vipi.db») cadrebbe in /app, che col processo non-root (T-062, più sotto) NON è
# scrivibile: il container moriva alla migrazione con «SQLite Error 14: unable to open database file». Il default
# dell'immagine sta quindi nell'unica cartella scrivibile; chi passa la sua stringa (Render, la CI) lo sovrascrive.
ENV ConnectionStrings__Vipi="Data Source=/app/data/vipi.db"
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
