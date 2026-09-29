using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Models;

/// <summary>
/// An ATC position aggregated from all .frq files (global itfreq.frq + FIR-specific).
/// Duplicate position codes across files are merged via <see cref="Sources"/>.
/// </summary>
public sealed class AtcPosition
{
    public string Code { get; set; } = string.Empty;       // e.g. "LIRR_NE_CTR"
    public decimal FrequencyMhz { get; set; }
    public IList<Transfer> TransferList { get; } = new List<Transfer>();
    public string? Profile { get; set; }                   // .cpr file reference; null when the line stops at the transfer list
    public string? AtisFile { get; set; }
    public bool BlockCpdlc { get; set; }
    public string? DatisFile { get; set; }

    /// <summary>
    /// Il 7° campo, il file <c>.loa</c> (manuale IVAO, «LOA &amp; XFL»: livelli di trasferimento per punto e settore),
    /// com'è scritto; null se vuoto o assente. Prima era un segnaposto sempre vuoto (lotto «Subito» slice 11a).
    /// </summary>
    public string? Loa { get; set; }

    /// <summary>
    /// Le posizioni o gli ICAO inclusi nei trasferimenti, separati da uno spazio (slice 11a, M1). Scriverli rimette la
    /// lista in ordine: prima gli inclusi, poi gli esclusi — un include dopo un escluso Aurora non lo legge (manuale).
    /// </summary>
    public string Inclusi
    {
        get => string.Join(" ", TransferList.Where(t => !t.IsNegative).Select(t => t.PositionCode));
        set => Riordina(Voci(value, esclusi: false), TransferList.Where(t => t.IsNegative).Select(t => t.PositionCode).ToList());
    }

    /// <summary>Le posizioni escluse dai trasferimenti, senza il <c>-</c> (che si può scrivere lo stesso), separate da uno spazio.</summary>
    public string Esclusi
    {
        get => string.Join(" ", TransferList.Where(t => t.IsNegative).Select(t => t.PositionCode));
        set => Riordina(TransferList.Where(t => !t.IsNegative).Select(t => t.PositionCode).ToList(), Voci(value, esclusi: true));
    }

    private void Riordina(List<string> inclusi, List<string> esclusi)
    {
        TransferList.Clear();
        foreach (string voce in inclusi)
            TransferList.Add(new Transfer { PositionCode = voce });
        foreach (string voce in esclusi)
            TransferList.Add(new Transfer { PositionCode = voce, IsNegative = true });
    }

    private static List<string> Voci(string? testo, bool esclusi)
    {
        var voci = new List<string>();
        foreach (string voce in (testo ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (voce.Contains(';', StringComparison.Ordinal))
                throw new ArgumentException("Le voci si separano con uno spazio: il «;» chiude il campo.", nameof(testo));
            if (voce.StartsWith('-') && !esclusi)
                throw new ArgumentException($"«{voce}» ha il «-» degli esclusi: va nell'altra lista.", nameof(testo));
            string nome = voce.TrimStart('-');
            if (nome.Length > 0)
                voci.Add(nome);
        }

        return voci;
    }

    /// <summary>itfreq.frq + all FIR-specific .frq files that contain this position code.</summary>
    public IList<SourceRef> Sources { get; } = new List<SourceRef>();

    /// <summary>True when divergent values for this position exist across .frq files (ARCHITECTURE §8.2).</summary>
    public bool HasConflict { get; set; }
}

/// <summary>A transfer-list entry; <see cref="IsNegative"/> when prefixed with '-' in the file.</summary>
public sealed class Transfer
{
    public string PositionCode { get; set; } = string.Empty;
    public bool IsNegative { get; set; }
}
