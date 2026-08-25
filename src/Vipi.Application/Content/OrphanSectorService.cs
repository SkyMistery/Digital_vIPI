using Vipi.Application.Abstractions;
using Vipi.Application.Auth;

namespace Vipi.Application.Content;

/// <summary>Un settore proiettato che i cataloghi non confermano più: disattivato, ma ancora in archivio.</summary>
/// <param name="Documents">I documenti che lo raccontano: chi resta scoperto se lo si toglie.</param>
/// <param name="Blockers">Chi lo referenzia con un vincolo che <b>impedisce</b> la cancellazione, già in
/// frasi leggibili. Vuoto = si può rimuovere.</param>
public sealed record OrphanSectorRow(
    int SectorId, string Callsign, string Name, string AccCode, bool StillInCatalog,
    int? DocumentId, string? DocumentTitle, IReadOnlyList<AffectedDoc> Documents,
    IReadOnlyList<string> Blockers);

/// <summary>Un settore attivo a cui si può riappendere il documento di un orfano.</summary>
public sealed record ReattachTargetRow(int SectorId, string Callsign, string Name);

/// <summary>
/// I <b>settori orfani</b>: quelli che la proiezione ha disattivato perché i cataloghi non li confermano più.
/// Fino al 25 agosto 2026 non esisteva nessun posto dove vederli — la proiezione li disattivava, recideva il
/// loro legame al documento e non lo diceva a nessuno. Ora il legame resta, la casella avvisa, e da qui una
/// persona decide: <b>riaggancia</b> il documento a un altro settore, oppure <b>rimuovi</b> per sempre.
///
/// <para>⚠️ «Rimuovi» non è definitivo nel senso che ci si aspetta: se la sorgente rimanda quel callsign, il
/// prossimo import lo ricrea. È scritto nella UI perché non sembri un guasto.</para>
/// </summary>
public interface IOrphanSectorService
{
    /// <summary>Gli orfani di un ACC, o di tutti gli ACC se <paramref name="accCode"/> è vuoto.</summary>
    Task<IReadOnlyList<OrphanSectorRow>> ListAsync(string? accCode = null, CancellationToken ct = default);

    /// <summary>I settori <b>attivi</b> dello stesso ACC a cui si può riappendere il documento.</summary>
    Task<IReadOnlyList<ReattachTargetRow>> ReattachTargetsAsync(int orphanSectorId, CancellationToken ct = default);

    /// <summary>Sposta il documento (e il ruolo di primario) dall'orfano al settore indicato.</summary>
    Task ReattachAsync(int orphanSectorId, int targetSectorId, CancellationToken ct = default);

    /// <summary>
    /// Toglie l'orfano dall'archivio: la riga proiettata e, se ancora presente, quella di catalogo.
    /// Rifiuta con un messaggio leggibile se qualcosa lo referenzia (sotto-settori, accordi, parti di vLOA,
    /// blocchi di contenuto) invece di lasciar esplodere il vincolo del database in faccia all'utente.
    /// </summary>
    Task RemoveAsync(int orphanSectorId, CancellationToken ct = default);
}

/// <inheritdoc cref="IOrphanSectorService"/>
public sealed class OrphanSectorService : IOrphanSectorService
{
    private readonly IOrphanSectorRepository _repo;
    private readonly IEditAuthorizationService _authz;

    public OrphanSectorService(IOrphanSectorRepository repo, IEditAuthorizationService authz)
    {
        _repo = repo;
        _authz = authz;
    }

    public Task<IReadOnlyList<OrphanSectorRow>> ListAsync(string? accCode = null, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        return _repo.ListOrphansAsync(string.IsNullOrWhiteSpace(accCode) ? null : accCode!.Trim(), ct);
    }

    public async Task<IReadOnlyList<ReattachTargetRow>> ReattachTargetsAsync(int orphanSectorId, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(orphanSectorId, ct);
        return await _repo.ReattachTargetsAsync(orphanSectorId, ct);
    }

    public async Task ReattachAsync(int orphanSectorId, int targetSectorId, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(orphanSectorId, ct);
        await _repo.ReattachAsync(orphanSectorId, targetSectorId, ct);
    }

    public async Task RemoveAsync(int orphanSectorId, CancellationToken ct = default)
    {
        // Rimuovere è un atto d'archivio, non di redazione: lo fa un admin. Riagganciare invece è editoriale,
        // e basta poter editare l'ACC.
        _authz.EnsureAdmin();

        var riga = await _repo.GetOrphanAsync(orphanSectorId, ct)
                   ?? throw new Aor.ValidationException("Settore orfano inesistente o tornato attivo.");
        if (riga.Blockers.Count > 0)
            throw new Aor.ValidationException(
                "Non si può rimuovere: " + string.Join("; ", riga.Blockers) + ".");

        await _repo.RemoveAsync(orphanSectorId, ct);
    }

    private async Task EnsureCanEditAsync(int orphanSectorId, CancellationToken ct)
    {
        var acc = await _repo.GetAccCodeAsync(orphanSectorId, ct);
        if (acc is not null) await _authz.EnsureCanEditAccAsync(acc, ct);
        else _authz.EnsureAdmin();
    }
}
