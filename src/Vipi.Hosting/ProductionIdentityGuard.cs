namespace Vipi.Hosting;

/// <summary>
/// Guardia di sicurezza all'avvio (audit D1): l'identità di sviluppo (<see cref="DevCurrentUserProvider"/>)
/// impersona un utente admin onnipotente con fallback statico. In un ambiente NON di sviluppo questo sarebbe
/// un bypass totale dell'autorizzazione. La guardia rileva la combinazione pericolosa e la fa fallire all'avvio,
/// prima che l'app serva richieste. Logica pura ⇒ testabile senza host.
///
/// <para>🔴 U-112 (revisione totale 3, scelta del committente del 28 settembre 2026): nel Vipi.Host l'identità di
/// sviluppo nasce <b>solo</b> in Development, quindi «dev identity fuori da Development» non poteva mai scattare —
/// la guardia era tautologica, e l'unica barriera vera era <c>ASPNETCORE_ENVIRONMENT</c>. Ora guarda anche
/// <b>dove</b> l'app è esposta (un indirizzo che non sia loopback) e <b>su quale database</b> scrive (MySQL è quello
/// di produzione): un «Development» lasciato per sbaglio su un server vero si ferma all'avvio.</para>
/// </summary>
public static class ProductionIdentityGuard
{
    /// <summary>
    /// Ritorna un messaggio d'errore se la configurazione d'identità è insicura per l'ambiente, altrimenti null.
    /// </summary>
    /// <param name="isDevelopmentEnvironment">true se l'ambiente ospitante è "Development".</param>
    /// <param name="useDevIdentity">true se il modulo è stato montato con l'identità dev fittizia.</param>
    /// <param name="urls">Gli indirizzi su cui ascolta il server (<c>ASPNETCORE_URLS</c>/<c>--urls</c>, separati da
    /// «;»). Null = non detti: quelli di default di Kestrel, che sono locali.</param>
    /// <param name="provider">Il provider di persistenza configurato (<c>Persistence:Provider</c>).</param>
    public static string? Validate(bool isDevelopmentEnvironment, bool useDevIdentity, string? urls = null, string? provider = null)
    {
        if (!useDevIdentity) return null;

        if (!isDevelopmentEnvironment)
            return "Identità di sviluppo (DevCurrentUserProvider) attiva in un ambiente non-Development: " +
                   "sarebbe un bypass totale dell'autorizzazione (admin onnipotente). " +
                   "Monta il modulo con useDevIdentity:false in produzione (identità dal login del sito host).";

        if (string.Equals(provider?.Trim(), "MySql", StringComparison.OrdinalIgnoreCase))
            return "Identità di sviluppo (admin onnipotente) su un database MySQL, cioè quello di produzione: " +
                   "attiva il login (VipiAuth:Enabled=true) o usa una copia SQLite.";

        if (IndirizzoEsposto(urls) is { } esposto)
            return $"Identità di sviluppo (admin onnipotente) con il server in ascolto su «{esposto}», che non è " +
                   "un indirizzo locale: chiunque lo raggiunga sarebbe admin. Ascolta su localhost/127.0.0.1 " +
                   "o attiva il login (VipiAuth:Enabled=true).";

        return null;
    }

    /// <summary>Il primo indirizzo non locale fra quelli dati, o null se sono tutti loopback.</summary>
    private static string? IndirizzoEsposto(string? urls)
    {
        foreach (var u in (urls ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var dopoSchema = u.Contains("://", StringComparison.Ordinal) ? u[(u.IndexOf("://", StringComparison.Ordinal) + 3)..] : u;
            var host = dopoSchema.Split('/')[0];
            if (host.StartsWith('['))
                host = host[..(host.IndexOf(']') + 1)];              // [::1]:5000 → [::1]
            else if (host.LastIndexOf(':') is var c and > 0)
                host = host[..c];                                      // 127.0.0.1:5199 → 127.0.0.1
            var locale = host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                         || host == "[::1]"
                         || (System.Net.IPAddress.TryParse(host, out var ip) && System.Net.IPAddress.IsLoopback(ip));
            if (!locale) return u;
        }
        return null;
    }

    /// <summary>Applica la guardia: lancia <see cref="InvalidOperationException"/> se la config è insicura.</summary>
    public static void EnsureSafe(bool isDevelopmentEnvironment, bool useDevIdentity, string? urls = null, string? provider = null)
    {
        if (Validate(isDevelopmentEnvironment, useDevIdentity, urls, provider) is { } error)
            throw new InvalidOperationException(error);
    }
}
