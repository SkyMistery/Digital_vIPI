using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Application.Abstractions;

/// <summary>Una richiesta dal campo come la legge chi sta sopra la persistenza (S56).</summary>
/// <param name="DocumentTitle">Il titolo del documento; null per una richiesta libera, o se il documento non c'è più.</param>
/// <param name="TaskId">L'incarico nato con «prendi in carico», se c'è.</param>
public sealed record FieldRequestRow(int Id, int ReporterUserId, string ReporterName, DateTime CreatedUtc,
    int? DocumentId, string? DocumentTitle, string SectionKey, int? ReleaseNumber, FieldRequestKind Kind, string Body,
    FieldRequestStatus Status, string HandledByName, DateTime? HandledUtc, string Reply, int? DuplicateOfId,
    int? TaskId = null, string PageUrl = "")
{
    /// <summary>Ancora da chiudere: nuova o presa in carico.</summary>
    public bool Aperta => Status is FieldRequestStatus.Nuova or FieldRequestStatus.PresaInCarico;
}

/// <summary>Persistenza delle richieste dal campo (S56, carta <c>docs/design/piano-segnalazioni.md</c>).</summary>
public interface IFieldRequestRepository
{
    Task<int> AddAsync(FieldRequest richiesta, CancellationToken ct = default);

    /// <summary>Quante richieste ancora aperte ha questo VID.</summary>
    Task<int> CountOpenAsync(int reporterUserId, CancellationToken ct = default);

    /// <summary>Quante richieste ha aperto questo VID da <paramref name="sinceUtc"/> in poi.</summary>
    Task<int> CountSinceAsync(int reporterUserId, DateTime sinceUtc, CancellationToken ct = default);

    Task<FieldRequestRow?> GetAsync(int id, CancellationToken ct = default);

    /// <summary>L'elenco, più recenti in cima: di un autore, di un documento, solo le aperte — o tutto.</summary>
    Task<IReadOnlyList<FieldRequestRow>> ListAsync(int? reporterUserId = null, int? documentId = null,
        bool soloAperte = false, CancellationToken ct = default);

    /// <summary>Cambia lo stato e scrive chi, quando e con quale frase. Ritorna false se la richiesta non c'è.</summary>
    Task<bool> SetStatusAsync(int id, FieldRequestStatus status, int handledByUserId, string handledByName,
        string reply, int? duplicateOfId, CancellationToken ct = default);

    /// <summary>Toglie una richiesta. False se non c'è.</summary>
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Toglie le richieste CHIUSE (risolte, respinte, doppioni) prima di <paramref name="chiuseprimaDiUtc"/>: la pulizia
    /// automatica. Le aperte restano, qualunque età abbiano. Ritorna quante ne ha tolte.
    /// </summary>
    Task<int> PotaChiuseAsync(DateTime chiuseprimaDiUtc, CancellationToken ct = default);

    /// <summary>Il titolo della sezione con questa chiave nella versione corrente del documento; null se non c'è.</summary>
    Task<string?> SectionTitleAsync(int documentId, string sectionKey, CancellationToken ct = default);
}
