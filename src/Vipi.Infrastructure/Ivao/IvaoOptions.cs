namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Config del polling IVAO (sezione "Ivao" di appsettings + user-secrets). PIANO §7.
/// Segreti (ClientId/ClientSecret) MAI in appsettings versionato: user-secrets in dev, env var in prod.
/// </summary>
public sealed class IvaoOptions
{
    public const string SectionName = "Ivao";

    /// <summary>Base delle API IVAO v2.</summary>
    public string BaseUrl { get; set; } = "https://api.ivao.aero";

    /// <summary>Path del riepilogo ATC online (relativo a BaseUrl). PIANO §7.1.</summary>
    public string AtcSummaryPath { get; set; } = "/v2/tracker/now/atc/summary";

    /// <summary>
    /// Path della fotografia completa della rete (ATC + piloti), relativo a BaseUrl. <b>Endpoint pubblico</b>:
    /// non richiede token. Dal 24 agosto 2026 è questo che alimenta il poller — stessa cadenza e stesso numero
    /// di chiamate di <see cref="AtcSummaryPath"/>, ma porta anche i piloti, che servono ad attribuire il
    /// traffico gestito. Misurato: 119 KB sul filo con Brotli, 0,21 s.
    /// </summary>
    public string WhazzupPath { get; set; } = "/v2/tracker/whazzup";

    /// <summary>Endpoint token OpenID (client_credentials). PIANO §7.3.</summary>
    public string TokenEndpoint { get; set; } = "https://api.ivao.aero/v2/oauth/token";

    /// <summary>Template path elenco membri divisione: <c>{0}</c> = codice divisione.
    /// ⚠ NON accessibile col token app (client_credentials): risponde 404/500. Il roster staffisti si costruisce dai
    /// LOGIN + verifica per-VID (vedi StaffRoster), non da qui. Questo endpoint richiede credenziali utente con lo
    /// scope membri: usarlo col token app è un footgun (fallisce a runtime con errore chiaro in IvaoDivisionClient).</summary>
    public string DivisionMembersPathFormat { get; set; } = "/v2/divisions/{0}/members";

    /// <summary>Path anagrafica aeroporti IVAO (paginato). Richiede scope <c>configuration</c>.</summary>
    public string AirportsPath { get; set; } = "/v2/airports";

    /// <summary>Path anagrafica center/ACC IVAO (paginato). Richiede scope <c>configuration</c>.</summary>
    public string CentersPath { get; set; } = "/v2/centers";

    /// <summary>Template subcenter di un ACC: <c>{0}</c> = ICAO ACC. Es. <c>/v2/centers/LIBB/subcenters</c>.</summary>
    public string SubcentersPathFormat { get; set; } = "/v2/centers/{0}/subcenters";

    /// <summary>Template dettaglio subcenter: <c>{0}</c> = composePosition. Es. <c>/v2/subcenters/LIBB_ES_CTR</c>.</summary>
    public string SubcenterDetailPathFormat { get; set; } = "/v2/subcenters/{0}";

    /// <summary>Template dettaglio postazione ATC d'aeroporto: <c>{0}</c> = composePosition. Es. <c>/v2/ATCPositions/LIRN_TWR</c>.</summary>
    public string AtcPositionDetailPathFormat { get; set; } = "/v2/ATCPositions/{0}";

    /// <summary>Template elenco aree speciali di un ACC (paginato): <c>{0}</c> = ICAO ACC. Es. <c>/v2/centers/LIRR/specialAreas</c>.</summary>
    public string SpecialAreasPathFormat { get; set; } = "/v2/centers/{0}/specialAreas";

    /// <summary>Template dettaglio area speciale (con shape): <c>{0}</c> = id. Es. <c>/v2/specialAreas/8963</c>.</summary>
    public string SpecialAreaDetailPathFormat { get; set; } = "/v2/specialAreas/{0}";

    /// <summary>Paese (countryId IVAO, es. "IT" per l'Italia) per cui scaricare gli aeroporti.</summary>
    public string AirportsCountryId { get; set; } = "IT";

    /// <summary>Ogni quante ore rinfrescare la cache di processo dell'anagrafica aeroporti.</summary>
    public int AirportsCacheHours { get; set; } = 12;

    /// <summary>Credenziali app-to-app. Vuote in dev => nessun Bearer (endpoint tracker pubblico).</summary>
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";

    /// <summary>Scope richiesti per il token client_credentials. <c>configuration</c> serve per /v2/airports.</summary>
    public string Scopes { get; set; } = "tracker configuration";

    /// <summary>Intervallo di polling in secondi (una sola chiamata/minuto, RNF-1/RNF-4).</summary>
    public int PollSeconds { get; set; } = 60;

    /// <summary>
    /// <b>Strumento di verifica live, non prodotto.</b> Elenco di callsign separati da virgola pubblicati
    /// come «online» al posto della chiamata al tracker IVAO. Vuoto = polling reale.
    /// <para>⚠️ Onorato <b>solo in Development</b>: <c>AtcPollingHostedService</c> lo rifiuta altrove e logga
    /// un errore, perché una configurazione dimenticata in produzione mostrerebbe traffico inesistente.</para>
    /// <para>Serve perché senza vicini online ogni punto di trasferimento risolve a UNICOM, che la vista live
    /// nasconde per default: la pagina si prova vuota. Carta:
    /// <c>docs/feature/2026-08-23-live-coordinamenti-a-colonne.md</c>.</para>
    /// </summary>
    public string FakeOnlineCallsigns { get; set; } = "";

    /// <summary>Path dello storico connessioni (relativo a BaseUrl). Richiede il token app, scope <c>tracker</c>.</summary>
    public string AtcSessionsPath { get; set; } = "/v2/tracker/sessions";

    /// <summary>Ogni quante ore ripassare lo storico delle connessioni ATC (statistiche).</summary>
    public int AtcHistoryImportHours { get; set; } = 24;

    /// <summary>
    /// Quanti giorni recupera il <b>primo</b> giro dello storico. 365 non è un numero tondo scelto a caso:
    /// la retention della sorgente è di ~366 giorni (misurato), quindi oltre non esiste niente da prendere.
    /// </summary>
    public int AtcHistoryBackfillDays { get; set; } = 365;

    /// <summary>
    /// Quanti giorni ripassa ogni giro successivo. Due giorni coprono un'applicazione rimasta giù una notte
    /// e costano una manciata di chiamate.
    /// </summary>
    public int AtcHistoryRefreshDays { get; set; } = 2;

    /// <summary>Ogni quante ore ri-verificare il roster staffisti via API (disattiva chi non è più staff IT).</summary>
    public int StaffVerifyHours { get; set; } = 24;

    /// <summary>Ogni quante ore re-importare automaticamente ACC + settori ATC dalla sorgente (default giornaliero).</summary>
    public int AccImportHours { get; set; } = 24;

    /// <summary>Ogni quante ore re-importare automaticamente i settori ATC degli aeroporti dalla sorgente (default giornaliero).</summary>
    public int AirportSectorImportHours { get; set; } = 24;

    /// <summary>
    /// Ogni quante ore riassegnare alla loro ACC gli aeroporti nuovi dell'anagrafica (default giornaliero).
    /// ⚠️ È il solo giro che <b>crea</b> entità (aeroporto + catalogo settori); additivo, non rimuove nulla.
    /// </summary>
    public int AirportDirectoryImportHours { get; set; } = 24;

    /// <summary>
    /// Ogni quante ore rileggere <b>TA e piste</b> di tutti gli aeroporti dalla sorgente (default giornaliero).
    /// Costo di un giro misurato sui 92 aeroporti in archivio: <b>1</b> chiamata per la TA (anagrafica
    /// paginata, già in cache di processo per <see cref="AirportsCacheHours"/>) più <b>una per aeroporto</b>
    /// per le piste.
    /// </summary>
    public int AirportDataImportHours { get; set; } = 24;
}
