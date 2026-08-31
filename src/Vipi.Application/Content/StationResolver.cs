using Vipi.Application.Abstractions;

namespace Vipi.Application.Content;

/// <summary>Informazioni di un ACC per la navigazione documentale (derivato dalle ACC nel DB).</summary>
public sealed record AccInfo(string Code, string Name);

/// <summary>Aeroporto con l'ACC di competenza (per mappare i callsign d'aeroporto al loro ACC) e la sua
/// anagrafica di campo.</summary>
/// <param name="HasMilitaryPresence">Dalla sorgente: c'è una base militare sul campo. ⚠️ Non vuol dire
/// «aeroporto militare» — è vero anche per Linate, Pisa, Ciampino.</param>
/// <param name="IsMilitaryOnly">Scelta di un amministratore: nessun traffico civile.</param>
/// <param name="ElevationFt">Quota del riferimento aeroporto, in piedi. null = la sorgente non la dà.</param>
/// <param name="MagneticVariation">Variazione magnetica in gradi, positiva a EST (in Italia è 1°–4° E).</param>
/// <param name="Iata">Codice IATA, dove la sorgente ce l'ha (55 aeroporti su 93 in archivio).</param>
/// <param name="Latitude">Coordinate del riferimento aeroporto, in gradi decimali.</param>
/// <remarks>
/// ⚠️ I cinque campi d'anagrafica stanno QUI e non in <c>AirportData</c> per la stessa ragione per cui ci
/// stanno già i due militari: questa mappa è <b>già in cache</b> e la scalda il layout, mentre il profilo
/// dell'aeroporto è una lettura di sei query che la pagina militare non fa nemmeno. Quota, variazione e
/// coordinate sono anagrafica — le riscrive il giro notturno, non un editor — e per questo non passano
/// dallo snapshot di release: congelarle vorrebbe dire mostrarle vuote su ogni documento già pubblicato
/// finché qualcuno non lo ripubblica.
/// </remarks>
public sealed record AirportStation(string Icao, string AccCode,
    bool HasMilitaryPresence = false, bool IsMilitaryOnly = false,
    int? ElevationFt = null, double? MagneticVariation = null, string? Iata = null,
    double? Latitude = null, double? Longitude = null);

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

    /// <summary>
    /// Forza il caricamento del catalogo (ACC + mappa aeroporti) <b>fuori dal render</b>.
    ///
    /// <para>⚠️ <b>Serve ancora, anche adesso che la cache è di processo</b>
    /// (<see cref="ICatalogoStazioni"/>), e serve per la stessa ragione di sempre: qualcuno dev'essere il
    /// <b>primo</b>. Dopo un riavvio — e su Plesk+Passenger il processo si spegne per inattività, quindi
    /// spesso — la prima pagina che chiede il catalogo paga la lettura, e se la paga <b>dentro il render</b>
    /// cade sullo stesso <c>DbContext</c> che la pagina sta già usando. Quel che è cambiato è <b>quante
    /// volte</b> capita: prima una per circuito, adesso una per processo.</para>
    ///
    /// <para>⚠️ E resta vero che può <b>lanciare</b>: chi la chiama nel ciclo di vita la avvolga, o un
    /// intoppo del database diventa una pagina d'errore — vedi <c>SopHome</c> e <c>SopLayout</c>.</para>
    /// </summary>
    void Prewarm();
}

/// <inheritdoc cref="IStationResolver"/>
///
/// <para><b>Non tiene piu' cache sue.</b> Fino al 31 agosto 2026 questa classe teneva le due copie in campi
/// d'istanza, e siccome e' <c>scoped</c> — e in Blazor Server lo scope e' il <b>circuito</b>, cioe' l'intera
/// sessione — ogni sessione aperta rileggeva ACC e aeroporti dal database per conto suo. Adesso le copie
/// stanno in <see cref="ICatalogoStazioni"/>, che e' singleton: qui resta il <b>come si legge</b>, che ha
/// bisogno del <c>DbContext</c> dello scope e quindi non puo' stare in un singleton.</para>
public sealed class StationResolver : IStationResolver
{
    private readonly IStationDirectory _dir;
    private readonly ICatalogoStazioni _catalogo;

    public StationResolver(IStationDirectory dir, ICatalogoStazioni catalogo)
    {
        _dir = dir;
        _catalogo = catalogo;
    }

    public IReadOnlyList<AccInfo> Accs => _catalogo.Accs(_dir.ListAccs);

    private IReadOnlyDictionary<string, AirportStation> Airports => _catalogo.Aeroporti(_dir.ListAirports);

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

    // Scalda tutt'e due le copie in una volta, dal ciclo di vita: contesto libero e sequenziale.
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
