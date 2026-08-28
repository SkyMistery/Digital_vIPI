using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;

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
/// <remarks>
/// ⚠️ Fino al 26 agosto 2026 questa riga portava anche un <c>RenameCandidate</c>: il possibile nome nuovo
/// della stessa posizione, quando ce n'era uno solo. Era un'ipotesi, e le ipotesi qui costano care — il caso
/// vero (<c>LIRR_NE1_CTR</c> nato accanto a <c>LIRR_NE_CTR</c> con la stessa frequenza e lo stesso nome IVAO)
/// era uno <b>sdoppiamento</b> che l'euristica avrebbe chiamato rinomina, spostando un documento sul settore
/// sbagliato. Adesso le rinomine le riconosce l'identità della sorgente e sono già applicate quando si
/// arriva qui, quindi un orfano è un orfano.
/// </remarks>
public sealed record OrphanSectorRow(
    int SectorId, string Callsign, string Name, string AccCode, OrphanReason Reason,
    int? DocumentId, string? DocumentTitle, IReadOnlyList<AffectedDoc> Documents,
    IReadOnlyList<string> Blockers, DateTime? LastSeenUtc = null);

/// <summary>Un settore attivo a cui si può riappendere il documento di un orfano.</summary>
public sealed record ReattachTargetRow(int SectorId, string Callsign, string Name);

/// <summary>
/// I <b>settori orfani</b>: quelli che la proiezione ha disattivato perché i cataloghi non li confermano più.
/// Fino al 25 agosto 2026 non esisteva nessun posto dove vederli — la proiezione li disattivava, recideva il
/// loro legame al documento e non lo diceva a nessuno. Ora il legame resta, la casella avvisa, e da qui una
/// persona decide: <b>riaggancia</b> il documento a un altro settore, oppure lo <b>elimina</b>.
///
/// <para>⚠️ L'eliminazione non abita più qui. Dal 26 agosto 2026 la fa <see cref="IDeletionService"/>, che è
/// il motore unico di tutto il sistema: questo servizio dice <b>chi</b> sono gli orfani, non come si
/// tolgono. Prima ne aveva uno suo, con una sua idea di cosa fosse lecito.</para>
/// </summary>
public interface IOrphanSectorService
{
    /// <summary>Gli orfani di un ACC, o di tutti gli ACC se <paramref name="accCode"/> è vuoto.</summary>
    Task<IReadOnlyList<OrphanSectorRow>> ListAsync(string? accCode = null, CancellationToken ct = default);

    /// <summary>I settori <b>attivi</b> dello stesso ACC a cui si può riappendere il documento.</summary>
    Task<IReadOnlyList<ReattachTargetRow>> ReattachTargetsAsync(int orphanSectorId, CancellationToken ct = default);

    /// <summary>Sposta il documento (e il ruolo di primario) dall'orfano al settore indicato.</summary>
    Task ReattachAsync(int orphanSectorId, int targetSectorId, CancellationToken ct = default);

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
        // ⚠️ Editor, non admin: gli orfani si leggono da Struttura, che è una pagina del contenuto. Finché
        // qui c'era EnsureAdmin, un chief d'ACC apriva la pagina e prendeva un 500 — la pagina si era
        // aperta, il servizio no. Trovato dalla verifica live del 29 agosto 2026, non dalla suite.
        _authz.EnsureAtLeast(VipiRole.Editor);
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

    private async Task EnsureCanEditAsync(int orphanSectorId, CancellationToken ct)
    {
        var acc = await _repo.GetAccCodeAsync(orphanSectorId, ct);
        if (acc is not null) _authz.EnsureAtLeast(VipiRole.Editor);
        else _authz.EnsureAdmin();
    }
}
