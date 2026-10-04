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
/// <para>In DUE tempi (revisione, S52; scelta del committente): il gesto COPIA nella bozza della vIPI ACC, e la vIPI
/// APP resta pubblica; quando la vIPI ACC va in vigore col gruppo, <see cref="ConcludiSpostamentiAsync"/> passa
/// l'ente alla vIPI ACC e nasconde la vIPI APP. Prima il pubblico perdeva la vIPI APP subito, e fino alla
/// pubblicazione della vIPI ACC — o per sempre, con la vIPI ACC nascosta — il contenuto non c'era da nessuna parte.</para>
/// </summary>
public interface IRemotizzazioneService
{
    Task<RemotizzazioneEsito> RemotizzaAsync(int unitId, CancellationToken ct = default);

    /// <summary>L'ACC nella cui vIPI (bozza o versione di lavoro) c'è già il gruppo APP dell'ente, mentre la sua vIPI
    /// APP è ancora quella in vigore; null se lo spostamento non è in corso.</summary>
    Task<string?> SpostamentoInCorsoAsync(int unitId, CancellationToken ct = default);

    /// <summary>Gli spostamenti in corso di tutti gli enti, in un giro: id dell'ente → ACC. Per la pagina degli enti
    /// (S53), che altrimenti caricherebbe la vIPI dell'ACC una volta per ente.</summary>
    Task<IReadOnlyDictionary<int, string>> SpostamentiInCorsoAsync(CancellationToken ct = default);

    /// <summary>Gli enti il cui gruppo APP è nella vIPI ACC IN VIGORE passano alla vIPI ACC, e la loro vIPI APP si
    /// nasconde. Idempotente; ritorna quanti ne ha conclusi. Gira alla pubblicazione e nel giro delle release.</summary>
    Task<int> ConcludiSpostamentiAsync(CancellationToken ct = default);
}

/// <inheritdoc cref="IRemotizzazioneService"/>
public sealed class RemotizzazioneService : IRemotizzazioneService
{
    private readonly IAtcUnitRepository _enti;
    private readonly IEditingRepository _editing;
    private readonly IAccDocumentService _acc;
    private readonly IAccDerivationRepository _accRepo;
    private readonly IDocumentProfileRepository _profili;
    private readonly IAppDerivationRepository _app;
    private readonly IDocumentUnionService _unioni;
    private readonly IDocumentAdminRepository _documenti;
    private readonly IDocumentLockGuard _lock;
    private readonly IEditAuthorizationService _authz;
    private readonly IUnitOfWork _uow;

    public RemotizzazioneService(IAtcUnitRepository enti, IEditingRepository editing, IAccDocumentService acc,
        IAccDerivationRepository accRepo, IDocumentProfileRepository profili, IAppDerivationRepository app,
        IDocumentUnionService unioni, IDocumentAdminRepository documenti, IDocumentLockGuard lockGuard,
        IEditAuthorizationService authz, IUnitOfWork uow)
    {
        _enti = enti;
        _editing = editing;
        _acc = acc;
        _accRepo = accRepo;
        _profili = profili;
        _app = app;
        _unioni = unioni;
        _documenti = documenti;
        _lock = lockGuard;
        _authz = authz;
        _uow = uow;
    }

    public async Task<RemotizzazioneEsito> RemotizzaAsync(int unitId, CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);
        var ente = await _enti.GetAsync(unitId, ct)
                   ?? throw new Aor.ValidationException(Lingua($"Ente {unitId} inesistente.", $"Unit {unitId} does not exist."));
        if (ente.Mode != AtcUnitMode.OwnDocument || ente.DocumentId is not int appDoc)
            throw new Aor.ValidationException(Lingua(
                $"«{ente.Name}» non ha una vIPI APP da spostare.", $"«{ente.Name}» has no APP vIPI to move."));
        // Senza posizioni il gruppo nascerebbe senza membri, e nessuna vista live lo troverebbe (revisione, S52).
        if (ente.Positions.Count == 0)
            throw new Aor.ValidationException(Lingua(
                $"«{ente.Name}» non ha posizioni: aggiungine una prima di spostarlo.",
                $"«{ente.Name}» has no positions: add one before moving it."));
        // Già copiato: un secondo gesto (o una seconda scheda) farebbe un secondo gruppo identico.
        if (await SpostamentoInCorsoAsync(unitId, ct) is { } gia)
            throw new Aor.ValidationException(Lingua(
                $"«{ente.Name}» è già nella vIPI dell'ACC {gia}: lo spostamento si conclude quando la pubblichi.",
                $"«{ente.Name}» is already in the {gia} ACC vIPI: the move completes when you publish it."));
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
            RemotizzazioneEsito? esito = null;
            // ⚠️ Tutto o niente (revisione, S52): erano sette scritture in fila senza rete. Un guasto dopo la copia
            // lasciava il gruppo nella bozza ACC con l'ente ancora «suo», e riprovando ne nasceva un secondo; il
            // rifiuto per profondità arrivava dopo aver già sciolto l'unione e creato la bozza.
            await _uow.ExecuteInTransactionAsync(async token =>
            {
                // 3. Fuori da un'eventuale unione PRIMA di copiare: la vIPI ACC non si unisce, e uscendo tornano
                //    visibili le sezioni che la scheda delle comuni aveva nascosto.
                var unione = await _unioni.ForDocumentAsync(appDoc, token);
                if (unione?.Of(appDoc) is { } membro) await _unioni.RimuoviMembroAsync(membro.MemberId, token);

                // 4. L'albero: dalla bozza della vIPI APP se c'è, altrimenti dalla versione pubblicata.
                var versioni = await _editing.ListVersionsAsync(appDoc, token);
                var sorgente = versioni.Where(v => v.Status == DocumentStatus.Draft).MaxBy(v => v.VersionNumber)
                               ?? versioni.Where(v => v.Status == DocumentStatus.Published).MaxBy(v => v.VersionNumber)
                               ?? versioni.MaxBy(v => v.VersionNumber)
                               ?? throw new Aor.ValidationException(Lingua("La vIPI APP non ha versioni.", "The APP vIPI has no versions."));
                var bozzaAcc = await _editing.CreateDraftAsync(accDoc, uid, token);
                var blocco = await _editing.CopyVersionIntoBlockAsync(sorgente.Id, bozzaAcc, "appgroup", ente.Name, token);

                // 5. Il profilo della vIPI APP diventa il blockmeta del gruppo: membri = posizioni dell'ente, ordine
                //    delle frequenze com'era, collegamenti da id di settore a nominativi (il gruppo li tiene per nome).
                var profilo = await _profili.GetAsync(appDoc, token);
                var collegati = profilo.FreqLinkSectorIds.Count == 0
                    ? new List<string>()
                    : (await _app.ResolveFreqLinksAsync(profilo.FreqLinkSectorIds, token)).Select(r => r.Callsign).ToList();
                var meta = new AccBlockMeta
                {
                    Key = "grp:" + Guid.NewGuid().ToString("N")[..8],
                    Kind = AccBlockKind.AppGroup,
                    MemberCallsigns = ente.Positions.ToList(),
                    FreqOrder = profilo.FreqOrder.ToList(),
                    FreqLinkCallsigns = collegati,
                    UnitId = ente.Id,
                };
                await _editing.SaveSectionBlockJsonBySectionAsync(blocco, JsonSerializer.Serialize(meta), uid, token);

                // 6. L'ente resta della sua vIPI APP, che resta pubblica: passa alla vIPI ACC quando quella va in
                //    vigore col gruppo (ConcludiSpostamentiAsync).
                esito = new RemotizzazioneEsito(ente.AccCode, accDoc, blocco, unione is not null);
            }, ct);
            return esito!;
        }
        finally
        {
            // Restituito solo se prima non c'era: chi aveva già la vIPI ACC in modifica la tiene.
            if (!giaMio) await _editing.ReleaseLockAsync(accDoc, uid, ct);
        }
    }

    public async Task<string?> SpostamentoInCorsoAsync(int unitId, CancellationToken ct = default)
    {
        var ente = await _enti.GetAsync(unitId, ct);
        if (ente is null || ente.Mode != AtcUnitMode.OwnDocument) return null;
        // Senza documento la vIPI ACC non ha gruppi, e chiedere il modello di lavoro la farebbe nascere.
        if ((await _accRepo.ResolveAccDocumentIdentityAsync(ente.AccCode, ct))?.DocumentId is null) return null;
        var modello = await _acc.LoadForEditAsync(ente.AccCode, ct);
        return modello.Blocks.Any(b => b.Block.Kind == AccBlockKind.AppGroup && b.Block.UnitId == unitId)
            ? ente.AccCode : null;
    }

    public async Task<IReadOnlyDictionary<int, string>> SpostamentiInCorsoAsync(CancellationToken ct = default)
    {
        var esito = new Dictionary<int, string>();
        var proprie = (await _enti.ListAsync(null, ct)).Where(u => u.Mode == AtcUnitMode.OwnDocument).ToList();
        foreach (var perAcc in proprie.GroupBy(u => u.AccCode, StringComparer.OrdinalIgnoreCase))
        {
            if ((await _accRepo.ResolveAccDocumentIdentityAsync(perAcc.Key, ct))?.DocumentId is null) continue;
            var nelGruppo = (await _acc.LoadForEditAsync(perAcc.Key, ct)).Blocks
                .Where(b => b.Block.Kind == AccBlockKind.AppGroup && b.Block.UnitId is not null)
                .Select(b => b.Block.UnitId!.Value).ToHashSet();
            foreach (var u in perAcc.Where(u => nelGruppo.Contains(u.Id))) esito[u.Id] = u.AccCode;
        }
        return esito;
    }

    public async Task<int> ConcludiSpostamentiAsync(CancellationToken ct = default)
    {
        var conclusi = 0;
        var inAttesa = (await _enti.ListAsync(null, ct))
            .Where(u => u.Mode == AtcUnitMode.OwnDocument && u.DocumentId is not null).ToList();
        foreach (var perAcc in inAttesa.GroupBy(u => u.AccCode, StringComparer.OrdinalIgnoreCase))
        {
            // La vIPI ACC IN VIGORE (null se nascosta o mai pubblicata): è quella che il pubblico vede.
            var vista = await _acc.LoadForViewAsync(perAcc.Key, ct);
            if (vista is null) continue;
            var nelGruppo = vista.Blocks
                .Where(b => b.Block.Kind == AccBlockKind.AppGroup && b.Block.UnitId is not null)
                .Select(b => b.Block.UnitId!.Value).ToHashSet();
            foreach (var ente in perAcc.Where(u => nelGruppo.Contains(u.Id)))
            {
                await _enti.SetModeAsync(ente.Id, AtcUnitMode.InAccVipi, ct);
                await _documenti.SetHiddenAsync(new ManagedDocRef(ReleaseTargetType.App, ente.Code, ente.DocumentId!.Value),
                    true, _authz.CurrentUserId ?? 0, ct);
                conclusi++;
            }
        }
        return conclusi;
    }
}
