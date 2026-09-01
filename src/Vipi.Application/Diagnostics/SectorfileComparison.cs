using System.Globalization;
using Vipi.Application.Abstractions;

namespace Vipi.Application.Diagnostics;

/// <summary>Una posizione ATC dei cataloghi vIPI, per il confronto col sectorfile.</summary>
/// <param name="IsManual">Riga aggiunta a mano (un ente estero catalogato dalla pagina Confinanti): la
/// sorgente non l'ha mai mandata, e il sectorfile italiano non ha nessuna ragione di elencarla.</param>
public sealed record VipiAtcPosition(string Callsign, string? Frequency, bool IsManual);

/// <summary>Un aeroporto dei cataloghi vIPI, per il confronto col sectorfile.</summary>
public sealed record VipiAirport(
    string Icao, int? TransitionAltitudeFt, int? ElevationFt, double? Lat, double? Lon);

/// <summary>Una estremità di pista dei cataloghi vIPI, per il confronto col sectorfile.</summary>
public sealed record VipiRunwayEnd(
    string Icao, string Ident, double? ThresholdLat, double? ThresholdLon);

/// <summary>
/// Fotografia di sola lettura del lato <b>vIPI</b> del confronto. Separa i dati (repository) dalla logica di
/// rilevazione (pura, testabile), come <see cref="ConsistencyDataset"/>.
/// </summary>
public sealed class SectorfileComparisonDataset
{
    public IReadOnlyList<VipiAtcPosition> Positions { get; init; } = Array.Empty<VipiAtcPosition>();
    public IReadOnlyList<VipiAirport> Airports { get; init; } = Array.Empty<VipiAirport>();
    public IReadOnlyList<VipiRunwayEnd> RunwayEnds { get; init; } = Array.Empty<VipiRunwayEnd>();

    /// <summary>
    /// I codici delle ACC (<c>LIRR</c>, <c>LIMM</c>, <c>LIPP</c>, <c>LIBB</c>).
    /// <para>⚠️ Servono a <b>escluderli</b> dagli aeroporti: nella tabella degli aeroporti di vIPI ci sono
    /// anche loro, e senza questo filtro il confronto direbbe «il sectorfile non ha l'aeroporto LIRR» —
    /// che non è un aeroporto.</para>
    /// </summary>
    public IReadOnlySet<string> AccCodes { get; init; } = new HashSet<string>();
}

/// <summary>
/// Confronto fra ciò che afferma il <b>sectorfile Aurora</b> della divisione e ciò che vIPI tiene dall'<b>API
/// IVAO</b>. Funzione pura: nessun I/O, nessun database, nessun orologio.
///
/// <para><b>Che cosa dice, e che cosa non dice.</b> Dice «le due sorgenti non concordano», mai «questo è
/// sbagliato»: chi ha ragione non lo sappiamo, e la riparazione non sta in questa applicazione — sta nel
/// sectorfile, che scrive l'IT-AOD. Per questo i rilievi sono tutti <see cref="ConsistencySeverity.Warning"/>
/// e hanno <c>Where = null</c>.</para>
///
/// <para>⚠️ <b>Le tolleranze non sono scelte a occhio: sono misurate</b> sui dati veri il 1 settembre 2026
/// (carta <c>docs/design/piano-coerenza-sectorfile.md</c> §0-bis). Chi le stringe «per prudenza» riempie la
/// pagina di rumore, ed è già stato provato — a 1° di QFU uscivano 115 falsi.</para>
/// </summary>
public static class SectorfileComparison
{
    /// <summary>
    /// Fino a questa differenza la frequenza è <b>lo stesso canale</b> scritto in due modi: nella spaziatura
    /// 8.33 lo stesso canale si scrive <c>118.955</c> o <c>118.950</c>.
    /// <para>⚠️ In <b>kHz interi</b>, non in MHz decimali, e non è pignoleria: <c>118.180 - 118.175</c> in
    /// virgola mobile fa <c>0.0050000000000067</c>, cioè «maggiore di 0.005» — e un canale scritto nei due
    /// modi diventava un rilievo. Le frequenze radio sono numerabili: si contano.</para>
    /// </summary>
    public const long TolleranzaFrequenzaKHz = 5;

    /// <summary>Arrotondamenti diversi alla fonte. Misurato: a questa soglia le divergenze vere sono zero.</summary>
    public const int TolleranzaElevazioneFt = 10;

    /// <summary>Il riferimento aeroporto detto con più o meno cifre resta lo stesso posto.</summary>
    public const double TolleranzaAeroportoNm = 0.5;

    /// <summary>~50 m: sotto, è la stessa soglia; sopra, o è una soglia spostata o è una coordinata rotta.</summary>
    public const double TolleranzaSogliaNm = 0.027;

    /// <summary>
    /// Come si leggono i rilievi: per famiglia, non alla rinfusa. Chi apre la pagina cerca «le frequenze» o
    /// «le piste», non l'ordine alfabetico.
    /// </summary>
    public enum Famiglia
    {
        Posizioni,
        Aeroporti,
        Piste,
    }

    /// <summary>
    /// A quale famiglia appartiene un rilievo. Sta qui e non nella pagina: è chi produce il rilievo a sapere
    /// di che cosa parla — la stessa regola di <see cref="ConsistencyFinding.Where"/>. Una mappa lato UI
    /// sarebbe un secondo posto da tenere allineato, e una categoria nuova nascerebbe muta.
    /// </summary>
    /// <remarks>⚠️ Il caso di default è <b>dichiarato</b> e non è una scelta di comodo: un rilievo di questa
    /// area che non si sa dove mettere è un errore di programmazione, e il test lo pretende — vedi
    /// <c>SectorfileComparisonTests</c>.</remarks>
    public static Famiglia FamigliaDi(ConsistencyFinding f) => f.CategoryKey switch
    {
        CatFrequenza or CatPosSoloSf or CatPosSoloVipi => Famiglia.Posizioni,
        CatTa or CatElevazione or CatCoordinate or CatAptSoloVipi => Famiglia.Aeroporti,
        CatPiste or CatSoglia => Famiglia.Piste,
        _ => Famiglia.Posizioni,
    };

    public const string CatFrequenza = "Diag_Cat_SfFrequenza";
    public const string CatPosSoloSf = "Diag_Cat_SfPosSoloSf";
    public const string CatPosSoloVipi = "Diag_Cat_SfPosSoloVipi";
    public const string CatTa = "Diag_Cat_SfTa";
    public const string CatElevazione = "Diag_Cat_SfElevazione";
    public const string CatCoordinate = "Diag_Cat_SfCoordinate";
    public const string CatAptSoloVipi = "Diag_Cat_SfAptSoloVipi";
    public const string CatPiste = "Diag_Cat_SfPiste";
    public const string CatSoglia = "Diag_Cat_SfSoglia";

    /// <summary>Tutte le categorie prodotte da questo confronto: la rete del test sulla mappa delle famiglie
    /// e sulle chiavi di traduzione.</summary>
    public static readonly IReadOnlyList<string> Categorie = new[]
    {
        CatFrequenza, CatPosSoloSf, CatPosSoloVipi, CatTa, CatElevazione, CatCoordinate,
        CatAptSoloVipi, CatPiste, CatSoglia,
    };

    /// <summary>
    /// I rilievi del confronto. <paramref name="sf"/> null = la sorgente non ha risposto: <b>nessun
    /// rilievo</b>, perché confrontare contro il vuoto aprirebbe una riga su ogni dato che abbiamo.
    /// </summary>
    public static IReadOnlyList<ConsistencyFinding> Analyze(SectorfileFacts? sf, SectorfileComparisonDataset d)
    {
        var findings = new List<ConsistencyFinding>();
        if (sf is null) return findings;

        Posizioni(findings, sf, d);
        Aeroporti(findings, sf, d);
        Piste(findings, sf, d);
        return findings;
    }

    // ------------------------------------------------------------------------------------ A. POSIZIONI

    /// <summary>
    /// ⚠️ Due filtri, e senza di loro il confronto è inutilizzabile (misurato): si guardano <b>solo i
    /// callsign italiani</b> — i cataloghi vIPI contengono i confinanti esteri, 142 su 345, e il sectorfile
    /// italiano non ha nessuna ragione di elencarli — e <b>non gli ATIS</b>, che in vIPI sono posizioni e nel
    /// sectorfile stanno nei file <c>.atis</c>, cioè da un'altra parte (25 callsign).
    /// </summary>
    private static bool Confrontabile(string callsign) =>
        callsign.StartsWith("LI", StringComparison.Ordinal) &&
        !callsign.EndsWith("_ATIS", StringComparison.Ordinal);

    private static void Posizioni(List<ConsistencyFinding> f, SectorfileFacts sf, SectorfileComparisonDataset d)
    {
        var lato = sf.Positions
            .Where(p => Confrontabile(p.Callsign))
            .GroupBy(p => p.Callsign, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Frequency, StringComparer.Ordinal);

        var casa = d.Positions
            .Where(p => !p.IsManual && Confrontabile(p.Callsign))
            .GroupBy(p => p.Callsign, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Frequency, StringComparer.Ordinal);

        foreach (var (callsign, freqSf) in lato.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (!casa.TryGetValue(callsign, out var freqCasa))
            {
                f.Add(Rilievo("Posizione assente in vIPI", CatPosSoloSf,
                    "Diag_Msg_SfPosSoloSf", new object[] { callsign },
                    "Diag_Ent_SfPosizione", new object[] { callsign },
                    $"Il sectorfile offre la posizione {callsign}, che i cataloghi IVAO non hanno."));
                continue;
            }

            // ⚠️ Un campo vuoto di casa NON è una divergenza: è «non lo so». Mai un rilievo su un dato che
            // manca a noi — la regola vale in tutte e tre le famiglie.
            var a = Khz(freqCasa);
            var b = Khz(freqSf);
            if (a is null || b is null) continue;
            if (Math.Abs(a.Value - b.Value) <= TolleranzaFrequenzaKHz) continue;

            f.Add(Rilievo("Frequenza divergente", CatFrequenza,
                "Diag_Msg_SfFrequenza", new object[] { callsign, freqCasa!, freqSf! },
                "Diag_Ent_SfPosizione", new object[] { callsign },
                $"{callsign}: i cataloghi IVAO dicono {freqCasa}, il sectorfile {freqSf}."));
        }

        foreach (var callsign in casa.Keys.Where(k => !lato.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal))
            f.Add(Rilievo("Posizione assente nel sectorfile", CatPosSoloVipi,
                "Diag_Msg_SfPosSoloVipi", new object[] { callsign },
                "Diag_Ent_SfPosizione", new object[] { callsign },
                $"La posizione {callsign} esiste nei cataloghi IVAO ma il sectorfile non la offre: " +
                "chi si connette lì non trova né profilo né mappa."));
    }

    // ------------------------------------------------------------------------------------ B. AEROPORTI

    private static void Aeroporti(List<ConsistencyFinding> f, SectorfileFacts sf, SectorfileComparisonDataset d)
    {
        var lato = sf.Airports
            .GroupBy(a => a.Icao, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        // ⚠️ I codici ACC vivono nella tabella degli aeroporti e aeroporti non sono.
        var casa = d.Airports
            .Where(a => !d.AccCodes.Contains(a.Icao))
            .GroupBy(a => a.Icao, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        foreach (var (icao, mio) in casa.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (!lato.TryGetValue(icao, out var suo))
            {
                // ⚠️ Il verso opposto NON si segnala: il sectorfile elenca 44 scali minori e voci che
                // aeroporti non sono (LIZZ «AIR DEFENCE»). Che noi non li documentiamo è la normalità.
                f.Add(Rilievo("Aeroporto assente nel sectorfile", CatAptSoloVipi,
                    "Diag_Msg_SfAptSoloVipi", new object[] { icao },
                    "Diag_Ent_SfAeroporto", new object[] { icao },
                    $"Documentiamo l'aeroporto {icao}, che il sectorfile non elenca."));
                continue;
            }

            if (mio.TransitionAltitudeFt is { } taCasa && suo.TransitionAltitudeFt is { } taSf && taCasa != taSf)
                f.Add(Rilievo("TA divergente", CatTa,
                    "Diag_Msg_SfTa", new object[] { icao, taCasa, taSf },
                    "Diag_Ent_SfAeroporto", new object[] { icao },
                    $"{icao}: la TA è {taCasa} ft per IVAO e {taSf} ft nel sectorfile."));

            if (mio.ElevationFt is { } elCasa && suo.ElevationFt is { } elSf &&
                Math.Abs(elCasa - elSf) > TolleranzaElevazioneFt)
                f.Add(Rilievo("Elevazione divergente", CatElevazione,
                    "Diag_Msg_SfElevazione", new object[] { icao, elCasa, elSf },
                    "Diag_Ent_SfAeroporto", new object[] { icao },
                    $"{icao}: elevazione {elCasa} ft per IVAO, {elSf} ft nel sectorfile."));

            if (Distanza(mio.Lat, mio.Lon, suo.Lat, suo.Lon) is { } nm && nm > TolleranzaAeroportoNm)
                f.Add(Rilievo("Riferimento aeroporto divergente", CatCoordinate,
                    "Diag_Msg_SfCoordinate", new object[] { icao, Nm(nm) },
                    "Diag_Ent_SfAeroporto", new object[] { icao },
                    $"{icao}: il punto di riferimento dista {Nm(nm)} NM fra le due sorgenti."));
        }
    }

    // ---------------------------------------------------------------------------------------- C. PISTE

    private static void Piste(List<ConsistencyFinding> f, SectorfileFacts sf, SectorfileComparisonDataset d)
    {
        var lato = Raggruppa(sf.RunwayEnds.Select(r => (r.Icao, r.Ident, r.ThresholdLat, r.ThresholdLon)));
        var casa = Raggruppa(d.RunwayEnds.Select(r => (r.Icao, r.Ident, r.ThresholdLat, r.ThresholdLon)));

        foreach (var (icao, mie) in casa.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (!lato.TryGetValue(icao, out var sue)) continue;   // aeroporto non confrontabile: non è un rilievo

            // ⚠️ UN rilievo per aeroporto, non uno per pista: una rinumerazione tocca tutte le estremità
            // insieme (LIRP: 3L/3R/21L/21R contro 4L/4R/22L/22R) e quattro righe che dicono la stessa cosa
            // sono quattro modi di non farla leggere.
            var soloCasa = mie.Keys.Where(k => !sue.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
            var soloSf = sue.Keys.Where(k => !mie.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
            if (soloCasa.Count > 0 || soloSf.Count > 0)
            {
                var a = soloCasa.Count > 0 ? string.Join(" ", soloCasa) : "—";
                var b = soloSf.Count > 0 ? string.Join(" ", soloSf) : "—";
                f.Add(Rilievo("Designatori pista divergenti", CatPiste,
                    "Diag_Msg_SfPiste", new object[] { icao, a, b },
                    "Diag_Ent_SfAeroporto", new object[] { icao },
                    $"{icao}: solo nei cataloghi IVAO {a}; solo nel sectorfile {b}."));
            }

            foreach (var (ident, mia) in mie.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                if (!sue.TryGetValue(ident, out var sua)) continue;
                if (Distanza(mia.Lat, mia.Lon, sua.Lat, sua.Lon) is not { } nm || nm <= TolleranzaSogliaNm) continue;

                var metri = (int)Math.Round(nm * 1852);
                f.Add(Rilievo("Soglia divergente", CatSoglia,
                    "Diag_Msg_SfSoglia", new object[] { icao, ident, metri },
                    "Diag_Ent_SfPista", new object[] { icao, ident },
                    $"{icao}/{ident}: le due soglie distano {metri} m."));
            }
        }
    }

    private static Dictionary<string, Dictionary<string, (double? Lat, double? Lon)>> Raggruppa(
        IEnumerable<(string Icao, string Ident, double? Lat, double? Lon)> righe)
    {
        var mappa = new Dictionary<string, Dictionary<string, (double?, double?)>>(StringComparer.Ordinal);
        foreach (var r in righe)
        {
            if (r.Ident.Length == 0) continue;
            if (!mappa.TryGetValue(r.Icao, out var per)) mappa[r.Icao] = per = new(StringComparer.Ordinal);
            per[r.Ident] = (r.Lat, r.Lon);
        }
        return mappa;
    }

    // ---------------------------------------------------------------------------------------- Utilità

    /// <summary>
    /// Il rilievo, con la stessa forma degli altri produttori: testo grezzo per i log e l'health check,
    /// chiavi per chi lo mostra. <b>Sempre</b> <see cref="ConsistencySeverity.Warning"/> e <c>Where = null</c>
    /// — vedi il commento di classe.
    /// </summary>
    private static ConsistencyFinding Rilievo(string categoria, string categoriaKey,
        string dettaglioKey, object[] dettaglioArgs, string bersaglioKey, object[] bersaglioArgs, string dettaglio) =>
        new(categoria, ConsistencySeverity.Warning, string.Join(" ", bersaglioArgs), dettaglio,
            ConsistencyArea.Sectorfile, Where: null,
            CategoryKey: categoriaKey, DetailKey: dettaglioKey, DetailArgs: dettaglioArgs,
            EntityKey: bersaglioKey, EntityArgs: bersaglioArgs);

    /// <summary>La frequenza in kHz interi: <c>118.955</c> → <c>118955</c>. null se il campo non è un numero.</summary>
    private static long? Khz(string? f) =>
        double.TryParse((f ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? (long)Math.Round(v * 1000) : null;

    private static string Nm(double nm) => nm.ToString("0.0", CultureInfo.InvariantCulture);

    /// <summary>Distanza in NM fra due punti in gradi decimali; null se un capo non c'è.</summary>
    /// <remarks>Equirettangolare, che a queste distanze basta: qui si decide sopra o sotto una soglia, non
    /// si naviga.</remarks>
    private static double? Distanza(double? lat1, double? lon1, double? lat2, double? lon2)
    {
        if (lat1 is null || lon1 is null || lat2 is null || lon2 is null) return null;
        var dLat = (lat2.Value - lat1.Value) * 60;
        var dLon = (lon2.Value - lon1.Value) * 60 * Math.Cos((lat1.Value + lat2.Value) / 2 * Math.PI / 180);
        return Math.Sqrt(dLat * dLat + dLon * dLon);
    }
}
