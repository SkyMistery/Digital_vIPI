namespace Vipi.Application.Abstractions;

/// <summary>Che cosa è un punto del catalogo. Serve a chi SCEGLIE, non al codice: due nomi uguali di natura
/// diversa si distinguono solo così, e chi scrive un CoP deve poter dire «no, io intendevo il VOR».</summary>
public enum NavaidKind
{
    /// <summary>Punto di riporto (<c>itfix.fix</c>): il grosso del catalogo, nomi di 5 lettere.</summary>
    Fix,
    /// <summary>Radioassistenza VOR (<c>itvor.vor</c>): nomi di 2-3 lettere.</summary>
    Vor,
    /// <summary>Radioassistenza NDB (<c>itndb.ndb</c>).</summary>
    Ndb,
}

/// <summary>Un punto del catalogo: il nome come si scrive, di che natura è, e dove sta.</summary>
/// <remarks>
/// ⚠️ Fino al 26 agosto 2026 le coordinate qui non c'erano, e il commento diceva: «*il giorno che servisse la
/// posizione … questo record cresce di due campi*». È quel giorno. Le vogliono i poligoni di settore del
/// sectorfile, dove **233 vertici su 20 692** non sono coordinate ma nomi di punto (<c>TUFTE;TUFTE;</c>) da
/// risolvere qui.
/// <para><see cref="Lat"/>/<see cref="Lon"/> restano <b>nullable</b>: una riga di catalogo malformata dà
/// comunque un nome buono per la completion delle SID, che della posizione non sa che farsene.</para>
/// </remarks>
public sealed record NavaidName(string Name, NavaidKind Kind, double? Lat = null, double? Lon = null);

/// <summary>
/// Il catalogo dei punti della divisione, nelle DUE forme in cui serve: l'elenco ordinato (per chi propone) e
/// l'insieme dei nomi (per chi completa il fix troncato di una SID).
///
/// <para>Sono due viste dello <b>stesso</b> dato tenute insieme di proposito: prima erano due caricamenti
/// distinti dello stesso file, ed è esattamente il «modello gemello» che il processo vieta. Chi cerca «dove
/// sta l'elenco dei fix» deve trovare <b>un</b> posto.</para>
/// </summary>
public sealed class NavaidCatalog
{
    /// <summary>Catalogo vuoto: sorgente non configurata o non raggiungibile. È un caso normale — si perdono i
    /// suggerimenti, non si rompe niente.</summary>
    public static readonly NavaidCatalog Empty = new(Array.Empty<NavaidName>());

    /// <summary>Costruisce il catalogo da voci grezze: scarta i vuoti, toglie i duplicati (vince la PRIMA
    /// occorrenza, quindi l'ordine con cui il chiamante accoda decide la natura di un nome omonimo) e ordina
    /// per nome.</summary>
    public NavaidCatalog(IEnumerable<NavaidName> entries)
    {
        var seen = new Dictionary<string, NavaidName>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries)
        {
            var name = (e.Name ?? "").Trim();
            if (name.Length == 0) continue;
            if (!seen.ContainsKey(name)) seen[name] = e with { Name = name };
        }

        Entries = seen.Values.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
        Names = new HashSet<string>(seen.Keys, StringComparer.OrdinalIgnoreCase);
        _points = seen.Values.Where(e => e.Lat is not null && e.Lon is not null)
            .ToDictionary(e => e.Name, e => (e.Lat!.Value, e.Lon!.Value), StringComparer.OrdinalIgnoreCase);
    }

    private readonly Dictionary<string, (double Lat, double Lon)> _points;

    /// <summary>
    /// Dove sta un punto, per nome. Falso se il nome non è in catalogo <b>o</b> se è in catalogo senza
    /// coordinate — per chi disegna sono la stessa cosa, e trattarle diversamente vorrebbe dire far finta di
    /// sapere dove sia.
    /// </summary>
    public bool TryGetPoint(string? name, out (double Lat, double Lon) point)
    {
        point = default;
        return !string.IsNullOrWhiteSpace(name) && _points.TryGetValue(name.Trim(), out point);
    }

    /// <summary>Quanti punti hanno una posizione (diagnostica: se scende a zero, il parser dei navaid ha smesso
    /// di leggere le coordinate e i poligoni di settore spariscono in silenzio).</summary>
    public int PointsWithPosition => _points.Count;

    /// <summary>I punti in ordine alfabetico, senza ripetizioni.</summary>
    public IReadOnlyList<NavaidName> Entries { get; }

    /// <summary>I soli nomi, confronto senza distinzione di maiuscole: la forma che serve alla completion SID.</summary>
    public IReadOnlySet<string> Names { get; }

    /// <summary>I nomi di una natura sola, in ordine. È la forma in cui il catalogo viaggia verso il browser:
    /// tre elenchi di stringhe pesano un sesto di un elenco di oggetti con la natura ripetuta a ogni voce.</summary>
    public IReadOnlyList<string> NamesOf(NavaidKind kind) =>
        Entries.Where(e => e.Kind == kind).Select(e => e.Name).ToList();
}

/// <summary>Porta neutra: il catalogo dei punti della divisione dalla sorgente esterna (impl. sectorfile
/// Aurora su GitHub in Infrastructure). Gemella di <see cref="ISidProvider"/> e <see cref="ITowerShapeSource"/>.</summary>
public interface INavaidSource
{
    /// <summary>Il catalogo. <see cref="NavaidCatalog.Empty"/> se la sorgente non è configurata.</summary>
    Task<NavaidCatalog> GetAsync(CancellationToken ct = default);

    /// <summary>
    /// Riscarica il catalogo adesso, buttando la copia tenuta in memoria.
    /// <para>Esiste perché fra due giri automatici passano ventiquattro ore, e un punto aggiunto oggi al
    /// sectorfile fino ad allora verrebbe segnalato come inesistente a chi lo scrive giusto. È l'unico caso in
    /// cui qualcuno ha ragione di forzare la mano alla sorgente.</para>
    /// </summary>
    Task<NavaidCatalog> RefreshAsync(CancellationToken ct = default);
}
