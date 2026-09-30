using System.Text.RegularExpressions;
using Vipi.Domain;

namespace Vipi.Application.Airspace;

/// <summary>
/// Una correzione a mano, come la leggono la pagina e le regole. Un campo corretto è un campo non nullo; la classe
/// ha il suo segno (<paramref name="ClassCorrected"/>), perché «nessuna classe» è una correzione anche lei.
/// I <c>File*</c> sono quel che il file diceva quando la correzione è stata fatta o riconfermata.
/// </summary>
public sealed record AirspaceCorrectionRow(
    int Id, string VolumeKey, int VolumeOrdinal, string Name,
    AirspaceFamily? Family, bool ClassCorrected, string? AirspaceClass, string? BaseRaw, string? TopRaw,
    AirspaceFamily FileFamily, string? FileClass, string FileBaseRaw, string FileTopRaw,
    DateTime UpdatedUtc, string? UpdatedByName);

/// <summary>Quel che chi corregge vuole vedere: i quattro campi interi, non solo quelli cambiati.</summary>
public sealed record AirspaceCorrectionInput(AirspaceFamily Family, string? AirspaceClass, string BaseRaw, string TopRaw);

/// <summary>I campi che si possono correggere.</summary>
public enum AirspaceCorrectionField { Family, Class, Base, Top }

/// <summary>Un campo corretto e il file: che cosa diceva, che cosa dice adesso, che cosa dice la correzione.</summary>
public sealed record AirspaceCorrectionDiff(
    AirspaceCorrectionField Field, string? FileBefore, string? FileNow, string? Corrected);

/// <summary>Che cosa è successo sotto una correzione dopo un caricamento nuovo.</summary>
/// <remarks>⚠️ È un codice: il testo lo scrive la UI, nelle due lingue.</remarks>
public enum AirspaceCorrectionFindingKind
{
    /// <summary>Il file ha cambiato un campo corretto, e non nel senso della correzione.</summary>
    FileChanged,

    /// <summary>Il file ora dice già quel che dice la correzione: la correzione non serve più.</summary>
    FileAgrees,

    /// <summary>Nel file in vigore il volume non c'è più (né con la sua chiave, né con un nome unico).</summary>
    VolumeMissing,
}

/// <summary>
/// Una cosa da guardare. <paramref name="Volume"/> è il volume <b>del file</b> (senza correzione) a cui la correzione
/// si riferisce adesso; null se non si trova. <paramref name="KeyChanged"/> = ritrovato per nome, con una chiave
/// diversa da quella che la correzione cita (e che citano gli agganci).
/// </summary>
public sealed record AirspaceCorrectionFinding(
    AirspaceCorrectionFindingKind Kind, AirspaceCorrectionRow Correction, AirspaceVolumeRow? Volume,
    bool KeyChanged, IReadOnlyList<AirspaceCorrectionDiff> Diffs);

/// <summary>
/// Le regole delle correzioni a mano dei volumi dell'AIP (carta <c>docs/feature/2026-09-30-correzioni-spazi-aerei.md</c>):
/// come una correzione si sovrappone al volume, che cosa si salva, e che cosa segnalare dopo un caricamento nuovo.
/// Funzioni pure: il database lo fa <c>EfAirspaceCatalog</c>.
/// </summary>
public static class AirspaceCorrections
{
    private static readonly Regex Classe = new("^[A-G]$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>La classe come si salva: una lettera maiuscola, o null per «nessuna».</summary>
    public static string? NormClass(string? c)
    {
        var t = (c ?? "").Trim().ToUpperInvariant();
        return t.Length == 0 || t is "—" or "-" ? null : t;
    }

    /// <summary>
    /// Che cosa non va in una correzione, o null se va bene. Il codice è la chiave del testo nella UI
    /// (<c>Asp_Fix_Err_&lt;codice&gt;</c>).
    /// </summary>
    public static string? Validate(AirspaceCorrectionInput input)
    {
        if (!Enum.IsDefined(input.Family)) return "Family";
        var classe = NormClass(input.AirspaceClass);
        if (classe is not null && !Classe.IsMatch(classe)) return "Class";
        var @base = AirspaceLevelParser.Parse(input.BaseRaw);
        if (@base is null) return "Base";
        var tetto = AirspaceLevelParser.Parse(input.TopRaw);
        if (tetto is null) return "Top";
        if (input.BaseRaw.Trim().Length > 32 || input.TopRaw.Trim().Length > 32) return "Length";

        // Una base sopra il tetto è un errore di battitura, non una scelta. Si confronta solo dove i piedi si
        // confrontano: AGL contro AMSL resta una domanda senza risposta.
        if (@base.Feet is { } b && tetto.Feet is { } t && b >= t && StessoRiferimento(@base.Datum, tetto.Datum))
            return "Order";
        if (@base.Datum == AirspaceDatum.Unlimited) return "Order";
        return null;
    }

    private static bool StessoRiferimento(AirspaceDatum a, AirspaceDatum b) =>
        a == b || a == AirspaceDatum.Gnd || b == AirspaceDatum.Gnd
        || (a == AirspaceDatum.FlightLevel && b == AirspaceDatum.Amsl)
        || (a == AirspaceDatum.Amsl && b == AirspaceDatum.FlightLevel);

    /// <summary>Due quote dicono la stessa cosa? <c>2500 FT AMSL</c> e <c>2500FT AMSL</c> sì.</summary>
    public static bool SameLevel(string? a, string? b)
    {
        if (string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase)) return true;
        var pa = AirspaceLevelParser.Parse(a);
        var pb = AirspaceLevelParser.Parse(b);
        return pa is not null && pb is not null && pa.Datum == pb.Datum && pa.Feet == pb.Feet;
    }

    private static bool SameClass(string? a, string? b) =>
        string.Equals(NormClass(a), NormClass(b), StringComparison.Ordinal);

    /// <summary>
    /// Il volume come lo vede chi lo usa: quello del file, con sopra la correzione. Senza correzione, lo stesso.
    /// </summary>
    public static AirspaceVolumeRow Apply(AirspaceVolumeRow v, AirspaceCorrectionRow? c)
    {
        if (c is null) return v;

        var esito = v with { IsCorrected = true };
        if (c.Family is { } f) esito = esito with { Family = f };
        if (c.ClassCorrected) esito = esito with { AirspaceClass = NormClass(c.AirspaceClass) };
        if (c.BaseRaw is not null && AirspaceLevelParser.Parse(c.BaseRaw) is { } b)
            esito = esito with { BaseDatum = b.Datum, BaseFeet = b.Feet, BaseRaw = b.Raw.Trim() };
        if (c.TopRaw is not null && AirspaceLevelParser.Parse(c.TopRaw) is { } t)
            esito = esito with { TopDatum = t.Datum, TopFeet = t.Feet, TopRaw = t.Raw.Trim() };
        return esito;
    }

    /// <summary>
    /// La correzione da salvare perché il volume <paramref name="file"/> si legga come <paramref name="input"/>:
    /// solo i campi che differiscono dal file. Null = nessuno differisce, e la correzione non serve.
    /// </summary>
    public static AirspaceCorrectionRow? Desired(AirspaceVolumeRow file, AirspaceCorrectionInput input,
        int id = 0, DateTime updatedUtc = default, string? updatedByName = null)
    {
        var classe = NormClass(input.AirspaceClass);
        var famiglia = input.Family != file.Family ? input.Family : (AirspaceFamily?)null;
        var classeCorretta = !SameClass(classe, file.AirspaceClass);
        var @base = SameLevel(input.BaseRaw, file.BaseRaw) ? null : input.BaseRaw.Trim();
        var tetto = SameLevel(input.TopRaw, file.TopRaw) ? null : input.TopRaw.Trim();

        if (famiglia is null && !classeCorretta && @base is null && tetto is null) return null;

        return new AirspaceCorrectionRow(
            id, file.NaturalKey, file.Ordinal, file.Name,
            famiglia, classeCorretta, classeCorretta ? classe : null, @base, tetto,
            file.Family, file.AirspaceClass, file.BaseRaw, file.TopRaw,
            updatedUtc, updatedByName);
    }

    /// <summary>
    /// Che cosa segnalare: per ogni correzione, il volume del file <b>in vigore</b> a cui si riferisce, e se il file
    /// sotto è cambiato. <paramref name="fileVolumes"/> sono i volumi <b>senza</b> correzioni.
    ///
    /// <para>⚠️ Il volume si cerca prima per chiave e ordinale. Se non c'è — il file ha cambiato tipo, base o
    /// tetto, cioè la chiave — si cerca per <b>nome</b>, ma solo se quel nome nel file è unico e nessun'altra
    /// correzione lo prende già per chiave: <c>GRAZZANISE CTR Z2</c> compare due volte, e scegliere a caso
    /// sposterebbe la correzione sul volume sbagliato.</para>
    /// </summary>
    public static IReadOnlyList<AirspaceCorrectionFinding> Review(
        IReadOnlyList<AirspaceCorrectionRow> corrections, IReadOnlyList<AirspaceVolumeRow> fileVolumes)
    {
        var perChiave = fileVolumes.GroupBy(v => (v.NaturalKey, v.Ordinal)).ToDictionary(g => g.Key, g => g.First());
        var presiPerChiave = corrections
            .Where(c => perChiave.ContainsKey((c.VolumeKey, c.VolumeOrdinal)))
            .Select(c => (c.VolumeKey, c.VolumeOrdinal)).ToHashSet();
        var perNome = fileVolumes
            .Where(v => !presiPerChiave.Contains((v.NaturalKey, v.Ordinal)))
            .GroupBy(v => v.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var esito = new List<AirspaceCorrectionFinding>();
        foreach (var c in corrections)
        {
            if (Find(c, perChiave, perNome) is not { } trovato)
            {
                esito.Add(new AirspaceCorrectionFinding(
                    AirspaceCorrectionFindingKind.VolumeMissing, c, null, false, Diffs(c, null)));
                continue;
            }

            var (v, chiaveCambiata) = trovato;
            var diff = Diffs(c, v);
            var cambiato = diff.Any(d => !Uguali(d.Field, d.FileBefore, d.FileNow));
            if (!cambiato && !chiaveCambiata) continue;   // il file sotto è quello di prima: niente da dire

            var giaCosi = diff.All(d => Uguali(d.Field, d.FileNow, d.Corrected));
            esito.Add(new AirspaceCorrectionFinding(
                giaCosi ? AirspaceCorrectionFindingKind.FileAgrees : AirspaceCorrectionFindingKind.FileChanged,
                c, v, chiaveCambiata, diff));
        }
        return esito;
    }

    private static (AirspaceVolumeRow Volume, bool KeyChanged)? Find(
        AirspaceCorrectionRow c,
        IReadOnlyDictionary<(string, int), AirspaceVolumeRow> perChiave,
        IReadOnlyDictionary<string, List<AirspaceVolumeRow>> perNome)
    {
        if (perChiave.TryGetValue((c.VolumeKey, c.VolumeOrdinal), out var v)) return (v, false);
        if (perNome.TryGetValue(c.Name.Trim(), out var omonimi) && omonimi.Count == 1) return (omonimi[0], true);
        return null;
    }

    private static IReadOnlyList<AirspaceCorrectionDiff> Diffs(AirspaceCorrectionRow c, AirspaceVolumeRow? v)
    {
        var esito = new List<AirspaceCorrectionDiff>(4);
        if (c.Family is { } f)
            esito.Add(new(AirspaceCorrectionField.Family, c.FileFamily.ToString(), v?.Family.ToString(), f.ToString()));
        if (c.ClassCorrected)
            esito.Add(new(AirspaceCorrectionField.Class, c.FileClass, v?.AirspaceClass, NormClass(c.AirspaceClass)));
        if (c.BaseRaw is not null)
            esito.Add(new(AirspaceCorrectionField.Base, c.FileBaseRaw, v?.BaseRaw, c.BaseRaw));
        if (c.TopRaw is not null)
            esito.Add(new(AirspaceCorrectionField.Top, c.FileTopRaw, v?.TopRaw, c.TopRaw));
        return esito;
    }

    private static bool Uguali(AirspaceCorrectionField campo, string? a, string? b) => campo switch
    {
        AirspaceCorrectionField.Base or AirspaceCorrectionField.Top => SameLevel(a, b),
        AirspaceCorrectionField.Class => SameClass(a, b),
        _ => string.Equals(a, b, StringComparison.Ordinal),
    };
}
