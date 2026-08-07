namespace Vipi.Application.Content;

/// <summary>
/// Use-case di gestione ACC (admin-only). L'import scarica le posizioni center dalla sorgente
/// (porta neutra <see cref="Abstractions.IAccDirectory"/>) e fa upsert su ACC + settori CTR: il sito resta
/// agnostico dalla sorgente e contiene SOLO ciò che la sorgente fornisce. L'admin può nascondere singoli ACC.
/// </summary>
public interface IAccAdminService
{
    Task<IReadOnlyList<AccAdminRow>> ListAccsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AccSectorRow>> ListSubcentersAsync(CancellationToken ct = default);
    /// <summary>
    /// Import manuale «da sorgente» (admin): ACC + subcenter + aree speciali. Ritorna i conteggi ACC e i
    /// fallimenti aree speciali per-ACC, che la UI logga (direttiva logging).
    /// </summary>
    Task<AccImportOutcome> ImportFromSourceAsync(CancellationToken ct = default);
    Task SetHiddenAsync(int accId, bool hidden, CancellationToken ct = default);

    /// <summary>
    /// Primo scarico manuale delle aree regolamentate di un ACC (tipicamente estero, che nasce spento) e
    /// abilitazione al giro periodico. Ritorna l'esito, coi fallimenti che la UI logga.
    /// </summary>
    Task<SpecialAreaImportResult> ImportSpecialAreasAsync(string accCode, CancellationToken ct = default);

    /// <summary>Accende/spegne l'import periodico delle aree di un ACC. Spegnendolo si liberano le sue aree
    /// (restano quelle che un altro ente abilitato elenca): ritorna quanti legami sono stati tolti.</summary>
    Task<int> SetSpecialAreasEnabledAsync(int accId, bool enabled, CancellationToken ct = default);
    Task SetSubcenterHiddenAsync(int id, bool hidden, CancellationToken ct = default);
    Task SetSubcenterLimitsAsync(int id, int? lower, int? upper, CancellationToken ct = default);
}
