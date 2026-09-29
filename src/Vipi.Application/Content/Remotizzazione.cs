using System.Text.Json;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;
using static Vipi.Application.Messaggio;

namespace Vipi.Application.Content;

/// <summary>Com'è andata una remotizzazione: dove è finito il contenuto.</summary>
/// <param name="BlockSectionId">La sezione-blocco del gruppo APP nuovo, nella bozza della vIPI ACC.</param>
/// <param name="UscitoDaUnione">La vIPI APP era unita ad altri documenti, ed è uscita dall'unione.</param>
public sealed record RemotizzazioneEsito(string AccCode, int AccDocumentId, int BlockSectionId, bool UscitoDaUnione);

/// <summary>
/// «Remotizza» (S50, committente, 29 settembre 2026): il contenuto della vIPI APP di un ente si <b>sposta</b> nella
/// vIPI del suo ACC, come gruppo APP, e lì resta. Serve quando un APP non remotizzato passa sotto un ACC (Palermo
/// sotto Roma): prima la sua parte andava riscritta da capo.
/// <para>Si può fare perché dal 21 settembre 2026 la vIPI APP e il blocco «gruppo APP» della vIPI ACC hanno la stessa
/// struttura di sezioni (profili <c>App</c> e <c>AccAppBlock</c>): l'albero si copia com'è, contenuti compresi.</para>
/// </summary>
public interface IRemotizzazioneService
{
    Task<RemotizzazioneEsito> RemotizzaAsync(int unitId, CancellationToken ct = default);
}

/// <inheritdoc cref="IRemotizzazioneService"/>
public sealed class RemotizzazioneService : IRemotizzazioneService
{
    private readonly IAtcUnitRepository _enti;
    private readonly IEditingRepository _editing;
    private readonly IAccDocumentService _acc;
    private readonly IDocumentProfileRepository _profili;
    private readonly IAppDerivationRepository _app;
    private readonly IDocumentUnionService _unioni;
    private readonly IDocumentAdminRepository _documenti;
    private readonly IDocumentLockGuard _lock;
    private readonly IEditAuthorizationService _authz;

    public RemotizzazioneService(IAtcUnitRepository enti, IEditingRepository editing, IAccDocumentService acc,
        IDocumentProfileRepository profili, IAppDerivationRepository app, IDocumentUnionService unioni,
        IDocumentAdminRepository documenti, IDocumentLockGuard lockGuard, IEditAuthorizationService authz)
    {
        _enti = enti;
        _editing = editing;
        _acc = acc;
        _profili = profili;
        _app = app;
        _unioni = unioni;
        _documenti = documenti;
        _lock = lockGuard;
        _authz = authz;
    }

    public async Task<RemotizzazioneEsito> RemotizzaAsync(int unitId, CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);
        var ente = (await _enti.ListAsync(null, ct)).FirstOrDefault(u => u.Id == unitId)
                   ?? throw new Aor.ValidationException(Lingua($"Ente {unitId} inesistente.", $"Unit {unitId} does not exist."));
        if (ente.Mode != AtcUnitMode.OwnDocument || ente.DocumentId is not int appDoc)
            throw new Aor.ValidationException(Lingua(
                $"«{ente.Name}» non ha una vIPI APP da spostare.", $"«{ente.Name}» has no APP vIPI to move."));
        var uid = _authz.CurrentUserId ?? 0;

        // 1. La vIPI APP la tiene chi preme (è in modifica): nessun altro la sta scrivendo.
        await _lock.EnsureMineAsync(appDoc, ct);

        // 2. La vIPI dell'ACC: c'è (o nasce), e nessun altro la sta scrivendo. Il lock si prende per il tempo del
        //    gesto e si restituisce, se prima non era di chi preme.
        var accDoc = await _acc.EnsureAsync(ente.AccCode, ct);
        var giaMio = await _editing.IsLockHeldByAsync(accDoc, uid, ct);
        var lockAcc = await _editing.AcquireOrInspectLockAsync(accDoc, uid, _authz.CurrentName,
            DocumentLockGuard.LockTtlMinutes, ct);
        if (lockAcc.Locked && lockAcc.ByUserId != uid)
            throw new EditConflictException(Lingua(
                $"La vIPI dell'ACC {ente.AccCode} è in modifica da {lockAcc.ByName}: riprova quando ha finito.",
                $"The {ente.AccCode} ACC vIPI is being edited by {lockAcc.ByName}: try again when they are done."));
        try
        {
            // 3. Fuori da un'eventuale unione PRIMA di copiare: la vIPI ACC non si unisce, e uscendo tornano visibili
            //    le sezioni che la scheda delle comuni aveva nascosto.
            var unione = await _unioni.ForDocumentAsync(appDoc, ct);
            if (unione?.Of(appDoc) is { } membro) await _unioni.RimuoviMembroAsync(membro.MemberId, ct);

            // 4. L'albero: dalla bozza della vIPI APP se c'è, altrimenti dalla versione pubblicata.
            var versioni = await _editing.ListVersionsAsync(appDoc, ct);
            var sorgente = versioni.Where(v => v.Status == DocumentStatus.Draft).MaxBy(v => v.VersionNumber)
                           ?? versioni.Where(v => v.Status == DocumentStatus.Published).MaxBy(v => v.VersionNumber)
                           ?? versioni.MaxBy(v => v.VersionNumber)
                           ?? throw new Aor.ValidationException(Lingua("La vIPI APP non ha versioni.", "The APP vIPI has no versions."));
            var bozzaAcc = await _editing.CreateDraftAsync(accDoc, uid, ct);
            var blocco = await _editing.CopyVersionIntoBlockAsync(sorgente.Id, bozzaAcc, "appgroup", ente.Name, ct);

            // 5. Il profilo della vIPI APP diventa il blockmeta del gruppo: membri = posizioni dell'ente, ordine delle
            //    frequenze com'era, collegamenti da id di settore a nominativi (il gruppo li tiene per nome).
            var profilo = await _profili.GetAsync(appDoc, ct);
            var collegati = profilo.FreqLinkSectorIds.Count == 0
                ? new List<string>()
                : (await _app.ResolveFreqLinksAsync(profilo.FreqLinkSectorIds, ct)).Select(r => r.Callsign).ToList();
            var meta = new AccBlockMeta
            {
                Key = "grp:" + Guid.NewGuid().ToString("N")[..8],
                Kind = AccBlockKind.AppGroup,
                MemberCallsigns = ente.Positions.ToList(),
                FreqOrder = profilo.FreqOrder.ToList(),
                FreqLinkCallsigns = collegati,
                UnitId = ente.Id,
            };
            await _editing.SaveSectionBlockJsonBySectionAsync(blocco, JsonSerializer.Serialize(meta), uid, ct);

            // 6. L'ente vive nella vIPI dell'ACC; la sua vIPI APP si nasconde e tiene la sua storia.
            await _enti.SetModeAsync(ente.Id, AtcUnitMode.InAccVipi, ct);
            await _documenti.SetHiddenAsync(new ManagedDocRef(ReleaseTargetType.App, ente.Code, appDoc), true, uid, ct);
            // Da qui la vIPI APP non si scrive più: il suo lock si restituisce subito, non alla scadenza.
            await _editing.ReleaseLockAsync(appDoc, uid, ct);

            return new RemotizzazioneEsito(ente.AccCode, accDoc, blocco, unione is not null);
        }
        finally
        {
            // Restituito solo se prima non c'era: chi aveva già la vIPI ACC in modifica la tiene.
            if (!giaMio) await _editing.ReleaseLockAsync(accDoc, uid, ct);
        }
    }
}
