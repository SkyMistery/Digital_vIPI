namespace Vipi.Application.Abstractions;

/// <summary>
/// Porta transazionale sorgente-neutra: esegue più operazioni di persistenza come una singola unità atomica
/// (tutte o nessuna). Serve ai use-case multi-passo dove uno stato parziale committato sarebbe incoerente
/// (es. import confinanti: persist catalogo estero + riproiezione settori devono vivere/morire insieme).
/// L'implementazione concreta (transazione DB) vive in Infrastructure; i consumatori Application non la conoscono.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Esegue <paramref name="action"/> dentro una transazione: commit se completa, rollback se lancia.</summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default);
}
