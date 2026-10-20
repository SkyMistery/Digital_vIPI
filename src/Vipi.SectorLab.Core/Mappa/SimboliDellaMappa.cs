using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Core.Mappa;

/// <summary>
/// I simboli dei punti sulla mappa: quelli di Aurora, uno per ruolo (lotto «Subito» slice 17b, decisione del
/// committente del 17 ottobre). Dal 20 ottobre si leggono dal profilo scelto nella cartella di Aurora
/// (<see cref="DallaCartellaDiAurora"/>); i disegni qui sotto restano quelli di base, quando un profilo non c'è.
/// <para>Perché non quelli del <c>.sym</c> (slice 4d, abbinati per nome): la prova T3 ha mostrato che Aurora disegna i
/// punti coi simboli del <b>profilo</b> (<c>SYMBOLS_&lt;RUOLO&gt;=</c> in un <c>.cpr</c>, scelti nella linguetta SYMBOLS),
/// e che il <c>.sym</c> è soltanto la tavolozza da cui si sceglie. I 15 <c>.cpr</c> del sector non hanno
/// <c>SYMBOLS_</c>: i disegni qui sotto vengono dai profili installati di Aurora (misurati il 17 ottobre: uguali in 37
/// profili su 38; il 38°, <c>LIPP_CE1_CTR</c>, ha i suoi fix e radioassistenze), copiati da <c>BASIC_RADAR.cpr</c>.</para>
/// <para>🟡 Proposte dell'agente, che il committente il 17 ottobre ha lasciato così «per ora»: un fix di confine (5° campo <c>1</c>) prende il simbolo
/// del confine qualunque sia il suo tipo; un fix nascosto (tipo 3), che Aurora non mostra, ha quello del fix in rotta; il
/// punto VFR obbligatorio (tipo 0) ha <c>FIX_VFRN</c> (nei profili è uguale a <c>FIX_VFR</c>).</para>
/// </summary>
public sealed class SimboliDellaMappa
{
    /// <summary>I ruoli di Aurora e il loro disegno di base (le colonne come nel <c>.sym</c>, da <c>BASIC_RADAR.cpr</c>).</summary>
    private static readonly IReadOnlyList<(string Ruolo, string Pixel)> DiBase =
    [
        ("FIX_ENR", "0000000000000;0000000000000;0000011000000;0000010100000;0000010010000;0000010001000;0000010000100;0000010001000;0000010010000;0000010100000;0000011000000;0000000000000;0000000000000;"),
        ("FIX_TERM", "0000000000000;0000000100000;0000001100000;0000011100000;0000111100000;0001111100000;0000111100000;0000011100000;0000001100000;0000000100000;0000000000000;0000000000000;0000000000000;"),
        ("FIX_ENR_TERM", "0000000000000;0000000000000;0000000000000;0000001000000;0000010100000;0000100010000;0001001001000;0000100010000;0000010100000;0000001000000;0000000000000;0000000000000;0000000000000;"),
        ("FIX_BOUND", "0000000000000;0000000000000;0000110000000;0000101000000;0000100100000;0000100010000;0000100100000;0000101000000;0000110000000;0000000000000;0000000000000;0000000000000;0000000000000;"),
        ("FIX_VOR", "0000001110000;0000010101000;0000100000100;0001010001010;0010000000001;0011100100111;0011100100111;0010000000001;0001010001010;0000100000100;0000010101000;0000001110000;0000000000000;"),
        ("FIX_VORDME", "0000111110000;0001001001000;0010101010100;0100011100010;1010100010101;1001001001001;1111010101111;1001001001001;1010100010101;0100011100010;0010101010100;0001001001000;0000111110000;"),
        ("FIX_VORTAC", "0000111110000;0001001001000;0010101010100;0100011100010;1010100010101;1001000001001;1111001001111;1001000001001;1010100010101;0100011100010;0010101010100;0001001001000;0000111110000;"),
        ("FIX_TACAN", "0000001000000;0001011101000;0000100010000;0101000001010;0010000000100;0100000000010;1100001000011;0100000000010;0010000000100;0101000001010;0000100010000;0001011101000;0000001000000;"),
        ("FIX_DME", "0000111110000;0001001001000;0010101010100;0100011100010;1010100010101;1001000001001;1111001001111;1001000001001;1010100010101;0100011100010;0010101010100;0001001001000;0000111110000;"),
        ("FIX_NDB", "0000000000000;0000010001000;0000000100000;0001010001010;0000000100000;0000101010100;0000000100000;0001010001010;0000000100000;0000010001000;0000000000000;0000000000000;0000000000000;"),
        ("FIX_VFR", "0000000000000;0000000000000;0000000000000;0000010000000;0000111000000;0001111100000;0011111110000;0001111100000;0000111000000;0000010000000;0000000000000;0000000000000;0000000000000;"),
        ("FIX_VFRN", "0000000000000;0000000000000;0000000000000;0000010000000;0000111000000;0001111100000;0011111110000;0001111100000;0000111000000;0000010000000;0000000000000;0000000000000;0000000000000;"),
        ("FIX_VFRH", "0000111110000;0001000001000;0010000000100;0100000110010;1000011110001;1000100010001;1001110110001;1000100010001;1000011110001;0100000110010;0010000000100;0001000001000;0000111110000;"),
        ("FIX_VFRA", "1111111111111;1000000000001;1000011111101;1011110000101;1010010000101;1010010000101;1011110000101;1000010000101;1000010000101;1000011111101;1000000000001;1000000000001;1111111111111;"),
        ("AIRPORT", "0000000000000;0100000000000;0010000000000;0001011100000;0000100010000;0001010001000;0001001001000;0001000101000;0000100010000;0000011101000;0000000000100;0000000000010;0000000000000;"),
        ("HELIPORT", "0000000000000;0000000000000;0000011100000;0000100010000;0001000001000;0010011100100;0010001000100;0010011100100;0001000001000;0000100010000;0000011100000;0000000000000;0000000000000;"),
        ("MILITARY", "0000000000000;0100000000000;0010000000000;0001011100000;0000111110000;0001111111000;0001111111000;0001111111000;0000111110000;0000011101000;0000000000100;0000000000010;0000000000000;"),
        ("PRIVATE", "0000000000000;0000000000000;0000011100000;0000100010000;0001000001000;0010111110100;0010101000100;0010111000100;0001000001000;0000100010000;0000011100000;0000000000000;0000000000000;"),
        ("UNCONTROLLED", "0000000000000;0000000000000;0000000000000;0000011100000;0000100010000;0001000001000;0001000001000;0001000001000;0000100010000;0000011100000;0000000000000;0000000000000;0000000000000;"),
    ];

    /// <summary>Tipo del punto (<see cref="FormaDellaMappa.Punto"/>) → ruolo di Aurora.</summary>
    public static IReadOnlyDictionary<string, string> Abbinamenti { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["FIX"] = "FIX_ENR",                // tipo non scritto
        ["FIX:0"] = "FIX_ENR",              // in rotta (ENR)
        ["FIX:1"] = "FIX_TERM",             // terminale
        ["FIX:2"] = "FIX_ENR_TERM",         // in rotta e terminale
        ["FIX:3"] = "FIX_ENR",              // 🟡 nascosto: Aurora non lo mostra, il Lab sì
        ["FIX:B"] = "FIX_BOUND",            // 🟡 di confine (5° campo 1), qualunque sia il tipo
        ["VOR"] = "FIX_VOR",
        ["VOR:0"] = "FIX_VOR",
        ["VOR:1"] = "FIX_VORDME",
        ["VOR:2"] = "FIX_VORTAC",
        ["VOR:3"] = "FIX_TACAN",
        ["VOR:4"] = "FIX_DME",
        ["NDB"] = "FIX_NDB",
        ["VFR"] = "FIX_VFR",                // tipo non scritto (i file italiani)
        ["VFR:0"] = "FIX_VFRN",             // 🟡 obbligatorio
        ["VFR:1"] = "FIX_VFR",
        ["VFR:2"] = "FIX_VFRH",             // elicotteri
        ["VFR:3"] = "FIX_VFRA",             // area
        ["APT"] = "AIRPORT",                // tipo fuori dal manuale
        ["APT:0"] = "AIRPORT",              // controllato
        ["APT:1"] = "HELIPORT",
        ["APT:2"] = "MILITARY",
        ["APT:3"] = "PRIVATE",
        ["APT:4"] = "UNCONTROLLED",
    };

    /// <summary>I simboli di base di Aurora, quando non c'è un profilo da cui leggerli (i test, una cartella di Aurora vuota).</summary>
    public static SimboliDellaMappa DiAurora { get; } = new(DiBase, null);

    /// <summary>Il profilo da cui si leggono i simboli quando l'AOD non ne ha scelto un altro: quello dei disegni di base.</summary>
    public const string ProfiloDiBase = "BASIC_RADAR.cpr";

    private readonly Dictionary<string, int> _perRuolo = new(StringComparer.Ordinal);

    private SimboliDellaMappa(IReadOnlyList<(string Ruolo, string Pixel)> disegni, string? profilo)
    {
        Profilo = profilo;
        Simboli = [.. disegni.Select((s, i) => new SimboloDelSector(i + 1, s.Ruolo, s.Pixel.Split(';', StringSplitOptions.RemoveEmptyEntries), 0))];
        for (int i = 0; i < Simboli.Count; i++)
            _perRuolo[Simboli[i].Nome!] = i;
    }

    /// <summary>Il profilo di Aurora da cui vengono i disegni; null per quelli di base.</summary>
    public string? Profilo { get; }

    /// <summary>I profili di Aurora (<c>Profiles\*.cpr</c>) della cartella di Aurora, in ordine; vuoto se non ce n'è.</summary>
    public static IReadOnlyList<string> ProfiliDi(string? cartellaDiAurora)
    {
        string? dove = string.IsNullOrWhiteSpace(cartellaDiAurora) ? null : Path.Combine(cartellaDiAurora, "Profiles");
        return dove is not null && Directory.Exists(dove)
            ? [.. Directory.GetFiles(dove, "*.cpr").Select(Path.GetFileName).OfType<string>().Order(StringComparer.OrdinalIgnoreCase)]
            : [];
    }

    /// <summary>
    /// I simboli dei punti come li disegna Aurora (committente, 20 ottobre: «devi usare i simboli di Aurora così la
    /// visualizzazione è uguale»): dal profilo scelto della cartella di Aurora (quello ricordato se c'è ancora, se no
    /// <see cref="ProfiloDiBase"/>, se no il primo). Senza profili, quelli di base.
    /// </summary>
    public static SimboliDellaMappa DallaCartellaDiAurora(string? cartellaDiAurora, string? ricordato)
    {
        var profili = ProfiliDi(cartellaDiAurora);
        string? scelto = profili.FirstOrDefault(p => string.Equals(p, ricordato, StringComparison.OrdinalIgnoreCase))
                         ?? profili.FirstOrDefault(p => string.Equals(p, ProfiloDiBase, StringComparison.OrdinalIgnoreCase))
                         ?? profili.FirstOrDefault();
        return scelto is null ? DiAurora : DalProfilo(Path.Combine(cartellaDiAurora!, "Profiles", scelto));
    }

    /// <summary>
    /// I simboli di un profilo <c>.cpr</c>. 🔴 Del file si leggono SOLO le righe <c>SYMBOLS_&lt;RUOLO&gt;=</c> dei ruoli dei
    /// punti: un profilo tiene anche la sezione <c>[Connection]</c>, che non si guarda. Un ruolo che manca o non è un
    /// disegno 13×13 resta quello di base; un profilo che non si legge dà quelli di base.
    /// </summary>
    public static SimboliDellaMappa DalProfilo(string fileCpr)
    {
        var letti = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            foreach (string riga in File.ReadLines(fileCpr))
            {
                if (!riga.StartsWith("SYMBOLS_", StringComparison.Ordinal))
                    continue;
                int uguale = riga.IndexOf('=', StringComparison.Ordinal);
                if (uguale < 0)
                    continue;
                string ruolo = riga[8..uguale];
                if (DiBase.Any(d => d.Ruolo == ruolo) && EUnDisegno(riga[(uguale + 1)..]))
                    letti.TryAdd(ruolo, riga[(uguale + 1)..].Trim());
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return DiAurora;
        }

        return new SimboliDellaMappa([.. DiBase.Select(d => (d.Ruolo, letti.GetValueOrDefault(d.Ruolo) ?? d.Pixel))], Path.GetFileName(fileCpr));
    }

    private static bool EUnDisegno(string pixel)
    {
        var colonne = pixel.Trim().Split(';', StringSplitOptions.RemoveEmptyEntries);
        return colonne.Length == SimboloDelSector.Lato
               && colonne.All(c => c.Length == SimboloDelSector.Lato && c.All(x => x is '0' or '1'));
    }

    /// <summary>I simboli, uno per ruolo, col nome del ruolo (<c>FIX_ENR</c>…).</summary>
    public IReadOnlyList<SimboloDelSector> Simboli { get; }

    /// <summary>Il simbolo di un punto (indice in <see cref="Simboli"/>), o null: resta un cerchio.</summary>
    public int? Di(FormaDellaMappa forma)
    {
        ArgumentNullException.ThrowIfNull(forma);
        if (forma.Punto is not { } punto)
            return null;
        if (!Abbinamenti.TryGetValue(punto, out string? ruolo))
        {
            // Un tipo che la tabella non ha (FIX:7): il simbolo della famiglia.
            int dueP = punto.IndexOf(':', StringComparison.Ordinal);
            if (dueP < 0 || !Abbinamenti.TryGetValue(punto[..dueP], out ruolo))
                return null;
        }

        return _perRuolo[ruolo];
    }
}
