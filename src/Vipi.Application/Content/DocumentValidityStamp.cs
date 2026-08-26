using Vipi.Application.Abstractions;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Il timbro di «Validità e revisione»: da quale ciclo AIRAC il documento vale, da quando, e <b>chi</b> l'ha
/// pubblicato — nome, posizioni staff e VID (richiesta del committente, 26 agosto 2026).
/// <para>
/// Sono tre <b>fatti</b>, non prosa: nessuno deve ricopiarli a mano, e infatti su alcuni documenti erano già
/// scritti a mano in una tabella che nessuno aggiornava.
/// </para>
/// </summary>
/// <param name="Published">Falso = il documento non è ancora stato pubblicato: gli altri campi non hanno valore.</param>
/// <param name="ReviewerVid">VID di chi ha premuto Pubblica. Null se la release non lo ha registrato.</param>
/// <param name="ReviewerName">Nome dal roster staff. Null se quel VID non è mai passato dal roster.</param>
public sealed record DocumentValidityStamp(
    bool Published,
    string? AiracCycle,
    DateTime? EffectiveUtc,
    int? ReviewerVid,
    string? ReviewerName,
    IReadOnlyList<string> ReviewerPositions)
{
    /// <summary>Documento mai pubblicato: è lo stato di ogni bozza, e la pagina lo dice invece di mostrare vuoti.</summary>
    public static DocumentValidityStamp NotPublished { get; } =
        new(false, null, null, null, null, Array.Empty<string>());
}

/// <summary>
/// Sceglie la release da cui leggere il timbro. Pura: la parte che decide <i>quale</i> release è quella che
/// sbaglia più facilmente, e qui si verifica senza database.
/// </summary>
public static class ValidityRelease
{
    /// <summary>
    /// La release a cui si riferisce la vista: quella indicata (anteprima di release) oppure quella
    /// <b>effettiva adesso</b>. Null = nessuna, cioè documento non ancora pubblicato.
    /// <para>
    /// ⚠️ Se l'anteprima punta a una release che non esiste più (cancellata mentre la si guardava) NON si ricade
    /// sull'effettiva: si mostra «non pubblicato». Ricadere direbbe al lettore, sotto l'intestazione «stai
    /// guardando la release #N», il ciclo e il firmatario di un'ALTRA release.
    /// </para>
    /// </summary>
    public static ReleaseInfo? Pick(IReadOnlyList<ReleaseInfo>? releases, int? releaseId)
    {
        if (releases is null || releases.Count == 0) return null;
        if (releaseId is int id) return releases.FirstOrDefault(r => r.Id == id);
        return releases.FirstOrDefault(r => r.IsEffectiveNow);
    }
}

/// <summary>Risolve il timbro di validità di un documento per la vista.</summary>
public interface IDocumentValidityService
{
    /// <param name="releaseId">Anteprima di una release precisa; null = la release effettiva adesso.</param>
    Task<DocumentValidityStamp> ResolveAsync(
        ReleaseTargetType type, string key, int? releaseId = null, CancellationToken ct = default);
}

/// <inheritdoc cref="IDocumentValidityService"/>
public sealed class DocumentValidityService : IDocumentValidityService
{
    private readonly IReleaseService _releases;
    private readonly IStaffRosterRepository _roster;

    public DocumentValidityService(IReleaseService releases, IStaffRosterRepository roster)
    {
        _releases = releases;
        _roster = roster;
    }

    public async Task<DocumentValidityStamp> ResolveAsync(
        ReleaseTargetType type, string key, int? releaseId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key)) return DocumentValidityStamp.NotPublished;

        var rel = ValidityRelease.Pick(await _releases.ListAsync(type, key, ct), releaseId);
        if (rel is null) return DocumentValidityStamp.NotPublished;

        // ⚠️ VID 0 = release scritta senza un utente (ce ne sono in archivio, su alcune vLOA). Non è «l'utente
        // numero zero»: è «non registrato», e va detto così invece di stampare uno zero.
        var vid = rel.CreatedByUserId > 0 ? rel.CreatedByUserId : (int?)null;
        var staff = vid is int v ? await _roster.FindAsync(v, ct) : null;

        return new DocumentValidityStamp(
            Published: true,
            AiracCycle: rel.ReleaseAiracCycle,
            EffectiveUtc: rel.ReleaseEffectiveUtc,
            ReviewerVid: vid,
            ReviewerName: CleanName(staff?.DisplayName, vid),
            ReviewerPositions: staff?.StaffPositions ?? Array.Empty<string>());
    }

    /// <summary>
    /// Il nome senza il VID che si porta dietro. ⚠️ Nel roster i nomi arrivano anche nella forma
    /// <c>«Carmine (704798)»</c>: chi li scrive è il login, e il numero ci sta perché nell'elenco dei permessi
    /// due omonimi vanno distinti. Qui il VID lo aggiunge già il link — lasciarlo nel nome lo stamperebbe
    /// <b>due volte</b>, ed è quel che si vedeva a schermo: «Carmine (704798) (VID 704798)».
    /// </summary>
    public static string? CleanName(string? name, int? vid)
    {
        var n = (name ?? "").Trim();
        if (n.Length == 0) return null;
        if (vid is not int v) return n;

        var coda = $"({v})";
        if (n.EndsWith(coda, StringComparison.Ordinal))
            n = n[..^coda.Length].TrimEnd();
        return n.Length == 0 ? null : n;
    }
}
