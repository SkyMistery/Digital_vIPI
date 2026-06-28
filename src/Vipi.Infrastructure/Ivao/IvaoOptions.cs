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

    /// <summary>Endpoint token OpenID (client_credentials). PIANO §7.3.</summary>
    public string TokenEndpoint { get; set; } = "https://api.ivao.aero/v2/oauth/token";

    /// <summary>Template path elenco membri divisione: <c>{0}</c> = codice divisione. Fase G; da confermare.</summary>
    public string DivisionMembersPathFormat { get; set; } = "/v2/divisions/{0}/members";

    /// <summary>Path anagrafica aeroporti IVAO (paginato). Richiede scope <c>configuration</c>.</summary>
    public string AirportsPath { get; set; } = "/v2/airports";

    /// <summary>Path anagrafica center/ACC IVAO (paginato). Richiede scope <c>configuration</c>.</summary>
    public string CentersPath { get; set; } = "/v2/centers";

    /// <summary>Template subcenter di un ACC: <c>{0}</c> = ICAO ACC. Es. <c>/v2/centers/LIBB/subcenters</c>.</summary>
    public string SubcentersPathFormat { get; set; } = "/v2/centers/{0}/subcenters";

    /// <summary>Template dettaglio subcenter: <c>{0}</c> = composePosition. Es. <c>/v2/subcenters/LIBB_ES_CTR</c>.</summary>
    public string SubcenterDetailPathFormat { get; set; } = "/v2/subcenters/{0}";

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

    /// <summary>Ogni quante ore ri-verificare il roster staffisti via API (disattiva chi non è più staff IT).</summary>
    public int StaffVerifyHours { get; set; } = 24;

    /// <summary>Ogni quante ore re-importare automaticamente ACC + settori ATC dalla sorgente (default giornaliero).</summary>
    public int AccImportHours { get; set; } = 24;
}
