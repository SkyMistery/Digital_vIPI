# Build multi-stage del modulo ospitato dall'host di esempio Vipi.Host.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore Vipi.slnx
RUN dotnet publish src/Vipi.Host/Vipi.Host.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app .
# Il DB SQLite di default è relativo: per la persistenza montare un volume su /app/data e impostare
# ConnectionStrings__Vipi="Data Source=/app/data/vipi.db". Segreti IVAO via env: Ivao__ClientId/Secret.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Vipi.Host.dll"]
