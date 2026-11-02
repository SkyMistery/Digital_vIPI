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

/// <summary>Un disegno a clic in corso (§9, R10): su quale elenco, se la forma si chiude, se si clicca solo sui punti col nome (D7).</summary>
public sealed record DisegnoSullaMappa(string File, int Record, string Campo, bool Chiudi, bool SoloNomi);

/// <summary>Un clic del disegno: dove, e il record a cui la calamita l'ha agganciato (null: nessuno).</summary>
public sealed record PuntoCliccato(double Lat, double Lon, string? SuFile, int SuRecord);

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
        // R10: mentre si disegna le maniglie si tolgono (un clic deve fare un punto, non prendere una maniglia).
        if (!ModificaSullaMappa || Scelta is not { } scelta || Disegno is not null)
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
    /// Un vertice lasciato sulla mappa (R2): il gesto «vertice spostato» della Scheda, con la coordinata del rilascio, o
    /// del punto a cui la calamita l'ha agganciato (<paramref name="suFile"/>#<paramref name="suRecord"/>, R5). Un vertice
    /// scritto col nome si sposta solo così, su un altro punto col nome (R6). Falso = rifiutato, e il motivo sta in
    /// <see cref="Rifiuto"/>.
    /// </summary>
    public bool VerticeDallaMappa(string campo, int posizione, double lat, double lon, string? suFile = null, int suRecord = 0)
    {
        if (!ModificaSullaMappa || Scelta is not { } scelta)
            return false;

        if (Maniglie().FirstOrDefault(m => m.Campo == campo && m.Posizione == posizione) is not { } maniglia)
            return Rifiuta(scelta, campo, posizione, "Quel vertice non c'è più: la forma è cambiata.");
        if (Fuori(lat, lon))
            return Rifiuta(scelta, campo, posizione, FuoriDalMondo);

        // R5: agganciato a un punto o a un vertice di un'altra forma, prende la sua coordinata ESATTA (quella del file,
        // non quella arrotondata che la mappa ha disegnato). R6: un vertice col nome (o un elenco fatto di nomi) prende il
        // nome del punto, e senza un punto col nome non si sposta.
        var aggancio = suFile is null ? null : AgganciaA(suFile, suRecord, lat, lon);
        if (suFile is not null && aggancio is null)
            return Rifiuta(scelta, campo, posizione, "Il punto a cui l'hai agganciato non c'è più sulla mappa.");
        bool perNome = maniglia.PerNome || !maniglia.Aggiungibile;
        string? nome = aggancio?.Nome;
        if (perNome && nome is null)
            return Rifiuta(scelta, campo, posizione, aggancio is null
                ? "Questo vertice è il nome di un punto: lascialo su un altro punto col nome (un fix, un VOR…), e la calamita lo prende."
                : "Lì non c'è un punto col nome che il master conosca: questo vertice è un nome, e resta un nome.");

        string testo = perNome ? nome! : aggancio is { } a ? CoordinateConverter.ToDottedDms(a.Dove) : Scritta(lat, lon);
        return Fatto(scelta, campo, posizione,
            GestoSuiVertici(scelta.File, scelta.Record, campo, GestoDeiVertici.Cambia, posizione, testo));
    }

    /// <summary>Dove si aggancia un vertice (R5): la coordinata esatta e, se è un punto che il master conosce, il suo nome.</summary>
    private sealed record Aggancio(Coordinate Dove, string? Nome);

    /// <summary>
    /// Il punto del record <paramref name="file"/>#<paramref name="record"/> a cui la mappa ha agganciato il rilascio: il punto
    /// stesso per un fix o un VOR, il vertice più vicino a (<paramref name="lat"/>, <paramref name="lon"/>) per una linea o
    /// un'area. Il nome solo se il catalogo del master scelto lo risolve proprio lì: un nome che Aurora risolverebbe altrove
    /// sposterebbe il vertice in un altro posto.
    /// </summary>
    private Aggancio? AgganciaA(string file, int record, double lat, double lon)
    {
        var forme = Core.PdfAip.AipSullaMappa.EDellAip(file) ? StratoDellAip?.Strato.Forme ?? [] : Strati.SelectMany(s => s.Forme);
        if (forme.FirstOrDefault(f => f.File == file && f.Record == record) is not { } forma)
            return null;
        var punti = forma.Tratti.SelectMany(t => t).ToList();
        if (punti.Count == 0)
            return null;

        // Il più vicino sul piano (la longitudine accorciata dalla latitudine): fra vertici a pochi pixel basta.
        double coseno = Math.Cos(lat * Math.PI / 180);
        var dove = punti.MinBy(p => Math.Pow(p.LatitudeDeg - lat, 2) + Math.Pow((p.LongitudeDeg - lon) * coseno, 2));
        string? nome = forma.Tipo is Core.Mappa.TipoDiForma.Punto && forma.Etichetta.Length > 0
            && CatalogoScelto?.Cerca(forma.Etichetta) is { } conosciuto && conosciuto.Posizione == dove
                ? conosciuto.Nome
                : null;
        return new Aggancio(dove, nome);
    }

    /// <summary>
    /// La maniglia di mezzo lasciata sulla mappa (R3): il gesto «vertice aggiunto» della Scheda, prima di
    /// <paramref name="primaDi"/>, con la coordinata del rilascio. Le maniglie dei due vertici che lo stringono devono
    /// esserci: una maniglia vecchia (la forma è cambiata nel frattempo) si rifiuta.
    /// </summary>
    public bool VerticeAggiuntoDallaMappa(string campo, int primaDi, double lat, double lon, string? suFile = null, int suRecord = 0)
    {
        if (!ModificaSullaMappa || Scelta is not { } scelta)
            return false;

        var maniglie = Maniglie();
        if (maniglie.FirstOrDefault(m => m.Campo == campo && m.Posizione == primaDi - 1) is not { Aggiungibile: true }
            || !maniglie.Any(m => m.Campo == campo && m.Posizione == primaDi))
            return Rifiuta(scelta, campo, primaDi, "Lì non si aggiunge un vertice: la forma è cambiata.");
        if (Fuori(lat, lon))
            return Rifiuta(scelta, campo, primaDi, FuoriDalMondo);

        // R5: il vertice nuovo agganciato prende la coordinata esatta; resta una coordinata (l'elenco la sa scrivere).
        var aggancio = suFile is null ? null : AgganciaA(suFile, suRecord, lat, lon);
        if (suFile is not null && aggancio is null)
            return Rifiuta(scelta, campo, primaDi, "Il punto a cui l'hai agganciato non c'è più sulla mappa.");
        string testo = aggancio is { } a ? CoordinateConverter.ToDottedDms(a.Dove) : Scritta(lat, lon);
        return Fatto(scelta, campo, primaDi,
            GestoSuiVertici(scelta.File, scelta.Record, campo, GestoDeiVertici.Aggiungi, primaDi, testo));
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

    // --- §9, D3: disegnare a clic ----------------------------------------------------------------------------------

    private DisegnoSullaMappa? _disegno;

    /// <summary>Il disegno a clic in corso sul record scelto (R10), o null. Scegliere un altro record lo lascia.</summary>
    public DisegnoSullaMappa? Disegno => _disegno is { } d && (d.File, d.Record) == Scelta ? d : null;

    /// <summary>
    /// Comincia a disegnare l'elenco <paramref name="campo"/> del record scelto (R10): i clic sulla mappa fanno i punti, e
    /// alla fine (<see cref="FormaDisegnata"/>) prendono il posto dei vertici. Negli elenchi fatti solo di nomi si clicca
    /// solo sui punti col nome (D7). Le maniglie si tolgono finché si disegna.
    /// </summary>
    public void DisegnaSullaMappa(string campo, bool chiudi)
    {
        if (Scelta is not { } scelta)
            return;
        bool linea = campo == ChiaveDellaLinea;
        var elenco = linea ? null : ElenchiDiVerticiDi(scelta.File, scelta.Record).FirstOrDefault(e => e.Chiave == campo);
        if (!linea && elenco is null)
            return;
        bool soloNomi = elenco is { AmmetteCoordinate: false };
        _disegno = new DisegnoSullaMappa(scelta.File, scelta.Record, campo, chiudi, soloNomi);
        Registro.Scrivi("disegno", $"{scelta.File}#{scelta.Record} {campo}: comincia");
        Avvisa();
    }

    /// <summary>La casella «Chiudi la forma» cambiata mentre si disegna (R12): l'anteprima sulla mappa la segue.</summary>
    public void ChiudiIlDisegno(bool chiudi)
    {
        if (Disegno is not { } d || d.Chiudi == chiudi)
            return;
        _disegno = d with { Chiudi = chiudi };
        Avvisa();
    }

    /// <summary>Esc, o «Lascia stare»: il disegno finisce senza cambiare niente.</summary>
    public void LasciaIlDisegno()
    {
        if (_disegno is null)
            return;
        _disegno = null;
        Avvisa();
    }

    /// <summary>
    /// Invio o doppio clic (R11): i punti cliccati diventano i vertici dell'elenco, col gesto «forma disegnata» — un
    /// annulla, le copie uguali, il diff. Un punto agganciato a un punto col nome, in un elenco che sa scrivere i nomi,
    /// diventa quel nome (D5); gli altri la coordinata esatta del punto agganciato, o del clic. Rifiutato, il disegno
    /// resta aperto coi suoi punti (si toglie l'ultimo con Backspace e si riprova).
    /// </summary>
    public bool FormaDisegnata(IReadOnlyList<PuntoCliccato> punti)
    {
        if (Disegno is not { } d)
            return false;
        ArgumentNullException.ThrowIfNull(punti);

        bool nomi = d.Campo != ChiaveDellaLinea
            && ElenchiDiVerticiDi(d.File, d.Record).FirstOrDefault(e => e.Chiave == d.Campo) is { AmmetteNomi: true };
        var righe = new List<string>();
        foreach (var punto in punti)
        {
            if (Fuori(punto.Lat, punto.Lon))
                return Rifiuta((d.File, d.Record), d.Campo, -1, FuoriDalMondo);
            var aggancio = punto.SuFile is null ? null : AgganciaA(punto.SuFile, punto.SuRecord, punto.Lat, punto.Lon);
            righe.Add(nomi && aggancio?.Nome is { } nome ? nome
                : aggancio is { } a ? CoordinateConverter.ToDottedDms(a.Dove)
                : Scritta(punto.Lat, punto.Lon));
        }

        bool fatto = GestoSuiVertici(d.File, d.Record, d.Campo, GestoDeiVertici.Disegna, testo: string.Join('\n', righe), chiudi: d.Chiudi);
        if (fatto)
            _disegno = null;
        // Nessun vertice da segnare (-1): è cambiato tutto l'elenco. Il rifiuto va accanto all'elenco, come gli altri.
        return Fatto((d.File, d.Record), d.Campo, -1, fatto);
    }

    /// <summary>Il testo di un disegno: un punto per riga.</summary>
    private static IReadOnlyList<string> RigheDelDisegno(string? testo)
        => (testo ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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
