using Vipi.Application.Abstractions;
using Vipi.Application.Auth;

namespace Vipi.Application.Content;

/// <summary>Perché un settore è finito in questo elenco. Tre fatti diversi, tre rimedi diversi.</summary>
public enum OrphanReason
{
    /// <summary>L'ha nascosto un admin: il callsign è ancora in catalogo, il settore è spento.</summary>
    Hidden,

    /// <summary>Il callsign non è più in catalogo: qualcuno ha tolto la riga, e il settore è spento.</summary>
    Gone,

    /// <summary>
    /// ⚠️ Il caso della <b>rinomina</b>, e l'unico in cui il settore è ancora <b>attivo</b>: la riga di
    /// catalogo c'è, ma la sorgente ha smesso di mandarla (timbro d'import rimasto indietro). Nessuno
    /// sparisce, quindi nient'altro se ne accorge: il fantasma continua a rivendicare la sua area e a
    /// portarsi dietro il documento mentre chi controlla si connette col nome nuovo.
    /// </summary>
    NotListed,
}

/// <summary>Un settore proiettato che i cataloghi non confermano più.</summary>
/// <param name="Documents">I documenti che lo raccontano: chi resta scoperto se lo si toglie.</param>
/// <param name="Blockers">Chi lo referenzia con un vincolo che <b>impedisce</b> la cancellazione, già in
/// frasi leggibili. Vuoto = si può rimuovere.</param>
/// <param name="LastSeenUtc">Ultimo timbro d'import (solo per <see cref="OrphanReason.NotListed"/>).</param>
/// <param name="RenameCandidate">
/// Il possibile nome nuovo della stessa posizione, quando ce n'è <b>uno solo</b>. È un suggerimento, non una
/// conclusione: con due candidati non è una rinomina ma uno sdoppiamento — che è proprio quel che significa
/// la cifra in <c>US0</c>/<c>US1</c> — e la macchina non sa distinguerli.
/// </param>
public sealed record OrphanSectorRow(
    int SectorId, string Callsign, string Name, string AccCode, OrphanReason Reason,
    int? DocumentId, string? DocumentTitle, IReadOnlyList<AffectedDoc> Documents,
    IReadOnlyList<string> Blockers, DateTime? LastSeenUtc = null, string? RenameCandidate = null);

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
    private readonly IImportStateStore _stati;

    public OrphanSectorService(IOrphanSectorRepository repo, IEditAuthorizationService authz,
        IImportStateStore stati)
    {
        _repo = repo;
        _authz = authz;
        _stati = stati;
    }

    /// <summary>La stessa soglia che usa il giro notturno: due letture diverse dello stesso metro sono il
    /// modo in cui due racconti divergono.</summary>
    private async Task<DateTime?> SogliaTimbroAsync(CancellationToken ct) =>
        SogliaTimbro.Calcola(
            await _stati.GetLastSuccessAsync(ImportCategories.AirportSector, ct),
            await _stati.GetLastSuccessAsync(ImportCategories.Acc, ct));

    public async Task<IReadOnlyList<OrphanSectorRow>> ListAsync(string? accCode = null, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        var soglia = await SogliaTimbroAsync(ct);

        // La stessa guardia del giro notturno: se gli stantìi sono troppi il guasto è a monte, e l'elenco
        // mostra solo gli orfani veri invece di trenta righe che non vogliono dire niente.
        if (soglia is { } s
            && SogliaTimbro.TroppiPerEssereVeri(
                (await _repo.ListStaleCatalogRowsAsync(s, ct)).Count, await _repo.CountCatalogRowsAsync(ct)))
            soglia = null;

        return await _repo.ListOrphansAsync(
            string.IsNullOrWhiteSpace(accCode) ? null : accCode!.Trim(), soglia, ct);
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

        var riga = await _repo.GetOrphanAsync(orphanSectorId, await SogliaTimbroAsync(ct), ct)
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
