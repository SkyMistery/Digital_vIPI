using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Auth;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Use-case di authoring del profilo strutturato dell'aeroporto (regole pista, quote di transizione,
/// frequenze+link, piste, SID) e rigenerazione del documento. Letture libere (servono anche al viewer);
/// scritture ACC-gated via <see cref="IEditAuthorizationService"/>. Validazioni hard sugli input.
/// </summary>
public interface IAirportEditingService
{
    /// <summary>Lettura per il viewer (nessuna guardia): regole pista + link frequenze + resto.</summary>
    Task<AirportData?> LoadForViewAsync(string icao, CancellationToken ct = default);
    /// <summary>Lettura per l'editor: richiede il permesso di editare la ACC dell'aeroporto.</summary>
    Task<AirportData?> LoadForEditAsync(string icao, CancellationToken ct = default);

    /// <summary>Policy di import globale (per editor e viewer): quali categorie sono di sorgente (sola lettura).</summary>
    Task<ImportPolicySnapshot> GetImportPolicyAsync(CancellationToken ct = default);

    /// <summary>Id del Document proiettato dell'aeroporto (per il banner di revisione), o null se non ancora generato. Lettura libera.</summary>
    Task<int?> GetDocumentIdAsync(string icao, CancellationToken ct = default);

    Task<IReadOnlyList<LinkableFrequencyRow>> ListLinkableFrequenciesAsync(CancellationToken ct = default);

    Task SetTransitionAltitudeAsync(string icao, int? ta, CancellationToken ct = default);
    Task SaveTransitionLevelsAsync(string icao, IReadOnlyList<TlRow> rows, CancellationToken ct = default);
    Task SaveRunwaysAsync(string icao, IReadOnlyList<RunwayRow> rows, CancellationToken ct = default);
    Task SaveRunwayRulesAsync(string icao, IReadOnlyList<RunwayRuleRow> rows, CancellationToken ct = default);
    Task SaveSidsAsync(string icao, IReadOnlyList<SidRow> rows, CancellationToken ct = default);
    /// <summary>Aggiorna priorità/forzatura pubblicazione/fix risolto e arricchimenti editoriali (initial climb, CAT,
    /// WTC, condition) di UNA riga SID importata (ACC-gated).</summary>
    Task UpdateImportedSidAsync(string icao, int sidId, int? priority, bool forcePublished, string? resolvedFix,
        string? initialClimb, bool initialClimbByApp, string? cat, string? wtc, string? condition, CancellationToken ct = default);
    Task SaveFrequencyLinksAsync(string icao, IReadOnlyList<int> sourceFrequencyIds, CancellationToken ct = default);
    Task SaveExtraSectionsAsync(string icao, IReadOnlyList<ExtraSectionRow> rows, CancellationToken ct = default);

    /// <summary>RenderMode della sezione SID nel documento corrente (doc 10 §S4c): Live (default) = derivata al view;
    /// Frozen = congelata al publish. Lettura libera (serve al viewer/editor).</summary>
    Task<RenderMode> GetSidsRenderModeAsync(string icao, CancellationToken ct = default);

    /// <summary>Imposta il RenderMode della sezione SID (doc 10 §S4c). ACC-gated. Preservato dai rebuild.</summary>
    Task SetSidsRenderModeAsync(string icao, RenderMode mode, CancellationToken ct = default);

    /// <summary>Re-importa da IVAO (merge mirato): aggiorna TA/ATIS/piste, preserva il lavoro editoriale.</summary>
    Task ReimportFromSourceAsync(string icao, CancellationToken ct = default);
    /// <summary>Rigenera e ripubblica le sezioni gestite del documento dalle entità. Ritorna l'id documento.</summary>
    Task<int> RebuildDocumentAsync(string icao, CancellationToken ct = default);
}

/// <inheritdoc cref="IAirportEditingService"/>
public sealed class AirportEditingService : IAirportEditingService
{
    private readonly IAirportRepository _repo;
    private readonly IEditAuthorizationService _authz;
    private readonly IAirportDirectory _directory;
    private readonly IAirportDetailProvider _details;
    private readonly IImportPolicyStore _policy;

    public AirportEditingService(IAirportRepository repo, IEditAuthorizationService authz,
        IAirportDirectory directory, IAirportDetailProvider details, IImportPolicyStore policy)
    {
        _repo = repo;
        _authz = authz;
        _directory = directory;
        _details = details;
        _policy = policy;
    }

    public Task<ImportPolicySnapshot> GetImportPolicyAsync(CancellationToken ct = default) => _policy.GetAsync(ct);

    public Task<int?> GetDocumentIdAsync(string icao, CancellationToken ct = default) => _repo.GetDocumentIdAsync(Norm(icao), ct);

    public Task<AirportData?> LoadForViewAsync(string icao, CancellationToken ct = default) =>
        _repo.LoadAsync(Norm(icao), ct);

    public async Task<AirportData?> LoadForEditAsync(string icao, CancellationToken ct = default)
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
            if (string.IsNullOrWhiteSpace(r.DepRunways) && string.IsNullOrWhiteSpace(r.ArrRunways))
                throw new ValidationException("Specifica almeno una pista DEP o ARR per la regola.");
            if (r.MaxTailwindKt is < 0 or > 40) throw new ValidationException("Vento in coda massimo fuori range (0–40 kt).");
            if (r.MaxCrosswindKt is < 0 or > 60) throw new ValidationException("Vento al traverso massimo fuori range (0–60 kt).");
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

    public async Task UpdateImportedSidAsync(string icao, int sidId, int? priority, bool forcePublished, string? resolvedFix,
        string? initialClimb, bool initialClimbByApp, string? cat, string? wtc, string? condition, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(icao, ct);
        await _repo.UpdateImportedSidAsync(sidId, priority, forcePublished, resolvedFix, initialClimb, initialClimbByApp, cat, wtc, condition, ct);
    }

    public async Task SaveFrequencyLinksAsync(string icao, IReadOnlyList<int> sourceFrequencyIds, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(icao, ct);
        await _repo.SaveFrequencyLinksAsync(Norm(icao), sourceFrequencyIds, ct);
    }

    public async Task SaveExtraSectionsAsync(string icao, IReadOnlyList<ExtraSectionRow> rows, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(icao, ct);
        foreach (var r in rows)
            if (string.IsNullOrWhiteSpace(r.Title)) throw new ValidationException("Titolo obbligatorio per ogni sezione extra.");
        await _repo.SaveExtraSectionsAsync(Norm(icao), rows, ct);
    }

    public Task<RenderMode> GetSidsRenderModeAsync(string icao, CancellationToken ct = default) =>
        _repo.GetSidsRenderModeAsync(Norm(icao), ct);

    public async Task SetSidsRenderModeAsync(string icao, RenderMode mode, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(icao, ct);
        await _repo.SetSidsRenderModeAsync(Norm(icao), mode, ct);
    }

    public async Task ReimportFromSourceAsync(string icao, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(icao, ct);
        icao = Norm(icao);
        var policy = await _policy.GetAsync(ct);

        // Solo le categorie importate vengono passate al merge: per quelle escluse il merge non tocca i dati
        // editoriali dell'utente (null TA / lista piste vuota = "nessun cambio"). L'ATIS è nel catalogo settori.
        var (ta, runways) = await SourceMergeInputs.ReadAsync(policy, icao, _directory, _details, ct);

        await _repo.MergeFromSourceAsync(icao, ta, runways, ct);
    }

    public async Task<int> RebuildDocumentAsync(string icao, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(icao, ct);
        return await _repo.RebuildDocumentAsync(Norm(icao), ct);
    }

    private async Task EnsureCanEditAsync(string icao, CancellationToken ct)
    {
        var acc = await _repo.GetAccCodeByIcaoAsync(Norm(icao), ct)
            ?? throw new ValidationException($"Aeroporto {Norm(icao)} inesistente.");
        await _authz.EnsureCanEditAccAsync(acc, ct);
    }

    private static string Norm(string icao) => (icao ?? "").Trim().ToUpperInvariant();
}
