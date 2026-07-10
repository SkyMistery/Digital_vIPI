using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Authoring dell'APP standalone sul modello unificato <c>Document</c> (doc refactor 08e, strategia A). Sostituisce
/// progressivamente <c>IAppProfileService</c>/<c>AppProfile</c>: le sezioni vivono in <c>DocumentSection</c>+
/// <c>ContentBlock</c>, gli override derivati in <c>DocumentProfile</c>. Le sezioni derivate (freq/coord/AoR) restano
/// calcolate live dai cataloghi.
/// </summary>
public interface IAppDocumentService
{
    /// <summary>Idempotente: garantisce il Document vIPI dell'APP (creato greenfield dalle sezioni di catalogo se
    /// mancante) e ne ritorna l'Id. ACC-gated.</summary>
    Task<int> EnsureAsync(string appCallsign, CancellationToken ct = default);
}

/// <inheritdoc cref="IAppDocumentService"/>
public sealed class AppDocumentService : IAppDocumentService
{
    private readonly IAppProfileRepository _apps;
    private readonly IEditingRepository _editing;
    private readonly IEditAuthorizationService _authz;

    public AppDocumentService(IAppProfileRepository apps, IEditingRepository editing, IEditAuthorizationService authz)
    {
        _apps = apps;
        _editing = editing;
        _authz = authz;
    }

    public async Task<int> EnsureAsync(string appCallsign, CancellationToken ct = default)
    {
        var callsign = (appCallsign ?? "").Trim().ToUpperInvariant();
        var id = await _apps.ResolveForDocumentAsync(callsign, ct)
            ?? throw new Aor.ValidationException($"APP {callsign} inesistente.");
        if (id.DocumentId is int existing) return existing;   // già migrato

        await _authz.EnsureCanEditAccAsync(id.AccCode, ct);

        // Greenfield: sezioni radice = membership di catalogo del profilo App (chiave + titolo, nell'ordine di default).
        var sections = SectionCatalog.For(SectionProfile.App).Select(d => (d.Key, d.Title)).ToList();
        return await _editing.EnsureVipiDocumentAsync(id.SectorId, id.Title, Language.It, sections, _authz.CurrentUserId ?? 0, ct);
    }
}
