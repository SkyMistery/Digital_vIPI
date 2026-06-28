using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Auth;

namespace Vipi.Application.Content;

/// <summary>
/// Use-case di authoring del profilo strutturato dell'aeroporto (regole pista, quote di transizione,
/// frequenze+link, piste, SID) e rigenerazione del documento. Letture libere (servono anche al viewer);
/// scritture FIR-gated via <see cref="IEditAuthorizationService"/>. Validazioni hard sugli input.
/// </summary>
public interface IAirportProfileService
{
    /// <summary>Lettura per il viewer (nessuna guardia): regole pista + link frequenze + resto.</summary>
    Task<AirportProfileData?> LoadForViewAsync(string icao, CancellationToken ct = default);
    /// <summary>Lettura per l'editor: richiede il permesso di editare la FIR dell'aeroporto.</summary>
    Task<AirportProfileData?> LoadForEditAsync(string icao, CancellationToken ct = default);

    /// <summary>Policy di import globale (per editor e viewer): quali categorie sono di sorgente (sola lettura).</summary>
    Task<ImportPolicySnapshot> GetImportPolicyAsync(CancellationToken ct = default);

    Task<IReadOnlyList<LinkableFrequencyRow>> ListLinkableFrequenciesAsync(CancellationToken ct = default);

    Task SetTransitionAltitudeAsync(string icao, int? ta, CancellationToken ct = default);
    Task SaveTransitionLevelsAsync(string icao, IReadOnlyList<TlRow> rows, CancellationToken ct = default);
    Task SaveRunwaysAsync(string icao, IReadOnlyList<RunwayRow> rows, CancellationToken ct = default);
    Task SaveRunwayRulesAsync(string icao, IReadOnlyList<RunwayRuleRow> rows, CancellationToken ct = default);
    Task SaveSidsAsync(string icao, IReadOnlyList<SidRow> rows, CancellationToken ct = default);
    Task SaveFrequencyLinksAsync(string icao, IReadOnlyList<int> sourceFrequencyIds, CancellationToken ct = default);

    /// <summary>Re-importa da IVAO (merge mirato): aggiorna TA/ATIS/piste, preserva il lavoro editoriale.</summary>
    Task ReimportFromSourceAsync(string icao, CancellationToken ct = default);
    /// <summary>Rigenera e ripubblica le sezioni gestite del documento dalle entità. Ritorna l'id documento.</summary>
    Task<int> RebuildDocumentAsync(string icao, CancellationToken ct = default);
}

/// <inheritdoc cref="IAirportProfileService"/>
public sealed class AirportProfileService : IAirportProfileService
{
    private readonly IAirportProfileRepository _repo;
    private readonly IEditAuthorizationService _authz;
    private readonly IAirportDirectory _directory;
    private readonly IAirportDetailProvider _details;
    private readonly IImportPolicyStore _policy;

    public AirportProfileService(IAirportProfileRepository repo, IEditAuthorizationService authz,
        IAirportDirectory directory, IAirportDetailProvider details, IImportPolicyStore policy)
    {
        _repo = repo;
        _authz = authz;
        _directory = directory;
        _details = details;
        _policy = policy;
    }

    public Task<ImportPolicySnapshot> GetImportPolicyAsync(CancellationToken ct = default) => _policy.GetAsync(ct);

    public Task<AirportProfileData?> LoadForViewAsync(string icao, CancellationToken ct = default) =>
        _repo.LoadAsync(Norm(icao), ct);

    public async Task<AirportProfileData?> LoadForEditAsync(string icao, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(icao, ct);
        return await _repo.LoadAsync(Norm(icao), ct);
    }

    public Task<IReadOnlyList<LinkableFrequencyRow>> ListLinkableFrequenciesAsync(CancellationToken ct = default) =>
        _repo.ListLinkableFrequenciesAsync(ct);

    public async Task SetTransitionAltitudeAsync(string icao, int? ta, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(icao, ct);
        if ((await _policy.GetAsync(ct)).TransitionAltitude)
            throw new ValidationException("Transition Altitude è gestita dalla sorgente (sola lettura). Per modificarla, escludila in «Sorgenti dati».");
        if (ta is < 0 or > 60000) throw new ValidationException("Transition Altitude non valida.");
        await _repo.SetTransitionAltitudeAsync(Norm(icao), ta, ct);
    }

    public async Task SaveTransitionLevelsAsync(string icao, IReadOnlyList<TlRow> rows, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(icao, ct);
        foreach (var r in rows)
        {
            if (string.IsNullOrWhiteSpace(r.Level)) throw new ValidationException("Transition Level obbligatorio per ogni riga.");
            if (r.QnhFrom is int a && r.QnhTo is int b && a > b) throw new ValidationException("Intervallo QNH invertito (From > To).");
        }
        await _repo.SaveTransitionLevelsAsync(Norm(icao), rows, ct);
    }

    public async Task SaveRunwaysAsync(string icao, IReadOnlyList<RunwayRow> rows, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(icao, ct);
        foreach (var r in rows)
            if (string.IsNullOrWhiteSpace(r.Ident)) throw new ValidationException("Ident pista obbligatorio.");

        // Se le piste sono di sorgente (bloccate), le colonne ident/lunghezza/bearing e l'insieme delle piste
        // non sono modificabili: si possono salvare solo le colonne editoriali (TORA/LDA/APP/Patterns/Circling).
        if ((await _policy.GetAsync(ct)).Runways)
        {
            var stored = (await _repo.LoadAsync(Norm(icao), ct))?.Runways ?? Array.Empty<RunwayRow>();
            var storedByIdent = stored.ToDictionary(r => r.Ident.Trim().ToUpperInvariant());
            var locked = rows.Count != stored.Count
                || rows.Any(r => !storedByIdent.TryGetValue(r.Ident.Trim().ToUpperInvariant(), out var s)
                    || s.LengthM != r.LengthM || s.Bearing != r.Bearing);
            if (locked)
                throw new ValidationException("Le piste sono gestite dalla sorgente (sola lettura): non puoi aggiungere/rimuovere piste né cambiarne ident, lunghezza o bearing. Per modificarle, escludi «Piste» in «Sorgenti dati».");
        }
        await _repo.SaveRunwaysAsync(Norm(icao), rows, ct);
    }

    public async Task SaveRunwayRulesAsync(string icao, IReadOnlyList<RunwayRuleRow> rows, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(icao, ct);
        foreach (var r in rows)
        {
            if (r.WindDirFrom is < 0 or > 360 || r.WindDirTo is < 0 or > 360)
                throw new ValidationException("Direzione vento fuori range (0–360).");
            if ((r.WindSpeedMin ?? 0) < 0 || (r.WindSpeedMax ?? 0) < 0)
                throw new ValidationException("Velocità vento negativa.");
            if (string.IsNullOrWhiteSpace(r.DepRunways) && string.IsNullOrWhiteSpace(r.ArrRunways))
                throw new ValidationException("Specifica almeno una pista DEP o ARR per la regola.");
        }
        await _repo.SaveRunwayRulesAsync(Norm(icao), rows, ct);
    }

    public async Task SaveSidsAsync(string icao, IReadOnlyList<SidRow> rows, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(icao, ct);
        foreach (var r in rows)
        {
            if (string.IsNullOrWhiteSpace(r.Name)) throw new ValidationException("Nome SID obbligatorio.");
            if (string.IsNullOrWhiteSpace(r.Fix)) throw new ValidationException("FIX obbligatorio per ogni SID.");
        }
        await _repo.SaveSidsAsync(Norm(icao), rows, ct);
    }

    public async Task SaveFrequencyLinksAsync(string icao, IReadOnlyList<int> sourceFrequencyIds, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(icao, ct);
        await _repo.SaveFrequencyLinksAsync(Norm(icao), sourceFrequencyIds, ct);
    }

    public async Task ReimportFromSourceAsync(string icao, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(icao, ct);
        icao = Norm(icao);
        var policy = await _policy.GetAsync(ct);

        var positions = await _details.GetAtcPositionsAsync(icao, ct);

        // Solo le categorie importate vengono passate al merge: per quelle escluse il merge non tocca i dati
        // editoriali dell'utente (null TA / null ATIS / lista piste vuota = "nessun cambio").
        var runways = policy.Runways
            ? (await _details.GetRunwaysAsync(icao, ct)).Select(r => (r.Ident, r.LengthM, r.Bearing)).ToList()
            : new List<(string, int?, int?)>();

        var atisFreq = policy.Atis
            ? positions.FirstOrDefault(p => string.Equals(SuffixOf(p.Callsign), "ATIS", StringComparison.OrdinalIgnoreCase))?.Frequency
            : null;

        int? ta = null;
        if (policy.TransitionAltitude)
            try
            {
                ta = (await _directory.GetAirportsAsync(ct))
                    .FirstOrDefault(a => string.Equals(a.Icao, icao, StringComparison.OrdinalIgnoreCase))?.TransitionAltitude;
            }
            catch { /* anagrafica non disponibile: TA resta invariata */ }

        await _repo.MergeFromSourceAsync(icao, ta, atisFreq, runways, ct);
    }

    public async Task<int> RebuildDocumentAsync(string icao, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(icao, ct);
        return await _repo.RebuildDocumentAsync(Norm(icao), ct);
    }

    private async Task EnsureCanEditAsync(string icao, CancellationToken ct)
    {
        var fir = await _repo.GetFirCodeByIcaoAsync(Norm(icao), ct)
            ?? throw new ValidationException($"Aeroporto {Norm(icao)} inesistente.");
        await _authz.EnsureCanEditFirAsync(fir, ct);
    }

    private static string Norm(string icao) => (icao ?? "").Trim().ToUpperInvariant();

    /// <summary>Suffisso del callsign dopo l'ultimo '_' (es. LIRF_ATIS → ATIS).</summary>
    private static string SuffixOf(string callsign) =>
        callsign.Contains('_') ? callsign[(callsign.LastIndexOf('_') + 1)..] : callsign;
}
