using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using static Vipi.Application.Messaggio;

namespace Vipi.Application.Content;

/// <summary>
/// Il lock del documento di uno scalo, come guardia delle scritture editoriali (carta
/// <c>docs/feature/2026-09-04-aeroporto-porta-sola.md</c>).
///
/// <para><b>Perché è una porta sola.</b> I dati di un aeroporto si scrivono da due service —
/// <see cref="IAirportEditingService"/> (piste, SID, quote, frequenze) e <see cref="IAirportSectorService"/>
/// (limiti, nascondi, principale, «di ACC») — e da due editor. Una garanzia scritta due volte è una garanzia
/// che fra sei mesi vale in un posto solo.</para>
///
/// <para>⚠️ <b>Vincola l'uomo, non i job.</b> Gli import di sfondo (anagrafica IVAO, SID dal sectorfile,
/// settori) non passano da questi service: scrivono per il repository e per il loro importatore. Un lock non
/// potrebbero prenderlo — non c'è nessun utente a cui intestarlo — e questa è la ragione per cui il confine
/// di agosto lasciava i dati strutturati fuori dal lock. La premessa era vera, la conclusione no.</para>
/// </summary>
public interface IAirportLockGuard
{
    /// <summary>Pretende che il lock del documento dello scalo sia <b>mio</b>. Per le scritture editoriali.</summary>
    Task EnsureMineAsync(string icao, CancellationToken ct = default);

    /// <summary>Pretende soltanto che il lock <b>non sia di un altro</b>. Per i comandi in blocco (re-import),
    /// che partono anche da pagine che quel lock non lo tengono.</summary>
    Task EnsureNotOtherAsync(string icao, CancellationToken ct = default);
}

/// <inheritdoc cref="IAirportLockGuard"/>
public sealed class AirportLockGuard : IAirportLockGuard
{
    private readonly IAirportRepository _repo;
    private readonly IEditingRepository _editing;
    private readonly IEditAuthorizationService _authz;

    public AirportLockGuard(IAirportRepository repo, IEditingRepository editing, IEditAuthorizationService authz)
    {
        _repo = repo;
        _editing = editing;
        _authz = authz;
    }

    /// <summary>
    /// Il documento sotto cui sta l'anagrafica di questo scalo: quello <b>civile</b> se c'è, altrimenti quello
    /// <b>militare</b>.
    ///
    /// <para>⚠️ I due casi ci vogliono tutti e due. Un campo solo militare senza vIPI civile l'editor
    /// d'aeroporto non lo apre nemmeno (<c>EnsureDocumentAsync</c> lo rifiuta): i suoi dati di scalo si
    /// scrivono dall'editor del vSOP militare, che tiene il lock del <i>suo</i> documento. Guardando il solo
    /// documento civile, quell'editor si sarebbe rotto in silenzio.</para>
    /// </summary>
    private async Task<int?> DocumentoDelloScaloAsync(string icao, CancellationToken ct)
    {
        var stato = await _repo.GetMilitaryStateAsync(Norm(icao), ct);
        return stato?.DocumentId ?? stato?.MilDocumentId;
    }

    /// <summary>
    /// <inheritdoc cref="IAirportLockGuard.EnsureMineAsync"/>
    ///
    /// <para><b>Perché qui e non nel bottone.</b> È la regola già scritta per l'eliminazione dei documenti: un
    /// tasto spento non è una guardia — l'editor è una fotografia, e chi arriva da un'altra scheda o con la
    /// pagina vecchia in mano passerebbe lo stesso. Fino al 4 settembre 2026 piste, SID, frequenze e quote di
    /// uno scalo si scrivevano <b>senza alcun lock</b>: due persone potevano lavorarci sopra senza vedersi.</para>
    /// </summary>
    public async Task EnsureMineAsync(string icao, CancellationToken ct = default)
    {
        var docId = await DocumentoDelloScaloAsync(icao, ct)
            ?? throw new EditConflictException(Lingua(
                $"{Norm(icao)} non ha ancora un documento: apri il suo editor, che lo crea, prima di scriverne i dati.",
                $"{Norm(icao)} has no document yet: open its editor, which creates one, before writing its data."));

        var lk = await _editing.InspectLockAsync(docId, _authz.CurrentUserId ?? 0, ct);
        if (lk is { Locked: true, IsMine: true }) return;

        throw new EditConflictException(lk.Locked
            ? Lingua($"{Norm(icao)} è in modifica da {Chi(lk)} fino alle {Quando(lk)} UTC.",
                     $"{Norm(icao)} is being edited by {Chi(lk)} until {Quando(lk)} UTC.")
            : Lingua("Premi «Modifica» prima di scrivere: senza il lock del documento le modifiche non si salvano.",
                     "Press «Edit» before writing: without the document lock changes are not saved."));
    }

    /// <summary>
    /// <inheritdoc cref="IAirportLockGuard.EnsureNotOtherAsync"/>
    ///
    /// <para>⚠️ Non è una scorciatoia: il re-import lo lancia anche la pagina degli aeroporti su N scali per
    /// volta, e il lock del singolo documento quella pagina non ce l'ha (ha il suo, per risorsa). Pretenderlo
    /// chiuderebbe l'amministratore fuori dal suo stesso strumento. Quel che deve non fare è passare sopra a
    /// chi sta lavorando: un re-import riscrive piste e quote.</para>
    /// </summary>
    public async Task EnsureNotOtherAsync(string icao, CancellationToken ct = default)
    {
        if (await DocumentoDelloScaloAsync(icao, ct) is not int docId) return;

        var lk = await _editing.InspectLockAsync(docId, _authz.CurrentUserId ?? 0, ct);
        if (!lk.Locked || lk.IsMine) return;

        throw new EditConflictException(Lingua(
            $"{Norm(icao)} è in modifica da {Chi(lk)} fino alle {Quando(lk)} UTC: aspetta che finisca prima di re-importarlo.",
            $"{Norm(icao)} is being edited by {Chi(lk)} until {Quando(lk)} UTC: wait until they are done before re-importing it."));
    }

    private static string Chi(LockInfo lk) => lk.ByName ?? $"VID {lk.ByUserId}";
    private static string Quando(LockInfo lk) => $"{lk.ExpiresUtc:HH:mm}";
    private static string Norm(string icao) => (icao ?? "").Trim().ToUpperInvariant();
}
