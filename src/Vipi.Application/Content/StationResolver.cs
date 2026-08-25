using Vipi.Application.Abstractions;

namespace Vipi.Application.Content;

/// <summary>Informazioni di un ACC per la navigazione documentale (derivato dalle ACC nel DB).</summary>
public sealed record AccInfo(string Code, string Name);

/// <summary>Aeroporto con l'ACC di competenza (per mappare i callsign d'aeroporto al loro ACC).</summary>
/// <param name="HasMilitaryPresence">Dalla sorgente: c'è una base militare sul campo. ⚠️ Non vuol dire
/// «aeroporto militare» — è vero anche per Linate, Pisa, Ciampino.</param>
/// <param name="IsMilitaryOnly">Scelta di un amministratore: nessun traffico civile.</param>
public sealed record AirportStation(string Icao, string AccCode,
    bool HasMilitaryPresence = false, bool IsMilitaryOnly = false);

/// <summary>
/// Contatore di processo del CATALOGO delle stazioni: sale di uno ogni volta che l'elenco degli ACC
/// visibili cambia (un ACC nascosto o rimostrato, un import che ne aggiunge).
///
/// <para>⚠️ Serve perché <see cref="IStationResolver"/> è <c>scoped</c>, e in Blazor Server «scoped» non
/// vuol dire «per richiesta» ma <b>per circuito</b>: la cache dura quanto la sessione SPA. Senza questo
/// contatore, chi nascondeva o mostrava un ACC continuava a vedere l'elenco vecchio in ogni pagina
/// interattiva finché non ricaricava il browser — mentre il chrome (SopLayout, SSR con uno scope per
/// richiesta) mostrava già quello nuovo. Due elenchi diversi nella stessa schermata, e la pagina degli
/// accordi che restava su titolo e riga ACC perché l'ACC scelto non si risolveva più.</para>
///
/// <para>Singleton di proposito: il cambio vale per TUTTE le sessioni aperte, non solo per chi l'ha fatto.</para>
/// </summary>
public interface IStationCatalogVersion
{
    int Current { get; }

    /// <summary>Da chiamare DOPO aver scritto: le cache si rileggeranno alla prossima lettura.</summary>
    void Bump();
}

/// <inheritdoc cref="IStationCatalogVersion"/>
public sealed class StationCatalogVersion : IStationCatalogVersion
{
    private int _v;
    public int Current => Volatile.Read(ref _v);
    public void Bump() => Interlocked.Increment(ref _v);
}

/// <summary>
/// Risolve la navigazione per ACC dalle ACC esistenti nel DB (via <see cref="IStationDirectory"/>).
/// </summary>
public interface IStationResolver
{
    IReadOnlyList<AccInfo> Accs { get; }
    AccInfo? Resolve(string accCode);

    /// <summary>ACC di competenza di un callsign ATC: per testa = codice ACC (es. LIRR_NE_CTR → LIRR),
    /// oppure per testa = ICAO di un aeroporto (es. LIRP_TWR → l'ACC di LIRP). Null se non riconosciuto.</summary>
    AccInfo? ResolveByCallsign(string callsign);

    /// <summary>
    /// Anagrafica militare di un aeroporto, per le testate dei documenti che lo riguardano. Null se l'ICAO non è
    /// un aeroporto in archivio.
    /// <para>Sta qui e non su un servizio a parte perché la mappa degli aeroporti è <b>già</b> in cache e la
    /// scalda il layout: una testata non deve interrogare il database mentre si disegna.</para>
    /// </summary>
    AirportStation? Airport(string? icao);

    /// <summary>
    /// L'aeroporto di un callsign, dalla sua testa: <c>LIBG_APP</c> → LIBG, <c>LIPE_W_APP</c> → LIPE. Null se la
    /// testa non è un aeroporto in archivio — ed è il caso normale per gli APP di ACC (<c>LIRR_APP</c>: la testa
    /// è un centro, non uno scalo), che infatti non descrivono nessun aeroporto.
    /// </summary>
    AirportStation? AirportOfCallsign(string? callsign);

    /// <summary>Forza il caricamento delle cache (ACC + mappa aeroporti→ACC) FUORI dal render. Va chiamato dal
    /// ciclo di vita async della pagina prima di usare <see cref="ResolveByCallsign"/> nel render: evita che il
    /// lazy-load colpisca il DbContext condiviso durante il render (crash "second operation" su Postgres).</summary>
    void Prewarm();
}

/// <inheritdoc cref="IStationResolver"/>
public sealed class StationResolver : IStationResolver
{
    private readonly IStationDirectory _dir;
    private readonly IStationCatalogVersion _version;
    private IReadOnlyList<AccInfo>? _cache;
    private Dictionary<string, AirportStation>? _airports;   // ICAO → aeroporto (ACC + anagrafica militare)
    private int _cachedAt = -1;                          // versione del catalogo con cui le cache furono riempite

    public StationResolver(IStationDirectory dir, IStationCatalogVersion version)
    {
        _dir = dir;
        _version = version;
    }

    /// <summary>
    /// ⚠️ La cache NON dura una richiesta: questo servizio è scoped e in Blazor Server lo scope è il
    /// <b>circuito</b>, cioè l'intera sessione. Prima di leggere si controlla la versione del catalogo: se
    /// qualcuno ha nascosto o importato un ACC — anche in un'altra sessione — le cache si buttano e si
    /// rilegge. Costa un confronto di interi per lettura; il caso normale resta senza query.
    /// </summary>
    private void ScadiSeVecchia()
    {
        var v = _version.Current;
        if (v == _cachedAt) return;
        _cache = null;
        _airports = null;
        _cachedAt = v;
    }

    public IReadOnlyList<AccInfo> Accs
    {
        get { ScadiSeVecchia(); return _cache ??= _dir.ListAccs(); }
    }

    private Dictionary<string, AirportStation> Airports
    {
        get
        {
            ScadiSeVecchia();
            return _airports ??= _dir.ListAirports()
                .GroupBy(a => a.Icao, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        }
    }

    public AirportStation? Airport(string? icao) =>
        string.IsNullOrWhiteSpace(icao) ? null : Airports.GetValueOrDefault(icao.Trim().ToUpperInvariant());

    public AirportStation? AirportOfCallsign(string? callsign)
    {
        var c = (callsign ?? "").Trim();
        if (c.Length == 0) return null;
        var testa = c.Contains('_') ? c[..c.IndexOf('_')] : c;
        return Airport(testa);
    }

    public AccInfo? Resolve(string accCode) =>
        Accs.FirstOrDefault(a => a.Code.Equals(accCode, StringComparison.OrdinalIgnoreCase));

    // Scalda entrambe le cache in una volta (chiamata dal ciclo di vita async, context libero e sequenziale).
    public void Prewarm() { _ = Accs; _ = Airports; }

    public AccInfo? ResolveByCallsign(string callsign)
    {
        if (string.IsNullOrWhiteSpace(callsign)) return null;
        var i = callsign.IndexOf('_');
        var head = i > 0 ? callsign[..i] : callsign;
        return Resolve(head)                                                   // testa = codice ACC
            ?? (Airports.TryGetValue(head, out var apt) ? Resolve(apt.AccCode) : null);   // testa = ICAO aeroporto → suo ACC
    }
}
