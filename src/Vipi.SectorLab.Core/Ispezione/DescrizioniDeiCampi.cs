using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Core.Ispezione;

/// <summary>Come si scrive un campo nella scheda (carta del lotto «Subito» §3, slice 3).</summary>
public enum Editor
{
    /// <summary>Testo libero.</summary>
    Testo,

    /// <summary>Un valore da un elenco chiuso, ognuno col suo significato (<see cref="DescrizioneDelCampo.Valori"/>).</summary>
    TipoFisso,

    /// <summary>Latitudine e longitudine, in una delle forme del sector.</summary>
    Coordinate,

    /// <summary>Coordinate, o il nome di un fix/VOR/NDB coi suggerimenti (scelto un nome si scrive <c>NOME;NOME;</c>).</summary>
    Punto,

    /// <summary>Il nome di un fix, VOR o NDB, coi suggerimenti.</summary>
    Navaid,

    /// <summary>Una quota: <c>FL80</c>, <c>2500ft</c>, o il numero nell'unità del campo.</summary>
    Quota,

    /// <summary>Un numero (frequenza, gradi, carattere).</summary>
    Numero,

    /// <summary>Uno o più valori presi dal sector (<see cref="DescrizioneDelCampo.Fonte"/>).</summary>
    Elenco,

    /// <summary>
    /// Un colore di <c>[FILLCOLOR]</c>: un nome di <c>colors.def</c> del master, o un valore nelle forme del manuale
    /// (<c>#RRGGBB</c>, <c>#AARRGGBB</c>, <c>R,G,B</c>, <c>%R:G:B</c>). Col selettore della slice 4; i
    /// <see cref="DescrizioneDelCampo.Valori"/>, se ci sono, danno il significato dei nomi (e il tipo del nuovo, 3e).
    /// </summary>
    Colore,

    /// <summary>Sì o no.</summary>
    SiNo,

    /// <summary>Si legge e basta: calcolato dal motore, o un elenco che ha la sua sezione sotto (i vertici).</summary>
    SolaLettura,
}

/// <summary>Da dove vengono le voci di un campo <see cref="Editor.Elenco"/>.</summary>
public enum FonteDellElenco
{
    /// <summary>Gli scali dell'<c>.ap</c>.</summary>
    Scali,

    /// <summary>Le piste dello scalo nel <c>.rw</c> (più piste si scrivono con <c>:</c>).</summary>
    Piste,

    /// <summary>Le posizioni ATC dei <c>.frq</c>.</summary>
    Posizioni,

    /// <summary>Le attese in rotta di <c>[HOLDENR]</c> (slice 10a).</summary>
    Attese,

    /// <summary>I profili <c>.cpr</c> dell'albero, scritti come li cita un <c>.frq</c> (<c>PREFS\TWR.cpr</c>, slice 11a).</summary>
    Profili,

    /// <summary>I modelli <c>.atis</c> dell'albero (slice 11a).</summary>
    Atis,

    /// <summary>I modelli <c>.datis</c> dell'albero (slice 11a).</summary>
    Datis,

    /// <summary>I file <c>.loa</c> dell'albero (slice 11a; sul fork non ce n'è nessuno).</summary>
    Loa,
}

/// <summary>Un valore di un campo a tipo fisso, col suo significato («3 · nascosto»).</summary>
/// <param name="Valore">Come lo scrive e lo rilegge la modifica: il testo del campo, o il nome dell'enum del motore.</param>
public sealed record ValoreFisso(string Valore, string Significato)
{
    /// <summary>La voce dell'elenco a schermo: il valore e il suo significato, il vuoto come «— non scritto».</summary>
    public string Voce { get; init; } = Valore.Length == 0 ? "— " + Significato
        : Significato == Valore ? Valore
        : $"{Valore} · {Significato}";
}

/// <summary>Un campo del record come lo spiega la scheda: nome italiano, significato, come si scrive.</summary>
/// <param name="Proprieta">Il nome della proprietà nel modello del motore (è la chiave, e resta nei <c>data-</c>).</param>
/// <param name="Nome">Il nome che legge l'AOD.</param>
/// <param name="Significato">Cosa vuol dire, e dove sta nella riga del file.</param>
public sealed record DescrizioneDelCampo(string Proprieta, string Nome, string Significato, Editor Editor)
{
    /// <summary>Per <see cref="Editor.TipoFisso"/>: i valori ammessi, nell'ordine del manuale.</summary>
    public IReadOnlyList<ValoreFisso> Valori { get; init; } = [];

    /// <summary>Per <see cref="Editor.Elenco"/>: da dove vengono le voci.</summary>
    public FonteDellElenco? Fonte { get; init; }

    /// <summary>Per <see cref="Editor.Quota"/>: il campo scrive le centinaia di piedi (<c>25</c> = 2 500 ft).</summary>
    public bool InCentinaia { get; init; }

    /// <summary>
    /// Quando il campo si scrive, se dipende dal record (slice 3c): il testo di un'etichetta L arriva nella riga solo
    /// se l'etichetta mostra un testo scelto. Null = sempre. La scheda non offre mai una scrittura che non scrive.
    /// </summary>
    public Func<object, bool>? SiScriveSe { get; init; }

    /// <summary>Il perché, quando <see cref="SiScriveSe"/> dice di no.</summary>
    public string? PercheNo { get; init; }

    /// <summary>Vero se, per QUEL record, la scheda fa scrivere il campo.</summary>
    public bool SiScriveIn(object record) => Editor != Editor.SolaLettura && (SiScriveSe?.Invoke(record) ?? true);
}

/// <summary>Un tipo di record descritto: il suo nome per l'AOD e i campi nell'ordine della riga.</summary>
public sealed record DescrizioneDelTipo(string Nome, IReadOnlyList<DescrizioneDelCampo> Campi);

/// <summary>
/// Le descrizioni dei campi, una per tipo di record (lotto «Subito», slice 3; voci A1, A2, H2, J2, E4 della carta
/// «file per file»). La scheda generica leggeva i campi per riflessione e li chiamava col nome della proprietà
/// (<c>ExtraField5</c>, <c>DefaultVisible</c>): qui ognuno ha il nome dell'AOD, il significato dal manuale IVAO e
/// l'editor giusto. La riflessione resta la riserva: una proprietà che non è qui si vede lo stesso, come «campo
/// sconosciuto» — e un test sui tipi del motore dice quando ne compare una nuova.
/// <para>🔴 I significati vengono dal manuale (carta «file per file», §1-§22), non da vIPI. Dove il manuale dà solo
/// i valori (la visibilità 0/1 dei VOR) il significato non si inventa.</para>
/// </summary>
public static class DescrizioniDeiCampi
{
    /// <summary>
    /// Le proprietà del motore che non sono campi della riga: da dove viene il record (la scheda lo dice già in testa,
    /// con le righe) e il segno di un conflitto fra copie, che è del motore.
    /// </summary>
    public static IReadOnlySet<string> Nascoste { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "Source", "Sources", "HasConflict", "TipoNonScritto",
    };

    /// <summary>La descrizione di un record in QUEL file (le MVA di ACC e di scalo leggono la quota da campi diversi).</summary>
    public static DescrizioneDelTipo? Di(object record, string? relativo = null)
    {
        ArgumentNullException.ThrowIfNull(record);
        bool diAcc = relativo is not null && relativo.Contains("/ENRMVA/", StringComparison.OrdinalIgnoreCase);
        return record switch
        {
            MvaSector => diAcc ? MvaDiAcc : MvaDiScalo,
            StrRecord { RunwaySpec: var piste } voce when EUnaMappa(piste) => MappeDelMaps[voce.GetType()],
            _ => PerTipo.GetValueOrDefault(record.GetType()),
        };
    }

    /// <summary>Tutti i tipi descritti, per i test: ogni tipo di record del motore deve esserci.</summary>
    public static IEnumerable<(Type Tipo, DescrizioneDelTipo Descrizione)> Tutte()
    {
        foreach (var (tipo, descrizione) in PerTipo)
            yield return (tipo, descrizione);
        yield return (typeof(MvaSector), MvaDiAcc);
        yield return (typeof(MvaSector), MvaDiScalo);
        foreach (var (tipo, descrizione) in MappeDelMaps)
            yield return (tipo, descrizione);
    }

    // --- i valori a tipo fisso, dal manuale ----------------------------------------------------------------------

    private static readonly ValoreFisso Vuoto = new("", "non scritto");

    private static IReadOnlyList<ValoreFisso> Valori(params (string Valore, string Significato)[] valori)
        => [.. valori.Select(v => new ValoreFisso(v.Valore, v.Significato))];

    /// <summary>I valori di un enum del motore: si scrivono col nome (Hidden), si leggono col numero del file (1).</summary>
    private static IReadOnlyList<ValoreFisso> DiEnum(params (string Nome, string NelFile, string Significato)[] valori)
        => [.. valori.Select(v => new ValoreFisso(v.Nome, v.Significato) { Voce = $"{v.NelFile} · {v.Significato}" })];

    private static readonly IReadOnlyList<ValoreFisso> TipiDelFix = Valori(
        ("0", "in rotta (ENR)"), ("1", "terminale (TERM)"), ("2", "in rotta e terminale"), ("3", "nascosto"));

    /// <summary>I tipi delle voci degli <c>.str</c> su una pista: i tasti della finestra delle procedure di Aurora.</summary>
    private static readonly IReadOnlyList<ValoreFisso> TipiDelloStr = DiEnum(
        (nameof(StrRecordType.Star), "0", "STAR"), (nameof(StrRecordType.Transition), "1", "transizione (TRANS)"),
        (nameof(StrRecordType.Holding), "2", "attesa (HOLD)"), (nameof(StrRecordType.Iap), "3", "avvicinamento (IAP)"),
        (nameof(StrRecordType.Fap), "4", "FAP"), (nameof(StrRecordType.GoAround), "5", "mancato avvicinamento (GA)"));

    private static readonly IReadOnlyList<ValoreFisso> ZeroOUno = [Vuoto, .. Valori(("0", "0"), ("1", "1"))];

    /// <summary>La visibilità di VOR e NDB (manuale IVAO: 0 Show, 1 Hide).</summary>
    private static readonly IReadOnlyList<ValoreFisso> Visibilita = [Vuoto, .. Valori(("0", "mostrato"), ("1", "nascosto"))];

    /// <summary>Un file citato da un .frq (slice 11a, M1, N4): scelto fra quelli che ci sono, e da lì si apre.</summary>
    private static DescrizioneDelCampo FileCitato(string proprieta, string nome, string significato, FonteDellElenco fonte)
        => C(proprieta, nome, significato, Editor.Elenco) with { Fonte = fonte };

    /// <summary>Una parte dell'info di un'attesa (slice 10c): si scrive solo se l'info ha la forma FIX/rotta+virata-quota.</summary>
    private static DescrizioneDelCampo ParteDellInfo(DescrizioneDelCampo campo) => campo with
    {
        SiScriveSe = r => r is Attesa { Fix: not null },
        PercheNo = "L'info non ha la forma FIX/rotta+virata-quota (ABBOZ/225R-9000): si scrive tutta nel campo Info.",
    };

    /// <summary>L'attesa in rotta di un fix, VOR o NDB (slice 10a): il nome coi suggerimenti delle attese di HOLDENR.</summary>
    private static DescrizioneDelCampo AttesaDelNavaid(string campo)
        => C("NomeDellAttesa", "Attesa", $"Il nome della sua attesa in rotta in HOLDENR.hold ({campo} campo), uguale a quello dell'attesa.",
            Editor.Elenco) with { Fonte = FonteDellElenco.Attese };

    /// <summary>
    /// I tipi dei <c>.geo</c> (H2): quelli che il fork usa, con le linee di scalo, la costa di <c>itgeo.geo</c> e le
    /// aree P/R/D. Un valore fuori elenco (o vuoto: 10 in <c>liap.geo</c>) resta com'è e si vede; l'avviso è della 12.
    /// </summary>
    private static readonly IReadOnlyList<ValoreFisso> TipiDelGeo = Valori(
        ("RUNWAY", "pista"), ("TAXIWAY", "bordo della taxiway"), ("TAXI_CENTER", "asse della taxiway"),
        ("APRON", "piazzale"), ("BUILDING", "edificio"), ("PIER", "molo d'imbarco"), ("STOPBAR", "stop bar"),
        ("STOPLINE", "linea d'arresto"), ("COAST", "costa"), ("DANGER", "area pericolosa (D)"),
        ("PROHIBIT", "area proibita (P)"), ("RESTRICT", "area regolamentata (R)"));

    /// <summary>I riempimenti dei <c>.pol</c> (nomi di <c>colors.def</c>): HOLE buca un'area già riempita.</summary>
    private static readonly IReadOnlyList<ValoreFisso> TipiDelPol = Valori(
        ("GRASS", "erba"), ("HOLE", "buco (toglie il riempimento sotto)"), ("RUNWAY", "pista"), ("TAXIWAY", "taxiway"),
        ("APRON", "piazzale"), ("CONCRETE", "cemento"), ("BUILDING", "edificio"));

    // --- i tipi ----------------------------------------------------------------------------------------------------

    private static DescrizioneDelCampo C(string proprieta, string nome, string significato, Editor editor = Editor.Testo)
        => new(proprieta, nome, significato, editor);

    private static DescrizioneDelCampo Scalo(string proprieta = "IcaoCode")
        => C(proprieta, "Scalo", "Il codice ICAO dello scalo.", Editor.Elenco) with { Fonte = FonteDellElenco.Scali };

    private static DescrizioneDelCampo Posizione(string proprieta = "Position", string nome = "Posizione", string cosa = "Latitudine e longitudine.")
        => C(proprieta, nome, cosa, Editor.Coordinate);

    private static readonly DescrizioneDelCampo Commentato = C("IsDisabled", "Commentato",
        "La riga c'è ma comincia con //: Aurora non la legge.", Editor.SiNo);

    private static DescrizioneDelCampo Vertici(string proprieta, string nome = "Vertici")
        => C(proprieta, nome, "I punti, uno per riga: si scrivono nella loro sezione qui sotto, e si vedono sulla mappa.", Editor.SolaLettura);

    private static readonly DescrizioneDelTipo MvaDiAcc = new("Zona MVA di ACC",
    [
        C("Nome", "Nome", "Il 2° campo della prima riga (lo scalo o il gruppo, LIMM): è il nome col quale il blocco si aggancia ai tag.", Editor.SolaLettura),
        C("AltLabel", "Quota", "La quota minima scritta sull'etichetta (5° campo della riga L), in centinaia di piedi: 25 = 2 500 ft.", Editor.Quota) with { InCentinaia = true },
        C("LabelSize", "Carattere", "La grandezza del testo dell'etichetta (6° campo della riga L).", Editor.Numero),
        C("LabelAnchors", "Etichette", "Dove sta la scritta della quota: le righe L della zona.", Editor.SolaLettura),
        Vertici("Vertices"),
    ]);

    private static readonly DescrizioneDelTipo MvaDiScalo = new("Zona MVA di scalo",
    [
        C("Nome", "Nome", "Il 2° campo della prima riga: è il nome col quale il blocco si aggancia ai tag.", Editor.SolaLettura),
        // 🔴 Slice 3b: il motore legge le MVA di scalo col 2° campo della L come quota e lo RISCRIVE anche nel 5°; sul fork
        // 194 etichette su 226 hanno invece il gruppo nel 2° (BB CS0) e la quota nel 5° (45), come le ACC. Cambiare
        // la quota o il carattere cancellerebbe la quota vera: si leggono e basta finché la slice 15 (S1-S2) non legge
        // queste MVA come quelle di ACC.
        C("AltLabel", "Etichetta", "Il 2° campo della riga L. Sul fork quasi sempre il gruppo (BB CS0), con la quota nel 5° campo: si scriverà con la slice 15, quando il Lab leggerà le MVA di scalo come quelle di ACC.", Editor.SolaLettura),
        C("LabelSize", "Carattere", "La grandezza del testo dell'etichetta (6° campo della riga L); si scriverà con la slice 15, come l'etichetta.", Editor.SolaLettura),
        C("LabelAnchors", "Etichette", "Dove sta la scritta: le righe L della zona.", Editor.SolaLettura),
        Vertici("Vertices"),
    ]);

    private static readonly IReadOnlyDictionary<Type, DescrizioneDelTipo> PerTipo = new Dictionary<Type, DescrizioneDelTipo>
    {
        // NAVAIDS (§12): Nome;Lat;Lon;Tipo;Confine;[Attesa] — l'attesa (6° campo; 8° di VOR e NDB) dalla slice 10a.
        [typeof(Fix)] = new("Fix",
        [
            C("Name", "Nome", "Il nome del fix (al massimo 5 lettere), come lo citano procedure e rotte."),
            Posizione(),
            C("DisplayType", "Tipo", "Con quale filtro lo mostra Aurora (4° campo).", Editor.TipoFisso) with { Valori = [Vuoto, .. TipiDelFix] },
            C("ExtraField", "Confine", "Fix di confine, 0 o 1 (5° campo).", Editor.TipoFisso) with { Valori = ZeroOUno },
            AttesaDelNavaid("6°"),
        ]),
        [typeof(Vor)] = new("VOR",
        [
            C("Ident", "Nome", "Il nome del VOR (al massimo 3 lettere)."),
            C("Frequency", "Frequenza", "In MHz (2° campo).", Editor.Numero),
            Posizione(),
            C("ExtraField5", "Visibilità", "5° campo: 0 lo mostra, 1 lo nasconde (manuale IVAO).", Editor.TipoFisso) with { Valori = Visibilita },
            C("ExtraField6", "Tipo", "6° campo.", Editor.TipoFisso) with
            {
                Valori = [Vuoto, .. Valori(("0", "VOR"), ("1", "VOR/DME"), ("2", "VORTAC"), ("3", "TACAN"), ("4", "DME"))],
            },
            C("CanaleTacan", "Canale TACAN", "7° campo, per VORTAC e TACAN: il canale (54Y)."),
            AttesaDelNavaid("8°"),
        ]),
        [typeof(Ndb)] = new("NDB",
        [
            C("Ident", "Nome", "Il nome dell'NDB."),
            C("Frequency", "Frequenza", "In kHz (2° campo).", Editor.Numero),
            Posizione(),
            C("Visibilita", "Visibilità", "5° campo: 0 lo mostra, 1 lo nasconde (manuale IVAO).", Editor.TipoFisso) with { Valori = Visibilita },
            C("ExtraField6", "6° campo", "Il manuale IVAO non lo descrive (nel suo esempio è vuoto): si tiene com'è.", Editor.SolaLettura),
            C("ExtraField7", "7° campo", "Il manuale IVAO non lo descrive (nel suo esempio è vuoto): si tiene com'è.", Editor.SolaLettura),
            AttesaDelNavaid("8°"),
        ]),
        // HOLDENR.hold (§20): NOME;Lat;Lon;[Info] — l'info in forma fissa FIX/rotta+virata-quota.
        [typeof(Attesa)] = new("Attesa in rotta",
        [
            C("Nome", "Nome", "Uguale a quello scritto nel 6° campo del fix (8° di VOR e NDB) che la usa."),
            C("Posizione", "Punto", "Coordinate o il nome del fix.", Editor.Punto),
            C("Descrizione", "Info", "Il testo mostrato, FIX/rotta+virata-quota (ABBOZ/225R-9000)."),
            // Slice 10c (U1): le parti dell'info si scrivono una alla volta, se l'info ha la forma solita.
            ParteDellInfo(C("Fix", "Fix", "Il fix dell'info, prima della «/».", Editor.Navaid)),
            ParteDellInfo(C("Rotta", "Rotta di avvicinamento", "Dall'info, in gradi (da 1 a 360, si scrive con tre cifre).", Editor.Numero)),
            ParteDellInfo(C("Verso", "Virata", "Dall'info: L a sinistra, R a destra.", Editor.TipoFisso) with
            {
                Valori = Valori(("L", "a sinistra"), ("R", "a destra")),
            }),
            ParteDellInfo(C("Quota", "Quota minima", "Dall'info: in piedi (9000) o livello di volo (FL105).")),
        ]),
        // ACC, HI_AIRSPACE, LOW_AIRSPACE (§1, §10, §11): T/L;Identificativo;Lat;Lon;[Font].
        [typeof(LabelPoint)] = new("Etichetta (L)",
        [
            C("Mode", "Cosa mostra", "Cosa c'è nel 2° campo.", Editor.TipoFisso) with
            {
                Valori =
                [
                    new ValoreFisso(nameof(LabelMode.FixName), "il nome del fix") { Voce = "il nome del fix" },
                    new ValoreFisso(nameof(LabelMode.Custom), "un testo scelto") { Voce = "un testo scelto" },
                    new ValoreFisso(nameof(LabelMode.None), "niente (campo vuoto)") { Voce = "niente (campo vuoto)" },
                ],
            },
            C("FixRef", "Nome", "Il nome del fix mostrato (2° campo, quando mostra il nome del fix).", Editor.Navaid) with
            {
                SiScriveSe = r => r is LabelPoint { Mode: LabelMode.FixName },
                PercheNo = "Conta solo quando l'etichetta mostra il nome del fix: prima cambia «Cosa mostra».",
            },
            C("CustomName", "Testo", "Il testo mostrato (2° campo, quando mostra un testo scelto).") with
            {
                SiScriveSe = r => r is LabelPoint { Mode: LabelMode.Custom },
                PercheNo = "Conta solo quando l'etichetta mostra un testo scelto: prima cambia «Cosa mostra».",
            },
            Posizione(cosa: "Dove sta la scritta."),
            C("FontSize", "Carattere", "La grandezza del testo, facoltativa (oggi 8 ovunque).", Editor.Numero),
        ]),
        [typeof(StaticBoundaryGroup)] = new("Traccia (T)",
        [
            C("Name", "Nome", "Il gruppo (2° campo di ogni riga T): è la voce della finestra di selezione di Aurora (FRA BDRY, RR CONF2)."),
            Vertici("Polygons", "Poligoni"),
        ]),
        // AIRWAY (§2): Tipo;Aerovia;Lat;Lon.
        [typeof(Airway)] = new("Aerovia",
        [
            C("Name", "Nome", "Il nome dell'aerovia (2° campo)."),
            Vertici("FixLabels", "Tracciato"),
            Vertici("Coordinates", "Coordinate"),
        ]),
        // DYNAMIC_SEC e GCI (§5): Posizioni;Riempimento;Bordo;ColoreBordo;[Opacità];[Filtro], poi i vertici.
        [typeof(TflSector)] = SettoreDinamico("Settore dinamico", []),
        [typeof(FicSector)] = SettoreDinamico("Settore della FIC",
        [
            C("ShapeLabel", "Nome della forma", "Dal commento sopra il blocco.", Editor.SolaLettura),
            C("IsFssPerimeter", "Perimetro FSS", "Calcolato: posizione …FSS con riempimento CTR.", Editor.SolaLettura),
        ]),
        // GND_LAYOUT (§9): STATIC;Riempimento;Bordo;ColoreBordo, poi i vertici.
        [typeof(Polygon)] = new("Poligono di terra",
        [
            C("FillColor", "Riempimento", "Il tipo di superficie (2° campo): un nome di colors.def, o un colore.", Editor.Colore) with { Valori = TipiDelPol },
            C("LineWeight", "Spessore del bordo", "3° campo.", Editor.Numero),
            C("LineColor", "Bordo", "Il colore del bordo (4° campo): un nome di colors.def, o un colore.", Editor.Colore) with { Valori = TipiDelPol },
            Vertici("Vertices"),
        ]),
        // GEO (§8, §8-bis, §15): LatInizio;LonInizio;LatFine;LonFine;Tipo;[Nome dell'area].
        [typeof(Line)] = new("Segmento",
        [
            Posizione("Start", "Inizio", "Il primo punto del segmento."),
            Posizione("End", "Fine", "Il secondo punto del segmento."),
            C("Color", "Tipo", "Cosa disegna (5° campo): lo strato e il colore in Aurora.", Editor.TipoFisso) with { Valori = [Vuoto, .. TipiDelGeo] },
            C("Nome", "Nome dell'area", "Solo nelle aree P/R/D (6° campo: D5A, R10A)."),
        ]),
        // OTHER (§13).
        [typeof(AirportInfo)] = new("Scalo",
        [
            C("IcaoCode", "Scalo", "Il codice ICAO."),
            C("ElevationFt", "Elevazione", "In piedi (2° campo).", Editor.Quota),
            C("TransitionAltFt", "Altitudine di transizione", "In piedi (3° campo).", Editor.Quota),
            Posizione("Centre"),
            C("Name", "Nome", "Il nome dello scalo, al massimo 50 caratteri (6° campo)."),
            C("HideTag", "Nascosto", "7° campo.", Editor.TipoFisso) with
            {
                Valori = [Vuoto, .. DiEnum((nameof(HideTag.Hidden), "1", "nascosto"), (nameof(HideTag.Shown), "2", "mostrato"))],
            },
            C("InstallationType", "Tipo", "8° campo.", Editor.TipoFisso) with
            {
                Valori = DiEnum((nameof(InstallationType.Airport), "0", "aeroporto"), (nameof(InstallationType.Helipad), "1", "eliporto"),
                                (nameof(InstallationType.Military), "2", "militare"), (nameof(InstallationType.Private), "3", "privato"),
                                (nameof(InstallationType.Uncontrolled), "4", "non controllato"),
                                (nameof(InstallationType.Custom), "?", "un valore che il manuale non ha (scritto sotto)")),
            },
            C("CustomInstallationTypeText", "Tipo (come è scritto)", "L'8° campo quando non è uno dei tipi del manuale.", Editor.SolaLettura),
            Commentato,
        ]),
        [typeof(AtcPosition)] = new("Posizione ATC",
        [
            C("Code", "Posizione", "Il nominativo della posizione (LIRR_NE_CTR)."),
            C("FrequencyMhz", "Frequenza", "In MHz (2° campo).", Editor.Numero),
            C("TransferList", "Trasferimenti (come sono scritti)", "3° campo: le posizioni o gli ICAO a cui passa il traffico, gli esclusi col «-».", Editor.SolaLettura),
            // Slice 11a (M1): le due liste; scriverne una rimette prima gli inclusi e poi gli esclusi, come vuole il manuale.
            C("Inclusi", "Trasferimenti: inclusi", "Posizioni (LIRR_NE_CTR) o ICAO (LIRF, tutte le sue posizioni), separate da uno spazio. Nella riga vengono sempre prima degli esclusi: un incluso dopo un escluso Aurora non lo legge."),
            C("Esclusi", "Trasferimenti: esclusi", "Le posizioni escluse, separate da uno spazio (il «-» lo scrive il Lab)."),
            FileCitato("Profile", "Profilo", "Il file .cpr della posizione (4° campo): le sue impostazioni sovrascrivono quelle dell'utente a ogni connessione.", FonteDellElenco.Profili),
            FileCitato("AtisFile", "ATIS", "Il modello .atis (5° campo).", FonteDellElenco.Atis),
            C("BlockCpdlc", "CPDLC bloccato", "6° campo: 1 = il CPDLC non si usa su questa posizione.", Editor.SiNo),
            FileCitato("Loa", "LOA", "Il file .loa (7° campo): i livelli di trasferimento per punto e settore (manuale IVAO, «LOA & XFL»).", FonteDellElenco.Loa),
            FileCitato("DatisFile", "D-ATIS", "Il modello .datis (8° campo).", FonteDellElenco.Datis),
        ]),
        // CPDLC (§13, M5; lotto «Subito» slice 11c): i messaggi e i nomi dei gruppi della finestra CPDLC di Aurora.
        [typeof(MessaggioCpdlc)] = new("Messaggio CPDLC",
        [
            C("Comando", "Messaggio", "Il testo (al più 128 caratteri); [0]…[3] sono i valori che Aurora riempie dalla strip."),
            C("Risposta", "Risposta", "Quella che il pilota deve dare (2° campo).", Editor.TipoFisso) with
            {
                Valori = [Vuoto, .. Valori(("WU", "WILCO o UNABLE"), ("AN", "AFFIRMATIVE o NEGATIVE"), ("R", "ROGER"), ("NE", "nessuna ora, si aspetta un report"))],
            },
            C("Gruppo", "Gruppo", "Il gruppo della finestra CPDLC (3° campo): da 0 a 18, 20 per il DCL. I nomi dei gruppi stanno nel .cpdlcnames.", Editor.TipoFisso) with
            {
                Valori = [.. Enumerable.Range(0, 19).Select(n => new ValoreFisso(n.ToString(System.Globalization.CultureInfo.InvariantCulture), $"gruppo {n}")), new ValoreFisso("20", "DCL")],
            },
            C("Valori", "Valori (uLink)", "I tredici campi dei valori (ReplVal, AtVal, TpVal, TotVal): li prepara uLink, il Lab li tiene come sono.", Editor.SolaLettura),
            C("TotaleDeiValori", "Valori dichiarati", "TotVal, l'ultimo dei campi dei valori: devono essere tanti quanti i [n] del messaggio.", Editor.SolaLettura),
        ]),
        [typeof(NomeDelGruppoCpdlc)] = new("Nome di un gruppo CPDLC",
        [
            C("Gruppo", "Gruppo", "Il numero del gruppo dopo GROUP. (da 0 a 18).", Editor.TipoFisso) with
            {
                Valori = [.. Enumerable.Range(0, 19).Select(n => new ValoreFisso(n.ToString(System.Globalization.CultureInfo.InvariantCulture), $"gruppo {n}"))],
            },
            C("Nome", "Nome", "Il nome del tasto nella finestra CPDLC, al più 20 caratteri."),
        ]),
        [typeof(Runway)] = new("Pista",
        [
            Scalo(),
            C("Designator1", "Pista", "Il verso primario (2° campo, 01-18). Nelle voci MAPS e di settore è il nome della voce di menu."),
            C("Designator2", "Pista opposta", "Il verso opposto (3° campo, 19-36)."),
            C("ElevThresh1Ft", "Elevazione della soglia", "Del verso primario, in piedi (4° campo).", Editor.Quota),
            C("ElevThresh2Ft", "Elevazione della soglia opposta", "In piedi (5° campo).", Editor.Quota),
            C("TrueHeading1", "Rotta", "Del verso primario, in gradi (6° campo).", Editor.Numero),
            C("TrueHeading2", "Rotta opposta", "In gradi (7° campo).", Editor.Numero),
            Posizione("Threshold1", "Soglia", "La soglia del verso primario."),
            Posizione("Threshold2", "Soglia opposta", "La soglia del verso opposto."),
            // Slice 11b (M4): accanto alla rotta scritta, quella che dicono le soglie; molto diverse = soglie invertite.
            C("RottaVeraDalleSoglie", "Rotta dalle soglie", "Calcolata dalla soglia del verso primario a quella opposta, in gradi VERI: la rotta scritta è magnetica, e in Italia viene 2-5° meno. Molto diverse vuol dire soglie invertite (avviso RottaDiversaDalleSoglie).", Editor.SolaLettura),
        ]),
        // File di scalo (§16-§19).
        [typeof(SidProcedure)] = new("SID",
        [
            Scalo(),
            C("Runway", "Piste", "Le piste della SID, più d'una separate da : (14L:14R).", Editor.Elenco) with { Fonte = FonteDellElenco.Piste },
            C("Name", "Nome", "Il nome della SID; con la transizione nel nome composto (SOS5A-ESI8H)."),
            C("Field4", "Latitudine dell'etichetta", "4° campo: nei file italiani vuoto o uno spazio."),
            C("Field5", "Longitudine dell'etichetta", "5° campo: nei file italiani vuoto o uno spazio."),
            C("DefaultVisible", "Tipo", "6° campo. Convenzione italiana: SID e transizione in un nome composto, col tipo 0 e il navaid nel 7° campo.", Editor.TipoFisso) with
            {
                Valori = [Vuoto, .. Valori(("0", "SID"), ("1", "transizione"))],
            },
            C("RelatedFix", "Navaid della transizione", "7° campo.", Editor.Navaid),
            C("IsRnav", "RNAV", "8° campo: 1 = RNAV.", Editor.SiNo),
            Vertici("Track", "Tracciato"),
        ]),
        [typeof(GeometricStrRecord)] = Procedura("Mappa (coordinate)", Vertici("Segments", "Tratti")),
        [typeof(ProcedureStrRecord)] = Procedura("Procedura (punti per nome)", Vertici("Waypoints", "Punti")),
        [typeof(HoldingStrRecord)] = Procedura("Attesa di scalo", Vertici("Points", "Punti")),
        [typeof(Stand)] = new("Stand",
        [
            C("Number", "Nome", "Il nome dello stand, al massimo 20 caratteri."),
            Scalo(),
            Posizione(cosa: "Lo stop point dello stand."),
            Commentato,
        ]),
        [typeof(TaxiwayLabel)] = new("Etichetta di taxiway",
        [
            C("Name", "Nome", "Il nome della taxiway."),
            Scalo(),
            Posizione(cosa: "Dove sta la scritta."),
        ]),
        [typeof(VfrPoint)] = new("Punto VFR",
        [
            C("Name", "Nome", "Il nome del punto, anche con spazi (PONTE GALERIA)."),
            C("Code", "Codice", "2° campo: per il manuale la quota, nei file italiani il codice (MMN1), mostrato sotto il nome."),
            Posizione(),
            C("Type", "Tipo", "5° campo.", Editor.TipoFisso) with
            {
                Valori = [Vuoto, .. Valori(("0", "obbligatorio"), ("1", "VFR"), ("2", "elicotteri"), ("3", "area"))],
            },
        ]),
        [typeof(RottaVfr)] = new("Rotta VFR",
        [
            C("Numero", "Numero", "Il numero della rotta (1° campo di ogni riga)."),
            Vertici("Punti", "Punti"),
        ]),
        [typeof(AtisData)] = new("Modello ATIS",
        [
            C("Template", "Modello", "Il testo, coi segnaposto."),
        ]),
    };

    private static DescrizioneDelTipo SettoreDinamico(string nome, IReadOnlyList<DescrizioneDelCampo> inPiu) => new(nome,
    [
        C("SectorCode", "Posizioni", "Le posizioni che lo accendono (1° campo, separate da spazio o da :): il settore si vede solo se una è collegata; Static = sempre.", Editor.Elenco) with
        {
            Fonte = FonteDellElenco.Posizioni,
        },
        C("FillColor", "Riempimento", "2° campo: un nome di colors.def o un colore. Con l'opacità a 1 non si riempie (convenzione italiana: si vede il bordo).", Editor.Colore),
        C("LineWeight", "Spessore del bordo", "3° campo.", Editor.Numero),
        C("StrokeColor", "Colore del bordo", "4° campo: un nome di colors.def o un colore.", Editor.Colore),
        C("Flags", "Opacità", "5° campo, 0 o 1.", Editor.TipoFisso) with { Valori = Valori(("0", "0"), ("1", "1")) },
        C("Type", "Tipo di settore", "Calcolato dal riempimento (CTR, APP, TMA…).", Editor.SolaLettura),
        .. inPiu,
        Vertici("Vertices"),
    ]);

    /// <summary>
    /// Le voci del <c>MAPS</c> (Q1): mappe del menu generale, non procedure. Il 6° campo è lo stesso, ma lì sceglie il
    /// TASTO della finestra delle procedure che accende la mappa (CTR → TRANS, ATZ → GA, RWYxx → FAP): si mostra così,
    /// e non è mai un errore.
    /// </summary>
    private static readonly Dictionary<Type, DescrizioneDelTipo> MappeDelMaps = new()
    {
        [typeof(GeometricStrRecord)] = Procedura("Mappa del MAPS (coordinate)", Vertici("Segments", "Tratti"), mappa: true),
        [typeof(ProcedureStrRecord)] = Procedura("Mappa del MAPS (punti per nome)", Vertici("Waypoints", "Punti"), mappa: true),
        [typeof(HoldingStrRecord)] = Procedura("Mappa del MAPS (punti misti)", Vertici("Points", "Punti"), mappa: true),
    };

    /// <summary>Vero per le voci del menu delle mappe (<c>MAPS</c>), non per una procedura su una pista.</summary>
    public static bool EUnaMappa(string? piste) => string.Equals(piste?.Trim(), VociDegliElenchi.Maps, StringComparison.OrdinalIgnoreCase);

    /// <summary>Le voci degli <c>.str</c> (§17): la stessa testa, tracciati di forma diversa.</summary>
    private static DescrizioneDelTipo Procedura(string nome, DescrizioneDelCampo tracciato, bool mappa = false) => new(nome,
    [
        Scalo(),
        C("RunwaySpec", "Piste", "Le piste della voce, più d'una separate da : — MAPS = una mappa del menu generale, non una pista.", Editor.Elenco) with
        {
            Fonte = FonteDellElenco.Piste,
        },
        C("ProcedureId", "Nome", "Il nome della procedura o della mappa."),
        C("LabelLat", "Latitudine dell'etichetta", "4° campo, di solito vuoto."),
        C("LabelLon", "Longitudine dell'etichetta", "5° campo, di solito vuoto."),
        mappa
            ? C("RecordType", "Si accende col tasto", "6° campo: nel MAPS non è il tipo della procedura, è il tasto della finestra delle procedure di Aurora che accende la mappa.", Editor.TipoFisso) with
            {
                Valori = DiEnum((nameof(StrRecordType.Star), "0", "STAR"), (nameof(StrRecordType.Transition), "1", "TRANS"),
                                (nameof(StrRecordType.Holding), "2", "HOLD"), (nameof(StrRecordType.Iap), "3", "IAP"),
                                (nameof(StrRecordType.Fap), "4", "FAP"), (nameof(StrRecordType.GoAround), "5", "GA")),
            }
            : C("RecordType", "Tipo", "6° campo: il tasto della finestra delle procedure di Aurora; nel MAPS è il tasto che accende la mappa.", Editor.TipoFisso) with
            {
                Valori = TipiDelloStr,
            },
        // Slice 9b: lo scrittore degli .str arriva all'8° campo (prima si fermava al 6°, e questi due erano in sola lettura).
        C("Transition", "Navaid della transizione", "7° campo (mai usato nei file italiani).", Editor.Navaid),
        C("IsRnav", "RNAV", "8° campo: 1 = RNAV.", Editor.SiNo),
        tracciato,
    ]);
}
