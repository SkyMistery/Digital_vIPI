using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Awos;

/// <summary>
/// Le tre decisioni del quadro che <b>non</b> hanno bisogno di parlare con niente: chi puo aprirlo, quali
/// scali elencare, e quale ATIS conta.
///
/// <para>⚠️ Stanno qui e non dentro <see cref="AwosService"/> perche il servizio, per essere provato,
/// pretenderebbe un doppio di <c>IAirportEditingService</c> — venti membri per verificarne uno. Il cancello
/// e la parte con conseguenze, e restava senza rete (revisione del 12 settembre 2026, sera).</para>
/// </summary>
public static class AwosGate
{
    /// <summary>
    /// I due documenti dello scalo, filtrati col cancello di <b>ogni</b> elenco pubblico: release AIRAC
    /// effettiva e documento non nascosto (doc 10 §3f).
    ///
    /// <para>⚠️ Il cancello guarda i <b>documenti</b>, non la categoria dello scalo: cambiare categoria non
    /// tocca i documenti (carta 2026-09-11-categorie-aeroporto.md), e un vSOP pubblicato su un campo
    /// diventato «civile» resta leggibile finche qualcuno non lo nasconde. Filtrare per categoria renderebbe
    /// il quadro irraggiungibile su uno scalo il cui documento invece si apre.</para>
    /// </summary>
    public static (bool Vipi, bool Vsop) Pubblicati(IEnumerable<ManagedDoc> documenti, string icao)
    {
        var docs = documenti.Where(m => Pubblico(m) && Eq(m.Scope, icao)).ToList();
        return (docs.Any(m => m.Kind == ReleaseTargetType.Airport),
                docs.Any(m => m.Kind == ReleaseTargetType.AirportMil));
    }

    /// <summary>
    /// Gli scali per cui il quadro si apre, in ordine di ICAO.
    /// <para>⚠️ E lo <b>stesso</b> insieme del cancello: un selettore che elencasse uno scalo che poi rifiuta
    /// di aprirsi sarebbe un gesto che non fa niente.</para>
    /// </summary>
    public static IReadOnlyList<AwosAirport> Elenco(IEnumerable<ManagedDoc> documenti) => documenti
        .Where(m => Pubblico(m)
                    && m.Kind is ReleaseTargetType.Airport or ReleaseTargetType.AirportMil
                    && m.Scope.Length == 4)
        .GroupBy(m => m.Scope.ToUpperInvariant(), StringComparer.OrdinalIgnoreCase)
        .Select(g => new AwosAirport(
            g.Key,
            // Il nome viene dal titolo del documento: e gia quello che il pubblico legge altrove, e non
            // costa una seconda interrogazione all'anagrafica per una tendina.
            NomeDalTitolo(g.OrderBy(m => m.Kind == ReleaseTargetType.Airport ? 0 : 1).First().Title, g.Key),
            g.Any(m => m.Kind == ReleaseTargetType.Airport),
            g.Any(m => m.Kind == ReleaseTargetType.AirportMil)))
        .OrderBy(a => a.Icao, StringComparer.Ordinal)
        .ToList();

    /// <summary>
    /// L'ATIS in onda su questo scalo, fra le postazioni online.
    ///
    /// <para>⚠️ Si preferisce la postazione <c>_ATIS</c>, poi la torre, poi qualunque altra dello scalo che
    /// trasmetta: quando su un campo ci sono ATIS e torre insieme, quella che parla ai piloti in anticipo e
    /// la prima, e le due possono dire lettere diverse per qualche minuto dopo un cambio.</para>
    ///
    /// <para>Nessuno online, o nessuno con un ATIS leggibile: <c>null</c>. Il quadro scrive «—», che e
    /// vero — e non una lettera vecchia tenuta li perche faceva scena.</para>
    /// </summary>
    public static AwosAtis? Atis(IEnumerable<OnlineAtc> online, string icao)
    {
        var scelto = online
            .Where(a => a.Callsign.StartsWith(icao + "_", StringComparison.OrdinalIgnoreCase))
            .Where(a => a.AtisLetter is not null || a.AtisArrRunways is not null || a.AtisDepRunways is not null)
            .OrderBy(a => a.Callsign.EndsWith("_ATIS", StringComparison.OrdinalIgnoreCase) ? 0
                        : a.Callsign.EndsWith("_TWR", StringComparison.OrdinalIgnoreCase) ? 1 : 2)
            .ThenBy(a => a.Callsign, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return scelto is null ? null
            : new AwosAtis(scelto.Callsign, scelto.AtisLetter, scelto.AtisTimeRaw, scelto.AtisText,
                           scelto.AtisArrRunways, scelto.AtisDepRunways);
    }

    /// <summary>Le piste di un campo dell'ATIS, spezzate: «16L/16R» -> due.</summary>
    public static IReadOnlyList<string>? Piste(string? csv) => csv is null ? null : csv
        .Split(new[] { '/', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Il nome dello scalo dal titolo del documento: «vIPI — LIBC Crotone» -> «Crotone».
    /// <para>La tendina scrive gia l'ICAO da se, e ripeterlo due volte in una riga larga cosi ruba lo spazio
    /// al nome, che e la parte per cui la si legge.</para>
    /// </summary>
    public static string NomeDalTitolo(string titolo, string icao)
    {
        var t = (titolo ?? "").Trim();
        foreach (var prefisso in new[] { "vIPI", "vSOP", "vLOA" })
            if (t.StartsWith(prefisso, StringComparison.OrdinalIgnoreCase))
                t = t[prefisso.Length..].TrimStart(SEPARATORI);
        if (t.StartsWith(icao, StringComparison.OrdinalIgnoreCase))
            t = t[icao.Length..].TrimStart(SEPARATORI);
        return t.Length == 0 ? icao : t;
    }

    private static readonly char[] SEPARATORI = { ' ', '—', '-', '–', ':' };

    private static bool Pubblico(ManagedDoc m) => m.HasEffectiveRelease && !m.IsHidden;
    private static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
