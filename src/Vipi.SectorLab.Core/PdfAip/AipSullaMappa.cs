using System.Globalization;
using System.Text.RegularExpressions;
using Aip;
using Aip.Aurora;
using Vipi.SectorLab.Core.Mappa;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>
/// Una voce dell'AIP com'è scritta nel modello, per la scheda della mappa: che cos'è, come si chiama, i suoi campi e,
/// per una rotta, le righe dei tratti. <see cref="Tipo"/> e <see cref="Chiave"/> dicono quale voce del modello è (per le
/// correzioni: carta «Le correzioni dell'AIP espanso»).
/// </summary>
public sealed record VoceDellAip(string Genere, string Titolo, IReadOnlyList<(string Nome, string Valore)> Campi, IReadOnlyList<string> Righe,
    CorrectionKind? Tipo = null, string? Chiave = null);

/// <summary>Lo strato dell'AIP espanso di un ciclo: le forme per la mappa e, con lo stesso indice, le voci per la scheda.</summary>
public sealed record StratoDellAip(string Ciclo, StratoDellaMappa Strato, IReadOnlyList<VoceDellAip> Voci)
{
    /// <summary>Il «file» delle forme dell'AIP: non è un file del sector, e nessun gesto lo tocca.</summary>
    public string File => AipSullaMappa.Prefisso + Ciclo;
}

/// <summary>
/// L'AIP espanso sulla mappa del Lab (carta F6, prima della slice 2b): le aree di ENR 2 ed ENR 5, le rotte e le attese di
/// ENR 3, in uno strato <b>suo</b> — «AIP espanso» — sopra quelli del sector, un genere per tipo (CTR, ATZ, TMA… rotte,
/// attese) da accendere e spegnere, un colore per genere. Sola lettura: le forme hanno per «file» <c>aip:&lt;ciclo&gt;</c>,
/// che non è un file del sector, e un clic apre la scheda della voce, non un record.
/// <list type="bullet">
/// <item>Le aree con <see cref="Outline"/> a un punto per grado; un tratto di confine o di costa (l'AIP non lo disegna)
/// segue il tracciato di <c>itgeo.geo</c> del sector (<see cref="ConfiniDelSector"/>, l'eccezione decisa dal committente);
/// dove non si ritrova è una retta fra i due capi, e la scheda lo dice.</item>
/// <item>Le rotte come linee, spezzate dove l'AIP le interrompe.</item>
/// <item>Le attese di ENR 3.4.1 non hanno coordinate (il punto è per nome): si mettono dove il sector ha quel punto, e
/// dove non ce l'ha non si disegnano (la scheda resta, e lo dice).</item>
/// </list>
/// </summary>
public static partial class AipSullaMappa
{
    public const string Prefisso = "aip:";

    /// <summary>Lo strato: un solo id, fuori dall'elenco degli strati del sector.</summary>
    public static readonly TipoDiStrato Tipo = new("aip", "AIP espanso");

    public const string Rotte = "Rotte";
    public const string Attese = "Attese";
    public const string Punti = "Punti";
    public const string Radioassistenze = "Radioassistenze";
    public const string Luci = "Luci";
    public const string Pericoli = "Pericoli";
    public const string Ostacoli = "Ostacoli";
    public const string Scali = "Scali";
    public const string Sid = "SID";
    public const string Star = "STAR";
    public const string Transizioni = "Transizioni";
    public const string SaliteIniziali = "Salite iniziali";
    public const string Avvicinamenti = "Avvicinamenti";

    /// <summary>Il colore di ogni genere sulla mappa (i colori del Lab: lo strato è uno, i tipi no).</summary>
    public static readonly IReadOnlyDictionary<string, string> Colori = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["CTR"] = "#2f6fd6",
        ["ATZ"] = "#1aa0b8",
        ["TMA"] = "#7a4fc9",
        ["CTA"] = "#a06ad8",
        ["FIR/UIR"] = "#8a8f99",
        ["RMZ/TMZ"] = "#2f9f7a",
        ["P"] = "#d03030",
        ["R"] = "#e07020",
        ["D"] = "#c9a000",
        ["TSA/TRA"] = "#c0409a",
        ["Militari"] = "#5f7d1e",
        ["Sport"] = "#3c9fd8",
        ["Parchi"] = "#3f9a3f",
        ["Altre"] = "#6b6b6b",
        [Rotte] = "#2e8b3e",
        [Attese] = "#b06a10",
        [Punti] = "#4f7fb0",
        [Radioassistenze] = "#2a9d8f",
        [Luci] = "#d4a017",
        [Pericoli] = "#b0306a",
        [Ostacoli] = "#9c4f2a",
        [Scali] = "#3f5fa0",
        [Sid] = "#d0662b",
        [Star] = "#2b8fd0",
        [Transizioni] = "#9a6fd0",
        [SaliteIniziali] = "#c08a2b",
        [Avvicinamenti] = "#c0392b",
    };

    /// <summary>
    /// La chiave dello schema di Aurora di ogni genere che ha un corrispondente nel sector (riordino R5, D16: un fix dell'AIP
    /// e lo stesso fix del sector si vedono uguali): coi colori «di Aurora» la forma prende il colore dello schema, come il
    /// file del sector che tiene quel genere. I generi che il sector non ha (CTR e ATZ stanno nei MAPS, TMA e CTA il sector
    /// non le tiene, luci, pericoli…) restano col colore del loro genere.
    /// 🟡 Proposta dell'agente: le rotte di ENR 3.2 (superiori, nella struttura ICAO) con AIRWAYHIGH, le altre AIRWAYLOW.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ChiaviDiAurora = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["P"] = "PROHIBITED",
        ["R"] = "RESTRICTED",
        ["D"] = "DANGER",
        ["FIR/UIR"] = "ARTCC",
        [Rotte] = "AIRWAYLOW",
        [Attese] = "HOLDINGS",
        [Punti] = "FIX",
        [Scali] = "AIRPORT",
        [Sid] = "SID",
        [SaliteIniziali] = "SID",
        [Star] = "STAR",
        [Transizioni] = "TRANSITIONS",
    };

    /// <summary>
    /// Il tipo di una radioassistenza come lo dice il sector (<c>VOR:1</c>, <c>NDB</c>: il simbolo di Aurora, R5), o null
    /// se il sector non ha quel tipo.
    /// </summary>
    public static string? TipoDelPunto(NavaidKind tipo) => tipo switch
    {
        NavaidKind.Vor or NavaidKind.Dvor => "VOR:0",
        NavaidKind.VorDme or NavaidKind.DvorDme or NavaidKind.TvorDme => "VOR:1",
        NavaidKind.Vortac or NavaidKind.Dvortac => "VOR:2",
        NavaidKind.Tacan => "VOR:3",
        NavaidKind.Dme => "VOR:4",
        NavaidKind.Ndb or NavaidKind.Locator => "NDB",
        _ => null,
    };

    /// <summary>Un punto significativo: di ENR 4.4 è in rotta (<c>FIX:0</c>), di un AD 2 è terminale (<c>FIX:1</c>).</summary>
    public static string TipoDelPunto(string sezione) =>
        sezione.StartsWith("AD 2", StringComparison.Ordinal) ? "FIX:1" : "FIX:0";

    /// <summary>Vero se il «file» è quello di una forma dell'AIP.</summary>
    public static bool EDellAip(string? file) => file?.StartsWith(Prefisso, StringComparison.Ordinal) == true;

    /// <summary>
    /// Lo strato del modello. <paramref name="puntoDelSector"/>: dove il sector ha un punto, per nome (il catalogo del
    /// master scelto); null se non lo ha.
    /// </summary>
    /// <param name="confini">Il tracciato dei confini e delle coste (<see cref="ConfiniDelSector"/>); null = rette.</param>
    public static StratoDellAip Di(AipDocument aip, string ciclo, Func<string, Coordinate?> puntoDelSector, BorderTracer? confini = null)
    {
        ArgumentNullException.ThrowIfNull(aip);
        ArgumentNullException.ThrowIfNull(puntoDelSector);
        var file = Prefisso + ciclo;
        var forme = new List<FormaDellaMappa>();
        var voci = new List<VoceDellAip>();

        // R5: la chiave dello schema e il tipo del punto, come la forma del sector che tiene lo stesso genere.
        void Aggiungi(VoceDellAip voce, TipoDiForma tipo, IReadOnlyList<IReadOnlyList<Coordinate>> tratti, IReadOnlyList<string>? nonRisolti = null,
                      string? punto = null, string? chiave = null)
        {
            forme.Add(new FormaDellaMappa(file, voci.Count, tipo, voce.Titolo, tratti, nonRisolti ?? [], Tratto: Colori[voce.Genere],
                Chiave: chiave ?? ChiaviDiAurora.GetValueOrDefault(voce.Genere), Punto: punto, Genere: voce.Genere));
            voci.Add(voce);
        }

        foreach (var area in aip.Airspaces)
        {
            var problemi = new List<AuroraIssue>();
            var punti = Outline.Points(area.Geometry, new OutlineOptions { PointsPerDegree = 1, Border = confini }, problemi, area.Key);
            IReadOnlyList<Coordinate> anello = [.. punti.Select(Coordinata)];
            var conConfine = area.Geometry is RingGeometry r && r.Segments.Any(s => s is BorderSegment);
            Aggiungi(Voce(area, problemi.Any(p => p.Kind == AuroraIssueKind.BorderNotDrawn), conConfine), TipoDiForma.Area, [anello]);
        }

        foreach (var rotta in aip.Routes ?? [])
        {
            // Una linea per pezzo: l'interruzione (la riga di barre dell'AIP) stacca la penna.
            var pezzi = new List<IReadOnlyList<Coordinate>>();
            var pezzo = new List<Coordinate> { Coordinata(rotta.Points[0].Position) };
            for (var i = 0; i < rotta.Segments.Count; i++)
            {
                if (rotta.Segments[i].Discontinuity)
                {
                    pezzi.Add(pezzo);
                    pezzo = [];
                }
                pezzo.Add(Coordinata(rotta.Points[i + 1].Position));
            }
            pezzi.Add(pezzo);
            Aggiungi(Voce(rotta), TipoDiForma.Linea, [.. pezzi.Where(p => p.Count > 1)],
                chiave: rotta.Section.StartsWith("ENR 3.2", StringComparison.Ordinal) ? "AIRWAYHIGH" : null);
        }

        foreach (var attesa in aip.Holdings ?? [])
        {
            var dove = puntoDelSector(attesa.Fix);
            Aggiungi(Voce(attesa, dove is not null), TipoDiForma.Punto, dove is { } c ? [[c]] : [], dove is null ? [attesa.Fix] : null);
        }

        // Le voci puntuali (slice 3 delle correzioni): si scelgono sulla mappa e si correggono dalla loro scheda.
        void Puntuale(VoceDellAip voce, Position? dove, string? tipoDelPunto = null, string? chiave = null) =>
            Aggiungi(voce, TipoDiForma.Punto, dove is { } p ? [[Coordinata(p)]] : [], dove is null ? [voce.Titolo] : null, tipoDelPunto, chiave);
        foreach (var punto in aip.Points ?? [])
            Puntuale(Voce(Punti, CorrectionKind.Point, punto.Designator, punto.Section, punto.Page, ("Rotte", string.Join(", ", punto.Routes)),
                ("Procedure", punto.Procedures), ("FRA", punto.Fra), ("Note", punto.Remarks)), punto.Position, TipoDelPunto(punto.Section));
        foreach (var r in aip.Navaids ?? [])
        {
            var tipo = TipoDelPunto(r.Kind);
            Puntuale(Voce(Radioassistenze, CorrectionKind.Navaid, r.Key, r.Section, r.Page, ("Nome", r.Name), ("Frequenza", r.Frequency),
                ("Canale", r.Channel), ("Militare", r.Military ? "sì" : null), ("Note", r.Remarks)), r.Position,
                tipo, tipo is null ? null : tipo == "NDB" ? "NDB" : "VOR");
        }
        foreach (var luce in aip.Lights ?? [])
            Puntuale(Voce(Luci, CorrectionKind.Light, luce.Name, luce.Section, luce.Page, ("Tipo", luce.Type), ("Caratteristiche", luce.Characteristics),
                ("Orari", luce.Hours), ("Note", luce.Remarks)), luce.Position);
        foreach (var pericolo in aip.Hazards ?? [])
            Puntuale(Voce(Pericoli, CorrectionKind.Hazard, pericolo.Name, pericolo.Section, pericolo.Page, ("Tipo", pericolo.Kind.ToString()),
                ("Raggio", pericolo.Radius is { } d ? $"{d.Value.ToString(CultureInfo.InvariantCulture)} {d.Unit.ToString().ToUpperInvariant()}" : null),
                ("Quota", pericolo.Vertical), ("Orari", pericolo.Hours), ("Note", pericolo.Remarks)), pericolo.Position);
        // ENR 5.4 (passo 4): gli ostacoli, puntuali e piattaforme come punti, i lineari come una linea dall'inizio alla fine.
        foreach (var ostacolo in aip.Obstacles ?? [])
        {
            if (ostacolo.End is { } fine)
                Aggiungi(Voce(ostacolo), TipoDiForma.Linea, [[Coordinata(ostacolo.Position), Coordinata(fine)]]);
            else
                Puntuale(Voce(ostacolo), ostacolo.Position);
        }
        foreach (var scalo in aip.Aerodromes ?? [])
            Puntuale(Voce(Scali, CorrectionKind.Aerodrome, scalo.Icao, scalo.Heliport ? "AD 3" : "AD 2", scalo.Page, ("Nome", scalo.Name),
                ("Eliporto", scalo.Heliport ? "sì" : null), ("Elevazione", scalo.Elevation),
                ("Piste", string.Join(", ", scalo.Runways.Select(p => p.Designator))), ("Frequenze", scalo.Communications.Count.ToString(CultureInfo.InvariantCulture)),
                ("Carte", scalo.Charts is { Count: > 0 } carte ? carte.Count.ToString(CultureInfo.InvariantCulture) : null))
                with { Righe = RigheDelloScalo(scalo) }, scalo.Arp, "APT:0");

        // Le procedure di AD 2 (F7, slice 7c): una linea per i loro punti, che si cercano nell'AIP espanso stesso — ENR 4.4, i
        // punti RNAV degli scali, le radioassistenze — e, se mancano, nel sector. Le convenzionali in sola prosa hanno i tratti
        // ricavati dalla prosa (7b); quelle la cui prosa non si è letta no: la scheda c'è, la linea no.
        var puntiDellAip = PuntiDellAip(aip);
        Coordinate? Punto(string nome) => puntiDellAip.TryGetValue(nome, out var p) ? Coordinata(p) : puntoDelSector(nome);
        // La soglia nei tratti di un avvicinamento («RWY24», «THR RWY 22C», passo 6): quella della pista del suo scalo, da AD 2.12.
        var soglie = (aip.Aerodromes ?? []).SelectMany(s => s.Runways.Where(r => r.Threshold is not null).Select(r => (s.Icao, r.Designator, r.Threshold!.Value)))
            .GroupBy(x => (x.Icao, x.Designator)).ToDictionary(g => g.Key, g => g.First().Value);
        foreach (var procedura in aip.Procedures ?? [])
        {
            Coordinate? PuntoDellaProcedura(string nome) =>
                RxSoglia().Match(nome) is { Success: true } m && soglie.TryGetValue((procedura.Aerodrome, m.Groups["p"].Value), out var soglia)
                    ? Coordinata(soglia) : Punto(nome);
            var (tratti, nonRisolti) = Percorso(procedura, PuntoDellaProcedura);
            Aggiungi(Voce(procedura, nonRisolti), TipoDiForma.Linea, tratti, nonRisolti);
        }

        return new StratoDellAip(ciclo, new StratoDellaMappa(Tipo, forme), voci);
    }

    private static Coordinate Coordinata(Position p) => new(p.Lat, p.Lon);

    [GeneratedRegex(@"^(?:THR\s+)?RWY\s?(?<p>\d{2}[LRC]?)$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxSoglia();

    /// <summary>Dove sta ogni punto con nome dell'AIP espanso: punti (ENR 4.4 e AD 2) e radioassistenze (ENR 4.1 e AD 2.19).</summary>
    private static Dictionary<string, Position> PuntiDellAip(AipDocument aip)
    {
        var dove = new Dictionary<string, Position>(StringComparer.Ordinal);
        foreach (var p in aip.Points ?? []) dove.TryAdd(p.Designator, p.Position);
        foreach (var n in aip.Navaids ?? []) dove.TryAdd(n.Designator, n.Position);
        foreach (var n in (aip.Aerodromes ?? []).SelectMany(s => s.NavigationAids)) dove.TryAdd(n.Designator, n.Position);
        return dove;
    }

    /// <summary>
    /// Il percorso di una procedura: i punti dei tratti in fila. Un tratto senza punto (CA: fino a una quota) non ha un posto
    /// sulla mappa e si salta; un punto che non si trova stacca la penna e si dice; un RF si disegna come arco attorno al suo
    /// centro, nel verso del tratto. I punti senza nome della prosa (7b) hanno la loro posizione calcolata; un AF (l'arco DME)
    /// si disegna attorno alla sua radioassistenza.
    /// </summary>
    internal static (IReadOnlyList<IReadOnlyList<Coordinate>> Tratti, IReadOnlyList<string> NonRisolti) Percorso(Procedure procedura, Func<string, Coordinate?> punto)
    {
        var pezzi = new List<IReadOnlyList<Coordinate>>();
        var nonRisolti = new List<string>();
        var pezzo = new List<Coordinate>();
        foreach (var t in procedura.Legs)
        {
            Coordinate qui;
            if (t.Position is { } calcolata)
            {
                qui = new Coordinate(calcolata.Lat, calcolata.Lon);
            }
            else if (t.Fix is null)
            {
                continue;
            }
            else if (punto(t.Fix) is { } trovato)
            {
                qui = trovato;
            }
            else
            {
                nonRisolti.Add(t.Fix);
                if (pezzo.Count > 1) pezzi.Add(pezzo);
                pezzo = [];
                continue;
            }
            // Il centro col nome, o calcolato (passo 6 delle segnalazioni 2611: il raccordo al localizzatore).
            var centro = t.ArcCenterPosition is { } calcolato ? new Coordinate(calcolato.Lat, calcolato.Lon)
                : t.ArcCenter is { } c ? punto(c) : null;
            if (t.PathTerminator is "RF" or "AF" && pezzo.Count > 0 && centro is { } attorno)
                pezzo.AddRange(Arco(pezzo[^1], qui, attorno, orario: t.Turn == "R"));
            else
                pezzo.Add(qui);
        }
        if (pezzo.Count > 1) pezzi.Add(pezzo);
        return (pezzi, nonRisolti.Distinct().ToList());
    }

    /// <summary>
    /// Un arco da <paramref name="da"/> ad <paramref name="a"/> attorno a <paramref name="centro"/>, un punto ogni 5°, in piano
    /// (equirettangolare attorno al centro: per archi di qualche miglio basta). Il raggio passa da quello dell'inizio a quello
    /// della fine, così l'arco finisce esattamente sul punto.
    /// </summary>
    internal static IEnumerable<Coordinate> Arco(Coordinate da, Coordinate a, Coordinate centro, bool orario)
    {
        var k = Math.Cos(centro.LatitudeDeg * Math.PI / 180);
        (double X, double Y) Piano(Coordinate p) => ((p.LongitudeDeg - centro.LongitudeDeg) * k, p.LatitudeDeg - centro.LatitudeDeg);
        var (x0, y0) = Piano(da);
        var (x1, y1) = Piano(a);
        double r0 = Math.Sqrt(x0 * x0 + y0 * y0), r1 = Math.Sqrt(x1 * x1 + y1 * y1);
        // Angoli matematici (antiorari); un arco orario va verso angoli che calano.
        double t0 = Math.Atan2(y0, x0), t1 = Math.Atan2(y1, x1);
        var ampiezza = orario ? t0 - t1 : t1 - t0;
        while (ampiezza <= 0) ampiezza += 2 * Math.PI;
        var passi = Math.Max(2, (int)Math.Ceiling(ampiezza / (5 * Math.PI / 180)));
        for (var i = 1; i <= passi; i++)
        {
            var f = (double)i / passi;
            var t = orario ? t0 - ampiezza * f : t0 + ampiezza * f;
            var r = r0 + (r1 - r0) * f;
            yield return i == passi ? a : new Coordinate(centro.LatitudeDeg + r * Math.Sin(t), centro.LongitudeDeg + r * Math.Cos(t) / k);
        }
    }

    /// <summary>
    /// Il testo dello scalo nella scheda (carta «L'AIP intero», passo 5): ogni sottosezione col suo titolo, le voci numerate
    /// (l'etichetta italiana e la prima riga del valore), quante righe ha una tabella, l'inizio della prosa; poi le carte di
    /// AD 2.24 con pagina e data.
    /// </summary>
    internal static IReadOnlyList<string> RigheDelloScalo(Aerodrome scalo)
    {
        static string Prima(string? testo, int quanti = 120)
        {
            var riga = testo?.Split('\n')[0].Trim() ?? "";
            return riga.Length > quanti ? riga[..quanti] + "…" : riga;
        }
        var righe = new List<string>();
        foreach (var s in scalo.Sections ?? [])
        {
            righe.Add($"{s.Number} {s.Title}");
            foreach (var v in s.Items ?? [])
                righe.Add($"    {v.Number}. {Prima(v.Label, 60)}: {Prima(v.Value)}");
            if (s.Rows is { Count: > 0 } r)
                righe.Add($"    tabella, {r.Count} righe");
            if (s.Text is { } t)
                righe.Add($"    {Prima(t)}");
        }
        if (scalo.Charts is { Count: > 0 } carte)
        {
            righe.Add("Carte");
            righe.AddRange(carte.Select(c => $"    {c.Title} — {c.Page}{(c.Effective is { } d ? $" ({d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)})" : "")}"));
        }
        return righe;
    }

    /// <summary>
    /// La voce di un ostacolo di ENR 5.4 (passo 4): da guardare, non ancora da correggere (nessun tipo di correzione).
    /// </summary>
    private static VoceDellAip Voce(Obstacle o)
    {
        static string? MetriPiedi(double? m, double? ft) => m is null && ft is null ? null
            : $"{m?.ToString(CultureInfo.InvariantCulture) ?? "—"} m / {ft?.ToString(CultureInfo.InvariantCulture) ?? "—"} ft";
        var voce = Voce(Ostacoli, CorrectionKind.Hazard, o.Designator, o.Section, o.Page,
            ("Tipo", o.TypeEnglish is null ? o.Type : $"{o.Type} / {o.TypeEnglish}"), ("Nome", o.Name),
            ("Regione", o.Region), ("Dove", string.Join(", ", new[] { o.Site, o.Town, o.Province }.Where(x => x is not null))),
            ("Altezza AGL", MetriPiedi(o.HeightM, o.HeightFt)), ("Elevazione AMSL", MetriPiedi(o.ElevationM, o.ElevationFt)),
            ("Lunghezza", o.LengthM is { } l ? $"{l.ToString(CultureInfo.InvariantCulture)} m" : null),
            ("Punto più basso AMSL", MetriPiedi(o.LowestM, o.LowestFt)), ("Punto più alto AMSL", MetriPiedi(o.HighestM, o.HighestFt)),
            ("Segnalazione", o.DayMarking is null && o.NightMarking is null ? null : $"giorno {o.DayMarking ?? "—"}, notte {o.NightMarking ?? "—"}"),
            ("Note", o.Remarks));
        return voce with { Tipo = null };
    }

    /// <summary>La voce di un oggetto puntuale: la sezione, i campi che ci sono, la pagina.</summary>
    private static VoceDellAip Voce(string genere, CorrectionKind tipo, string chiave, string sezione, AipPage? pagina, params (string Nome, string? Valore)[] campi)
    {
        var tutti = new List<(string, string)> { ("Sezione", sezione) };
        tutti.AddRange(campi.Where(c => !string.IsNullOrWhiteSpace(c.Valore)).Select(c => (c.Nome, c.Valore!.Replace('\n', ' '))));
        tutti.Add(("Pagina", Pagina(pagina)));
        return new VoceDellAip(genere, chiave, tutti, [], tipo, chiave);
    }

    /// <summary>Il genere di un'area sulla mappa: i tipi dell'AIP raggruppati come li guarda un AOD.</summary>
    public static string Genere(AirspaceType tipo) => tipo switch
    {
        AirspaceType.Ctr => "CTR",
        AirspaceType.Atz => "ATZ",
        AirspaceType.Tma => "TMA",
        AirspaceType.Cta or AirspaceType.Uta => "CTA",
        AirspaceType.Fir or AirspaceType.Uir => "FIR/UIR",
        AirspaceType.Rmz or AirspaceType.Tmz => "RMZ/TMZ",
        AirspaceType.Prohibited => "P",
        AirspaceType.Restricted => "R",
        AirspaceType.Danger => "D",
        AirspaceType.Tsa or AirspaceType.Tra or AirspaceType.Cba => "TSA/TRA",
        // ENR 5.2 (slice 4a): i poligoni militari, le zone e i corridoi APR.
        AirspaceType.Military or AirspaceType.Adiz => "Militari",
        // ENR 5.5 e 5.6 (carta «L'AIP intero», passo 4).
        AirspaceType.Parachute or AirspaceType.Aerobatic or AirspaceType.AerialWork or AirspaceType.Ultralight => "Sport",
        AirspaceType.NaturePark => "Parchi",
        _ => "Altre",
    };

    private static string Pagina(AipPage? p) =>
        p is null ? "—" : p.Effective is { } d ? $"{p.Reference} ({d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)})" : p.Reference;

    private static VoceDellAip Voce(Airspace a, bool confineDritto, bool conConfine)
    {
        var campi = new List<(string, string)>
        {
            ("Tipo", a.Type.ToString().ToUpperInvariant()),
            ("Sezione", a.Section),
        };
        if (a.Name is { } nome) campi.Add(("Nome", nome));
        if (a.Bands is { Count: > 0 } fasce)
            foreach (var f in fasce) campi.Add(("Fascia", $"{f.Upper.Text} – {f.Lower.Text}{(f.Class is { } c ? $" · classe {c}" : "")}"));
        else
            campi.Add(("Limiti", $"{a.Upper.Text} – {a.Lower.Text}"));
        if (a.Class is { } classe) campi.Add(("Classe", classe));
        if (a.Activity is { } attivita) campi.Add(("Attività", attivita));
        if (a.Unit is { } ente) campi.Add(("Ente", ente.Replace('\n', ' ')));
        if (a.Hours is { } orari) campi.Add(("Orari", orari));
        if (a.TransitionAltitude is { } ta) campi.Add(("Altitudine di transizione", ta));
        if (a.Remarks is { } note && note != "NIL") campi.Add(("Note", note));
        if (a.Geometry is CorridorGeometry k)
            campi.Add(("Corridoio", $"{k.Width.Value.ToString(CultureInfo.InvariantCulture)} {k.Width.Unit.ToString().ToUpperInvariant()} di ampiezza, {k.Points.Count} punti"));
        campi.Add(("Pagina", Pagina(a.Page)));
        if (confineDritto) campi.Add(("⚠", "un tratto di confine o di costa che l'AIP non disegna è qui una retta"));
        else if (conConfine) campi.Add(("Confine", "il tratto di confine o di costa segue itgeo.geo del sector (l'AIP lo nomina, non lo disegna)"));
        return new VoceDellAip(Genere(a.Type), a.Key, campi, [], CorrectionKind.Airspace, a.Key);
    }

    private static VoceDellAip Voce(Route r)
    {
        var campi = new List<(string, string)> { ("Sezione", r.Section), ("Punti", r.Points.Count.ToString(CultureInfo.InvariantCulture)) };
        if (r.Navigation is { } nav) campi.Add(("Navigazione", nav));
        campi.Add(("Pagina", Pagina(r.Page)));
        // Il riquadro «NOTE / REMARKS» (carta «L'AIP intero», passo 3): il testo italiano, l'inglese resta nel modello.
        foreach (var n in r.Notes ?? [])
            campi.Add((n.Number is { } numero ? $"Nota {numero}" : "Nota", n.Text));
        var righe = new List<string>();
        for (var i = 0; i < r.Points.Count; i++)
        {
            var p = r.Points[i];
            righe.Add($"{(p.Compulsory ? "▲" : "∆")} {p.Designator}{(p.Remarks is { } n ? $" — {n.Replace('\n', ' ')}" : "")}");
            if (i >= r.Segments.Count) break;
            var t = r.Segments[i];
            if (t.Discontinuity)
            {
                righe.Add("    — interruzione —");
                continue;
            }
            var verso = (t.Forward, t.Backward) switch { (true, true) => "↕", (true, false) => "↓ solo avanti", (false, true) => "↑ solo indietro", _ => "?" };
            righe.Add($"    {t.Track} {t.DistanceNm?.ToString("0.0", CultureInfo.InvariantCulture)} NM · {t.Upper?.Text} – {t.Lower?.Text} · {verso}"
                      + $" · MNM {t.MinimumOdd ?? "—"}/{t.MinimumEven ?? "—"}{(t.Remarks is { } note ? $" · {note}" : "")}");
        }
        return new VoceDellAip(Rotte, r.Designator, campi, righe, CorrectionKind.Route, r.Designator);
    }

    /// <summary>Il genere di una procedura sulla mappa.</summary>
    public static string Genere(ProcedureKind genere) => genere switch
    {
        ProcedureKind.Sid => Sid,
        ProcedureKind.Star => Star,
        ProcedureKind.Transition => Transizioni,
        ProcedureKind.Approach => Avvicinamenti,
        _ => SaliteIniziali,
    };

    private static VoceDellAip Voce(Procedure p, IReadOnlyList<string> nonRisolti)
    {
        var campi = new List<(string, string)> { ("Scalo", p.Aerodrome) };
        if (p.Runways.Count > 0) campi.Add(("Piste", string.Join(", ", p.Runways)));
        if (p.Section is { } sezione) campi.Add(("Sezione", sezione));
        // Gli avvicinamenti (passo 6 della carta «L'AIP intero»).
        if (p.Segment is { } segmento) campi.Add(("Segmento", segmento));
        if (p.Fixes is { Count: > 0 } fissi) campi.Add(("FIX/POINT", string.Join(" · ", fissi.Select(f => f.Position is null ? $"{f.Text} (coordinate illeggibili)" : f.Text))));
        if (p.DataBlock is { Count: > 0 } dati) campi.Add(("SBAS FAS", $"{dati.Count} righe"));
        if (p.Description is { } prosa) campi.Add(("Descrizione", prosa.Replace('\n', ' ')));
        if (p.Minimums is { } minime) campi.Add(("Minime", minime.Replace('\n', ' ')));
        if (p.Remarks is { } note) campi.Add(("Note", note.Replace('\n', ' ')));
        campi.Add(("Pagina", Pagina(p.Page)));
        if (p.Legs.Count == 0 && p.Kind == ProcedureKind.Approach)
            campi.Add(("ⓘ", "senza tabella dei tratti (la carta è un'immagine): sulla mappa non c'è"));
        else if (p.Legs.Count == 0)
            campi.Add(("⚠", "solo prosa, e la prosa non si è letta (vedi le segnalazioni di «Leggi»): sulla mappa non c'è"));
        else if (p.LegsFromDescription)
            campi.Add(("ⓘ", "tratti ricavati dalla prosa (7b): i punti senza nome calcolati dalle radioassistenze; fa fede il testo"));
        else if (nonRisolti.Count > 0)
            campi.Add(("⚠", $"punti che non si trovano (né nell'AIP espanso né nel sector): {string.Join(", ", nonRisolti)}"));
        var righe = p.Legs.Select(t => string.Join(' ', new[]
        {
            t.Sequence, t.PathTerminator, t.Fix ?? t.Computed ?? "—", t.Computed is not null && t.Fix is null ? "(calcolato)" : null, t.FlyOver ? "(fly-over)" : null,
            t.Course is { } c ? $"{c}{(t.TrueCourse is { } v ? $" ({v})" : "")}" : null,
            t.DistanceNm is { } d ? $"{d.ToString("0.0", CultureInfo.InvariantCulture)} NM" : null,
            t.Turn is { } verso ? $"virata {verso}" : null,
            t.ArcCenter is { } centro ? $"centro {centro} r {t.ArcRadiusNm?.ToString("0.0", CultureInfo.InvariantCulture)} NM" : null,
            t.Altitude, t.Speed is { } v2 ? $"{v2} kt" : null, t.VerticalPath is { } vpa ? $"VPA/TCH {vpa}" : null, t.Navigation, t.Notes,
        }.OfType<string>())).ToList();
        righe.AddRange((p.Fixes ?? []).Select(f => $"FIX/POINT {f.Text}"
            + (f.Position is { } dove ? $" {dove.Lat.ToString("0.00000", CultureInfo.InvariantCulture)} {dove.Lon.ToString("0.00000", CultureInfo.InvariantCulture)}" : "")));
        var titolo = p.Kind == ProcedureKind.Approach ? $"{p.Aerodrome} {p.Designator}{(p.Segment is { } s ? $" — {s}" : "")}" : $"{p.Aerodrome} {p.Designator}{(p.Kind == ProcedureKind.InitialClimb && p.Runways.Count == 0 ? "" : p.Runways.Count > 0 && p.Kind != ProcedureKind.InitialClimb ? $" RWY {string.Join("/", p.Runways)}" : "")}";
        // La chiave della procedura, che non si corregge (Tipo null): serve alle segnalazioni per portare alla voce (26 ottobre).
        return new VoceDellAip(Genere(p.Kind), titolo, campi, righe, Chiave: p.Key);
    }

    private static VoceDellAip Voce(Holding h, bool disegnata)
    {
        var campi = new List<(string, string)>
        {
            ("Sezione", h.Section),
            ("Quota minima", h.Minimum.Text),
            ("Virata", h.Turn == TurnDirection.Right ? "destra" : "sinistra"),
            ("Rotta d'avvicinamento", h.InboundTrack.ToString("000", CultureInfo.InvariantCulture) + "°"),
        };
        if (h.Bearing is { } rif) campi.Add(("Riferimento", rif));
        // Le attese delle tabelle di AD 2 e AD 3 (passo 6 della carta «L'AIP intero»).
        if (h.TrueTrack is { } vera) campi.Add(("Rotta vera", vera + "°"));
        if (h.Leg is { } tratto) campi.Add(("Tratto", tratto));
        if (h.Maximum is { } massima) campi.Add(("Quota massima", massima.Text));
        if (h.Speed is { } velocita) campi.Add(("Velocità", velocita));
        if (h.Navigation is { } specifica) campi.Add(("Specifica", specifica));
        if (h.NoteRefs is { } rimandi) campi.Add(("Rimandi", rimandi));
        if (h.Remarks is { } note) campi.Add(("Note", note.Replace('\n', ' ')));
        if (h.Text is { } riga) campi.Add(("Riga stampata", riga));
        campi.Add(("Pagina", Pagina(h.Page)));
        campi.Add(disegnata
            ? ("Posizione", h.Section.StartsWith("AD ", StringComparison.Ordinal)
                ? "quella del punto nel sector: la tabella dell'attesa non dà coordinate"
                : "quella del punto nel sector: ENR 3.4.1 non dà coordinate")
            : ("⚠", $"il sector non ha il punto {h.Fix}: sulla mappa non c'è"));
        return new VoceDellAip(Attese, $"{h.Fix} {h.Minimum.Text} {(h.Turn == TurnDirection.Right ? "R" : "L")} {h.InboundTrack:000}°", campi, [],
            CorrectionKind.Holding, AipCorrections.HoldingKey(h));
    }
}
