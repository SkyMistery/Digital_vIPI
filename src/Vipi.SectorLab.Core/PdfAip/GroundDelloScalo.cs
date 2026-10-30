using System.Globalization;
using System.Text.RegularExpressions;
using Aip;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;
using PistaDelSector = Vipi.Sectorfile.Models.Runway;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>
/// Le taxiway di AD 2.8 (AD 3.8) di uno scalo, com'è stampate: i nomi, e se l'AIP ne ha una senza nome («TWY 16.0 M»), le
/// dice tutte insieme («ALL») o nessuna («NIL»). <see cref="Testo"/>: la colonna dei designatori com'è letta, per guardarla.
/// </summary>
public sealed record TaxiwayDiAd2(string Sezione, IReadOnlyList<string> Nomi, bool SenzaNome, bool Tutte, string Testo, AipPage? Pagina);

/// <summary>
/// Una testata di una pista del sector contro la soglia di AD 2.12: a quanti metri. <see cref="NellAip"/> false: AD 2.12 non
/// ha la testata; true con <see cref="Metri"/> null: la ha senza le coordinate della soglia (LIAQ, LICP sul 2611).
/// </summary>
public sealed record SogliaNelGround(string Pista, string Testata, string File, int Record, bool NellAip, double? Metri)
{
    /// <summary>Oltre <see cref="ConfrontoDiAd2.SogliaDellaSogliaM"/>: la stessa soglia del confronto di AD 2.</summary>
    public bool Diversa => Metri > ConfrontoDiAd2.SogliaDellaSogliaM;
}

/// <summary>
/// I nomi delle taxiway di AD 2.8 contro le etichette dei <c>.txi</c> del sector per lo scalo: quelli che ci sono tutti e
/// due, quelli che il sector non ha, quelli che l'AIP non dice; e i file del sector da cui vengono. Le etichette che non hanno
/// la forma di una taxiway («APRON», «MIL ZONES», «HELIPAD», «100», «RAMP1») stanno a parte, in <see cref="AltreEtichette"/>:
/// il sector le scrive nei <c>.txi</c>, AD 2.8 non le dice.
/// </summary>
public sealed record ConfrontoDelleTaxiway(
    TaxiwayDiAd2? Aip,
    IReadOnlyList<string> Uguali,
    IReadOnlyList<string> MancanoNelSector,
    IReadOnlyList<string> SoloNelSector,
    IReadOnlyList<string> AltreEtichette,
    IReadOnlyList<string> File);

/// <summary>I controlli del ground di uno scalo (carta «Gli aeroporti nel Lab», §2 D, controlli 1 e 2).</summary>
public sealed record ControlliDelGround(IReadOnlyList<SogliaNelGround> Soglie, IReadOnlyList<string> PisteSenzaSector, ConfrontoDelleTaxiway Taxiway);

/// <summary>
/// I controlli del ground che il Lab fa da sé dal testo (carta «Gli aeroporti nel Lab», §2 D): <b>1</b>, le soglie delle
/// piste del sector (<c>.rw</c>) contro AD 2.12, in metri, testata per testata; <b>2</b>, i nomi delle taxiway dei
/// <c>.txi</c> contro AD 2.8. In sola lettura: per correggere una soglia c'è «Sostituisci» del confronto di AD 2.
/// <para>AD 2.8 non ha un campo nel modello: i nomi si leggono dalla sottosezione com'è stampata (<see cref="Aerodrome.Sections"/>),
/// la voce 2 «Larghezza, superficie e resistenza delle TWY». Sul 2611 (97 scali) arriva in due forme: una voce numerata col
/// valore riga per riga (<c>A 15.0 M ASPH PCN 36/A/X/T</c>), o una tabella con le colonne unite in una riga di celle
/// (<c>Identificativo della TWY Designator of TWY A B BC C …</c>), dove negli scali grandi la colonna porta anche le note
/// (LIRF, LIPE), i nomi composti (<c>AD-AE</c>, LIMC <c>EM-DE-GE- GW-F-…</c>) e i rimandi (<c>H (4)</c>).</para>
/// </summary>
public static partial class GroundDelloScalo
{
    /// <summary>
    /// Le parole maiuscole corte che stanno nella colonna e non sono taxiway. Non «M» né «E»: sono taxiway (LIRL, LIRN); le
    /// larghezze «23 M» si tolgono prima, e le parole italiane delle note sono minuscole.
    /// </summary>
    private static readonly HashSet<string> NonNomi = new(StringComparer.Ordinal)
    {
        "OF", "TWY", "TWYS", "NIL", "ALL", "NOTE", "REMARKS", "AND", "TO", "AT", "IN", "ON", "OR", "BY",
    };

    /// <summary>
    /// Un designatore come lo stampa AD 2.8: una o due lettere e al più due cifre (<c>A</c>, <c>BC</c>, <c>A1</c>, <c>R4</c>).
    /// Anche per le etichette del sector: «MIL4», «RAMP1», «APRON», «100» non hanno la forma di una taxiway.
    /// </summary>
    [GeneratedRegex(@"^[A-Z]{1,2}[0-9]{0,2}$", RegexOptions.CultureInvariant)]
    private static partial Regex RxNome();

    [GeneratedRegex(@"^\s*\d+\)\s*", RegexOptions.CultureInvariant)]
    private static partial Regex RxRimandoInTesta();

    [GeneratedRegex(@"Designator\s+of(?:\s+Width)?\s+TWY\s*(?<nomi>.*)$", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex RxColonnaDeiNomi();

    [GeneratedRegex(@"\s*[-–]\s*", RegexOptions.CultureInvariant)]
    private static partial Regex RxTrattino();

    [GeneratedRegex(@"\s+[0-9]+(?:\.[0-9]+)?\s+M\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxLarghezza();

    /// <summary>Le taxiway di AD 2.8 dello scalo, o null se il testo non ha la sottosezione o la sua voce 2.</summary>
    public static TaxiwayDiAd2? Taxiway(Aerodrome scalo)
    {
        ArgumentNullException.ThrowIfNull(scalo);
        var numero = scalo.Heliport ? "AD 3.8" : "AD 2.8";
        if (scalo.Sections?.FirstOrDefault(s => s.Number == numero) is not { } sezione)
            return null;
        if (sezione.Items?.FirstOrDefault(i => i.Number == "2") is { Value: { } valore })
            return DallaVoce(numero, valore, sezione.Page);
        if (sezione.Rows?.FirstOrDefault(r => r.Count > 2 && r[0] == "2") is { } riga && RxColonnaDeiNomi().Match(riga[2]) is { Success: true } m)
            return DallaColonna(numero, m.Groups["nomi"].Value.Trim(), sezione.Page);
        return null;
    }

    /// <summary>
    /// La voce numerata: sotto le quattro righe della testata (l'ultima finisce con <c>TWY</c>), una taxiway per riga, il nome
    /// in testa. <c>TWY 16.0 M</c> è una taxiway senza nome; <c>TWY N 15.0 M</c> e <c>1) TWY</c> tolgono la parola o il rimando.
    /// </summary>
    private static TaxiwayDiAd2 DallaVoce(string sezione, string valore, AipPage? pagina)
    {
        var righe = valore.Split('\n').Select(r => r.Trim()).ToList();
        var testata = righe.FindIndex(r => r == "TWY" || r.EndsWith("of TWY", StringComparison.Ordinal));
        var nomi = new List<string>();
        bool senzaNome = false, tutte = false;
        var dati = righe.Skip(testata + 1).Where(r => r.Length > 0).ToList();
        foreach (var r in dati)
        {
            var parole = RxRimandoInTesta().Replace(r, "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parole.Length == 0) continue;
            var primo = parole[0];
            if (primo.Equals("TWY", StringComparison.OrdinalIgnoreCase) || primo.Equals("Taxiway", StringComparison.OrdinalIgnoreCase))
            {
                if (parole.Length > 1 && RxNome().IsMatch(parole[1]) && !NonNomi.Contains(parole[1]))
                    nomi.Add(parole[1]);
                else
                    senzaNome = true;
            }
            else if (primo == "ALL")
                tutte = true;
            else if (primo != "NIL" && RxNome().IsMatch(primo))
                nomi.Add(primo);
        }
        return new TaxiwayDiAd2(sezione, Distinti(nomi), senzaNome, tutte, string.Join(" · ", dati), pagina);
    }

    /// <summary>
    /// La colonna di una tabella con le righe unite: i nomi separati da spazi, coi composti spezzati sui trattini (<c>AD-AE</c>
    /// sono due), senza le larghezze (<c>A 23 M</c>), i rimandi fra parentesi e le parole delle note. Dopo <c>HOLDING POSITION</c>
    /// viene un punto d'attesa, non una taxiway (LIME <c>B - EAST OF HOLDING POSITION B5</c>).
    /// </summary>
    private static TaxiwayDiAd2 DallaColonna(string sezione, string colonna, AipPage? pagina)
    {
        var senzaLarghezze = RxLarghezza().Replace(" " + colonna, " ");
        var parole = RxTrattino().Replace(senzaLarghezze, " ").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var nomi = new List<string>();
        for (var i = 0; i < parole.Length; i++)
        {
            var p = parole[i];
            if (p.StartsWith('(') || NonNomi.Contains(p) || !RxNome().IsMatch(p)) continue;
            if (i > 0 && parole[i - 1] == "POSITION") continue;
            nomi.Add(p);
        }
        return new TaxiwayDiAd2(sezione, Distinti(nomi), false, false, colonna, pagina);
    }

    private static List<string> Distinti(IEnumerable<string> nomi) => [.. nomi.Distinct(StringComparer.Ordinal).OrderBy(n => n, OrdineDeiNomi.Uno)];

    /// <summary>I due controlli per lo scalo, sul sector aperto.</summary>
    public static ControlliDelGround Di(Aerodrome scalo, SessioneAperta sessione)
    {
        ArgumentNullException.ThrowIfNull(scalo);
        ArgumentNullException.ThrowIfNull(sessione);
        var (soglie, senza) = Soglie(scalo, sessione);
        return new ControlliDelGround(soglie, senza, Confronta(Taxiway(scalo), scalo.Icao, sessione));
    }

    /// <summary>
    /// Le testate dei <c>.rw</c> dello scalo contro AD 2.12, una riga per file e testata (le FIR possono essere diverse da
    /// <c>itrw.rw</c>), e le piste dell'AIP che nessun <c>.rw</c> ha.
    /// </summary>
    private static (List<SogliaNelGround>, List<string>) Soglie(Aerodrome scalo, SessioneAperta sessione)
    {
        var perDesignatore = scalo.Runways.GroupBy(r => ConfrontoDiAd2.Designatore(r.Designator)).ToDictionary(g => g.Key, g => g.First());
        var soglie = new List<SogliaNelGround>();
        var trovate = new HashSet<string>(StringComparer.Ordinal);
        foreach (var v in ConfrontoDiAd2.Voci<PistaDelSector>(sessione, ".rw").Where(v => v.Dato.IcaoCode.Trim().Equals(scalo.Icao, StringComparison.OrdinalIgnoreCase)))
        {
            var d = v.Dato;
            var pista = $"{d.Designator1.Trim()}/{d.Designator2.Trim()}";
            foreach (var (testata, soglia) in new[] { (d.Designator1, d.Threshold1), (d.Designator2, d.Threshold2) })
            {
                var chiave = ConfrontoDiAd2.Designatore(testata);
                var nellAip = perDesignatore.TryGetValue(chiave, out var aip);
                if (nellAip) trovate.Add(chiave);
                soglie.Add(new SogliaNelGround(pista, testata.Trim(), v.Relativo, v.Record, nellAip,
                    aip?.Threshold is { } t ? ConfrontoDiAd2.Metri(t, soglia) : null));
            }
        }
        var senza = scalo.Runways.Where(r => !trovate.Contains(ConfrontoDiAd2.Designatore(r.Designator))).Select(r => r.Designator).ToList();
        return (soglie, senza);
    }

    private static ConfrontoDelleTaxiway Confronta(TaxiwayDiAd2? aip, string icao, SessioneAperta sessione)
    {
        var etichette = ConfrontoDiAd2.Voci<TaxiwayLabel>(sessione, ".txi")
            .Where(v => v.Dato.IcaoCode.Trim().Equals(icao, StringComparison.OrdinalIgnoreCase) && v.Dato.Name.Trim().Length > 0).ToList();
        var tutte = etichette.Select(v => v.Dato.Name.Trim().ToUpperInvariant()).ToHashSet(StringComparer.Ordinal);
        var nelSector = tutte.Where(n => RxNome().IsMatch(n)).ToHashSet(StringComparer.Ordinal);
        var nellAip = (aip?.Nomi ?? []).ToHashSet(StringComparer.Ordinal);
        List<string> Ordina(IEnumerable<string> nomi) => [.. nomi.OrderBy(n => n, OrdineDeiNomi.Uno)];
        return new ConfrontoDelleTaxiway(
            aip,
            Ordina(nellAip.Intersect(nelSector)),
            Ordina(nellAip.Except(nelSector)),
            Ordina(nelSector.Except(nellAip)),
            [.. tutte.Except(nelSector).Order(StringComparer.Ordinal)],
            [.. etichette.Select(v => v.Relativo).Distinct(StringComparer.OrdinalIgnoreCase)]);
    }

    /// <summary>A, A1, A2, AA, B…: le lettere prima, poi il numero come numero (A2 prima di A10).</summary>
    private sealed class OrdineDeiNomi : IComparer<string>
    {
        public static readonly OrdineDeiNomi Uno = new();

        public int Compare(string? x, string? y)
        {
            var (lx, nx) = Parti(x ?? "");
            var (ly, ny) = Parti(y ?? "");
            var c = string.CompareOrdinal(lx, ly);
            return c != 0 ? c : nx.CompareTo(ny);
        }

        private static (string, int) Parti(string nome)
        {
            var lettere = new string([.. nome.TakeWhile(ch => !char.IsAsciiDigit(ch))]);
            return (lettere, int.TryParse(nome.AsSpan(lettere.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : -1);
        }
    }
}
