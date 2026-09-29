using System.Text.RegularExpressions;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Core.Ispezione;

/// <summary>
/// Le voci degli editor a elenco (lotto «Subito», slice 3b): gli scali dell'<c>.ap</c>, le piste del <c>.rw</c>, le
/// posizioni dei <c>.frq</c>, le attese in rotta di <c>HOLDENR.hold</c> (slice 10a). Si leggono dai record della sessione come sono ADESSO (una pista aggiunta nel Lab si
/// propone subito), da tutti i file: le copie gemelle dei file di FIR dicono le stesse cose.
/// </summary>
public sealed partial class VociDegliElenchi
{
    private readonly Dictionary<string, List<string>> _piste;

    private readonly Dictionary<string, List<string>> _file;

    private VociDegliElenchi(
        IReadOnlyList<string> scali, IReadOnlyList<string> posizioni, IReadOnlyList<string> attese, Dictionary<string, List<string>> piste,
        Dictionary<string, List<string>> file)
    {
        Scali = scali;
        Posizioni = posizioni;
        Attese = attese;
        _piste = piste;
        _file = file;
    }

    // Le estensioni dei file che un .frq cita (slice 11a).
    private static readonly Dictionary<FonteDellElenco, string> Estensioni = new()
    {
        [FonteDellElenco.Profili] = ".cpr", [FonteDellElenco.Atis] = ".atis", [FonteDellElenco.Datis] = ".datis", [FonteDellElenco.Loa] = ".loa",
    };

    /// <summary>
    /// Il file dell'albero che un campo cita (<c>PREFS\TWR.cpr</c> → <c>SectorFiles/Include/IT/PREFS/TWR.cpr</c>), fra
    /// quelli aperti; null se non c'è. Un <c>\</c> in testa (<c>\liml.atis</c>) vale dalla cartella dei dati.
    /// </summary>
    public static string? FileCitato(SessioneAperta sessione, string? citato)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        string cercato = (citato ?? string.Empty).Trim().Replace('\\', '/').TrimStart('/');
        if (cercato.Length == 0)
            return null;
        return sessione.File.Keys.FirstOrDefault(k => DallaCartellaDeiDati(k) is { } suo && string.Equals(suo, cercato, StringComparison.OrdinalIgnoreCase));
    }

    // SectorFiles/Include/IT/PREFS/TWR.cpr → PREFS/TWR.cpr; null fuori da Include/<cartella dei dati>.
    private static string? DallaCartellaDeiDati(string relativo)
    {
        int include = relativo.IndexOf("/Include/", StringComparison.OrdinalIgnoreCase);
        if (include < 0)
            return null;
        int cartella = relativo.IndexOf('/', include + "/Include/".Length);
        return cartella < 0 ? null : relativo[(cartella + 1)..];
    }

    /// <summary>I codici ICAO degli scali, in ordine alfabetico (non i commentati: Aurora non li legge).</summary>
    public IReadOnlyList<string> Scali { get; }

    /// <summary>I nominativi delle posizioni ATC, in ordine alfabetico.</summary>
    public IReadOnlyList<string> Posizioni { get; }

    /// <summary>I nomi delle attese in rotta di <c>[HOLDENR]</c>, in ordine alfabetico (slice 10a: il campo attesa dei NAVAIDS).</summary>
    public IReadOnlyList<string> Attese { get; }

    /// <summary>La voce del menu generale di Aurora: le mappe degli <c>.str</c> che non stanno su una pista.</summary>
    public const string Maps = "MAPS";

    /// <summary>
    /// Le piste del <c>.rw</c> di uno scalo coi due versi di ognuna, nell'ordine del file (16L, 34R, 07, 25…), e in
    /// coda <see cref="Maps"/>. 🔴 Le voci di menu del <c>.rw</c> (<c>//MENU MAPPE</c>, <c>//ACC</c>) per il motore non
    /// sono record — sono righe che tiene com'erano — e quindi da qui non si vedono: <c>MAPS</c> si propone sempre,
    /// perché ogni scalo ce l'ha (96 sul fork); le voci di settore (<c>LIRR;NE</c>) sono della slice 11.
    /// </summary>
    public IReadOnlyList<string> PisteDi(string? scalo)
        => scalo is not { Length: > 0 } ? []
            : _piste.TryGetValue(scalo.Trim(), out var sue) ? [.. sue, Maps]
            : [Maps];

    /// <summary>Vero per una pista vera (01-36, con L/C/R), falso per una voce di menu del <c>.rw</c>.</summary>
    public static bool EUnaPistaVera(string voce) => PistaVera().IsMatch(voce.Trim());

    private readonly Dictionary<string, SortedSet<string>> _chiavi = ChiaviDiAurora();

    /// <summary>
    /// Le chiavi dei profili per una sezione (slice 11d, N1): quelle di un profilo completo di Aurora (risorsa
    /// <c>ChiaviDeiProfiliDiAurora.txt</c>) e quelle scritte nei <c>.cpr</c> dell'albero, in ordine alfabetico.
    /// </summary>
    public IReadOnlyList<string> ChiaviDi(string? sezione)
        => _chiavi.TryGetValue((sezione ?? string.Empty).Trim(), out var sue) ? [.. sue] : [];

    private static Dictionary<string, SortedSet<string>> ChiaviDiAurora()
    {
        var chiavi = new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
        using var flusso = typeof(VociDegliElenchi).Assembly.GetManifestResourceStream("ChiaviDeiProfiliDiAurora.txt");
        if (flusso is null)
            return chiavi;
        using var lettore = new StreamReader(flusso);
        while (lettore.ReadLine() is { } riga)
        {
            int sep = riga.IndexOf(';', StringComparison.Ordinal);
            if (riga.StartsWith("//", StringComparison.Ordinal) || sep < 0)
                continue;
            Aggiungi(chiavi, riga[..sep], riga[(sep + 1)..]);
        }

        return chiavi;
    }

    private static void Aggiungi(Dictionary<string, SortedSet<string>> chiavi, string sezione, string chiave)
    {
        if (!chiavi.TryGetValue(sezione.Trim(), out var sue))
            chiavi[sezione.Trim()] = sue = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        sue.Add(chiave.Trim());
    }

    /// <summary>Le voci per quella fonte; per le piste serve lo scalo del record, per le chiavi dei profili la sezione.</summary>
    public IReadOnlyList<string> Voci(FonteDellElenco fonte, string? scalo = null) => fonte switch
    {
        FonteDellElenco.ChiaviDelProfilo => ChiaviDi(scalo),
        FonteDellElenco.Scali => Scali,
        FonteDellElenco.Posizioni => Posizioni,
        FonteDellElenco.Attese => Attese,
        FonteDellElenco.Profili or FonteDellElenco.Atis or FonteDellElenco.Datis or FonteDellElenco.Loa => _file.GetValueOrDefault(Estensioni[fonte]) ?? [],
        FonteDellElenco.Piste => PisteDi(scalo),
        _ => [],
    };

    public static VociDegliElenchi Di(SessioneAperta sessione)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        var scali = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var posizioni = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var attese = new SortedSet<string>(StringComparer.Ordinal);
        var profili = new List<(string Sezione, string Chiave)>();
        var piste = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        // I file citabili, scritti come li scrive un .frq: dalla cartella dei dati, col «\».
        var citabili = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (string relativo in sessione.File.Keys.Order(StringComparer.OrdinalIgnoreCase))
        {
            if (Estensioni.ContainsValue(Path.GetExtension(relativo).ToLowerInvariant()) && DallaCartellaDeiDati(relativo) is { } suo)
            {
                string estensione = Path.GetExtension(relativo).ToLowerInvariant();
                if (!citabili.TryGetValue(estensione, out var suoi))
                    citabili[estensione] = suoi = [];
                suoi.Add(suo.Replace('/', '\\'));
            }
        }

        foreach (var file in sessione.File.Values.OrderBy(f => f.Relativo, StringComparer.Ordinal))
        {
            if (file is not IFileConRecord conRecord)
                continue;
            foreach (object record in conRecord.RecordDelModello)
            {
                switch (record)
                {
                    case AirportInfo { IsDisabled: false } scalo when scalo.IcaoCode.Length > 0:
                        scali.Add(scalo.IcaoCode.Trim());
                        break;
                    case AtcPosition posizione when posizione.Code.Length > 0:
                        posizioni.Add(posizione.Code.Trim());
                        break;
                    case Attesa attesa when attesa.Nome.Trim().Length > 0:
                        attese.Add(attesa.Nome.Trim());
                        break;
                    case ImpostazioneDelProfilo impostazione when impostazione.Chiave.Trim().Length > 0:
                        profili.Add((impostazione.Sezione, impostazione.Chiave));
                        break;
                    case Runway pista when pista.IcaoCode.Length > 0:
                        if (!piste.TryGetValue(pista.IcaoCode.Trim(), out var sue))
                            piste[pista.IcaoCode.Trim()] = sue = [];
                        foreach (string verso in new[] { pista.Designator1, pista.Designator2 }.Select(v => v.Trim()))
                        {
                            if (verso.Length > 0 && !sue.Contains(verso, StringComparer.OrdinalIgnoreCase))
                                sue.Add(verso);
                        }

                        break;
                }
            }
        }

        var voci = new VociDegliElenchi([.. scali], [.. posizioni], [.. attese], piste, citabili);
        foreach (var (sezione, chiave) in profili)
            Aggiungi(voci._chiavi, sezione, chiave);
        return voci;
    }

    [GeneratedRegex(@"^(0[1-9]|[12][0-9]|3[0-6])[LCR]?$")]
    private static partial Regex PistaVera();
}
