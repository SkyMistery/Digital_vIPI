namespace Vipi.Application.Abstractions;

/// <summary>Un cliente delle API come lo leggono la pagina e la verifica. Non porta mai la chiave: non c'è.</summary>
public sealed record ApiClientRow(
    int Id,
    string Nome,
    string Prefisso,
    IReadOnlyList<string> Endpoint,
    int CreataDaUserId,
    DateTime CreataUtc,
    DateTime? RevocataUtc,
    int? RevocataDaUserId,
    DateTime? UltimoUsoUtc)
{
    public bool Attiva => RevocataUtc is null;
}

/// <summary>
/// Lettura e scrittura dei clienti delle API (<c>ApiClients</c>). Carta
/// <c>docs/feature/2026-09-13-chiavi-api.md</c>.
///
/// <para>⚠️ Non controlla chi chiede: il cancello di chi emette sta in
/// <see cref="Vipi.Application.Auth.IApiClientService"/>. Qui si registra, con l'audit.</para>
/// </summary>
public interface IApiClientStore
{
    /// <summary>Tutti, attivi e revocati, dal più recente.</summary>
    Task<IReadOnlyList<ApiClientRow>> ListAsync(CancellationToken ct = default);

    Task<ApiClientRow> AddAsync(string nome, string prefisso, string impronta, IReadOnlyList<string> endpoint,
        int actorUserId, CancellationToken ct = default);

    /// <summary>Revoca. <c>false</c> se non c'è o era già revocata: revocare due volte non è un errore.</summary>
    Task<bool> RevocaAsync(int id, int actorUserId, CancellationToken ct = default);

    /// <summary>Il cliente di un'impronta, attivo o revocato; null se nessuno.</summary>
    Task<ApiClientRow?> TrovaPerImprontaAsync(string impronta, CancellationToken ct = default);

    /// <summary>Scrive l'ultimo uso. Chi chiama decide quando vale la pena (non a ogni richiesta).</summary>
    Task SegnaUsoAsync(int id, DateTime quandoUtc, CancellationToken ct = default);
}
