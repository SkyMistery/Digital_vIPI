using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Auth;
using Vipi.Domain;
using static Vipi.Application.Messaggio;

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

    /// <summary>
    /// Anagrafica militare dello scalo e i due legami documentali. null = ICAO inesistente.
    /// <para>Lettura libera come <see cref="LoadForViewAsync"/>: dice soltanto <b>quale edizione esiste</b>,
    /// e serve alle pagine per mandare la gente nel posto giusto <i>prima</i> che una guardia le fermi. La
    /// guardia vera resta <see cref="EnsureDocumentAsync"/>.</para>
    /// </summary>
    Task<AirportMilitaryState?> GetMilitaryStateAsync(string icao, CancellationToken ct = default);

    /// <summary>
    /// Piste e regole-pista di più aeroporti in una volta: quel poco che basta a dire quale pista è in uso.
    /// La usa l'elenco degli aeroporti di una ACC, che prima chiamava <see cref="LoadForViewAsync"/> una
    /// volta per scalo — otto query a testa, in fila, per leggerne due liste.
    /// </summary>
    Task<IReadOnlyDictionary<string, PisteDiAeroporto>> ListRunwayDataAsync(
        IReadOnlyCollection<string> icaos, CancellationToken ct = default);
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

    /// <summary>RenderMode della sezione SID nel documento corrente (doc 10 §S4c): Live (default) = derivata al view;


    /// <summary>Re-importa da IVAO (merge mirato): aggiorna TA/ATIS/piste, preserva il lavoro editoriale.</summary>
    /// <summary>Re-importa dalla sorgente e dice che cosa ha fatto alle piste — in particolare quali orfane
    /// con lavoro editoriale ha lasciato lì perché le tolga una persona.</summary>
    Task<RunwayMergeOutcome> ReimportFromSourceAsync(string icao, CancellationToken ct = default);
    /// <summary>Idempotente: garantisce il documento dell'aeroporto e le sue sezioni di catalogo. Ritorna l'id
    /// documento. Non cuoce più il contenuto: le sezioni fisse si derivano a view-time (carta 2026-08-26).</summary>
    Task<int> EnsureDocumentAsync(string icao, CancellationToken ct = default);
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

    public Task<IReadOnlyDictionary<string, PisteDiAeroporto>> ListRunwayDataAsync(
        IReadOnlyCollection<string> icaos, CancellationToken ct = default) =>
        _repo.ListRunwayDataAsync(icaos, ct);

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
            throw new ValidationException(Lingua("Transition Altitude è gestita dalla sorgente (sola lettura). Per modificarla, escludila in «Sorgenti dati».", "The Transition Altitude comes from the source (read-only). To change it, exclude it under «Data sources»."));
        if (ta is < 0 or > 60000) throw new ValidationException(Lingua("Transition Altitude non valida.", "Invalid Transition Altitude."));
        await _repo.SetTransitionAltitudeAsync(Norm(icao), ta, ct);
    }

    public async Task SaveTransitionLevelsAsync(string icao, IReadOnlyList<TlRow> rows, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(icao, ct);
        foreach (var r in rows)
        {
            if (string.IsNullOrWhiteSpace(r.Level)) throw new ValidationException(Lingua("Transition Level obbligatorio per ogni riga.", "A Transition Level is required on every row."));
            if (r.QnhFrom is int a && r.QnhTo is int b && a > b) throw new ValidationException(Lingua("Intervallo QNH invertito (From > To).", "QNH range is inverted (From > To)."));
        }
        await _repo.SaveTransitionLevelsAsync(Norm(icao), rows, ct);
    }

    public async Task SaveRunwaysAsync(string icao, IReadOnlyList<RunwayRow> rows, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(icao, ct);
        foreach (var r in rows)
            if (string.IsNullOrWhiteSpace(r.Ident)) throw new ValidationException(Lingua("Ident pista obbligatorio.", "The runway ident is required."));

        // Se le piste sono di sorgente (bloccate), ident/lunghezza/bearing non si toccano e piste NUOVE non
        // si inventano: restano scrivibili le sole colonne editoriali (TORA/LDA/APP/Patterns/Circling).
        //
        // ⚠️ Le RIMOZIONI invece passano, ed è deliberato. Quando IVAO ri-denomina uno scalo (Rimini 13/31 →
        // 12/30) le piste morte con TORA/LDA restano in archivio apposta — il merge non distrugge lavoro
        // editoriale — e qualcuno deve poterle togliere. Vietarlo qui chiudeva l'amministratore fuori dal
        // suo stesso archivio: la ✕ c'era nell'editor solo a policy spenta, e spegnerla è globale.
        // Il rischio è piccolo e si ripara da sé: togliere per sbaglio una pista che la sorgente ha ancora
        // dura fino al re-import successivo, che la rimette. Un'AGGIUNTA a mano no — e infatti resta vietata.
        if ((await _policy.GetAsync(ct)).Runways)
        {
            var stored = (await _repo.LoadAsync(Norm(icao), ct))?.Runways ?? Array.Empty<RunwayRow>();
            var storedByIdent = stored.ToDictionary(r => r.Ident.Trim().ToUpperInvariant());
            var locked = rows.Any(r => !storedByIdent.TryGetValue(r.Ident.Trim().ToUpperInvariant(), out var s)
                || s.LengthM != r.LengthM || s.Bearing != r.Bearing);
            if (locked)
                throw new ValidationException(Lingua("Le piste sono gestite dalla sorgente (sola lettura): non puoi aggiungere piste né cambiarne ident, lunghezza o bearing. Toglierne una è permesso: se la sorgente ce l'ha ancora, il prossimo re-import la rimette. Per il resto, escludi «Piste» in «Sorgenti dati».", "Runways come from the source (read-only): you cannot add runways, nor change their ident, length or bearing. Removing one is allowed: if the source still has it, the next re-import brings it back. For anything else, exclude «Runways» under «Data sources»."));
        }
        await _repo.SaveRunwaysAsync(Norm(icao), rows, ct);
    }

    public async Task SaveRunwayRulesAsync(string icao, IReadOnlyList<RunwayRuleRow> rows, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(icao, ct);
        foreach (var r in rows)
        {
            if (string.IsNullOrWhiteSpace(r.DepRunways) && string.IsNullOrWhiteSpace(r.ArrRunways))
                throw new ValidationException(Lingua("Specifica almeno una pista DEP o ARR per la regola.", "Give the rule at least one DEP or ARR runway."));
            if (r.MaxTailwindKt is < 0 or > 40) throw new ValidationException(Lingua("Vento in coda massimo fuori range (0–40 kt).", "Maximum tailwind out of range (0–40 kt)."));
            if (r.MaxCrosswindKt is < 0 or > 60) throw new ValidationException(Lingua("Vento al traverso massimo fuori range (0–60 kt).", "Maximum crosswind out of range (0–60 kt)."));
        }
        await _repo.SaveRunwayRulesAsync(Norm(icao), rows, ct);
    }

    public async Task SaveSidsAsync(string icao, IReadOnlyList<SidRow> rows, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(icao, ct);
        foreach (var r in rows)
        {
            if (string.IsNullOrWhiteSpace(r.Name)) throw new ValidationException(Lingua("Nome SID obbligatorio.", "The SID name is required."));
            if (string.IsNullOrWhiteSpace(r.Fix)) throw new ValidationException(Lingua("FIX obbligatorio per ogni SID.", "A FIX is required on every SID."));
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

            public async Task<RunwayMergeOutcome> ReimportFromSourceAsync(string icao, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(icao, ct);
        icao = Norm(icao);
        var policy = await _policy.GetAsync(ct);

        // Solo le categorie importate vengono passate al merge: per quelle escluse il merge non tocca i dati
        // editoriali dell'utente (null TA / lista piste vuota = "nessun cambio"). L'ATIS è nel catalogo settori.
        var (ta, runways) = await SourceMergeInputs.ReadAsync(policy, icao, _directory, _details, ct);

        return await _repo.MergeFromSourceAsync(icao, ta, runways, ct);
    }

    public Task<AirportMilitaryState?> GetMilitaryStateAsync(string icao, CancellationToken ct = default) =>
        _repo.GetMilitaryStateAsync(Norm(icao), ct);

    public async Task<int> EnsureDocumentAsync(string icao, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(icao, ct);
        icao = Norm(icao);

        // ---- La guardia dei campi SOLO MILITARI (carta vSOP militari §5-bis) --------------------------
        //
        // Su Aviano, Ghedi, Decimomannu, Rivolto una vIPI CIVILE non descrive niente: non c'è traffico
        // civile da descrivere. Il documento resterebbe lì, vuoto, in un elenco dove nessuno saprebbe
        // perché c'è — e siccome nasce dall'APERTURA dell'editor, bastava arrivare all'indirizzo per
        // farlo nascere senza volerlo.
        //
        // ⚠️ La guardia sta QUI, nel servizio, e non solo nella tendina di «Nuovo documento»: una tendina
        // filtra, non autorizza. È la stessa lezione già pagata su /services/vsop/versions il 21 agosto
        // 2026, dove il tasto spento non impediva la creazione a chi conosceva l'URL.
        //
        // ⚠️ Blocca la NASCITA, non l'apertura. Se una vIPI civile su un campo solo militare esiste già —
        // creata prima di questa regola, o perché il campo è stato marcato dopo — l'editor deve continuare
        // ad aprirla: rifiutare qui renderebbe illeggibile un documento che c'è, e la via d'uscita
        // (spostarne il contenuto, poi eliminarlo) passa proprio da lì.
        var stato = await _repo.GetMilitaryStateAsync(icao, ct);
        if (stato is { IsMilitaryOnly: true, DocumentId: null })
            throw new ValidationException(Lingua(
                $"{icao} è un campo solo militare: la sua edizione è il vSOP militare, non la vIPI civile.",
                $"{icao} is a military-only field: its edition is the military vSOP, not the civil vIPI."));

        return await _repo.EnsureDocumentAsync(icao, ct);
    }

    private async Task EnsureCanEditAsync(string icao, CancellationToken ct)
    {
        var acc = await _repo.GetAccCodeByIcaoAsync(Norm(icao), ct)
            ?? throw new ValidationException(Lingua($"Aeroporto {Norm(icao)} inesistente.", $"Airport {Norm(icao)} does not exist."));
        _authz.EnsureAtLeast(VipiRole.Editor);
    }

    private static string Norm(string icao) => (icao ?? "").Trim().ToUpperInvariant();
}
