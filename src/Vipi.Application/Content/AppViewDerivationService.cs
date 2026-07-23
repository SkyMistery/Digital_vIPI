using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Sezioni derivate dell'APP standalone risolte per la vista (frozen o live) — doc 10 §3d. config-table resta
/// fuori: deriva da input già congelati → sempre live nella pagina.</summary>
public sealed record AppViewDerived(IReadOnlyList<AppFreqRow> Freqs, AppCoordination Coord, AccAorView Aor);

/// <summary>
/// Risolve le sezioni derivate (freq/coord/aor) dell'APP standalone per la VISTA (doc 10 §3d): se <paramref
/// name="useFrozen"/> e c'è una release effettiva, legge l'output CONGELATO dallo snapshot per chiave di sezione;
/// altrimenti deriva live via <see cref="IAppDocumentService"/>. Chiave di release = callsign APP. Separato dallo
/// storage per non accoppiarlo alle derivazioni; la cattura salva SOLO le sezioni Frozen → per una Live il reader
/// ritorna null e si ricade su live (nessun check di RenderMode qui).
/// </summary>
public interface IAppViewDerivationService
{
    Task<AppViewDerived> ResolveForViewAsync(string appCallsign, bool useFrozen, CancellationToken ct = default);
}

/// <inheritdoc cref="IAppViewDerivationService"/>
public sealed class AppViewDerivationService : IAppViewDerivationService
{
    private readonly IAppDocumentService _app;
    private readonly IFrozenSectionReader _frozen;

    public AppViewDerivationService(IAppDocumentService app, IFrozenSectionReader frozen)
    {
        _app = app;
        _frozen = frozen;
    }

    public async Task<AppViewDerived> ResolveForViewAsync(string appCallsign, bool useFrozen, CancellationToken ct = default)
    {
        var app = (appCallsign ?? "").Trim().ToUpperInvariant();

        var freqs = (useFrozen ? await FrozenAsync<List<AppFreqRow>>("frequencies") : null)
            ?? (await _app.DeriveFrequenciesAsync(app, ct)).ToList();
        var coord = (useFrozen ? await FrozenAsync<AppCoordination>("coordination") : null)
            ?? await _app.DeriveCoordinationAsync(app, ct);
        var aor = (useFrozen ? await FrozenAsync<AccAorView>("aor") : null)
            ?? await _app.GetAorViewAsync(app, ct);
        return new AppViewDerived(freqs, coord, aor);

        Task<T?> FrozenAsync<T>(string key) where T : class =>
            _frozen.GetFrozenByKeyAsync<T>(ReleaseTargetType.App, app, key, ct);
    }
}
