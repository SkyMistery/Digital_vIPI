using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Gestione (admin) della policy di import globale: il gestore del sito decide quali categorie di dati
/// arrivano dalla sorgente (sola lettura) e quali restano manuali. Lettura libera, scrittura solo admin.
/// </summary>
public interface IImportPolicyService
{
    Task<ImportPolicySnapshot> GetAsync(CancellationToken ct = default);

    /// <summary>La policy più chi l'ha decisa e quando (per la pagina admin).</summary>
    Task<ImportPolicyInfo> GetInfoAsync(CancellationToken ct = default);

    Task SaveAsync(ImportPolicySnapshot policy, CancellationToken ct = default);

    /// <summary>
    /// Salva <b>solo</b> le categorie che <paramref name="voluta"/> cambia rispetto a <paramref name="letta"/>
    /// (la policy che la pagina aveva all'apertura), sopra la policy riletta adesso. Restituisce le categorie
    /// che nel frattempo un altro aveva cambiato e che sono rimaste com'erano nel database.
    ///
    /// <para>⚠️ Scrivere la <paramref name="voluta"/> intera (U-184) riportava indietro, in silenzio, la
    /// decisione presa da un altro amministratore in un'altra scheda: la pagina è una fotografia dell'apertura.
    /// Una categoria toccata qui non può essere in conflitto: i valori sono due, e se l'altro l'ha cambiata
    /// l'ha portata proprio dove la si vuole.</para>
    /// </summary>
    Task<IReadOnlyList<ImportCategory>> SaveChangesAsync(ImportPolicySnapshot letta, ImportPolicySnapshot voluta,
        CancellationToken ct = default);
}

/// <inheritdoc cref="IImportPolicyService"/>
internal sealed class ImportPolicyService : IImportPolicyService
{
    private readonly IImportPolicyStore _store;
    private readonly IEditAuthorizationService _authz;

    public ImportPolicyService(IImportPolicyStore store, IEditAuthorizationService authz)
    {
        _store = store;
        _authz = authz;
    }

    public Task<ImportPolicySnapshot> GetAsync(CancellationToken ct = default) => _store.GetAsync(ct);

    public Task<ImportPolicyInfo> GetInfoAsync(CancellationToken ct = default) => _store.GetInfoAsync(ct);

    public Task SaveAsync(ImportPolicySnapshot policy, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        return _store.SaveAsync(policy, _authz.CurrentUserId ?? 0, ct);
    }

    public async Task<IReadOnlyList<ImportCategory>> SaveChangesAsync(ImportPolicySnapshot letta,
        ImportPolicySnapshot voluta, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        var corrente = await _store.GetAsync(ct);
        var categorie = Enum.GetValues<ImportCategory>();

        var daScrivere = corrente;
        foreach (var c in categorie.Where(c => voluta.IsImported(c) != letta.IsImported(c)))
            daScrivere = daScrivere.With(c, voluta.IsImported(c));

        await _store.SaveAsync(daScrivere, _authz.CurrentUserId ?? 0, ct);

        return categorie
            .Where(c => voluta.IsImported(c) == letta.IsImported(c) && corrente.IsImported(c) != letta.IsImported(c))
            .ToList();
    }
}
