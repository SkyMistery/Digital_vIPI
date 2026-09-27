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
