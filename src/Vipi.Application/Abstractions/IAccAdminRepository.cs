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

    /// <summary>Upsert aree speciali/regolamentate per IvaoId (chiave naturale) + del legame con l'ACC che le
    /// elenca (additivo: un centro non se le porta via agli altri). Ritorna (create, aggiornate).</summary>
    Task<SpecialAreaUpsertOutcome> ImportSpecialAreasAsync(IReadOnlyList<SourceSpecialArea> areas, CancellationToken ct = default);

    /// <summary>
    /// IvaoId delle aree di un ACC che hanno già una shape importata dopo <paramref name="importedAfterUtc"/>:
    /// per queste il dettaglio sorgente si può saltare (i metadati arrivano comunque dall'elenco).
    /// </summary>
    Task<IReadOnlySet<string>> ListAreasWithFreshShapeAsync(string accCode, DateTime importedAfterUtc, CancellationToken ct = default);

    /// <summary>Toglie a un ACC i legami verso le aree che non elenca più; l'area sopravvive finché almeno un altro
    /// ente la elenca, e si cancella quando resta senza. Ritorna il numero di legami rimossi.</summary>
    Task<SpecialAreaPruneOutcome> PruneSpecialAreasNotInAsync(string accCode, IReadOnlyCollection<string> keepIvaoIds, CancellationToken ct = default);

    /// <summary>Tutti gli ACC (anche nascosti).</summary>
    Task<IReadOnlyList<AccAdminRow>> ListAccsAsync(CancellationToken ct = default);

    /// <summary>Tutti i settori ATC (anche nascosti).</summary>
    Task<IReadOnlyList<AccSectorRow>> ListSubcentersAsync(CancellationToken ct = default);

    /// <summary>Mostra/nasconde un ACC dalla navigazione pubblica.</summary>
    Task SetHiddenAsync(int accId, bool hidden, CancellationToken ct = default);

    /// <summary>Accende/spegne l'import periodico delle aree regolamentate di un ACC. Spegnendolo ne pota anche i
    /// legami (le aree che nessun altro ente elenca spariscono): ritorna quanti legami ha tolto.</summary>
    Task<int> SetSpecialAreasEnabledAsync(int accId, bool enabled, CancellationToken ct = default);

    /// <summary>Mostra/nasconde un settore ATC.</summary>
    Task SetSubcenterHiddenAsync(int id, bool hidden, CancellationToken ct = default);

    /// <summary>Contesto gerarchico (radice? figli visibili?) del settore ATC, per validare l'occultamento (Regola 1). null se l'id non esiste.</summary>
    Task<SubcenterHideContext?> GetSubcenterHideContextAsync(int id, CancellationToken ct = default);

    /// <summary>Imposta i limiti di quota (inferiore/superiore) di un settore ATC (admin).</summary>
    Task SetSubcenterLimitsAsync(int id, int? lower, int? upper, CancellationToken ct = default);
}
