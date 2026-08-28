using Vipi.Application.Aor;
using static Vipi.Application.Messaggio;

namespace Vipi.Application.Content;

/// <summary>Natura del callsign estero aggiunto a mano: postazione d'aeroporto (APP/DEP/TWR/GND/DEL) oppure
/// center/subcenter (CTR/FSS). Determina l'endpoint sorgente usato per la verifica.</summary>
public enum ForeignSectorKind { Airport, Center }

/// <summary>
/// Parsing puro (senza IO) del callsign di un settore estero aggiunto a mano nella pagina confinanti
/// (es. <c>LGKR_APP</c>, <c>LGGG_N_CTR</c>). Estrae l'ICAO (prime 4 lettere prima del primo <c>_</c>),
/// il suffisso di posizione (ultimo pezzo) e la <see cref="ForeignSectorKind"/> per instradare la verifica.
/// Cuore deterministico isolato da <c>NeighbourImportService</c> per testabilità (FEATURE-PROCESS §post-flight).
/// </summary>
public sealed record ForeignSectorCallsign(string Callsign, string Icao, string Suffix, ForeignSectorKind Kind)
{
    private static readonly HashSet<string> AirportSuffixes =
        new(StringComparer.OrdinalIgnoreCase) { "APP", "DEP", "TWR", "GND", "DEL" };
    private static readonly HashSet<string> CenterSuffixes =
        new(StringComparer.OrdinalIgnoreCase) { "CTR", "FSS" };

    /// <summary>Normalizza e valida il callsign. Lancia <see cref="ValidationException"/> con motivo leggibile
    /// se la forma non è un settore riconoscibile (manca l'underscore, ICAO non a 4 caratteri, suffisso ignoto).</summary>
    public static ForeignSectorCallsign Parse(string? raw)
    {
        var cs = (raw ?? "").Trim().ToUpperInvariant();
        if (cs.Length == 0)
            throw new ValidationException("Callsign obbligatorio.");

        var parts = cs.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            throw new ValidationException(Lingua($"Callsign «{cs}» non valido: atteso ICAO_…_POS (es. LGKR_APP, LGGG_N_CTR).", $"Callsign «{cs}» is not valid: ICAO_…_POS expected (e.g. LGKR_APP, LGGG_N_CTR)."));

        var icao = parts[0];
        if (icao.Length != 4)
            throw new ValidationException(Lingua($"ICAO «{icao}» non valido: attesi 4 caratteri (prime lettere del callsign).", $"ICAO «{icao}» is not valid: 4 characters expected (the first letters of the callsign)."));

        var suffix = parts[^1];
        var kind =
            AirportSuffixes.Contains(suffix) ? ForeignSectorKind.Airport :
            CenterSuffixes.Contains(suffix) ? ForeignSectorKind.Center :
            throw new ValidationException(
                $"Suffisso «{suffix}» non gestito: ammessi APP/DEP/TWR/GND/DEL (aeroporto) o CTR/FSS (center).");

        return new ForeignSectorCallsign(cs, icao, suffix, kind);
    }
}
