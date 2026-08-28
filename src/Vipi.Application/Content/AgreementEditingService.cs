using System.Linq;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Auth;
using Vipi.Domain;
using static Vipi.Application.Messaggio;

namespace Vipi.Application.Content;

/// <inheritdoc cref="IAgreementService"/>
public sealed class AgreementService : IAgreementService
{
    private readonly IAgreementRepository _repo;
    private readonly IEditAuthorizationService _authz;
    private readonly ITopologyProvider _topology;

    public AgreementService(IAgreementRepository repo, IEditAuthorizationService authz, ITopologyProvider topology)
    {
        _repo = repo;
        _authz = authz;
        _topology = topology;
    }

    public Task<IReadOnlyList<AgreementRow>> ListByAccAsync(string accCode, CancellationToken ct = default) =>
        _repo.ListByAccAsync(accCode, ct);

    public async Task<IReadOnlyList<TransferFlowRow>> ListFlowsByAccAsync(string accCode, CancellationToken ct = default) =>
        AgreementExpansion.Expand(await _repo.ListByAccAsync(accCode, ct));

    public async Task<IReadOnlyList<ResolvedTransferFlow>> ResolveForAccAsync(
        string accCode, IReadOnlySet<string> online, CancellationToken ct = default)
    {
        var flows = await ListFlowsByAccAsync(accCode, ct);
        var topo = await _topology.BuildGlobalAsync(ct);

        // Catena di candidati di un settore: sé stesso + antenati di copertura (cross-ACC), in ordine di priorità.
        IReadOnlyList<string> Chain(string? callsign) =>
            string.IsNullOrWhiteSpace(callsign)
                ? Array.Empty<string>()
                : new[] { callsign }.Concat(topo.Ancestors(callsign)).ToList();

        return flows.Select(f =>
        {
            var ownerHit = TransferOnlineResolver.FirstOnline(Chain(f.OwningSectorCallsign), online);
            var points = f.Points.Select(p =>
            {
                var (handler, isOnline) = TransferOnlineResolver.Resolve(Chain(p.NextSectorCallsign), online);
                return new ResolvedTransferPoint { Point = p, ResolvedHandler = handler, IsOnline = isOnline };
            }).ToList();

            return new ResolvedTransferFlow
            {
                Flow = f,
                ResolvedOwnerCallsign = ownerHit ?? f.OwningSectorCallsign,
                OwnerOnline = ownerHit is not null,
                Points = points,
            };
        }).ToList();
    }

    public Task<int?> FindByPairAsync(string accCode, int sectorX, int sectorY, CancellationToken ct = default) =>
        _repo.FindByPairAsync(accCode, sectorX, sectorY, ct);

    public async Task<int> AddAgreementAsync(string accCode, AgreementInput input, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        ValidateAgreement(input);
        return await _repo.AddAgreementAsync(accCode, input, ct);
    }

    public async Task UpdateAgreementAsync(string accCode, int agreementId, AgreementInput input, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        ValidateAgreement(input);
        await _repo.UpdateAgreementAsync(accCode, agreementId, input, ct);
    }

    public async Task DeleteAgreementAsync(string accCode, int agreementId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        await _repo.DeleteAgreementAsync(accCode, agreementId, ct);
    }

    public async Task<int> AddSectionAsync(string accCode, int agreementId, AgreementSectionInput input,
        CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        ValidateSection(input);
        return await _repo.AddSectionAsync(accCode, agreementId, input, ct);
    }

    public async Task UpdateSectionAsync(string accCode, int sectionId, AgreementSectionInput input,
        CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        ValidateSection(input);
        await _repo.UpdateSectionAsync(accCode, sectionId, input, ct);
    }

    public async Task DeleteSectionAsync(string accCode, int sectionId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        await _repo.DeleteSectionAsync(accCode, sectionId, ct);
    }

    public async Task<int?> CopySectionToReverseAsync(string accCode, int sectionId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        return await _repo.CopySectionToReverseAsync(accCode, sectionId, ct);
    }

    public async Task<int> MergeSectionsAsync(string accCode, int keepId, int absorbId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        if (keepId == absorbId)
            throw new ValidationException(Lingua("Una sezione non può assorbire sé stessa.", "A section cannot absorb itself."));
        return await _repo.MergeSectionsAsync(accCode, keepId, absorbId, ct);
    }

    public async Task<int> AddClauseAsync(string accCode, int sectionId, AgreementClauseInput input,
        CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        ValidateClause(input);
        return await _repo.AddClauseAsync(accCode, sectionId, input, ct);
    }

    public async Task UpdateClauseAsync(string accCode, int clauseId, AgreementClauseInput input, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        ValidateClause(input);
        await _repo.UpdateClauseAsync(accCode, clauseId, input, ct);
    }

    public async Task DeleteClauseAsync(string accCode, int clauseId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        await _repo.DeleteClauseAsync(accCode, clauseId, ct);
    }

    public async Task MoveClauseAsync(string accCode, int clauseId, bool up, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        await _repo.MoveClauseAsync(accCode, clauseId, up, ct);
    }

    public async Task MoveClauseToAsync(string accCode, int clauseId, int targetClauseId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        await _repo.MoveClauseToAsync(accCode, clauseId, targetClauseId, ct);
    }

    public async Task<int> AddAlternativeAsync(string accCode, int clauseId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        return await _repo.AddAlternativeAsync(accCode, clauseId, ct);
    }

    public async Task<int> AddExceptionAsync(string accCode, int clauseId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        return await _repo.AddExceptionAsync(accCode, clauseId, ct);
    }

    public async Task<int> DuplicateVariantGroupAsync(string accCode, int clauseId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        return await _repo.DuplicateVariantGroupAsync(accCode, clauseId, ct);
    }

    public async Task DetachVariantAsync(string accCode, int clauseId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        await _repo.DetachVariantAsync(accCode, clauseId, ct);
    }

    public async Task<int> SetLevelAsync(string accCode, IReadOnlyList<int> clauseIds, ParsedLevel level, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        return await _repo.SetLevelAsync(accCode, clauseIds, level, ct);
    }

    public async Task<int> SetConditionAsync(string accCode, IReadOnlyList<int> clauseIds, string? areaLabel,
        string? customLabel, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        return await _repo.SetConditionAsync(accCode, clauseIds, areaLabel, customLabel, ct);
    }

    public async Task<int> DeleteClausesAsync(string accCode, IReadOnlyList<int> clauseIds, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        return await _repo.DeleteClausesAsync(accCode, clauseIds, ct);
    }

    public async Task<int> RestoreAgreementAsync(string accCode, AgreementSnapshot snapshot, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        return await _repo.RestoreAgreementAsync(accCode, snapshot, ct);
    }

    public async Task<int?> RestoreSectionAsync(string accCode, AgreementSectionRestore section, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        return await _repo.RestoreSectionAsync(accCode, section, ct);
    }

    public async Task<int> RestoreClausesAsync(string accCode, IReadOnlyList<AgreementClauseRestore> clauses, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        return await _repo.RestoreClausesAsync(accCode, clauses, ct);
    }

    // ---- validazione SOFT ---------------------------------------------------------------------------
    // Solo ciò senza cui l'accordo direbbe una cosa diversa da quella scritta. Un accordo a metà è lavoro in
    // corso e si salva: rifiutarlo costringerebbe a tenerlo altrove, cioè fuori dall'archivio.

    private static void ValidateAgreement(AgreementInput i)
    {
        // Un accordo ha DUE capi, e nessuno dei due è opzionale.
        //
        // ⚠️ Un lato vuoto non ha mai voluto dire «a UNICOM», anche se l'interfaccia lo insegnava: UNICOM lo
        // calcola TransferOnlineResolver a runtime quando il ricevente è offline. Voleva dire «non finito», e un
        // accordo così non produceva niente — la derivazione scartava la riga. Dal 18 agosto 2026 la regola è
        // anche di schema (due colonne NOT NULL), e questa validazione resta perché l'errore arrivi come una
        // frase e non come una violazione di vincolo.
        if (i.SideASectorId <= 0 || i.SideBSectorId <= 0)
            throw new ValidationException(Lingua("Indica tutti e due gli enti dell'accordo.", "Name both units of the agreement."));

        if (i.SideASectorId == i.SideBSectorId)
            throw new ValidationException(Lingua("Un ente non può stare su entrambi i lati dello stesso accordo.", "A unit cannot be on both sides of the same agreement."));
    }

    /// <summary>
    /// Cosa una sezione deve dire per non mentire. La regola degli aeroporti è quella di sempre, spostata
    /// dall'accordo alla sezione — che è il posto dove il tipo di traffico adesso vive.
    /// </summary>
    private static void ValidateSection(AgreementSectionInput i)
    {
        // Arrivi e partenze sono definiti RISPETTO a un aeroporto: senza, la frase resta orfana («con
        // destinazione …») e la derivazione scarta la riga. È una scelta del committente, riconfermata il
        // 18 agosto 2026.
        if (i.Kind is TransferFlowKind.Arrival or TransferFlowKind.Departure && i.Airports.Count == 0)
            throw new ValidationException(
                "Arrivi e Partenze richiedono almeno un aeroporto. Per il traffico senza aeroporto usa Sorvoli/VFR/Altro.");

        // Un sorvolo con un aeroporto sarebbe una contraddizione scritta: il traffico che sorvola non ha
        // relazione con lo scalo, e la frase userebbe comunque la forma neutra ignorandolo. VFR e Altro invece
        // possono averne — è la regola «dove non sono esclusi», già pagata una volta col catch-22 di ferragosto.
        if (i.Kind == TransferFlowKind.Overflight && i.Airports.Count > 0)
            throw new ValidationException(Lingua("I sorvoli non hanno aeroporti: il traffico attraversa, non atterra.", "Overflights have no airports: the traffic crosses, it does not land."));

        if (i.Airports.Any(a => string.IsNullOrWhiteSpace(a.Icao)))
            throw new ValidationException(Lingua("Ogni aeroporto della sezione deve avere un ICAO.", "Every airport in the section must have an ICAO."));

        // Lo stesso scalo due volte non aggiunge niente e moltiplica le righe derivate: è un errore di
        // digitazione, non una scelta.
        var duplicati = i.Airports.GroupBy(a => a.Icao.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicati.Count > 0)
            throw new ValidationException(Lingua(
                $"Aeroporto ripetuto nella sezione: {string.Join(", ", duplicati)}.",
                $"Airport repeated in the section: {string.Join(", ", duplicati)}."));
    }

    private static void ValidateClause(AgreementClauseInput i)
    {
        if (i.LevelConstraint != LevelConstraint.Special && i.LevelValue is null
            && CopList.Format(CopList.Parse(i.Cops)).Length == 0)
            throw new ValidationException("Indica almeno un punto o un livello.");

        // Il tipo «punto» senza etichetta non dice dove, e resterebbe muto nella frase. «Confine dell'AoR»
        // invece si descrive da sé, e l'etichetta lì non serve.
        if (i.HandoffKind is TransferHandoffKind.Point or TransferHandoffKind.Custom
            && string.IsNullOrWhiteSpace(i.HandoffLabel))
            throw new ValidationException("Indica dove avviene il trasferimento (punto o testo).");
        if (i.CommsHandoffKind is TransferHandoffKind.Point or TransferHandoffKind.Custom
            && string.IsNullOrWhiteSpace(i.CommsHandoffLabel))
            throw new ValidationException("Indica dove passano le comunicazioni (punto o testo).");

        if (i.SpeedConstraint != SpeedConstraint.Unspecified && i.SpeedValue is null)
            throw new ValidationException(Lingua("Indica il valore della velocità, o togli il vincolo.", "Give the speed value, or drop the constraint."));

        // Una clausola che scavalca le alternative deve dire A QUALI CONDIZIONI le scavalca («di notte,
        // qualunque pista»): senza condizione non si distinguerebbe da un'alternativa in più.
        if (i.IsGroupWide && string.IsNullOrWhiteSpace(i.ConditionLabel)
                          && string.IsNullOrWhiteSpace(i.ConditionAreaLabel)
                          && string.IsNullOrWhiteSpace(i.ConditionCustomLabel))
            throw new ValidationException("Una clausola «in ogni caso» deve dire a quali condizioni vale.");
    }
}
