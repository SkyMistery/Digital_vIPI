using System.Text.RegularExpressions;
using Vipi.Sectorfile.Models;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Le chiavi <c>//@</c> ammesse in un tipo di file (carta «file per file» §M, 27 settembre 2026): il catalogo è il
/// contratto fra il Lab, che scrive, e vIPI, che legge. Una chiave fuori dal catalogo del suo file si legge lo stesso,
/// ma il validatore la segnala (avviso): un refuso non deve passare in silenzio, e nemmeno rompere il file.
/// </summary>
/// <param name="Formato">Il tipo di file, per i messaggi (<c>.sid</c>, <c>.str</c>).</param>
/// <param name="DelRecord">Le chiavi della dichiarazione <c>//@"NOME" …</c>.</param>
/// <param name="PerVerso">
/// Le chiavi che valgono per un verso di pista e si scrivono col suo numero davanti (§M regola 7: <c>06.tora=2628</c>).
/// </param>
/// <param name="DelPunto">Le chiavi di un punto, <c>//@@"PUNTO" …</c> (§M regola 4).</param>
public sealed partial record CatalogoDeiTag(
    string Formato,
    IReadOnlyList<string> DelRecord,
    IReadOnlyList<string> PerVerso,
    IReadOnlyList<string> DelPunto)
{
    /// <summary>Le chiavi di ogni record, in ogni file (§M: import, generatori, note).</summary>
    public static IReadOnlyList<string> Comuni { get; } = ["locked", "gen", "note"];

    /// <summary>Le chiavi del file, nelle prime righe: il ciclo AIRAC dei dati importati.</summary>
    public static IReadOnlyList<string> DelFile { get; } = ["source"];

    /// <summary>
    /// SID (§M, P6 e P11): fix intero, transizione, salita iniziale, WTC, categoria, specifica di navigazione; i punti
    /// col ruolo e i vincoli, per quando le SID avranno il tracciato (P9).
    /// </summary>
    public static CatalogoDeiTag Sid { get; } = new(
        ".sid",
        [.. Comuni, "fix", "trans", "initialclimb", "wtc", "cat", "nav"],
        [],
        ["role", "alt", "spd"]);

    /// <summary>
    /// Voci dei <c>.str</c> (§M, Q2-Q2d, Q5, Q8, F3-bis): quelle delle SID, più l'avvicinamento (tipo, minimi,
    /// pendenza), le mappe composte del <c>MAPS</c>, e per ATZ/CTR la famiglia di forme, i limiti e la classe.
    /// </summary>
    public static CatalogoDeiTag Str { get; } = new(
        ".str",
        [.. Comuni, "fix", "trans", "initialclimb", "wtc", "cat", "nav", "type", "mins", "gp",
         Metadati.Compose, Metadati.Whole, "form", "lower", "upper", "class"],
        [],
        ["role", "alt", "spd"]);

    /// <summary>
    /// Pista (§M, M9): il record è la coppia (<c>//@"LIRN 06/24"</c>). Larghezza e lunghezza della pista, e per verso
    /// soglia spostata, ILS, distanze dichiarate (<c>NU</c> = non utilizzabile), decolli dagli intermedi
    /// (<c>06.int=B:2540,C:1893</c>), lato del circuito, limiti d'uso; <c>vfronly</c> anche senza verso, per tutta la pista.
    /// </summary>
    public static CatalogoDeiTag Rw { get; } = new(
        ".rw",
        [.. Comuni, "width", "length", "vfronly"],
        ["thr", "ils", "tora", "toda", "asda", "lda", "int", "circuit", "dep", "arr", "vfronly"],
        []);

    /// <summary>Scalo (§M, M10): declinazione e suo anno, codice di riferimento, antincendio, traffico, pista preferenziale, vento in coda, orario ATS.</summary>
    public static CatalogoDeiTag Ap { get; } = new(
        ".ap",
        [.. Comuni, "magvar", "magvar.year", "refcode", "rff", "traffic", "pref", "tailwind", "ats"],
        [],
        []);

    /// <summary>Stand (§M, R2b): codice ICAO, contatto o remoto, uso, compagnie, pushback e suo verso, piazzale.</summary>
    public static CatalogoDeiTag Gts { get; } = new(
        ".gts",
        [.. Comuni, "code", "kind", "use", "airlines", "push", "pushdir", "apron"],
        [],
        []);

    /// <summary>Etichetta di taxiway (§M, R6): codice massimo, senso unico.</summary>
    public static CatalogoDeiTag Txi { get; } = new(".txi", [.. Comuni, "code", "oneway"], [], []);

    /// <summary>Fix, VOR, NDB, punti VFR, posizioni, attese in rotta: per ora solo le chiavi comuni.</summary>
    public static CatalogoDeiTag Fix { get; } = new(".fix", [.. Comuni], [], []);

    /// <inheritdoc cref="Fix"/>
    public static CatalogoDeiTag Vor { get; } = new(".vor", [.. Comuni], [], []);

    /// <inheritdoc cref="Fix"/>
    public static CatalogoDeiTag Ndb { get; } = new(".ndb", [.. Comuni], [], []);

    /// <inheritdoc cref="Fix"/>
    public static CatalogoDeiTag Vfi { get; } = new(".vfi", [.. Comuni], [], []);

    /// <inheritdoc cref="Fix"/>
    public static CatalogoDeiTag Frq { get; } = new(".frq", [.. Comuni], [], []);

    /// <inheritdoc cref="Fix"/>
    public static CatalogoDeiTag Hold { get; } = new(".hold", [.. Comuni], [], []);

    /// <summary>
    /// Etichette e confini di un <c>.artcc</c> (§1): per ora le chiavi comuni — i parametri dei generatori di gate e AOCC
    /// (§M-G, A5-A7) arrivano coi generatori.
    /// </summary>
    public static CatalogoDeiTag Artcc { get; } = new(".artcc", [.. Comuni], [], []);

    /// <summary>
    /// Aerovia (§2, B1, B5): il blocco dell'aerovia porta le chiavi comuni (<c>locked=si</c> per quelle a mano); ogni
    /// tratto il suo verso e le sue quote sul punto che lo apre (B2: <c>//@@"GARGA" dir=both lower=FL95 upper=FL195</c>).
    /// </summary>
    public static CatalogoDeiTag Aerovia { get; } = new(".lairway/.hairway", [.. Comuni], [], ["dir", "lower", "upper"]);

    /// <summary>Zona MVA (§6 E1, §19 S1): il soprannome, anche ripetuto (<c>//@"LIMM" zone="Torino"</c>).</summary>
    public static CatalogoDeiTag Mva { get; } = new(".mva", [.. Comuni, "zone"], [], []);

    /// <summary>Settore dinamico (§5 D5, D9): la famiglia di forme, i limiti verticali e la classe.</summary>
    public static CatalogoDeiTag Tfl { get; } = new(".tfl", [.. Comuni, "form", "lower", "upper", "class"], [], []);

    /// <summary>
    /// Gruppi di <c>.hartcc</c>/<c>.lartcc</c> (§10-11, J3, J6, J7): come i settori dinamici, più le configurazioni
    /// composte dalle parti (<c>//@"RR CONF2" compose="RR NE","RR TS"</c>).
    /// </summary>
    public static CatalogoDeiTag Confini { get; } = new(".hartcc/.lartcc",
        [.. Comuni, "form", "lower", "upper", "class", Metadati.Compose, Metadati.Whole], [], []);

    /// <summary>
    /// Segmenti dei <c>.geo</c> (§8, §8-bis, I2, H10) e delle aree P/R/D (G5), che hanno lo stesso lettore: la famiglia
    /// di forme, e i limiti e la classe delle aree.
    /// </summary>
    public static CatalogoDeiTag Geo { get; } = new(".geo", [.. Comuni, "form", "lower", "upper", "class"], [], []);

    /// <summary>Poligoni dei <c>.pol</c> (§9, I2, H10): la famiglia di forme, uguale al bordo del <c>.geo</c>.</summary>
    public static CatalogoDeiTag Pol { get; } = new(".pol", [.. Comuni, "form"], [], []);

    /// <summary>Il catalogo del tipo di record <typeparamref name="T"/>, o null se i suoi file non portano tag.</summary>
    public static CatalogoDeiTag? Di<T>() => Di(typeof(T));

    /// <summary>
    /// Il catalogo di un tipo di record (anche una sua sottoclasse: le voci di un <c>.str</c>, i settori di un
    /// <c>.fic</c>). I record di un <c>.artcc</c> si leggono come <see cref="ElementoArtcc"/>: il loro gruppo di confini
    /// è un <see cref="StaticBoundaryGroup"/> come quelli dei <c>.hartcc</c>, ma col catalogo del suo file.
    /// </summary>
    public static CatalogoDeiTag? Di(Type tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        if (typeof(SidProcedure).IsAssignableFrom(tipo))
            return Sid;
        if (typeof(StrRecord).IsAssignableFrom(tipo))
            return Str;
        if (tipo == typeof(ElementoArtcc))
            return Artcc;
        if (tipo == typeof(Airway))
            return Aerovia;
        if (tipo == typeof(MvaSector))
            return Mva;
        if (typeof(TflSector).IsAssignableFrom(tipo))
            return Tfl;
        if (tipo == typeof(StaticBoundaryGroup))
            return Confini;
        if (tipo == typeof(Line))
            return Geo;
        if (tipo == typeof(Polygon))
            return Pol;
        if (tipo == typeof(Runway))
            return Rw;
        if (tipo == typeof(AirportInfo))
            return Ap;
        if (tipo == typeof(Stand))
            return Gts;
        if (tipo == typeof(TaxiwayLabel))
            return Txi;
        if (tipo == typeof(Models.Fix))
            return Fix;
        if (tipo == typeof(Models.Vor))
            return Vor;
        if (tipo == typeof(Models.Ndb))
            return Ndb;
        if (tipo == typeof(VfrPoint))
            return Vfi;
        if (tipo == typeof(AtcPosition))
            return Frq;
        if (tipo == typeof(Attesa))
            return Hold;
        return null;
    }

    /// <summary>Vero se <paramref name="chiave"/> sta nel catalogo dei record, anche col verso di pista davanti.</summary>
    public bool AmmetteDelRecord(string chiave)
    {
        ArgumentNullException.ThrowIfNull(chiave);
        if (DelRecord.Contains(chiave))
            return true;
        var perVerso = ConIlVerso().Match(chiave);
        return perVerso.Success && PerVerso.Contains(perVerso.Groups[1].Value);
    }

    /// <summary>Vero se <paramref name="chiave"/> sta nel catalogo dei punti.</summary>
    public bool AmmetteDelPunto(string chiave) => DelPunto.Contains(chiave);

    // Il verso di pista davanti alla chiave: 06, 16L, 34R, 18C.
    [GeneratedRegex(@"^\d{2}[LRC]?\.([a-z]+)$")]
    private static partial Regex ConIlVerso();
}
