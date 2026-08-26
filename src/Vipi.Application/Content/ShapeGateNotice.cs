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

/// <summary>Una riga d'avviso: quel settore, in questa release, porterebbe l'area <b>precedente</b>.</summary>
/// <param name="FromCycle">Il ciclo dal quale l'area nuova entra in vigore.</param>
public sealed record DeferredShapeNotice(string Callsign, string? Name, string FromCycle);

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
/// gemello di <c>AirportSid.ForcePublished</c>, che a schermo un interruttore ce l'ha già.</para>
///
/// <para>⚠️ <b>Nessuna regola nuova.</b> La domanda «è differita?» la fa <see cref="ShapeAiracGate"/>, la
/// stessa che usa il congelamento: se le due divergessero, l'avviso mentirebbe.</para>
/// </summary>
public sealed class ShapeGateNoticeService : IShapeGateNoticeService
{
    private readonly IShapeGateRepository _repo;
    private readonly IAiracService _airac;
    private readonly Auth.IEditAuthorizationService _authz;

    public ShapeGateNoticeService(
        IShapeGateRepository repo, IAiracService airac, Auth.IEditAuthorizationService authz)
    {
        _repo = repo;
        _airac = airac;
        _authz = authz;
    }

    public async Task<IReadOnlyList<DeferredShapeNotice>> ListDeferredAsync(
        ReleaseTargetType target, string key, IReadOnlyList<string> cycles, CancellationToken ct = default)
    {
        var scope = await _repo.GetScopeAsync(target, key, ct);
        return Differite(scope, cycles)
            .Select(r => new DeferredShapeNotice(r.Callsign, r.Name, r.Shape.FromCycle!))
            .OrderBy(n => n.Callsign, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<int> ForcePublishAsync(
        ReleaseTargetType target, string key, IReadOnlyList<string> cycles, CancellationToken ct = default)
    {
        var scope = await _repo.GetScopeAsync(target, key, ct);

        // Il permesso è quello del documento che si sta pubblicando: forzare una shape è un atto editoriale,
        // non un'operazione di sistema. Chi non può pubblicare quel documento non può nemmeno forzarne le aree.
        if (scope.AccCode is { Length: > 0 } acc) await _authz.EnsureCanEditAccAsync(acc, ct);
        else if (scope.DocumentId is { } docId) await _authz.EnsureCanEditDocumentAsync(docId, ct);
        else return 0;   // perimetro sconosciuto: non si tocca niente

        var righe = Differite(scope, cycles).Select(r => (r.Catalog, r.Id)).ToList();
        return righe.Count == 0 ? 0 : await _repo.SetForcePublishedAsync(righe, ct);
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
