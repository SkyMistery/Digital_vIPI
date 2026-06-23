namespace Vipi.Application.Content;

/// <summary>
/// Risolve il "primo online" di una catena handler di trasferimento (F3). Puro/testabile.
///
/// I token della catena (es. "WS2", "ES2", "DTTC") sono codici settore di FIR confinanti, non
/// necessariamente uguali al callsign IVAO. Euristica per stabilire se un token è online:
///  1. match esatto col callsign;
///  2. token uguale a un segmento del callsign (split su '_'): es. "WS2" ↔ "LIMM_WS2_CTR";
///  3. solo per token "lunghi" (≥4 char, es. compositi "LIMM_WS2"): sottostringa del callsign.
/// Il vincolo di lunghezza al punto 3 evita falsi positivi su token cortissimi.
/// </summary>
public static class TransferOnlineResolver
{
    public static ResolvedTransferRow Resolve(TransferRow row, IReadOnlySet<string> online)
    {
        foreach (var token in row.HandlerChain)
        {
            if (IsTokenOnline(token, online))
                return new ResolvedTransferRow { Row = row, ResolvedHandler = token, IsOnline = true };
        }

        // Nessun handler della catena online: ricade sul fallback standard (UNICOM/Confine/...).
        return new ResolvedTransferRow { Row = row, ResolvedHandler = row.StandardFallback, IsOnline = false };
    }

    private static bool IsTokenOnline(string token, IReadOnlySet<string> online)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;

        foreach (var callsign in online)
        {
            // 1. match esatto.
            if (callsign.Equals(token, StringComparison.OrdinalIgnoreCase)) return true;
            // 2. token = segmento del callsign (LIMM_WS2_CTR → {LIMM, WS2, CTR}).
            foreach (var seg in callsign.Split('_', StringSplitOptions.RemoveEmptyEntries))
                if (seg.Equals(token, StringComparison.OrdinalIgnoreCase)) return true;
            // 3. solo token lunghi (compositi): sottostringa. Evita falsi positivi su token corti.
            if (token.Length >= 4 && callsign.Contains(token, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
