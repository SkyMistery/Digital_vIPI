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

    /// <summary>Il catalogo del tipo di record <typeparamref name="T"/>, o null se i suoi file non portano tag.</summary>
    public static CatalogoDeiTag? Di<T>() => Di(typeof(T));

    /// <summary>Il catalogo di un tipo di record (anche una sua sottoclasse: le voci di un <c>.str</c>).</summary>
    public static CatalogoDeiTag? Di(Type tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        if (typeof(SidProcedure).IsAssignableFrom(tipo))
            return Sid;
        if (typeof(StrRecord).IsAssignableFrom(tipo))
            return Str;
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
