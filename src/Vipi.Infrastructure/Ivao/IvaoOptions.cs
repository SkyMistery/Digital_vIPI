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

    /// <summary>Credenziali app-to-app. Vuote in dev => nessun Bearer (endpoint tracker pubblico).</summary>
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";

    /// <summary>Scope richiesti per il token client_credentials.</summary>
    public string Scopes { get; set; } = "tracker";

    /// <summary>Intervallo di polling in secondi (una sola chiamata/minuto, RNF-1/RNF-4).</summary>
    public int PollSeconds { get; set; } = 60;
}
