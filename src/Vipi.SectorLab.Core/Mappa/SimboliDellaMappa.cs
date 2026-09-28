using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Core.Mappa;

/// <summary>
/// I simboli dei punti sulla mappa, dal <c>.sym</c> del master (<c>[SYMBOLS]</c>, <c>symbols.sym</c>): il committente li
/// vuole dal sector, non dai profili (28 settembre). Lotto «Subito» slice 4d.
/// <para>🟡 <b>Quale simbolo per quale punto il sector non lo dice</b>: Aurora sceglie i simboli del sector per numero,
/// e come li numeri è ancora da provare (T3). L'abbinamento qui sotto è per NOME del simbolo (il commento sopra i
/// pixel) ed è una proposta dell'agente, da confermare accanto ad Aurora; un nome che il file non ha → il punto resta
/// un cerchio.</para>
/// </summary>
public sealed class SimboliDellaMappa
{
    /// <summary>Tipo del punto (<see cref="FormaDellaMappa.Punto"/>) → nome del simbolo nel <c>.sym</c>.</summary>
    public static IReadOnlyDictionary<string, string> Abbinamenti { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["FIX"] = "FIX vuoto",              // tipo non scritto
        ["FIX:0"] = "FIX vuoto",            // in rotta (ENR)
        ["FIX:1"] = "TERM",                 // terminale
        ["FIX:2"] = "FIX pieno",            // in rotta e terminale
        ["FIX:3"] = "FIX vuoto piccolo",    // nascosto (in Aurora non si vede; qui piccolo)
        ["VOR"] = "VOR",
        ["VOR:0"] = "VOR",
        ["VOR:1"] = "VOR2",                 // VOR/DME
        ["VOR:2"] = "VOR3",                 // VORTAC
        ["VOR:3"] = "TAC2",                 // TACAN
        ["VOR:4"] = "VOR2",                 // DME
        ["NDB"] = "NDB",
        ["VFR"] = "VFR",
        ["APT"] = "APT",
    };

    private readonly Dictionary<string, int> _perNome = new(StringComparer.OrdinalIgnoreCase);

    public SimboliDellaMappa(IReadOnlyList<SimboloDelSector> simboli)
    {
        Simboli = simboli ?? throw new ArgumentNullException(nameof(simboli));
        for (int i = 0; i < simboli.Count; i++)
        {
            if (simboli[i].Nome is { } nome)
                _perNome.TryAdd(nome.Trim(), i);
        }
    }

    /// <summary>I simboli del file, nell'ordine.</summary>
    public IReadOnlyList<SimboloDelSector> Simboli { get; }

    /// <summary>Il simbolo di un punto (indice in <see cref="Simboli"/>), o null: resta un cerchio.</summary>
    public int? Di(FormaDellaMappa forma)
    {
        ArgumentNullException.ThrowIfNull(forma);
        if (forma.Punto is not { } punto)
            return null;
        if (!Abbinamenti.TryGetValue(punto, out string? nome))
        {
            // Un tipo che la tabella non ha (FIX:7): il simbolo della famiglia.
            int dueP = punto.IndexOf(':', StringComparison.Ordinal);
            if (dueP < 0 || !Abbinamenti.TryGetValue(punto[..dueP], out nome))
                return null;
        }

        return _perNome.TryGetValue(nome, out int indice) ? indice : null;
    }

    /// <summary>
    /// I simboli dei <c>.sym</c> che il master carica, nell'ordine dei carichi (dal disco: il Lab li tiene come testo).
    /// Null se il master non ne carica.
    /// </summary>
    public static SimboliDellaMappa? DelMaster(SessioneAperta sessione, CatalogoDeiPunti? master, IWarningCollector avvisi)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        if (master is null)
            return null;

        var simboli = new List<SimboloDelSector>();
        foreach (string relativo in master.FileCaricati.Where(f => f.EndsWith(".sym", StringComparison.OrdinalIgnoreCase)))
        {
            string percorso = sessione.Cartella.Assoluto(relativo);
            if (File.Exists(percorso))
                simboli.AddRange(new SymParser(avvisi).Parse(percorso));
        }

        return simboli.Count == 0 ? null : new SimboliDellaMappa(simboli);
    }
}
