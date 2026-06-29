namespace Vipi.Application.Content;

/// <summary>
/// Risolve il ricevente "primo online" di un punto di trasferimento (vista operativa/Ridotta). Puro/testabile.
///
/// I <paramref name="candidates"/> sono callsign in ordine di priorità: [ricevente nominale, suoi antenati
/// di copertura] (risalita gerarchia <c>ParentCallsign</c>, cross-ACC, costruita dal chiamante).
/// Euristica callsign↔candidato: 1) match esatto; 2) candidato = segmento del callsign (split '_');
/// 3) solo candidati lunghi (≥4) per sottostringa. Se nessuno è online → <see cref="Unicom"/>.
/// </summary>
public static class TransferOnlineResolver
{
    /// <summary>Etichetta terminale: nessun settore della catena è online → il traffico va su UNICOM.</summary>
    public const string Unicom = "UNICOM";

    public static (string Handler, bool IsOnline) Resolve(
        IReadOnlyList<string> candidates, IReadOnlySet<string> online)
    {
        var hit = FirstOnline(candidates, online);
        return hit is null ? (Unicom, false) : (hit, true);
    }

    /// <summary>Primo candidato online (in ordine di priorità), o null se nessuno è online.</summary>
    public static string? FirstOnline(IReadOnlyList<string> candidates, IReadOnlySet<string> online)
    {
        foreach (var c in candidates)
            if (IsOnline(c, online))
                return c;
        return null;
    }

    private static bool IsOnline(string candidate, IReadOnlySet<string> online)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return false;
        foreach (var callsign in online)
        {
            if (callsign.Equals(candidate, StringComparison.OrdinalIgnoreCase)) return true;
            foreach (var seg in callsign.Split('_', StringSplitOptions.RemoveEmptyEntries))
                if (seg.Equals(candidate, StringComparison.OrdinalIgnoreCase)) return true;
            if (candidate.Length >= 4 && callsign.Contains(candidate, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
