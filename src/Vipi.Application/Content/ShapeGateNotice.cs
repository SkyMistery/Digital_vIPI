using Vipi.Domain;
using Vipi.Domain.Services;

namespace Vipi.Application.Content;

/// <summary>Un settore del perimetro di un documento, con lo stato della sua shape e come scriverci sopra.</summary>
/// <param name="Catalog">Da quale dei due cataloghi viene: serve alla forzatura.</param>
public sealed record ShapeGateRow(
    SourceCatalog Catalog, int Id, string Callsign, string? Name, ShapeState Shape);

/// <summary>Il perimetro di un documento: i suoi settori e la ACC che lo governa (per il permesso).</summary>
/// <param name="AccCode">Null quando il bersaglio non è governato da una ACC (allora comanda il documento).</param>
/// <param name="DocumentId">Il documento, quando è lui il bersaglio (vLOA). Null altrimenti.</param>
public sealed record ShapeGateScope(string? AccCode, int? DocumentId, IReadOnlyList<ShapeGateRow> Rows)
{
    public static readonly ShapeGateScope Empty = new(null, null, Array.Empty<ShapeGateRow>());
}

/// <summary>Che cosa aspetta il suo ciclo: un'area di settore o di torre, una radioassistenza, una carta MRVA.</summary>
public enum DeferredKind { Area, Radioassistenza, CartaMrva }

/// <summary>Una riga d'avviso: quel dato, in questa release, porterebbe la versione <b>precedente</b>.</summary>
/// <param name="Callsign">Chi è: il callsign del settore, l'identità della radioassistenza, il file della carta.</param>
/// <param name="FromCycle">Il ciclo dal quale la versione nuova entra in vigore.</param>
public sealed record DeferredShapeNotice(string Callsign, string? Name, string FromCycle,
    DeferredKind Kind = DeferredKind.Area);

/// <summary>
/// Un dato del sectorfile che non è un'area — radioassistenza o carta MRVA — con un cambio che aspetta il suo ciclo
/// (U-037). <paramref name="Id"/> è quello della sua tabella; serve alla forzatura.
/// </summary>
public sealed record SectorfileDeferral(DeferredKind Kind, int Id, string Label, string FromCycle, bool Forced);

/// <summary>
/// Radioassistenze e carte MRVA con un cambio in attesa, nel perimetro di un documento, e la loro forzatura.
///
/// <para>Il perimetro, come per le aree, è largo dalla parte giusta: le radioassistenze che il documento
/// <b>cita</b> in una sua versione qualsiasi, le carte MRVA dell'ente (per una vIPI ACC l'enroute e quelle degli
/// aeroporti della ACC, per un APP quella del suo aeroporto).</para>
/// </summary>
public interface ISectorfileGateRepository
{
    Task<IReadOnlyList<SectorfileDeferral>> ListAsync(
        ReleaseTargetType target, string key, int? documentId, CancellationToken ct = default);

    /// <summary>Accende la forzatura sulle righe indicate. Ritorna quante ne ha toccate.</summary>
    Task<int> ForceAsync(IReadOnlyList<(DeferredKind Kind, int Id)> rows, CancellationToken ct = default);
}

/// <summary>I settori che un documento può disegnare, e la forzatura della loro shape.</summary>
public interface IShapeGateRepository
{
    /// <summary>Il perimetro del bersaglio di release: quali settori può disegnare quel documento.</summary>
    Task<ShapeGateScope> GetScopeAsync(ReleaseTargetType target, string key, CancellationToken ct = default);

    /// <summary>Accende <c>ShapeForcePublished</c> sulle righe indicate. Ritorna quante ne ha toccate.</summary>
    Task<int> SetForcePublishedAsync(
        IReadOnlyList<(SourceCatalog Catalog, int Id)> rows, CancellationToken ct = default);
}

/// <inheritdoc cref="ShapeGateNoticeService"/>
public interface IShapeGateNoticeService
{
    /// <summary>
    /// Le aree che, pubblicando questo documento per uno dei cicli indicati, resterebbero indietro: la
    /// geometria nuova non è ancora in vigore e la release porterebbe la precedente.
    /// </summary>
    Task<IReadOnlyList<DeferredShapeNotice>> ListDeferredAsync(
        ReleaseTargetType target, string key, IReadOnlyList<string> cycles, CancellationToken ct = default);

    /// <summary>
    /// «Pubblicale lo stesso»: accende la forzatura su tutte le aree differite del perimetro. Chiede il
    /// permesso di modifica come qualsiasi altro atto editoriale.
    /// </summary>
    Task<int> ForcePublishAsync(
        ReleaseTargetType target, string key, IReadOnlyList<string> cycles, CancellationToken ct = default);
}

/// <summary>
/// L'avviso a chi pubblica: <b>c'è un'area nuova che a questo ciclo non è ancora in vigore</b>.
///
/// <para><b>Perché serve.</b> Il gate AIRAC (<see cref="ShapeAiracGate"/>) fa già la cosa giusta da solo —
/// congelando una release mette la geometria in vigore <i>al ciclo di quella release</i>, non quella
/// dell'editor. Ma lo fa in silenzio: chi pubblica vede a schermo il confine nuovo e nel documento ne trova
/// un altro, senza che niente glielo abbia detto. E se il confine nuovo è una <b>correzione urgente</b>,
/// l'unica strada era aspettare il ciclo.</para>
///
/// <para>Qui l'informazione viene a galla nel posto dove si pubblica, con l'interruttore accanto: è il
/// gemello di <c>AirportProcedure.ForcePublished</c>, che a schermo un interruttore ce l'ha già.</para>
///
/// <para>⚠️ <b>Nessuna regola nuova.</b> La domanda «è differita?» la fa <see cref="ShapeAiracGate"/>, la
/// stessa che usa il congelamento: se le due divergessero, l'avviso mentirebbe.</para>
/// </summary>
public sealed class ShapeGateNoticeService : IShapeGateNoticeService
{
    private readonly IShapeGateRepository _repo;
    private readonly IAiracService _airac;
    private readonly Auth.IEditAuthorizationService _authz;
    private readonly ISectorfileGateRepository? _altri;
    private readonly Abstractions.IReleaseTargetRegistry? _targets;

    /// <param name="altri">U-037: radioassistenze e carte MRVA, che dal 28 settembre 2026 aspettano il loro ciclo
    /// come le aree. Senza, l'avviso parla delle sole aree — il comportamento di prima.</param>
    /// <param name="targets">Per trovare il documento del bersaglio, cioè quali radioassistenze cita.</param>
    public ShapeGateNoticeService(
        IShapeGateRepository repo, IAiracService airac, Auth.IEditAuthorizationService authz,
        ISectorfileGateRepository? altri = null, Abstractions.IReleaseTargetRegistry? targets = null)
    {
        _repo = repo;
        _airac = airac;
        _authz = authz;
        _altri = altri;
        _targets = targets;
    }

    public async Task<IReadOnlyList<DeferredShapeNotice>> ListDeferredAsync(
        ReleaseTargetType target, string key, IReadOnlyList<string> cycles, CancellationToken ct = default)
    {
        var scope = await _repo.GetScopeAsync(target, key, ct);
        var aree = Differite(scope, cycles)
            .Select(r => new DeferredShapeNotice(r.Callsign, r.Name, r.Shape.FromCycle!))
            .OrderBy(n => n.Callsign, StringComparer.OrdinalIgnoreCase);
        var altri = (await AltriDifferitiAsync(target, key, cycles, ct))
            .Select(d => new DeferredShapeNotice(d.Label, null, d.FromCycle, d.Kind))
            .OrderBy(n => n.Kind).ThenBy(n => n.Callsign, StringComparer.OrdinalIgnoreCase);
        return aree.Concat(altri).ToList();
    }

    public async Task<int> ForcePublishAsync(
        ReleaseTargetType target, string key, IReadOnlyList<string> cycles, CancellationToken ct = default)
    {
        var scope = await _repo.GetScopeAsync(target, key, ct);
        var docId = await DocumentoAsync(target, key, ct);

        // Il permesso è quello del documento che si sta pubblicando: forzare una shape è un atto editoriale,
        // non un'operazione di sistema. Chi non può pubblicare quel documento non può nemmeno forzarne le aree.
        if (scope.AccCode is { Length: > 0 } || scope.DocumentId is not null || docId is not null)
            _authz.EnsureAtLeast(VipiRole.Editor);
        else return 0;   // perimetro sconosciuto: non si tocca niente

        var righe = Differite(scope, cycles).Select(r => (r.Catalog, r.Id)).ToList();
        var forzate = righe.Count == 0 ? 0 : await _repo.SetForcePublishedAsync(righe, ct);

        var altri = (await AltriDifferitiAsync(target, key, cycles, ct)).Select(d => (d.Kind, d.Id)).ToList();
        if (altri.Count > 0 && _altri is not null) forzate += await _altri.ForceAsync(altri, ct);
        return forzate;
    }

    /// <summary>
    /// U-037: radioassistenze e carte MRVA del perimetro che a uno dei cicli in gioco porterebbero la versione di
    /// prima. La domanda «è differita?» è la stessa delle aree (<see cref="ShapeAiracGate.IsDeferredAt"/>): se le
    /// due divergessero, l'avviso mentirebbe.
    /// </summary>
    private async Task<IReadOnlyList<SectorfileDeferral>> AltriDifferitiAsync(
        ReleaseTargetType target, string key, IReadOnlyList<string> cycles, CancellationToken ct)
    {
        if (_altri is null) return Array.Empty<SectorfileDeferral>();
        var validi = cycles.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.Ordinal).ToList();
        if (validi.Count == 0) return Array.Empty<SectorfileDeferral>();

        var righe = await _altri.ListAsync(target, key, await DocumentoAsync(target, key, ct), ct);
        return righe.Where(d => validi.Any(c => ShapeAiracGate.IsDeferredAt(
                // Una versione «in vigore» c'è sempre qui: è il motivo per cui la riga esiste.
                new ShapeState("·", "·", d.FromCycle, ShapeSource.Sectorfile, d.Forced), c, _airac)))
            .ToList();
    }

    private async Task<int?> DocumentoAsync(ReleaseTargetType target, string key, CancellationToken ct)
    {
        if (_targets is null) return null;
        try { return await _targets.For(target).ResolveDocumentIdAsync(key, ct); }
        catch (KeyNotFoundException) { return null; }   // tipo non registrato: nessun documento da guardare
    }

    /// <summary>
    /// Le righe differite ad <b>almeno uno</b> dei cicli in gioco. I cicli sono due perché i tasti sono due:
    /// «pubblica ora» usa il ciclo corrente, «pubblica al ciclo» quello scelto nella tendina. Avvisare per
    /// l'unione non sbaglia mai per difetto — ed è il difetto che conta, qui.
    /// </summary>
    private IEnumerable<ShapeGateRow> Differite(ShapeGateScope scope, IReadOnlyList<string> cycles)
    {
        var validi = cycles.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.Ordinal).ToList();
        if (validi.Count == 0) return Array.Empty<ShapeGateRow>();
        return scope.Rows.Where(r => validi.Any(c => ShapeAiracGate.IsDeferredAt(r.Shape, c, _airac)));
    }
}
