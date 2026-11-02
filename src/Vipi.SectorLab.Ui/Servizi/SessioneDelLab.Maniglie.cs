using Vipi.SectorLab.Core.Modifiche;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Ui.Servizi;

/// <summary>
/// Una maniglia sulla mappa (carta «L'editor sulla mappa», R1-R2): il vertice in posizione <paramref name="Posizione"/>
/// dell'elenco <paramref name="Campo"/>, dov'è adesso. <paramref name="PerNome"/>: è scritto col nome di un punto, e la
/// posizione è quella del catalogo (R6: non si trascina dove capita). <paramref name="Aggiungibile"/>: l'elenco sa
/// scrivere una coordinata, quindi fra questo vertice e il prossimo c'è la maniglia di mezzo (R3); non nei punti di una
/// procedura <c>.str</c> né nel tracciato di un'aerovia, che sono solo nomi.
/// </summary>
public sealed record ManigliaDelVertice(string Campo, int Posizione, double Lat, double Lon, bool PerNome, bool Aggiungibile);

/// <summary>L'ultimo vertice mosso dalla mappa, col rifiuto che ha avuto (null = fatto): la Scheda lo mostra accanto al suo elenco (R7).</summary>
public sealed record GestoDallaMappa(string File, int Record, string Campo, int Posizione, string? Rifiuto);

public sealed partial class SessioneDelLab
{
    /// <summary>Quanti punti di una linea di un <c>.geo</c> si vedono prima e dopo il segmento scelto: nella Scheda e sulla mappa.</summary>
    public const int IntornoDellaLinea = 20;

    private (string File, int Record)? _modificaSullaMappa;

    /// <summary>
    /// Vero se il record scelto ha le maniglie sulla mappa del Sector (R1). Scegliere un altro record le spegne: il modo
    /// vale per un record, non per la sessione.
    /// </summary>
    public bool ModificaSullaMappa => _modificaSullaMappa is { } m && m == Scelta;

    /// <summary>L'ultimo vertice mosso dalla mappa sul record scelto, o null.</summary>
    public GestoDallaMappa? UltimoDallaMappa => _ultimoDallaMappa is { } g && (g.File, g.Record) == Scelta ? g : null;

    private GestoDallaMappa? _ultimoDallaMappa;

    /// <summary>Il vertice della Scheda sotto il mouse (R7): la sua maniglia si evidenzia. Null = nessuno.</summary>
    public (string Campo, int Posizione)? VerticeIndicato { get; private set; }

    /// <summary>Accende o spegne le maniglie sul record scelto (R1). Senza scelta, o senza vertici, non si accende.</summary>
    public void ModificaSullaMappaDi(bool accesa)
    {
        var prima = ModificaSullaMappa;
        _modificaSullaMappa = accesa && Scelta is { } scelta && HaVerticiDaMuovere(scelta.File, scelta.Record) ? scelta : null;
        VerticeIndicato = null;
        if (prima == ModificaSullaMappa)
            return;
        Registro.Scrivi("modifica sulla mappa", ModificaSullaMappa ? $"{Scelta!.Value.File}#{Scelta.Value.Record}" : "spenta");
        Avvisa();
    }

    /// <summary>Vero se il record ha almeno un elenco di vertici, o è un segmento di una linea di un <c>.geo</c>.</summary>
    public bool HaVerticiDaMuovere(string fileRelativo, int record)
        => LineaDi(fileRelativo, record) is not null || ElenchiDiVerticiDi(fileRelativo, record).Any(e => e.Quanti > 0);

    /// <summary>
    /// Le maniglie del record con le maniglie accese, una per vertice che ha una posizione: un nome che il catalogo non
    /// conosce non ha dove stare, e non ne ha. Della linea di un <c>.geo</c> solo i punti intorno al segmento scelto, come
    /// nella Scheda: le coste sono migliaia di punti.
    /// </summary>
    public IReadOnlyList<ManigliaDelVertice> Maniglie()
    {
        if (!ModificaSullaMappa || Scelta is not { } scelta)
            return [];

        if (LineaDi(scelta.File, scelta.Record) is { } linea)
        {
            int qui = linea.Record.ToList().IndexOf(scelta.Record);
            int da = Math.Max(0, qui - IntornoDellaLinea);
            int finoA = Math.Min(linea.Punti.Count - 1, qui + 1 + IntornoDellaLinea);
            return [.. Enumerable.Range(da, Math.Max(0, finoA - da + 1))
                .Select(i => new ManigliaDelVertice(ChiaveDellaLinea, i, linea.Punti[i].LatitudeDeg, linea.Punti[i].LongitudeDeg, PerNome: false, Aggiungibile: true))];
        }

        var maniglie = new List<ManigliaDelVertice>();
        foreach (var elenco in ElenchiDiVerticiDi(scelta.File, scelta.Record))
        {
            for (int i = 0; i < elenco.Quanti; i++)
            {
                var punto = elenco.PuntoDi(i);
                if (punto.TryRisolvi(CatalogoScelto, out var dove))
                    maniglie.Add(new ManigliaDelVertice(elenco.Chiave, i, dove.LatitudeDeg, dove.LongitudeDeg, punto.PerNome, elenco.AmmetteCoordinate));
            }
        }

        return maniglie;
    }

    /// <summary>
    /// Un vertice lasciato sulla mappa (R2): il gesto «vertice spostato» della Scheda, con la coordinata del rilascio. Un
    /// vertice scritto col nome non si sposta così (R6: lo porta solo la calamita su un altro nome). Falso = rifiutato,
    /// e il motivo sta in <see cref="Rifiuto"/>.
    /// </summary>
    public bool VerticeDallaMappa(string campo, int posizione, double lat, double lon)
    {
        if (!ModificaSullaMappa || Scelta is not { } scelta)
            return false;

        if (Maniglie().FirstOrDefault(m => m.Campo == campo && m.Posizione == posizione) is not { } maniglia)
            return Rifiuta(scelta, campo, posizione, "Quel vertice non c'è più: la forma è cambiata.");
        if (maniglia.PerNome)
            return Rifiuta(scelta, campo, posizione, "Questo vertice è il nome di un punto: sulla mappa si sposta solo su un altro punto col nome.");
        if (!maniglia.Aggiungibile)
            return Rifiuta(scelta, campo, posizione, "Qui un punto si scrive per nome: sulla mappa si sposta solo su un altro punto col nome.");

        if (Fuori(lat, lon))
            return Rifiuta(scelta, campo, posizione, FuoriDalMondo);

        return Fatto(scelta, campo, posizione,
            GestoSuiVertici(scelta.File, scelta.Record, campo, GestoDeiVertici.Cambia, posizione, Scritta(lat, lon)));
    }

    /// <summary>
    /// La maniglia di mezzo lasciata sulla mappa (R3): il gesto «vertice aggiunto» della Scheda, prima di
    /// <paramref name="primaDi"/>, con la coordinata del rilascio. Le maniglie dei due vertici che lo stringono devono
    /// esserci: una maniglia vecchia (la forma è cambiata nel frattempo) si rifiuta.
    /// </summary>
    public bool VerticeAggiuntoDallaMappa(string campo, int primaDi, double lat, double lon)
    {
        if (!ModificaSullaMappa || Scelta is not { } scelta)
            return false;

        var maniglie = Maniglie();
        if (maniglie.FirstOrDefault(m => m.Campo == campo && m.Posizione == primaDi - 1) is not { Aggiungibile: true }
            || !maniglie.Any(m => m.Campo == campo && m.Posizione == primaDi))
            return Rifiuta(scelta, campo, primaDi, "Lì non si aggiunge un vertice: la forma è cambiata.");
        if (Fuori(lat, lon))
            return Rifiuta(scelta, campo, primaDi, FuoriDalMondo);

        return Fatto(scelta, campo, primaDi,
            GestoSuiVertici(scelta.File, scelta.Record, campo, GestoDeiVertici.Aggiungi, primaDi, Scritta(lat, lon)));
    }

    /// <summary>
    /// «Togli questo vertice» dal clic destro su una maniglia (R4): il gesto «vertice tolto» della Scheda, col suo rifiuto
    /// sull'ultimo vertice. Anche un vertice col nome si toglie: non va scritto, va via.
    /// </summary>
    public bool VerticeToltoDallaMappa(string campo, int posizione)
    {
        if (!ModificaSullaMappa || Scelta is not { } scelta)
            return false;
        if (!Maniglie().Any(m => m.Campo == campo && m.Posizione == posizione))
            return Rifiuta(scelta, campo, posizione, "Quel vertice non c'è più: la forma è cambiata.");

        // Tolto, il vertice non c'è più: la Scheda segna quello che ha preso il suo posto (o l'ultimo).
        bool fatto = GestoSuiVertici(scelta.File, scelta.Record, campo, GestoDeiVertici.Togli, posizione);
        int segnato = fatto ? Math.Min(posizione, Math.Max(0, VerticiDi(scelta.File, scelta.Record, campo).Count - 1)) : posizione;
        return Fatto(scelta, campo, segnato, fatto);
    }

    private const string FuoriDalMondo = "Lì non c'è una coordinata che il sector sappia scrivere.";

    // Leaflet dà longitudini oltre ±180 se la mappa ha fatto il giro del mondo.
    private static bool Fuori(double lat, double lon)
        => !double.IsFinite(lat) || !double.IsFinite(lon) || Math.Abs(lat) > 90 || Math.Abs(lon) > 180;

    /// <summary>Il punto del rilascio come lo scrive la Scheda (N041.15.00.000 E012.07.30.000): lo legge la stessa lettura dei campi.</summary>
    private static string Scritta(double lat, double lon) => CoordinateConverter.ToDottedDms(new Coordinate(lat, lon));

    /// <summary>Una coordinata scritta (due pezzi), come la scrive <see cref="Scritta"/>; falso se non lo è.</summary>
    private static bool PuntoDelTesto(string testo, out Coordinate dove)
    {
        dove = default;
        string[] pezzi = testo.Split([' ', '\t', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (pezzi.Length != 2)
            return false;
        try
        {
            dove = CoordinateConverter.ParsePair(pezzi[0], pezzi[1]);
            return true;
        }
        catch (Exception e) when (e is CoordinateParseException or FormatException)
        {
            return false;
        }
    }

    private bool Fatto((string File, int Record) scelta, string campo, int posizione, bool fatto)
    {
        _ultimoDallaMappa = new GestoDallaMappa(scelta.File, scelta.Record, campo, posizione, fatto ? null : Rifiuto);
        Avvisa();
        return fatto;
    }

    private bool Rifiuta((string File, int Record) scelta, string campo, int posizione, string motivo)
    {
        Rifiuto = motivo;
        _ultimoDallaMappa = new GestoDallaMappa(scelta.File, scelta.Record, campo, posizione, motivo);
        Registro.Scrivi("vertice dalla mappa", $"{scelta.File}#{scelta.Record} {campo}[{posizione}]: {motivo}");
        Avvisa();
        return false;
    }

    /// <summary>Il mouse su un vertice della Scheda (o fuori: null): la sua maniglia sulla mappa si evidenzia (R7).</summary>
    public void IndicaIlVertice(string? campo, int posizione = 0)
    {
        (string, int)? adesso = campo is null || !ModificaSullaMappa ? null : (campo, posizione);
        if (adesso == VerticeIndicato)
            return;
        VerticeIndicato = adesso;
        Avvisa();
    }
}
