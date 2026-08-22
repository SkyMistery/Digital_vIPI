namespace Vipi.Host;

/// <summary>
/// Dove finiscono gli URL di ieri. È una <b>tabella</b>, non una riscrittura di prefisso, e il motivo è che
/// il 22 agosto 2026 sono cambiate due cose insieme: il prefisso (<c>/vsop</c> → <c>/services/vsop</c>, perché
/// il sito è diventato il contenitore dei servizi per gli ATC) e dieci segmenti rimasti in italiano
/// (<c>guida</c> → <c>guide</c>, <c>permessi</c> → <c>permissions</c>, …). Riscrivere solo il prefisso
/// porterebbe <c>/vsop/guida</c> su <c>/services/vsop/guida</c>, che non esiste: servirebbe un secondo salto.
/// <para>
/// <b>Un salto solo</b> è il requisito, non un dettaglio: questi sono indirizzi che stanno nei preferiti di chi
/// controlla e nei messaggi su Discord, e una catena di redirect si paga a ogni apertura. Ogni URL storico esce
/// da qui con l'indirizzo <b>finale</b>. La specifica è in
/// <c>docs/feature/2026-08-22-servizi-atc-e-profile-swapper.md</c> §2 e §4.
/// </para>
/// <para>
/// ⚠️ <b>Gli endpoint macchina non sono qui e non si spostano</b>: <c>/vsop/health</c>, <c>/vsop/health/ready</c>,
/// <c>/vsop/api/v1/*</c>, <c>/vsop/live/atc</c> e <c>/vsop/media/*</c> restano ai loro indirizzi, perché li
/// conoscono <c>render.yaml</c> e la dashboard Render, lo smoke della CI, i binari del bridge Aurora già
/// distribuiti e l'HTML in cache dei browser. Nessun essere umano li digita. Il routing li protegge da sé —
/// un segmento letterale batte una catch-all — e <see cref="Resolve"/> li rifiuta comunque, esplicitamente.
/// </para>
/// </summary>
public static class LegacyRoutes
{
    /// <summary>Il prefisso di oggi. Un posto solo: lo citano anche i test.</summary>
    public const string Prefix = "/services/vsop";

    /// <summary>I due prefissi storici, in ordine di lunghezza decrescente (<c>/vsop</c> prima di <c>/sop</c>).</summary>
    private static readonly string[] OldPrefixes = { "/vsop", "/sop" };

    /// <summary>
    /// Primo segmento che indica un endpoint macchina: non è una pagina, non si sposta, non si redirige.
    /// </summary>
    private static readonly HashSet<string> MachineFirstSegments =
        new(StringComparer.OrdinalIgnoreCase) { "health", "api", "media" };

    /// <summary>
    /// I segmenti tradotti il 22 agosto 2026. <c>struttura</c> è più vecchia (Round 12) e finisce qui perché
    /// anche lei deve arrivare all'indirizzo di oggi in un salto solo, non a quello del 2025.
    /// </summary>
    private static readonly Dictionary<string, string> Segments =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["guida"] = "guide",
            ["versioni"] = "versions",
            ["permessi"] = "permissions",
            ["trasferimenti"] = "transfers",
            ["confinanti"] = "neighbours",
            ["diagnostica"] = "diagnostics",
            ["sorgenti"] = "sources",
            ["sectorstructure"] = "sector-structure",
            ["struttura"] = "sector-structure",
            ["newdoc"] = "new-document",
            ["aeroporti"] = "airports",
            ["aeroporto"] = "airports",
        };

    /// <summary>
    /// Le due viste operative per-ACC sono diventate UNA vista per callsign (doc refactor 12). Il callsign
    /// arrivava in query — <c>?p=</c> per le postazioni d'area, <c>?app=</c> per gli APP — e oggi è un segmento.
    /// </summary>
    private static readonly Dictionary<string, string> LiveViews =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["operativa"] = "p",
            ["live"] = "p",
            ["operativa-app"] = "app",
            ["live-app"] = "app",
        };

    /// <summary>
    /// L'indirizzo di oggi per un percorso storico, query compresa; <c>null</c> se non è un percorso storico
    /// (o se è un endpoint macchina, che non va redirezionato ma servito).
    /// </summary>
    /// <param name="request">
    /// La richiesta: servono il percorso e la query. Si passa la richiesta intera e non le due parti perché la
    /// query va sia <b>letta</b> (il callsign delle viste live) sia <b>ricopiata</b> (tutte le altre rotte, es.
    /// <c>?icao=LIRF</c>), e tenerle insieme evita che le due cose si separino. In prova basta un
    /// <c>DefaultHttpContext</c>: assegnato <c>QueryString</c>, <c>Query</c> si popola da sé.
    /// </param>
    public static string? Resolve(HttpRequest request)
    {
        string path = request.Path.Value ?? "";
        var query = request.Query;

        if (string.IsNullOrEmpty(path)) return null;

        var prefix = OldPrefixes.FirstOrDefault(p =>
            path.Equals(p, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase));
        if (prefix is null) return null;

        var rest = path.Substring(prefix.Length).Trim('/');
        if (rest.Length == 0) return Prefix + request.QueryString;

        var segs = rest.Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Endpoint macchina: si serve, non si redirige. In pratica il routing non arriva mai qui (un segmento
        // letterale ha la precedenza su una catch-all), ma dirlo qui rende la regola leggibile e verificabile.
        if (MachineFirstSegments.Contains(segs[0])) return null;
        if (segs.Length == 2 && segs[0].Equals("live", StringComparison.OrdinalIgnoreCase)
                             && segs[1].Equals("atc", StringComparison.OrdinalIgnoreCase)) return null;

        // Vista live: /{acc}/operativa · /{acc}/live · /{acc}/operativa-app · /{acc}/live-app.
        // ⚠️ Solo con DUE segmenti, o /{acc}/live/{callsign} — che è già la forma nuova — ci ricadrebbe dentro.
        if (segs.Length == 2 && LiveViews.TryGetValue(segs[1], out var parametro))
        {
            var callsign = query[parametro].ToString().Trim().ToLowerInvariant();
            return callsign.Length > 0
                ? $"{Prefix}/live/{Uri.EscapeDataString(callsign)}"
                : $"{Prefix}/live";
        }

        for (int i = 0; i < segs.Length; i++)
            if (Segments.TryGetValue(segs[i], out var tradotto))
                segs[i] = tradotto;

        // La query si ricopia tal quale: /vsop/{acc}/airports?icao=LIRF deve restare sull'aeroporto che
        // stava guardando chi ha aperto il segnalibro, non sull'elenco.
        return $"{Prefix}/{string.Join('/', segs)}{request.QueryString}";
    }
}
