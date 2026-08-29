using Vipi.Domain.Entities;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Una voce di biblioteca come la vede chi la mostra: la voce <b>più la sua versione corrente</b>, che è
/// l'unica che i documenti servono.
/// </summary>
/// <param name="VersionNumber">Il progressivo della versione corrente: <c>3</c> si legge «v3».</param>
/// <param name="VersionCount">Quante versioni ha avuto in tutto. Uguale a <paramref name="VersionNumber"/>
/// finché nessuno ne cancella una, ed è il modo di vedere a colpo d'occhio che un file è stato rifatto.</param>
public sealed record AttachmentRow(
    int Id, string Slug, string Title,
    AttachmentKind Kind, AttachmentScope Scope, string? ScopeKey, string? Notes,
    int VersionNumber, int VersionCount, AttachmentProvider Provider, string ExternalId,
    DateTime UpdatedUtc, DateTime VersionCreatedUtc)
{
    /// <summary>Il perimetro come si legge: <c>LIRR</c>, oppure il trattino della divisione.</summary>
    public string ScopeLabel => ScopeKey ?? "—";
}

/// <summary>Che cosa chiede chi crea una voce. Il link è quello che lo staffista <b>incolla</b>: l'id lo
/// ricava <c>AttachmentRules</c>, perché nel database va il dato e non l'impacchettamento di Google.</summary>
public sealed record AttachmentDraft(
    string Slug, string Title, AttachmentKind Kind, AttachmentScope Scope, string? ScopeKey,
    string? Notes, string Link);

/// <summary>
/// Esito della creazione di una voce. ⚠️ I rifiuti sono <b>distinti</b> e non un «non valido» solo: chi
/// incolla un link sbagliato e chi sceglie uno slug già preso devono correggere due cose diverse, e un
/// messaggio unico li manda a indovinare.
/// </summary>
public enum AttachmentCreate
{
    Ok,

    /// <summary>Lo slug non ha la forma giusta (maiuscole, spazi, accenti, o troppo corto).</summary>
    SlugNonValido,

    /// <summary>Lo slug è già di un'altra voce. ⚠️ È l'unica identità che i documenti citano: due voci con lo
    /// stesso slug renderebbero il link ambiguo, e a sbagliarsi sarebbe il redirect.</summary>
    SlugOccupato,

    /// <summary>Manca il titolo: è quel che si legge dentro il documento, quindi non è un dettaglio.</summary>
    TitoloMancante,

    /// <summary>Dal link incollato non si ricava nessun id di file.</summary>
    LinkNonValido,

    /// <summary>Il perimetro e la sua chiave non stanno insieme: la divisione non vuole una chiave, un ACC o
    /// uno scalo la pretendono.</summary>
    AmbitoNonValido,
}

/// <summary>
/// La biblioteca degli allegati (carta <c>docs/feature/2026-08-25-biblioteca-allegati.md</c>): i PDF stanno
/// sul Drive di divisione, qui stanno <b>identità, organizzazione e versioni</b>.
///
/// <para><b>Le regole che questa porta fa rispettare</b>, e che non si deducono dai nomi dei metodi:</para>
/// <list type="number">
/// <item><b>Lo slug è l'identità e non si cambia mai.</b> È ciò che i documenti citano: cambiarlo spegne
/// ogni citazione già scritta. Per questo si sceglie alla creazione e poi non c'è un metodo per
/// rinominarlo.</item>
/// <item><b>Una voce nasce con la v1.</b> Non esiste lo stato «voce senza file»: sarebbe una riga che non sa
/// rispondere alla domanda per cui esiste.</item>
/// <item><b>L'elenco mostra tutto</b>, anche ciò che non cita nessuno. Un elenco che mostrasse solo le voci
/// citate renderebbe la prima voce caricata irraggiungibile — è il catch-22 già pagato con l'elenco degli
/// APP, dove si vedevano solo i pubblicati e il primo non si poteva creare.</item>
/// </list>
///
/// <para>⚠️ <b>Nessun controllo di autorizzazione qui dentro</b>: il cancello sta dove sta per tutte le
/// scritture editoriali, e ripeterlo darebbe due cancelli che col tempo dicono cose diverse.</para>
/// </summary>
public interface IAttachmentLibrary
{
    /// <summary>
    /// Tutta la biblioteca, con la versione corrente di ogni voce, in ordine di titolo.
    /// <para>⚠️ Ordinata per <b>titolo</b> e non per slug: lo slug è un identificatore da citare, il titolo è
    /// quel che si legge — e un elenco ordinato per una cosa che l'occhio non guarda sembra disordinato.</para>
    /// </summary>
    Task<IReadOnlyList<AttachmentRow>> ListAsync(CancellationToken ct = default);

    /// <summary>Una voce dal suo slug, o <c>null</c>. È la lettura del redirect <c>/vsop/files/{slug}</c>.</summary>
    Task<AttachmentRow?> BySlugAsync(string slug, CancellationToken ct = default);

    /// <summary>
    /// Crea una voce e la sua <b>v1</b> in un colpo solo.
    /// <para>Il risultato porta la riga solo quando l'esito è <see cref="AttachmentCreate.Ok"/>: negli altri
    /// casi non è stato scritto niente.</para>
    /// </summary>
    Task<(AttachmentCreate Esito, AttachmentRow? Riga)> CreateAsync(
        AttachmentDraft draft, int userId, CancellationToken ct = default);
}
