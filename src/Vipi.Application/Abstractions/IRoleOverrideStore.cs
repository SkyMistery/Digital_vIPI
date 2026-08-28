using Vipi.Domain;

namespace Vipi.Application.Abstractions;

/// <summary>Una promozione a mano, come la legge chi la mostra o la applica.</summary>
public sealed record RoleOverrideRow(
    int UserId,
    VipiRole Level,
    int GrantedByUserId,
    DateTime GrantedAtUtc,
    string? Note,
    string? DisplayName);

/// <summary>
/// Lettura e scrittura delle promozioni a mano (<c>RoleOverride</c>). Carta
/// <c>docs/feature/2026-08-28-autorizzazioni-a-livelli.md</c> §5.
///
/// <para>⚠️ <b>Chi decide un permesso non passa di qui.</b> L'autorizzazione legge
/// <see cref="Vipi.Application.Auth.IRoleOverrides"/>, che è la stessa tabella tenuta in memoria: questo
/// contratto è per chi la <b>amministra</b>, cioè una manciata di volte al mese. Interrogare il database a
/// ogni richiesta rimetterebbe nel layout la query che questa funzione toglie.</para>
/// </summary>
public interface IRoleOverrideStore
{
    /// <summary>Tutte le promozioni, ordinate per VID. Sono poche decine: si legge l'intera tabella apposta.</summary>
    Task<IReadOnlyList<RoleOverrideRow>> ListAsync(CancellationToken ct = default);

    /// <summary>
    /// Scrive la promozione di una persona: <b>una riga per VID</b>, quindi riscrive se c'è già.
    /// <para>⚠️ Non controlla il pavimento, ed è voluto: il pavimento è il <c>max</c> con il livello dello
    /// staff, e vive dove i due si incontrano. Qui si registra ciò che un admin ha deciso.</para>
    /// </summary>
    Task SetAsync(int userId, VipiRole level, int grantedByUserId, string? displayName, string? note, CancellationToken ct = default);

    /// <summary>Toglie la promozione. <c>false</c> se non ce n'era una: cancellare due volte non è un errore.</summary>
    Task<bool> RemoveAsync(int userId, int actorUserId, CancellationToken ct = default);
}
