using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Authoring della vIPI ACC sul modello unificato <c>Document</c> (doc refactor 08e-acc, strategia A/Opzione A). Sostituirà
/// il vecchio <c>AccProfileService</c>/<c>AccProfile</c> (rimossi in 08i): i blocchi vivono come sezioni radice del Document
/// (una per blocco) con le sezioni-catalogo come figlie; il metadata del blocco (natura/membri/override) nel <c>BodyJson</c>
/// del blocco proprio della sezione-blocco, config/aree/separations/vfr nei <c>BodyJson</c> delle figlie keyed. Le derivazioni
/// (freq/coord/AoR/config-table) restano calcolate live: si passa il blocco assemblato a <see cref="IAccProfileService"/>.
/// Questo service possiede solo il ciclo storage (Ensure/assembla/salva); l'autorizzazione è ACC-scoped.
/// </summary>
public interface IAccDocumentService
{
    /// <summary>Idempotente: garantisce il Document vIPI ACC (creato greenfield col blocco Aerovia di default se mancante,
    /// chiavizzato sul settore CTR radice primario) e ne ritorna l'Id. ACC-gated.</summary>
    Task<int> EnsureAsync(string accCode, CancellationToken ct = default);

    /// <summary>Identità del Document ACC (settore radice, codice/nome ACC, DocumentId se migrato). Null se l'ACC non esiste o non ha radici CTR.</summary>
    Task<AccDocumentIdentity?> GetIdentityAsync(string accCode, CancellationToken ct = default);

    /// <summary>Carica la vIPI ACC dalla versione di lavoro (bozza se esiste, sennò la pubblicata) assemblando i blocchi. ACC-gated; garantisce il Document.</summary>
    Task<AccDocumentModel> LoadForEditAsync(string accCode, CancellationToken ct = default);
}

/// <inheritdoc cref="IAccDocumentService"/>
public sealed class AccDocumentService : IAccDocumentService
{
    private readonly IAccProfileRepository _repo;
    private readonly IEditingRepository _editing;
    private readonly IEditAuthorizationService _authz;

    public AccDocumentService(IAccProfileRepository repo, IEditingRepository editing, IEditAuthorizationService authz)
    {
        _repo = repo;
        _editing = editing;
        _authz = authz;
    }

    private static string Norm(string s) => (s ?? "").Trim().ToUpperInvariant();

    // Sezioni "live" della vIPI ACC (derivate o rese live da componenti dedicati): ricevono un blocco placeholder alla
    // creazione così restano visibili nel viewer anche senza contenuto memorizzato. Union dei due profili ACC.
    private static readonly string[] LiveKeys =
        { "separations", "aor", "frequencies", "minima", "vfr", "coordination" };

    public Task<AccDocumentIdentity?> GetIdentityAsync(string accCode, CancellationToken ct = default) =>
        _repo.ResolveAccDocumentIdentityAsync(Norm(accCode), ct);

    public async Task<int> EnsureAsync(string accCode, CancellationToken ct = default)
    {
        accCode = Norm(accCode);
        var id = await _repo.ResolveAccDocumentIdentityAsync(accCode, ct)
            ?? throw new Aor.ValidationException($"ACC {accCode} inesistente o senza settori CTR.");
        if (id.DocumentId is int existing) return existing;   // già migrato

        await _authz.EnsureCanEditAccAsync(accCode, ct);

        // Struttura di default: un solo blocco Aerovia con le sezioni del catalogo. I gruppi APP si aggiungono dall'editor.
        var aerovia = new VipiBlockSpec("aerovia", "Settori di aerovia",
            SectionCatalog.For(SectionProfile.AccAerovia).Select(d => (d.Key, d.Title)).ToList());

        return await _editing.EnsureVipiDocumentTreeAsync(id.SectorId, $"vIPI {id.AccName}", Language.It,
            new[] { aerovia }, _authz.CurrentUserId ?? 0, LiveKeys, ct);
    }

    public async Task<AccDocumentModel> LoadForEditAsync(string accCode, CancellationToken ct = default)
    {
        accCode = Norm(accCode);
        var id = await _repo.ResolveAccDocumentIdentityAsync(accCode, ct)
            ?? throw new Aor.ValidationException($"ACC {accCode} inesistente o senza settori CTR.");
        await _authz.EnsureCanEditAccAsync(accCode, ct);

        var docId = await EnsureAsync(accCode, ct);
        var doc = await _editing.LoadForEditAsync(docId, ct)
            ?? throw new Aor.ValidationException($"vIPI ACC {accCode} senza versione di lavoro.");

        var blocks = AccDocumentAssembler.Assemble(doc);
        return new AccDocumentModel(doc.DocumentId, doc.VersionId, doc.IsEditable, accCode, id.AccName, blocks);
    }
}
