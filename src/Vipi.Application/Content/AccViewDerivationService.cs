using Vipi.Application.Abstractions;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Risolve le sezioni derivate (freq/coord/aor) della vIPI ACC per la VISTA (doc 10 §3d): se richiesto e c'è una
/// release effettiva, legge l'output CONGELATO dallo snapshot per Id sotto-sezione; altrimenti deriva live. Separato da
/// <see cref="IAccDocumentService"/> (storage) per non accoppiare lo storage alle derivazioni. La cattura salva SOLO le
/// sezioni Frozen → per una sezione Live il reader ritorna null e si ricade su live (nessun check di RenderMode qui).
/// </summary>
public interface IAccViewDerivationService
{
    Task<AccDerivedSections> ResolveForViewAsync(string accCode, IReadOnlyList<AccAssembledBlock> blocks, bool useFrozen, CancellationToken ct = default);
}

/// <inheritdoc cref="IAccViewDerivationService"/>
public sealed class AccViewDerivationService : IAccViewDerivationService
{
    private readonly IAccDerivationRepository _repo;
    private readonly IAccDerivationService _deriv;
    private readonly IFrozenSectionReader _frozen;

    public AccViewDerivationService(IAccDerivationRepository repo, IAccDerivationService deriv, IFrozenSectionReader frozen)
    {
        _repo = repo;
        _deriv = deriv;
        _frozen = frozen;
    }

    public async Task<AccDerivedSections> ResolveForViewAsync(string accCode, IReadOnlyList<AccAssembledBlock> blocks, bool useFrozen, CancellationToken ct = default)
    {
        accCode = (accCode ?? "").Trim().ToUpperInvariant();
        // Chiave/root dall'identità (come AccDocumentService.LoadForViewAsync e il target di release): garantisce che
        // relKey combaci con quella sotto cui la cattura ha salvato lo snapshot.
        var id = await _repo.ResolveAccDocumentIdentityAsync(accCode, ct);
        var root = id?.RootCallsign;
        var relKey = $"{accCode}|{root}";

        // Lo snapshot una volta sola, non una per sotto-sezione: con due blocchi erano otto letture dello
        // stesso payload da 62 KB a ogni apertura della pagina pubblica (doc 14 §3c).
        var frozen = useFrozen ? await _frozen.LoadAsync(ReleaseTargetType.AccVipi, relKey, ct) : FrozenSections.Empty;

        var freqs = new Dictionary<string, IReadOnlyList<AppFreqRow>>(StringComparer.OrdinalIgnoreCase);
        var coord = new Dictionary<string, AccCoordination>(StringComparer.OrdinalIgnoreCase);
        var aor = new Dictionary<string, AccAorView>(StringComparer.OrdinalIgnoreCase);
        var minima = new Dictionary<string, MinimaView>(StringComparer.OrdinalIgnoreCase);

        foreach (var ab in blocks)
        {
            freqs[ab.Block.Key] = Congelata<List<AppFreqRow>>(ab, "frequencies")
                ?? (await _deriv.DeriveFrequenciesAsync(accCode, ab.Block, root, ct)).ToList();
            coord[ab.Block.Key] = Congelata<AccCoordination>(ab, "coordination")
                ?? await _deriv.DeriveCoordinationAsync(accCode, ab.Block, root, ct);
            aor[ab.Block.Key] = Congelata<AccAorView>(ab, "aor")
                ?? await _deriv.DeriveAorViewAsync(accCode, ab.Block, root, ct);
            minima[ab.Block.Key] = Congelata<MinimaView>(ab, "minima")
                ?? await _deriv.DeriveMinimaAsync(accCode, ab.Block, root, ct);
        }
        return new AccDerivedSections(freqs, coord, aor, minima);

        // Frozen della sotto-sezione, keyato per Id (== RawSection.Id catturato); null se non catturata (Live/assente).
        T? Congelata<T>(AccAssembledBlock ab, string key) where T : class =>
            ab.ChildSectionIdsByKey.TryGetValue(key, out var sid) ? frozen.Get<T>(sid) : null;
    }
}
