using Vipi.Application.Abstractions;

namespace Vipi.Application.Content;

/// <summary>Informazioni di un ACC per la navigazione documentale (derivato dalle ACC nel DB).</summary>
public sealed record AccInfo(string Code, string Name);

/// <summary>Aeroporto con l'ACC di competenza (per mappare i callsign d'aeroporto al loro ACC).</summary>
public sealed record AirportStation(string Icao, string AccCode);

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
    private Dictionary<string, string>? _airportToAcc;   // ICAO → codice ACC
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
        _airportToAcc = null;
        _cachedAt = v;
    }

    public IReadOnlyList<AccInfo> Accs
    {
        get { ScadiSeVecchia(); return _cache ??= _dir.ListAccs(); }
    }

    private Dictionary<string, string> AirportToAcc
    {
        get
        {
            ScadiSeVecchia();
            return _airportToAcc ??= _dir.ListAirports()
                .GroupBy(a => a.Icao, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().AccCode, StringComparer.OrdinalIgnoreCase);
        }
    }

    public AccInfo? Resolve(string accCode) =>
        Accs.FirstOrDefault(a => a.Code.Equals(accCode, StringComparison.OrdinalIgnoreCase));

    // Scalda entrambe le cache in una volta (chiamata dal ciclo di vita async, context libero e sequenziale).
    public void Prewarm() { _ = Accs; _ = AirportToAcc; }

    public AccInfo? ResolveByCallsign(string callsign)
    {
        if (string.IsNullOrWhiteSpace(callsign)) return null;
        var i = callsign.IndexOf('_');
        var head = i > 0 ? callsign[..i] : callsign;
        return Resolve(head)                                                   // testa = codice ACC
            ?? (AirportToAcc.TryGetValue(head, out var acc) ? Resolve(acc) : null);   // testa = ICAO aeroporto → suo ACC
    }
}
