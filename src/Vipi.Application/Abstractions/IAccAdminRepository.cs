using Vipi.Application.Content;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Porta di persistenza per la gestione ACC importati dalla sorgente. Gli ACC e i loro settori CTR
/// del sito sono SOLO quelli forniti dalla sorgente: l'import fa upsert (mai creazione manuale).
/// L'admin può solo nascondere (IsHidden). Impl. EF in Infrastructure.
/// </summary>
public interface IAccAdminRepository
{
    /// <summary>Upsert ACC (center area) dalla sorgente. Preserva IsHidden esistente. Ritorna (creati, aggiornati).</summary>
    Task<(int Created, int Updated)> ImportAsync(IReadOnlyList<SourceCenter> centers, CancellationToken ct = default);

    /// <summary>Upsert settori ATC (subcenter). Preserva IsHidden e i limiti admin (salvo valori dalla sorgente). Ritorna (creati, aggiornati).</summary>
    Task<(int Created, int Updated)> ImportSubcentersAsync(IReadOnlyList<SourceSubcenter> subs, CancellationToken ct = default);

    /// <summary>Upsert aree speciali/regolamentate per IvaoId (chiave naturale). Ritorna (creati, aggiornati).</summary>
    Task<(int Created, int Updated)> ImportSpecialAreasAsync(IReadOnlyList<SourceSpecialArea> areas, CancellationToken ct = default);

    /// <summary>Cancella le aree speciali di un ACC il cui IvaoId non è più presente sulla sorgente. Ritorna il numero rimosse.</summary>
    Task<int> PruneSpecialAreasNotInAsync(string accCode, IReadOnlyCollection<string> keepIvaoIds, CancellationToken ct = default);

    /// <summary>Tutti gli ACC (anche nascosti).</summary>
    Task<IReadOnlyList<AccAdminRow>> ListAccsAsync(CancellationToken ct = default);

    /// <summary>Tutti i settori ATC (anche nascosti).</summary>
    Task<IReadOnlyList<AccSectorRow>> ListSubcentersAsync(CancellationToken ct = default);

    /// <summary>Mostra/nasconde un ACC dalla navigazione pubblica.</summary>
    Task SetHiddenAsync(int accId, bool hidden, CancellationToken ct = default);

    /// <summary>Mostra/nasconde un settore ATC.</summary>
    Task SetSubcenterHiddenAsync(int id, bool hidden, CancellationToken ct = default);

    /// <summary>Imposta i limiti di quota (inferiore/superiore) di un settore ATC (admin).</summary>
    Task SetSubcenterLimitsAsync(int id, int? lower, int? upper, CancellationToken ct = default);
}
